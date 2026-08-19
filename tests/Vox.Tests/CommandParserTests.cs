using Vox;

namespace Vox.Tests;

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
    [InlineData("ei vox abra o youtube em coldplay paradise", "YouTube", "coldplay paradise")]
    [InlineData("ei vox abre o discord", "Discord", null)]
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
    [InlineData("xyz abc qwerty")]
    [InlineData("")]
    public void Parse_returns_null_for_unknown(string raw)
    {
        Assert.Null(CommandParser.Parse(raw, Bindings()));
    }

    [Theory]
    [InlineData("pausa", SystemCommand.PlayPause)]
    [InlineData("ei vox play", SystemCommand.PlayPause)]
    [InlineData("continua a musica", SystemCommand.PlayPause)]
    [InlineData("proxima faixa", SystemCommand.Next)]
    [InlineData("anterior", SystemCommand.Previous)]
    [InlineData("aumenta o volume", SystemCommand.VolumeUp)]
    [InlineData("diminui o volume", SystemCommand.VolumeDown)]
    [InlineData("mudo", SystemCommand.Mute)]
    [InlineData("sem volume", SystemCommand.Mute)]
    [InlineData("bloqueia a tela", SystemCommand.Lock)]
    [InlineData("vox dorme", SystemCommand.Sleep)]
    [InlineData("hibernar", SystemCommand.Hibernate)]
    public void Parse_detects_system_commands(string raw, SystemCommand expected)
    {
        var cmd = CommandParser.Parse(raw, Bindings());
        Assert.NotNull(cmd);
        Assert.Equal(expected, cmd.System);
        Assert.Null(cmd.Binding);
    }

    [Theory]
    [InlineData("que horas sao", SystemCommand.Time, null, 0)]
    [InlineData("que horas e", SystemCommand.Time, null, 0)]
    [InlineData("que dia e hoje", SystemCommand.Date, null, 0)]
    [InlineData("qual a data", SystemCommand.Date, null, 0)]
    [InlineData("quanto e 2 mais 2", SystemCommand.Calc, "2 mais 2", 0)]
    [InlineData("calcule 15 vezes 3", SystemCommand.Calc, "15 vezes 3", 0)]
    [InlineData("volume 50", SystemCommand.VolumeSet, null, 50)]
    [InlineData("volume maximo", SystemCommand.VolumeSet, null, 100)]
    [InlineData("deixa o volume em 30", SystemCommand.VolumeSet, null, 30)]
    [InlineData("tema escuro", SystemCommand.Theme, "dark", 0)]
    [InlineData("modo claro", SystemCommand.Theme, "light", 0)]
    [InlineData("tema do sistema", SystemCommand.Theme, "system", 0)]
    [InlineData("lembre em 5 minutos", SystemCommand.Timer, "300", 300)]
    [InlineData("me lembre em 10 segundos", SystemCommand.Timer, "10", 10)]
    [InlineData("cancele", SystemCommand.Cancel, null, 0)]
    [InlineData("leia a area de transferencia", SystemCommand.Clipboard, null, 0)]
    [InlineData("recarregue o config", SystemCommand.ReloadConfig, null, 0)]
    [InlineData("mostre o vox", SystemCommand.ShowWindow, null, 0)]
    [InlineData("tire um print", SystemCommand.Screenshot, null, 0)]
    [InlineData("captura de tela", SystemCommand.Screenshot, null, 0)]
    [InlineData("minimize tudo", SystemCommand.ShowDesktop, null, 0)]
    [InlineData("mostre a area de trabalho", SystemCommand.ShowDesktop, null, 0)]
    [InlineData("previsao do tempo", SystemCommand.Weather, null, 0)]
    [InlineData("que clima faz", SystemCommand.Weather, null, 0)]
    [InlineData("feche o chrome", SystemCommand.CloseApp, "chrome", 0)]
    [InlineData("fecha o discord", SystemCommand.CloseApp, "discord", 0)]
    [InlineData("fechar o visual studio code", SystemCommand.CloseApp, "visual studio code", 0)]
    public void Parse_detects_new_system_commands(string raw, SystemCommand expected, string? expectedText, int expectedNumber)
    {
        var cmd = CommandParser.Parse(raw, Bindings());
        Assert.NotNull(cmd);
        Assert.Equal(expected, cmd.System);
        Assert.Equal(expectedText, cmd.SystemText);
        Assert.Equal(expectedNumber, cmd.SystemNumber);
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