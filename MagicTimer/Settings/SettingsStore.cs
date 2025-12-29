using System.IO;
using System.Text.Json;

namespace MagicTimer.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;

    public SettingsStore(string path)
    {
        _path = path;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                var defaults = CreateDefaults();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            
            if (settings == null)
            {
                var defaults = CreateDefaults();
                Save(defaults);
                return defaults;
            }

            // Uzupe³nij brakuj¹ce wartoœci domyœlne
            settings.TimerFontFamily ??= "Consolas";
            settings.LastDuration ??= "05:00";

            return settings;
        }
        catch
        {
            var defaults = CreateDefaults();
            try { Save(defaults); } catch { /* ignore */ }
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_path, json);
        }
        catch
        {
            // best-effort
        }
    }

    private static AppSettings CreateDefaults()
    {
        return new AppSettings
        {
            SoundFilePath = "",
            LastDuration = "05:00",
            TimerFontFamily = "Consolas"
        };
    }
}
