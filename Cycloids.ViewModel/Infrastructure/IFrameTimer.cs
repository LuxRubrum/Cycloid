namespace Cycloids.ViewModel.Infrastructure;

/// <summary>
/// Таймер кадров анимации. ViewModel не зависит от конкретного UI-фреймворка:
/// в приложении Avalonia его реализует DispatcherTimer, в тестах — «пустышка».
/// </summary>
public interface IFrameTimer
{
    event EventHandler? Tick;

    void Start();

    void Stop();
}
