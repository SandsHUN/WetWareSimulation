using System.Diagnostics;
using System.Windows.Media;

namespace WetWare.App.Services;

/// <summary>
/// Display-synchronised animation clock (CompositionTarget.Rendering). Plays the role of the telemetry
/// channel: every frame the active page advances its simulation and pulls a fresh snapshot.
/// </summary>
public sealed class FrameClock
{
    private readonly Stopwatch _stopwatch = new();
    private EventHandler<double>? _tick;
    private double _last;
    private long _frames;
    private double _workMs;
    private double _intervalMs;

    /// <summary>Raised once per rendered frame with the elapsed real time in milliseconds (clamped to 50 ms).</summary>
    public event EventHandler<double> Tick
    {
        add
        {
            bool wasEmpty = _tick is null;
            _tick += value;
            if (wasEmpty) Start();
        }
        remove
        {
            _tick -= value;
            if (_tick is null) Stop();
        }
    }

    private void Start()
    {
        _stopwatch.Restart();
        _last = 0;
        CompositionTarget.Rendering += OnRendering;
    }

    private void Stop()
    {
        CompositionTarget.Rendering -= OnRendering;
        _stopwatch.Stop();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        double now = _stopwatch.Elapsed.TotalMilliseconds;
        double dt = now - _last;
        if (dt < 12) return; // cap at ~80 Hz
        _last = now;
        _tick?.Invoke(this, Math.Min(dt, 50));
        _frames++;
        _intervalMs += dt;
        _workMs += _stopwatch.Elapsed.TotalMilliseconds - now;
    }

    /// <summary>Diagnostics: average frame interval and average time spent in tick handlers (ms).</summary>
    public string Statistics => _frames == 0 ? "no frames"
        : $"frames={_frames} avgInterval={_intervalMs / _frames:F1}ms avgWork={_workMs / _frames:F1}ms";
}
