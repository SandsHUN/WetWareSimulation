using System.IO;
using System.Text.Json;

namespace WetWare.App.Services;

public sealed class AppSettings
{
    public string Language { get; set; } = "hu";
    public bool ShowGuides { get; set; } = true;
    public bool ShowSimple { get; set; } = true;
    public bool SidebarExpanded { get; set; } = true;
}

/// <summary>Persists user preferences in %AppData%\WetWareLab\settings.json.</summary>
public sealed class SettingsService
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WetWareLab", "settings.json");

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
        }
        catch (Exception)
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            // Preferences are a convenience; failing to save them must never crash the lab.
        }
    }
}
