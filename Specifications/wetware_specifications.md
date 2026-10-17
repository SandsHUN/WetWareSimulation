# Biological Neural Network Simulator and Teaching Workstation

**English** · [Magyar](wetware_specifications.hu.md)

## System specification

> This is the original design document the project started from. The implementation deviates in one major point: instead of a Python/Brian 2 engine connected over ZeroMQ, the same equations run in a native C# engine inside the app (see the main [README](../README.md#architecture)).

The goal of the system is to demonstrate the principles of biological computation (**wetware / neuromorphic computing**) in a software environment that combines biologically faithful mathematical models with real-time, interactive visualisation. The application is primarily a **teaching and demonstration tool**: it aims to make abstract biophysical and machine-learning concepts (membrane dynamics, spike coding, synaptic plasticity) directly manipulable and visually understandable for software engineers and researchers.

---

## 1. System architecture and data flow

The software runs as two separate processes, minimising UI blocking by the numerical computation:

```
+-----------------------------------------------------------------------------+
|                       WPF Teaching Client (.NET 8)                          |
|  * MVVM architecture (CommunityToolkit.Mvvm)                                |
|  * ScottPlot 5.0 real-time charts + WPF 2D/3D visualisation layer           |
|  * Interactive Maths Lab (formulas, dynamic phase-space plots)              |
+-------------------------------^-----------------------------+---------------+
                                |                             |
                PUB/SUB (telemetry, 30-60 Hz)                 | REQ/REP (commands)
                ZeroMQ (tcp://127.0.0.1:5556)                 | ZeroMQ (tcp://127.0.0.1:5555)
                                |                             |
+-------------------------------+-----------------------------v---------------+
|                       Brian 2 Simulation Engine                             |
|  * Real-time differential equation integration (Euler / Runge-Kutta)        |
|  * 100-neuron biological population (80% excitatory, 20% inhibitory)        |
|  * STDP plasticity rules and Poisson background noise generator             |
+-----------------------------------------------------------------------------+
```

* **Simulation core (Python / Brian 2):** continuously integrates the neurons' state equations in fixed time steps ($dt = 0.1\text{ ms}$). It does no rendering; internal state (spike timestamps, membrane potentials, synaptic weights) is collected in a memory buffer.
* **Transport layer (ZeroMQ / NetMQ):**
  * *Telemetry channel (PUB/SUB):* 30–60 times per second it sends the latest simulation window to the UI as a compact binary or JSON packet.
  * *Control channel (REQ/REP):* forwards user interventions (current injection, pausing the simulation, parameter changes) to the engine.
* **WPF front end (.NET 8):** receives the data stream, updates the high-speed charts, animates the network topology and shows the mathematical explanation panels.

---

## 2. The simulated biological model

The population contains $N = 100$ biologically plausible neurons connected by a randomised *small-world* topology.

### Membrane dynamics: Leaky Integrate-and-Fire (LIF)

The cell membrane acts as an analogue integrator that leaks and discharges when it reaches a threshold:

$$\tau_m \frac{dv}{dt} = -(v - E_L) + R_m \cdot (I_{syn} + I_{ext} + I_{noise})$$

* $v$: membrane potential ($\text{mV}$).
* $E_L = -70\text{ mV}$: resting potential.
* $V_{th} = -50\text{ mV}$: firing threshold. Crossing it generates an action potential, after which the voltage immediately resets to $V_{reset} = -65\text{ mV}$.
* $\tau_m = 20\text{ ms}$: membrane time constant (the speed of the leak).
* $\tau_{ref} = 2\text{ ms}$: absolute refractory period (the time after a spike during which the cell cannot be excited).

### Synaptic learning: Spike-Timing-Dependent Plasticity (STDP)

Synaptic strength changes depending on the relative timing of pre- and postsynaptic spikes ($\Delta t = t_{post} - t_{pre}$):

$$\Delta w =
\begin{cases}
A_+ \cdot e^{-\Delta t / \tau_+}, & \text{if } \Delta t > 0 \text{ (causal: the input caused the output} \rightarrow \text{potentiation / LTP)} \\
-A_- \cdot e^{\Delta t / \tau_-}, & \text{if } \Delta t < 0 \text{ (coincidence: the postsynaptic cell fired first} \rightarrow \text{depression / LTD)}
\end{cases}$$

---

## 3. Structural layout of the WPF interface

The interface follows a dark scientific dashboard layout divided into functional zones:

```
+--------------------------------------------------------------------------------------------------------+
| [MENU] File | Experiment scenarios | Simulation speed: [ 1.0x v ] | [ > ] [ || ] [ Step 5ms ]          |
+---------------------------------------------------+----------------------------------------------------+
| 1. LIVE ELECTROPHYSIOLOGY MONITORS                | 2. NETWORK TOPOLOGY AND PLASTICITY                 |
|                                                   |                                                    |
| [Spike raster plot: neuron ID 0-99 vs time (ms)]  | [Interactive network graph]                        |
|  | : :  :: : :  :::  :  ::  :  :::  : :           |   (N1) --[w=0.8]--> (N2) (pulsing discharges)      |
|  |:  : :  : : ::   :  : :  : ::   : : :           |                                                    |
|                                                   | [STDP weight-matrix heatmap]                       |
| [Oscilloscope of the selected neuron's V(t)]      |   Pre vs post synapse strength on a blue-red scale |
|   -50mV (threshold) ---------------------         |                                                    |
|         /\    /\                                  | [Weight distribution histogram]                    |
|   -70mV/  \--/  \------------------------         |   Visualising the bimodal distribution             |
+---------------------------------------------------+----------------------------------------------------+
| 3. INTERACTIVE MATHS AND THEORY LAB (teaching centre)                                                  |
|                                                                                                        |
| [Formulas & live parameter binding]               | [Phase space and dynamics curves]                  |
|  tau_m * (dv/dt) = -(v - E_L) + R_m * I           |  * dv/dt vs v (V-nullcline)                        |
|  [tau_m: 20ms] [E_L: -70mV] [V_th: -50mV]         |  * Asymmetric STDP learning curve                  |
|                                                   |                                                    |
| [Glossary & "What is happening now?" info]        | [Step-by-step Bio-Debugger]                        |
|  "Neuron 5 fired because the sum of incoming      |  [ || Freeze & Inspect ] -> breakdown of the       |
|   EPSPs reached the critical depolarisation."     |  stimulation, computation step by step.            |
+---------------------------------------------------+----------------------------------------------------+
| 4. STIMULATION AND CONTROL PANEL                  | 5. REAL-TIME METRICS                               |
|  * Electrode stimulation (injected: 0-500 pA)     |  * Network synchrony index: 0.42                   |
|  * STDP learning-rate multiplier: [ 1.0x ]        |  * Mean firing rate: 14.2 Hz                       |
|  * Experiment pattern: [ Hebbian Demo v ]         |  * Share of active synapses: 68%                   |
+---------------------------------------------------+----------------------------------------------------+
```

---

## 4. Detailed design of the interactive maths panel

To make it truly educational, the maths panel does not show static text: it works as a visual laboratory synchronised with the live simulation data.

* **Dynamically reacting equations:**
  * The LIF and STDP differential equations are shown as interactive components.
  * When the user hovers over a variable (e.g. $\tau_m$ or $R_m$), that part of the formula is highlighted and an info panel explains what the parameter means biologically (e.g. membrane time constant = the product of the membrane's capacitance and resistance, which sets the length of the cell's "memory").
  * Next to the variables, live numerical values are shown and coloured according to the inspected neuron's state (e.g. during depolarisation $v$ turns from green to yellow, then red when it crosses the threshold).
* **STDP explanatory chart:**
  * A dedicated 2D coordinate system plots the two-branched exponential learning curve ($\Delta w$ as a function of $\Delta t$).
  * When two connected neurons fire one after the other, an animated point appears on the chart at the exact time difference ($\Delta t$), explaining visually: *"Because the pre-neuron fired 4 ms before the post-neuron, the weight increased by +0.08 (LTP point)."*
* **Phase-plane analysis ($v$ vs. $dv/dt$):**
  * Next to the classic voltage–time trace, the interface draws the phase diagram of the membrane state.
  * It shows the stable fixed point (resting state, where $dv/dt = 0$) and the limit cycle, helping to understand nonlinear dynamical systems.
* **"Step-by-step Bio-Debugger" (didactic mode):**
  * The simulation can be paused with one click.
  * The user can step the numerical integrator button by button (in $0.1\text{ ms}$ steps) while the interface prints each computation:
    1. Leak current: $-(v - E_L) / \tau_m$.
    2. Summing the incoming synaptic weights.
    3. Numerical integration: $v_{new} = v_{old} + dt \cdot (dv/dt)$.
    4. Threshold check: $v_{new} \ge V_{th}$?

---

## 5. Built-in teaching scenarios

The interface contains preconfigured experiments that can be loaded with one click to demonstrate biological learning:

| Experiment | Phenomenon demonstrated | What does the learner see on screen? |
|---|---|---|
| **Classical conditioning (Pavlovian Hebbian learning)** | Associative synaptic potentiation (LTP) | Two independent input groups are stimulated. If firing of group A is systematically followed by stimulation of group B, the connecting synapses visibly strengthen on the heatmap until A alone triggers B. |
| **Synchronisation and seizure (epileptiform activity)** | Loss of inhibition / excessive excitation | When the effect of the inhibitory neurons ($I_{inh}$) is switched off, the network's disordered activity suddenly turns into massive, synchronised spike oscillations on the raster plot. |
| **Synaptic scaling and homeostasis** | Network stability vs. saturation | If depression ($A_-$) is too small compared to potentiation ($A_+$) in the STDP rule, every synapse runs up to the maximum ($w_{max}$) and the network loses its selectivity. |
| **Information coding modes** | Rate coding vs. timing coding | Comparing DC stimulation and patterned spike stimulation of equal intensity: how a cell encodes information in its firing rate (rate coding) or in the precise phase of its spikes (phase coding). |

---

## 6. Proposed extensions

The base system can be extended modularly towards more advanced research and teaching directions:

* **LLM embedding / tokenizer bridge (neuromorphic-to-LLM interface):**
  * A module that converts the dimensions of a text prompt (e.g. a sentence embedding vector) into input electrode currents.
  * The biological network processes the input as a reservoir (*Liquid State Machine*), and the read-out response vector is fed back into the hidden state of a small language model (e.g. a Llama / Gemma layer), illustrating hybrid biological–silicon coding.
* **3D organoid and MEA visualisation:**
  * Using WPF 3D (Helix Toolkit or Direct3D) to render a spherical cell cluster (3D brain organoid) and a virtual microelectrode array (MEA).
  * Neurons are placed according to their physical position in space, and spikes propagate through the 3D tissue as flashes of light (similar to real calcium-imaging microscopy).
* **Electrophysiological sonification (spike sonification):**
  * As in real neuroscience labs, spikes on selected electrodes are turned into short sound clicks, so synchronisation or rate changes become instantly recognisable by ear.
* **Biological damage and regeneration (fault-tolerance simulation):**
  * A "laser ablation" tool: the user selects a group of neurons and virtually destroys (switches off) them, then observes whether the remaining network can rewire the lost function through STDP plasticity.
* **Hardware target export (neuromorphic code generation):**
  * Exporting the trained or emerged weight matrix to silicon neuromorphic formats (e.g. Intel Loihi / Lava framework, SpiNNaker configuration or an snnTorch definition).
