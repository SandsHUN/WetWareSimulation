using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace WetWare.App.Localization;

/// <summary>
/// Runtime-switchable string tables (Hungarian / English). XAML binds to the indexer through
/// <see cref="TrExtension"/>; raising <c>Item[]</c> refreshes every bound text at once.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public const string Hungarian = "hu";
    public const string English = "en";

    private readonly Dictionary<string, Dictionary<string, string>> _tables = new();
    private Dictionary<string, string> _current = new();
    private Dictionary<string, string> _fallback = new();

    public static LocalizationService Instance { get; } = new();

    private LocalizationService()
    {
        _tables[Hungarian] = Load(Hungarian);
        _tables[English] = Load(English);
        _fallback = _tables[English];
        SetLanguage(English);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changed and all bindings were refreshed.</summary>
    public event EventHandler? LanguageChanged;

    public string Language { get; private set; } = English;

    public bool IsHungarian => Language == Hungarian;

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    public IReadOnlyCollection<string> Keys => _current.Keys;

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (_current.TryGetValue(key, out var value)) return value;
        if (_fallback.TryGetValue(key, out value)) return value;
        return $"⟨{key}⟩";
    }

    public bool Has(string key) => _current.ContainsKey(key) || _fallback.ContainsKey(key);

    /// <summary>Formats a localized pattern with the current culture (decimal comma in Hungarian).</summary>
    public string Format(string key, params object[] args) => string.Format(Culture, Get(key), args);

    public void SetLanguage(string language)
    {
        if (!_tables.TryGetValue(language, out var table)) return;
        Language = language;
        _current = table;
        Culture = CultureInfo.GetCultureInfo(language == Hungarian ? "hu-HU" : "en-US");
        CultureInfo.CurrentUICulture = Culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHungarian)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Merges every embedded table of the language: the main <c>Strings/{lang}.json</c> plus the course module
    /// tables under <c>Strings/Course/*.{lang}.json</c>.
    /// </summary>
    private static Dictionary<string, string> Load(string language)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var table = new Dictionary<string, string>();
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Strings.", StringComparison.OrdinalIgnoreCase)
                        && n.EndsWith($".{language}.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Length);
        foreach (string name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var part = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd(), options);
            if (part is null) continue;
            foreach (var (key, value) in part) table[key] = value;
        }
        return table;
    }
}
