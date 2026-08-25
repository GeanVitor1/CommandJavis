using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Vox;

public class HotkeyBinding : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string Category { get; set; } = "app";
    public string Modifiers { get; set; } = "";
    public string Key { get; set; } = "";
    public string Action { get; set; } = "open";
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconPath { get; set; } = "";
    public string SearchTemplate { get; set; } = "";

    private ImageSource? _icon;

    [JsonIgnore]
    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasIcon)));
        }
    }

    [JsonIgnore]
    public bool HasIcon => Icon != null;

    public bool HasCombo => !string.IsNullOrWhiteSpace(Key);

    public string AvatarChar => string.IsNullOrWhiteSpace(Description)
        ? "?"
        : Description.Trim().Substring(0, 1).ToUpperInvariant();

    public string[] ComboParts
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Modifiers))
                parts.AddRange(Modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries));
            if (!string.IsNullOrWhiteSpace(Key))
                parts.Add(Key);
            return parts.ToArray();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class VoiceSettings
{
    public bool Enabled { get; set; } = true;
    public string TalkHotkey { get; set; } = "F9";
    public bool WakeWord { get; set; } = false;
    public string MicrophoneId { get; set; } = "";
}

public class AppearanceSettings
{
    public string Theme { get; set; } = "dark";
}

public class HotkeyConfig
{
    public List<HotkeyBinding> Hotkeys { get; set; } = new();
    public VoiceSettings? Voice { get; set; }
    public AppearanceSettings? Appearance { get; set; }
}

public static class Config
{
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "config.json");

    public static List<HotkeyBinding> Load(string? path = null)
    {
        path ??= DefaultPath;
        return LoadConfig(path).Hotkeys;
    }

    public static VoiceSettings LoadVoice(string? path = null)
    {
        path ??= DefaultPath;
        return LoadConfig(path).Voice ?? new VoiceSettings();
    }

    public static void Save(List<HotkeyBinding> bindings, VoiceSettings? voice = null, string? path = null)
    {
        path ??= DefaultPath;
        var current = LoadConfig(path);
        var cfg = new HotkeyConfig
        {
            Hotkeys = bindings,
            Voice = voice ?? current.Voice ?? new VoiceSettings(),
            Appearance = current.Appearance ?? new AppearanceSettings()
        };
        WriteAtomic(cfg, path);
    }

    public static void SaveVoice(VoiceSettings voice, string? path = null)
    {
        path ??= DefaultPath;
        var current = LoadConfig(path);
        current.Voice = voice;
        WriteAtomic(current, path);
    }

    public static AppearanceSettings LoadAppearance(string? path = null)
    {
        path ??= DefaultPath;
        return LoadConfig(path).Appearance ?? new AppearanceSettings();
    }

    public static void SaveAppearance(AppearanceSettings appearance, string? path = null)
    {
        path ??= DefaultPath;
        var current = LoadConfig(path);
        current.Appearance = appearance;
        WriteAtomic(current, path);
    }

    private static HotkeyConfig LoadConfig(string path)
    {
        if (!File.Exists(path))
            return new HotkeyConfig();
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Logger.Error($"Config.LoadConfig [{path}]", ex);
        }
        var backup = path + ".bak";
        if (File.Exists(backup))
        {
            try
            {
                return Parse(File.ReadAllText(backup));
            }
            catch (Exception ex)
            {
                Logger.Error($"Config.LoadConfig backup [{backup}]", ex);
            }
        }
        return new HotkeyConfig();
    }

    private static HotkeyConfig Parse(string json)
    {
        var cfg = JsonSerializer.Deserialize<HotkeyConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        var list = cfg?.Hotkeys ?? new List<HotkeyBinding>();
        foreach (var b in list)
        {
            if (string.IsNullOrWhiteSpace(b.Category))
                b.Category = b.Action.Equals("url", StringComparison.OrdinalIgnoreCase) ? "site" : "app";
        }
        cfg ??= new HotkeyConfig();
        cfg.Hotkeys = list;
        return cfg;
    }

    private static void WriteAtomic(HotkeyConfig cfg, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);

        if (File.Exists(path))
        {
            try { File.Copy(path, path + ".bak", overwrite: true); }
            catch { }
        }
        File.Move(tmp, path, overwrite: true);
    }
}
