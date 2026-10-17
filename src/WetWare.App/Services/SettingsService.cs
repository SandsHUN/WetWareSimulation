using System.IO;
using System.Text.Json;

namespace WetWare.App.Services;

public sealed class AppSettings
{
    /// <summary>Schema version of the saved file; files written before versioning deserialize as 0.</summary>
    public int Version { get; set; }

    public string Language { get; set; } = "en";
    public bool ShowGuides { get; set; } = true;
    public bool ShowSimple { get; set; } = true;
    public bool SidebarExpanded { get; set; } = true;
}

/// <summary>Persists user preferences in %AppData%\WetWareLab\settings.json.</summary>
public sealed class SettingsService
{
    /// <summary>Version 1: English became the default language (older files saved Hungarian implicitly).</summary>
    private const int CurrentVersion = 1;

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

        if (Current.Version < CurrentVersion)
        {
            if (Current.Version < 1) Current.Language = "en";
            Current.Version = CurrentVersion;
            Save();
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
