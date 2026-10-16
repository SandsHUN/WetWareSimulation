namespace WetWare.App.Services;

/// <summary>Decouples pages that want to navigate (e.g. "start this experiment") from the shell.</summary>
public sealed class NavigationService
{
    public event EventHandler<string>? NavigateRequested;

    public void Navigate(string pageKey) => NavigateRequested?.Invoke(this, pageKey);
}
