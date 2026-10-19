using Avalonia.Threading;
using Cycloids.ViewModel.Infrastructure;

namespace Cycloids.App;

/// <summary>Реализация таймера кадров для ViewModel на основе Avalonia DispatcherTimer (≈ 60 кадров/с).</summary>
public sealed class AvaloniaFrameTimer : IFrameTimer
{
    private readonly DispatcherTimer _timer;

    public AvaloniaFrameTimer()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _timer.Tick += (_, e) => Tick?.Invoke(this, e);
    }

    public event EventHandler? Tick;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();
}
