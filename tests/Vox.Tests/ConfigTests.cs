using System.IO;
using Vox;

namespace Vox.Tests;

public class ConfigTests : IDisposable
{
    private readonly string _dir;

    public ConfigTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "vox_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    private string PathFor(string name) => Path.Combine(_dir, name);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Save_and_load_roundtrip()
    {
        var path = PathFor("config.json");
        var voice = new VoiceSettings { Enabled = true, TalkHotkey = "F10" };
        Config.Save(new List<HotkeyBinding>
        {
            new() { Category = "app", Description = "Bloco de Notas", Target = "notepad.exe", Key = "B", Modifiers = "Alt" }
        }, voice, path);

        var bindings = Config.Load(path);
        Assert.Single(bindings);
        Assert.Equal("Bloco de Notas", bindings[0].Description);

        var loadedVoice = Config.LoadVoice(path);
        Assert.True(loadedVoice.Enabled);
        Assert.Equal("F10", loadedVoice.TalkHotkey);
    }

    [Fact]
    public void Save_preserves_voice_when_null()
    {
        var path = PathFor("config.json");
        Config.Save(new List<HotkeyBinding>(), new VoiceSettings { Enabled = true, TalkHotkey = "F12" }, path);
        Config.Save(new List<HotkeyBinding>
        {
            new() { Category = "site", Description = "YouTube", Target = "https://www.youtube.com" }
        }, voice: null, path);

        var voice = Config.LoadVoice(path);
        Assert.True(voice.Enabled);
        Assert.Equal("F12", voice.TalkHotkey);
    }

    [Fact]
    public void Corrupt_config_falls_back_to_backup()
    {
        var path = PathFor("config.json");
        Config.Save(new List<HotkeyBinding>
        {
            new() { Category = "app", Description = "Excel", Target = "EXCEL.EXE" }
        }, voice: null, path);
        Config.Save(new List<HotkeyBinding>
        {
            new() { Category = "app", Description = "Excel", Target = "EXCEL.EXE" }
        }, voice: null, path);

        Assert.True(File.Exists(path + ".bak"));
        File.WriteAllText(path, "{invalido:::");

        var bindings = Config.Load(path);
        Assert.Single(bindings);
        Assert.Equal("Excel", bindings[0].Description);
    }

    [Fact]
    public void Missing_config_returns_empty()
    {
        var bindings = Config.Load(PathFor("nao_existe.json"));
        Assert.Empty(bindings);
        Assert.NotNull(Config.LoadVoice(PathFor("nao_existe.json")));
    }

    [Fact]
    public void Appearance_roundtrip()
    {
        var path = PathFor("config.json");
        Config.SaveAppearance(new AppearanceSettings { Theme = "light" }, path);

        var loaded = Config.LoadAppearance(path);
        Assert.Equal("light", loaded.Theme);
    }

    [Fact]
    public void Appearance_defaults_to_system()
    {
        var loaded = Config.LoadAppearance(PathFor("nao_existe.json"));
        Assert.Equal("system", loaded.Theme);
    }

    [Fact]
    public void Save_preserves_appearance()
    {
        var path = PathFor("config.json");
        Config.SaveAppearance(new AppearanceSettings { Theme = "dark" }, path);
        Config.Save(new List<HotkeyBinding>
        {
            new() { Category = "app", Description = "Notepad", Target = "notepad.exe" }
        }, voice: null, path);

        Assert.Equal("dark", Config.LoadAppearance(path).Theme);
    }

    [Fact]
    public void Theme_resolves_system_and_explicit()
    {
        Assert.Equal("dark", Theme.Resolve("dark"));
        Assert.Equal("light", Theme.Resolve("light"));
        var sys = Theme.Resolve("system");
        Assert.True(sys == "dark" || sys == "light");
    }
}