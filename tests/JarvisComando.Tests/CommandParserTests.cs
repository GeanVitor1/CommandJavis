using JarvisComando;

namespace JarvisComando.Tests;

public class CommandParserTests
{
    private static List<HotkeyBinding> Bindings() => new()
    {
        new HotkeyBinding { Category = "app", Description = "Visual Studio Code", Target = @"C:\Program Files\Microsoft VS Code\Code.exe" },
        new HotkeyBinding { Category = "app", Description = "Google Chrome", Target = @"C:\Program Files\Google\Chrome\Application\chrome.exe" },
        new HotkeyBinding { Category = "app", Description = "Discord", Target = "%LOCALAPPDATA%\\Discord\\Update.exe" },
        new HotkeyBinding { Category = "app", Description = "WhatsApp", Target = "shell:AppsFolder\\x!App" },
        new HotkeyBinding { Category = "site", Description = "YouTube", Target = "https://www.youtube.com" },
        new HotkeyBinding { Category = "site", Description = "Google", Target = "https://www.google.com.br" },
        new HotkeyBinding { Category = "site", Description = "Netflix", Target = "https://www.netflix.com" },
        new HotkeyBinding { Category = "site", Description = "Globo (G1)", Target = "https://www.globo.com" },
        new HotkeyBinding { Category = "site", Description = "X (Twitter)", Target = "https://x.com" }
    };

    [Theory]
    [InlineData("ei jarvis abra o youtube em coldplay paradise", "YouTube", "coldplay paradise")]
    [InlineData("ei jarvis abre o discord", "Discord", null)]
    [InlineData("abra o youtube", "YouTube", null)]
    [InlineData("abra o visual studio code", "Visual Studio Code", null)]
    [InlineData("abrir o site youtube", "YouTube", null)]
    [InlineData("toque heaven", "YouTube", "heaven")]
    [InlineData("toca bad guy", "YouTube", "bad guy")]
    [InlineData("pesquise receita de bolo", "YouTube", "receita de bolo")]
    [InlineData("pesquisar gato fofo no youtube", "YouTube", "gato fofo")]
    [InlineData("abra a globo", "Globo (G1)", null)]
    [InlineData("quero abrir o youtube em teste", "YouTube", "teste")]
    [InlineData("abra o netflix", "Netflix", null)]
    [InlineData("abra o google chrome", "Google Chrome", null)]
    public void Parse_opens_expected_target(string raw, string expectedName, string? expectedQuery)
    {
        var cmd = CommandParser.Parse(raw, Bindings());
        Assert.NotNull(cmd);
        Assert.Equal(expectedName, cmd.Binding?.Description);
        Assert.Equal(expectedQuery, cmd.Query);
        Assert.Null(cmd.System);
    }

    [Theory]
    [InlineData("fecha o youtube")]
    [InlineData("xyz abc qwerty")]
    [InlineData("")]
    public void Parse_returns_null_for_unknown(string raw)
    {
        Assert.Null(CommandParser.Parse(raw, Bindings()));
    }

    [Theory]
    [InlineData("pausa", SystemCommand.PlayPause)]
    [InlineData("ei jarvis play", SystemCommand.PlayPause)]
    [InlineData("continua a musica", SystemCommand.PlayPause)]
    [InlineData("proxima faixa", SystemCommand.Next)]
    [InlineData("anterior", SystemCommand.Previous)]
    [InlineData("aumenta o volume", SystemCommand.VolumeUp)]
    [InlineData("diminui o volume", SystemCommand.VolumeDown)]
    [InlineData("mudo", SystemCommand.Mute)]
    [InlineData("bloqueia a tela", SystemCommand.Lock)]
    [InlineData("jarvis dorme", SystemCommand.Sleep)]
    [InlineData("hibernar", SystemCommand.Hibernate)]
    public void Parse_detects_system_commands(string raw, SystemCommand expected)
    {
        var cmd = CommandParser.Parse(raw, Bindings());
        Assert.NotNull(cmd);
        Assert.Equal(expected, cmd.System);
        Assert.Null(cmd.Binding);
    }

    [Theory]
    [InlineData("abra o youtube em playlist de rock", "YouTube", "playlist de rock")]
    public void Play_word_inside_query_does_not_trigger_system(string raw, string expectedName, string expectedQuery)
    {
        var cmd = CommandParser.Parse(raw, Bindings());
        Assert.NotNull(cmd);
        Assert.Equal(expectedName, cmd.Binding?.Description);
        Assert.Equal(expectedQuery, cmd.Query);
        Assert.Null(cmd.System);
    }

    [Theory]
    [InlineData("abra o vs code", "Visual Studio Code")]
    [InlineData("abre o chrome", "Google Chrome")]
    [InlineData("abra o disc", "Discord")]
    public void Parse_fuzzy_matches_names(string raw, string expectedName)
    {
        var cmd = CommandParser.Parse(raw, Bindings());
        Assert.NotNull(cmd);
        Assert.Equal(expectedName, cmd.Binding?.Description);
        Assert.Null(cmd.System);
    }
}