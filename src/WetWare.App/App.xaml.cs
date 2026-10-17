using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using WetWare.App.Localization;
using WetWare.App.Services;
using WetWare.App.ViewModels;

namespace WetWare.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Tooltips carry the explanations of the lab, so keep them open as long as the pointer stays.
        ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(int.MaxValue));

        var services = new ServiceCollection();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<FrameClock>();
        services.AddSingleton<NavigationService>();

        // Page order defines the navigation rail.
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<NeuronLabViewModel>();
        services.AddSingleton<NetworkViewModel>();
        services.AddSingleton<StdpLabViewModel>();
        services.AddSingleton<CodingViewModel>();
        services.AddSingleton<VisionViewModel>();
        services.AddSingleton<RecognitionViewModel>();
        services.AddSingleton<ScenariosViewModel>();
        services.AddSingleton<GlossaryViewModel>();
        services.AddSingleton<IEnumerable<PageViewModel>>(sp =>
        [
            sp.GetRequiredService<HomeViewModel>(),
            sp.GetRequiredService<NeuronLabViewModel>(),
            sp.GetRequiredService<NetworkViewModel>(),
            sp.GetRequiredService<StdpLabViewModel>(),
            sp.GetRequiredService<CodingViewModel>(),
            sp.GetRequiredService<VisionViewModel>(),
            sp.GetRequiredService<RecognitionViewModel>(),
            sp.GetRequiredService<ScenariosViewModel>(),
            sp.GetRequiredService<GlossaryViewModel>(),
        ]);
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

        var settings = _services.GetRequiredService<SettingsService>();
        settings.Load();
        LocalizationService.Instance.SetLanguage(settings.Current.Language);

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
        ApplyCommandLine(e.Args, window);
    }

    /// <summary>
    /// Developer switches: <c>--lang en</c>, <c>--page Network</c>, <c>--scenario Seizure</c>, <c>--train 900</c>, <c>--snapshot out.png [--delay ms]</c>
    /// (renders the window to a PNG after the delay and exits – used for visual regression checks).
    /// </summary>
    private void ApplyCommandLine(string[] args, MainWindow window)
    {
        string? Arg(string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        if (Arg("--lang") is { } lang) LocalizationService.Instance.SetLanguage(lang);
        if (Arg("--page") is { } page) _services!.GetRequiredService<NavigationService>().Navigate(page);
        if (Arg("--scenario") is { } scenario) _services!.GetRequiredService<NetworkViewModel>().LoadScenario(scenario);
        if (int.TryParse(Arg("--train"), out int samples))
        {
            // Pre-train the digit classifier and start recognising a "3", so screenshots show a learned state.
            var recognition = _services!.GetRequiredService<RecognitionViewModel>();
            for (int k = 0; k < samples; k += 300) recognition.QuickTrainCommand.Execute(null);
            recognition.LoadDigitCommand.Execute("3");
            recognition.RecognizeCommand.Execute(null);
        }
        if (Arg("--snapshot") is { } path)
        {
            int delay = int.TryParse(Arg("--delay"), out int d) ? d : 4000;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                window.SaveSnapshot(path);
                System.IO.File.WriteAllText(path + ".txt", _services!.GetRequiredService<FrameClock>().Statistics);
                Shutdown();
            };
            timer.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
