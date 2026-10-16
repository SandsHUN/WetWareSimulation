\# Biológiai Neurális Hálózat Szimulátor és Oktató Munkaállomás

\## Rendszerspecifikáció



A rendszer célja a biológiai számítási elvek (\*\*wetware / neuromorphic computing\*\*) szemléltetése egy olyan szoftverkörnyezetben, amely a biológiai hűségű matematikai modelleket ötvözi a valós idejű, interaktív vizualizációval. Az alkalmazás elsődlegesen \*\*oktató és demonstrációs eszköz\*\*: célja, hogy elvont biofizikai és gépi tanulási koncepciókat (membrándinamika, tüskekódolás, szinaptikus plaszticitás) tegyen közvetlenül manipulálhatóvá és vizuálisan értelmezhetővé szoftvermérnökök és kutatók számára.



\---



\## 1. Rendszerarchitektúra és Adatáramlás



A szoftver két különálló folyamatként fut, minimalizálva a numerikus számítások felületi blokkolását:



```

+-----------------------------------------------------------------------------+

|                       WPF Oktató Kliens (.NET 8)                            |

|  \* MVVM architektúra (CommunityToolkit.Mvvm)                                |

|  \* ScottPlot 5.0 valós idejű grafikonok + WPF 2D/3D vizualizációs réteg     |

|  \* Interaktív Matematikai Labor (képletek, dinamikus fázistér-grafikonok)   |

+-------------------------------^-----------------------------+---------------+

&#x20;                               |                             |

&#x20;               PUB/SUB (Telemetria, 30-60 Hz)                | REQ/REP (Parancsok)

&#x20;               ZeroMQ (tcp://127.0.0.1:5556)                 | ZeroMQ (tcp://127.0.0.1:5555)

&#x20;                               |                             |

+-------------------------------+-----------------------------v---------------+

|                       Brian 2 Szimulációs Motor                             |

|  \* Valós idejű differenciálegyenlet-integráció (Euler / Runge-Kutta)        |

|  \* 100 neuronos biológiai populáció (80% serkentő, 20% gátló)               |

|  \* STDP plaszticitási szabályok és Poisson-háttérzaj generátor               |

+-----------------------------------------------------------------------------+

```



\*   \*\*Szimulációs mag (Python / Brian 2):\*\* Folyamatosan integrálja a neuronok állapotegyenleteit rögzített időlépésekben ($dt = 0.1\\text{ ms}$). Nem végez renderelést; a belső állapotokat (tüskék időbélyegei, membránpotenciálok, szinapszissúlyok) memóriapufferbe gyűjti.

\*   \*\*Adatátviteli réteg (ZeroMQ / NetMQ):\*\* 

&#x20;   \*   \*Telemetria csatorna (PUB/SUB):\* Másodpercenként 30–60 alkalommal kompakt bináris vagy JSON csomagban küldi a legfrissebb szimulációs ablak adatait a felhasználói felület felé.

&#x20;   \*   \*Vezérlő csatorna (REQ/REP):\* A felhasználó által kezdeményezett beavatkozásokat (áraminjektálás, szimuláció megállítása, paraméterátállítás) továbbítja a motornak.

\*   \*\*WPF Megjelenítő (.NET 8):\*\* Fogadja az adatfolyamot, frissíti a nagy sebességű grafikonokat, animálja a hálózati topológiát, és megjeleníti a matematikai magyarázó felületeket.



\---



\## 2. A Szimulált Biológiai Modell



A bemutatott populáció $N = 100$ biológiailag hiteles idegsejtet tartalmaz, randomizált, kisvilág (\*small-world\*) topológiával összekötve.



\### Membránpotenciál Dinamika: Leaky Integrate-and-Fire (LIF)

A sejt membránja analóg integrátorként működik, amely szivárog és küszöbérték elérésekor kisül:



$$\\tau\_m \\frac{dv}{dt} = -(v - E\_L) + R\_m \\cdot (I\_{syn} + I\_{ext} + I\_{noise})$$



\*   $v$: Membránfeszültség ($\\text{mV}$).

\*   $E\_L = -70\\text{ mV}$: Nyugalmi feszültség.

\*   $V\_{th} = -50\\text{ mV}$: Tüzelési küszöb. Átlépésekor akciós potenciál generálódik, majd a feszültség azonnal visszaáll $V\_{reset} = -65\\text{ mV}$-ra.

\*   $\\tau\_m = 20\\text{ ms}$: Membrán időállandó (a szivárgás sebessége).

\*   $\\tau\_{ref} = 2\\text{ ms}$: Abszolút refrakter periódus (a kisülés utáni időszak, amikor a sejt ingerelhetetlen).



\### Szinaptikus Tanulás: Spike-Timing-Dependent Plasticity (STDP)

A szinapszisok erőssége a sejtek tüzelésének relatív időzítésétől függően módosul ($\\Delta t = t\_{post} - t\_{pre}$):



$$\\Delta w = 

\\begin{cases} 

A\_+ \\cdot e^{-\\Delta t / \\tau\_+}, \& \\text{ha } \\Delta t > 0 \\text{ (Oksági kapcsolat: a bemenet kiváltotta a kimenetet } \\rightarrow \\text{Erősítés / LTP)} \\\\

\-A\_- \\cdot e^{\\Delta t / \\tau\_-}, \& \\text{ha } \\Delta t < 0 \\text{ (Véletlen egybeesés: a posztszinaptikus sejt előbb tüzelt } \\rightarrow \\text{Gyengítés / LTD)}

\\end{cases}$$



\---



\## 3. A WPF Felület Strukturális Terve



A felület egy sötét hátterű, tudományos műszerfal elrendezést követ, funkcionális zónákra bontva:



```

+--------------------------------------------------------------------------------------------------------+

| \[MENU] Fájl | Kísérleti forgatókönyvek | Szimulációs sebesség: \[ 1.0x v ] | \[ > ] \[ || ] \[ Step 5ms ]  |

+---------------------------------------------------+----------------------------------------------------+

| 1. ÉLŐ ELEKTROFIZIOLÓGIAI MONITOROK               | 2. HÁLÓZATI TOPOLÓGIA ÉS PLASZTICITÁS              |

|                                                   |                                                    |

| \[Spike Raster Plot: Neuron ID 0-99 vs Idő (ms)]   | \[Interaktív Hálózati Gráf]                         |

|  | : :  :: : :  :::  :  ::  :  :::  : :           |   (N1) --\[w=0.8]--> (N2) (Pulzáló kisülések)       |

|  |:  : :  : : ::   :  : :  : ::   : : :           |                                                    |

|                                                   | \[STDP Súly-Mátrix Heatmap]                         |

| \[Kijelölt neuron V(t) oszcilloszkópja]            |   Pre vs Post szinapszisok erőssége kék-vörös skálán|

|   -50mV (Küszöb) ------------------------         |                                                    |

|         /\\    /\\                                  | \[Súlyeloszlási Hisztogram]                         |

|   -70mV/  \\--/  \\------------------------         |   Bimodális eloszlás vizualizációja               |

+---------------------------------------------------+----------------------------------------------------+

| 3. INTERAKTÍV MATEMATIKAI ÉS ELMÉLETI MAGYARÁZÓ LABOR (Oktató Központ)                                 |

|                                                                                                        |

| \[Formulák \& Élő Paraméter-kötés]                  | \[Fázistér és Dinamikai Görbék]                     |

|  tau\_m \* (dv/dt) = -(v - E\_L) + R\_m \* I           |  \* dv/dt vs v (V-nullcline görbe)                  |

|  \[tau\_m: 20ms] \[E\_L: -70mV] \[V\_th: -50mV]         |  \* STDP Aszimmetrikus Tanulási Görbe               |

|                                                   |                                                    |

| \[Biológiai Fogalomtár \& "Mi történik most?" Info] | \[Lépésenkénti Bio-Debugger]                        |

|  "Az 5-ös neuron tüzelt, mert a bejövő EPSP       |  \[ || Freeze \& Inspect ] -> Ingerlés bomlása       |

|   összege elérte a kritikus depolarizációt."      |  számítási lépésről lépésre.                       |

+---------------------------------------------------+----------------------------------------------------+

| 4. INGERLŐ ÉS VEZÉRLŐ PULT                        | 5. VALÓS IDEJŰ METRIKÁK                            |

|  \* Elektróda stimuláció (Injektált áram: 0-500 pA)|  \* Hálózati szinkronizációs index: 0.42            |

|  \* STDP tanulási ráta szorzó: \[ 1.0x ]            |  \* Átlagos tüzelési frekvencia: 14.2 Hz            |

|  \* Kísérleti mintázat: \[ Hebbian Demo v ]         |  \* Aktív szinapszisok aránya: 68%                  |

+---------------------------------------------------+----------------------------------------------------+

```



\---



\## 4. Az Interaktív Matematikai Magyarázó Panel Részletes Terve



Az oktató jelleg biztosításához a matematikai panel nem statikus szöveget mutat, hanem a szimuláció pillanatnyi adataival szinkronizált vizuális laboratóriumként működik.



\*   \*\*Dinamikusan reagáló egyenlet-megjelenítés:\*\*

&#x20;   \*   A LIF és STDP differenciálegyenletek interaktív komponensként jelennek meg.

&#x20;   \*   Ha a felhasználó az egérrel egy változó fölé áll (pl. $\\tau\_m$ vagy $R\_m$), a képlet adott része kiemelkedik, a képernyőn pedig megjelenik egy infópanel: mit jelent a paraméter biológiailag (pl. membrán időállandó = a sejtfal kapacitásának és ellenállásának szorzata, amely meghatározza a sejt "memóriájának" hosszát).

&#x20;   \*   A képletben lévő változók mellett valós idejű numerikus értékek láthatók, amelyek a vizsgált neuron állapotának megfelelően színeződnek (pl. depolarizáció alatt $v$ zöldből sárgába, majd küszöbátlépéskor pirosba vált).

\*   \*\*STDP Magyarázó Grafikon:\*\*

&#x20;   \*   Egy dedikált 2D koordináta-rendszer ábrázolja a kétágú exponenciális tanulási görbét ($\\Delta w$ a $\\Delta t$ függvényében).

&#x20;   \*   Amikor a hálózatban két összekapcsolt neuron kisül egymás után, a grafikonon megjelenik egy animált pont a pontos időkülönbségnél ($\\Delta t$), vizuálisan bemutatva: \*„Mivel a pre-neuron 4 ms-mal a post-neuron előtt tüzelt, a súly megnőtt +0.08 egységgel (LTP pont).”\*

\*   \*\*Fázissík Vizuális Elemzés ($v$ vs. $dv/dt$):\*\*

&#x20;   \*   A klasszikus feszültség-idő diagram mellett a felület kirajzolja a membránállapot fázisdiagramját.

&#x20;   \*   Szemlélteti a stabil fixpontot (nyugalmi állapot, ahol $dv/dt = 0$) és a határciklust, segítve a nemlineáris dinamikai rendszerek megértését.

\*   \*\*„Lépésenkénti Bio-Debugger” (Didaktikai mód):\*\*

&#x20;   \*   Egyetlen gombnyomással megállítható a szimuláció.

&#x20;   \*   A felhasználó gombonként léptetheti a numerikus integrátort ($0.1\\text{ ms}$-os lépésekben), miközben a felület lépésről lépésre kiírja a lefutó számításokat:

&#x20;       1. Szivárgó áram kiszámítása: $-(v - E\_L) / \\tau\_m$.

&#x20;       2. Beérkező szinaptikus súlyok összegezése.

&#x20;       3. Numerikus integráció: $v\_{új} = v\_{régi} + dt \\cdot (dv/dt)$.

&#x20;       4. Küszöb-ellenőrzés: $v\_{új} \\ge V\_{th}$?



\---



\## 5. Beépített Oktatási Kísérleti Forgatókönyvek



A felület előre konfigurált kísérleteket tartalmaz, amelyek egyetlen kattintással betölthetők a biológiai tanulás demonstrálására:



| Kísérlet Neve | Demonstrált Jelenség | Mit figyelhet meg a tanuló a képernyőn? |

|---|---|---|

| \*\*Klasszikus Kondicionálás (Pavlovi Hebb-tanulás)\*\* | Asszociatív szinaptikus megerősítés (LTP) | Két független bemeneti neuroncsoport ingerlése. Ha az A csoport tüzelését szisztematikusan a B csoport ingerlése követi, az összekötő szinapszisok súlya a szemünk előtt nő meg a heatmapen, míg végül A önmagában is kiváltja B tüzelését. |

| \*\*Szinkronizáció és Roham (Epileptiform aktivitás)\*\* | Gátlás kiesése / Túlzott gerjesztés | A gátló neuronok hatásának ($I\_{inh}$) lekapcsolásakor a hálózat rendezetlen aktivitása hirtelen masszív, szinkronizált tüske-oszcillációvá alakul a raster ploton. |

| \*\*Szinaptikus Skálázás és Homeosztázis\*\* | Hálózati stabilitás vs. telítődés | Ha az STDP szabályban a depresszió ($A\_-$) túl kicsi a potencírozáshoz ($A\_+$) képest, az összes szinapszis maximális értékre ($w\_{max}$) fut fel, a hálózat elveszíti szelektivitását. |

| \*\*Információkódolási Módok\*\* | Frekvenciakódolás vs. Időzítéskódolás | Azonos intenzitású egyenáramú ingerlés és tüske-mintázatos ingerlés összehasonlítása: hogyan kódol a sejt tüzelési frekvenciával (Rate Coding) vagy a tüske mikroszekundumos fázisával (Phase Coding). |



\---



\## 6. Továbbfejlesztési Javaslatok



A fenti alaprendszer modulárisan bővíthető a fejlettebb kutatási és oktatási irányokba:



\*   \*\*LLM Embedding / Tokenizer Híd (Neuromorphic-to-LLM Interfész):\*\*

&#x20;   \*   Egy beépített modul, amely képes egy szöveges prompt (pl. mondat beágyazási vektora) dimenzióit bemeneti elektróda-áramokká konvertálni.

&#x20;   \*   A biológiai hálózat rezervoárként (\*Liquid State Machine\*) dolgozza fel a bemenetet, és a leolvasott válaszvektort egy kis méretű nyelvi modell (pl. Llama / Gemma réteg) rejtett állapotába táplálja vissza. Ezzel szemléltethető a hibrid biológiai-szilícium kódolás.

\*   \*\*3D Organoid és MEA Vizualizáció:\*\*

&#x20;   \*   WPF 3D (Helix Toolkit vagy Direct3D) használatával egy gömbszerű sejtcsomó (3D agy-organoid) és a virtuális mikroelektróda-tömb (MEA) modelljének renderelése.

&#x20;   \*   A neuronok fizikai elhelyezkedése szerint jeleníthetők meg a térben, és a tüzelések fényfelvillanásként terjednek végig a 3D szöveten (hasonlóan a valós kalcium-képalkotó mikroszkópos felvételekhez).

\*   \*\*Elektrofiziológiai Audifikáció (Spike Sonification):\*\*

&#x20;   \*   A valós idegtudományi laboratóriumok analógiájára a kijelölt elektródák tüskéinek átalakítása rövid hangimpulzusokká (pattogó hanghatás). A szinkronizáció vagy a frekvenciaváltás hallás után azonnal felismerhetővé válik a felhasználó számára.

\*   \*\*Biológiai Sérülés és Regeneráció (Fault-Tolerance szimuláció):\*\*

&#x20;   \*   „Lézeres ablációs” eszköz a felületen: a felhasználó kijelölhet egy neuroncsoportot, amelyet virtuálisan elpusztít (kikapcsol). Megfigyelhető, hogy a megmaradt hálózat az STDP plaszticitás révén képes-e újrahuzalozni a kiesett funkciót.

\*   \*\*Hardveres Célplatform-Export (Neuromorf kódgenerálás):\*\*

&#x20;   \*   A betanult vagy kialakult hálózati súlymátrix exportálása szilícium alapú neuromorf formátumokba (pl. Intel Loihi / Lava framework, SpiNNaker konfiguráció vagy snnTorch definíció).

