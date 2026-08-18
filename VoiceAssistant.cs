using System.Text;
using Microsoft.Win32;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;

namespace JarvisComando;

public class VoiceAssistant : IDisposable
{
    private const int SpeechPrivacyError = unchecked((int)0x80045509);
    private const string PrivacyKey = @"SOFTWARE\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy";
    private const double MinConfidence = 0.35;

    private readonly ShortcutManager _manager;
    private readonly VoiceSpeaker _speaker;
    private readonly List<string> _commandWords = new();
    private SpeechRecognizer? _recognizer;
    private GlobalTalkHook? _talkHook;
    private Task<SpeechRecognitionResult>? _pending;
    private bool _listening;
    private bool _privacyRetried;
    private bool _disposed;

    public event Action<string>? StatusChanged;
    public event Action? PrivacyFixRequested;
    public event Action<bool>? ListeningChanged;

    public bool IsListening => _listening;

    public VoiceAssistant(ShortcutManager manager)
    {
        _manager = manager;
        _speaker = new VoiceSpeaker();
        _manager.Changed += RebuildCommandWords;
        RebuildCommandWords();
    }

    public void ConfigureTalkHotkey(string keyName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyName))
            {
                _talkHook?.Dispose();
                _talkHook = null;
                return;
            }
            if (!Enum.TryParse<System.Windows.Forms.Keys>(keyName, true, out var key))
                key = System.Windows.Forms.Keys.F9;

            if (_talkHook != null && _talkHook.KeyEquals(key))
                return;

            _talkHook?.Dispose();
            _talkHook = new GlobalTalkHook(key);
            _talkHook.TalkPressed += OnGlobalPressed;
            _talkHook.TalkReleased += OnGlobalReleased;
            _talkHook.HookFailed += () =>
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null)
                    dispatcher.BeginInvoke(() => SetStatus("Não foi possível instalar a tecla global de fala"));
                else
                    SetStatus("Não foi possível instalar a tecla global de fala");
            };
            _talkHook.Start();
        }
        catch
        {
            _talkHook = null;
        }
    }

    private void OnGlobalPressed()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        dispatcher.BeginInvoke(async () => await StartAsync());
    }

    private void OnGlobalReleased()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        dispatcher.BeginInvoke(async () => await StopAsync());
    }

    public async Task<bool> StartAsync()
    {
        if (_listening) return true;
        if (!await EnsureReadyAsync()) return false;
        _listening = true;
        ListeningChanged?.Invoke(true);
        SetStatus("Ouvindo... fale agora");
        try
        {
            _pending = _recognizer!.RecognizeAsync().AsTask();
            _ = ProcessAsync(_pending);
            return true;
        }
        catch (Exception ex) when (ex.HResult == SpeechPrivacyError)
        {
            _listening = false;
            ListeningChanged?.Invoke(false);
            if (!_privacyRetried && TryAcceptSpeechPrivacy())
            {
                _privacyRetried = true;
                SetStatus("Reconhecimento de fala ativado");
                return await StartAsync();
            }
            PrivacyFixRequested?.Invoke();
            SetStatus("Ative a fala em Configurações > Privacidade e segurança > Fala");
            return false;
        }
        catch
        {
            _listening = false;
            ListeningChanged?.Invoke(false);
            SetStatus("Microfone não disponível");
            return false;
        }
    }

    public async Task StopAsync()
    {
        if (!_listening || _recognizer == null) return;
        _listening = false;
        ListeningChanged?.Invoke(false);
        try { await _recognizer.StopRecognitionAsync().AsTask(); } catch { }
        try { if (_pending != null) await _pending; } catch { }
    }

    private async Task ProcessAsync(Task<SpeechRecognitionResult> task)
    {
        SpeechRecognitionResult? result = null;
        try { result = await task; } catch { }
        if (!ReferenceEquals(task, _pending)) return;
        _pending = null;
        _listening = false;
        ListeningChanged?.Invoke(false);

        var text = result?.Text?.Trim();
        if (result == null || string.IsNullOrEmpty(text))
        {
            SetStatus(result?.Status == SpeechRecognitionResultStatus.MicrophoneUnavailable
                ? "Microfone não disponível"
                : "Não entendi, tente de novo");
            return;
        }

        if (result.Status == SpeechRecognitionResultStatus.Success && result.RawConfidence < MinConfidence)
        {
            SetStatus($"Não entendi, tente de novo (\"{text}\")");
            return;
        }

        SetStatus($"Você disse: \"{text}\"");
        HandleCommand(text);
    }

    private void HandleCommand(string raw)
    {
        var cmd = CommandParser.Parse(raw, _manager.All);
        if (cmd == null)
        {
            SetStatus("Não reconheci esse comando");
            Speak("Não entendi, tente de novo");
            return;
        }

        if (cmd.System is SystemCommand sys)
        {
            SystemActions.Run(sys);
            SetStatus(SystemActions.Label(sys));
            Speak("Pronto");
            return;
        }

        var binding = cmd.Binding!;
        if (cmd.Query != null && binding.Category == "site" && TrySearchUrl(binding, cmd.Query, out var url))
        {
            _manager.Execute(new HotkeyBinding { Action = "open", Target = url, Description = binding.Description });
            SetStatus($"Abrindo {binding.Description}: {cmd.Query}");
            Speak($"Abrindo {binding.Description} com {cmd.Query}");
        }
        else
        {
            _manager.Execute(binding);
            SetStatus($"Abrindo {binding.Description}");
            Speak($"Abrindo {binding.Description}");
        }
    }

    public void TestSpeech()
    {
        Speak("Olá, eu sou o Jarvis. Comando de voz funcionando.");
        SetStatus("Testando a voz de resposta");
    }

    private static bool TrySearchUrl(HotkeyBinding b, string query, out string url)
    {
        url = "";
        if (!Uri.TryCreate(b.Target, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
        var q = Uri.EscapeDataString(query);
        if (host.StartsWith("youtube", StringComparison.OrdinalIgnoreCase))
        {
            url = $"https://www.youtube.com/results?search_query={q}";
            return true;
        }
        if (host.StartsWith("google", StringComparison.OrdinalIgnoreCase))
        {
            url = $"https://www.google.com/search?q={q}";
            return true;
        }
        return false;
    }

    private async Task<bool> EnsureReadyAsync()
    {
        if (_recognizer != null) return true;
        try
        {
            var tag = PickLanguageTag();
            if (tag == null)
            {
                SetStatus("Nenhum idioma de fala instalado");
                return false;
            }

            var rec = new SpeechRecognizer(new Language(tag));
            rec.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(8);
            rec.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.2);
            rec.Timeouts.BabbleTimeout = TimeSpan.FromSeconds(10);

            rec.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "jarvis"));
            try
            {
                var compiled = await rec.CompileConstraintsAsync();
                if (compiled.Status != SpeechRecognitionResultStatus.Success)
                    throw new InvalidOperationException("dictation unavailable");
            }
            catch
            {
                rec.Constraints.Clear();
                rec.Constraints.Add(new SpeechRecognitionListConstraint(_commandWords, "comandos"));
                var compiled = await rec.CompileConstraintsAsync();
                if (compiled.Status != SpeechRecognitionResultStatus.Success)
                {
                    rec.Dispose();
                    SetStatus("Reconhecimento de voz indisponível");
                    return false;
                }
            }

            _recognizer = rec;
            return true;
        }
        catch (Exception ex)
        {
            SetStatus("Erro ao preparar a voz: " + ex.Message);
            return false;
        }
    }

    private static string? PickLanguageTag()
    {
        string? fallback = null;
        foreach (var language in SpeechRecognizer.SupportedGrammarLanguages)
        {
            var tag = language.LanguageTag;
            if (string.Equals(tag, "pt-BR", StringComparison.OrdinalIgnoreCase))
                return tag;
            fallback ??= tag;
        }
        return fallback;
    }

    private static bool TryAcceptSpeechPrivacy()
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(PrivacyKey, writable: true);
            if (key == null) return false;
            var current = key.GetValue("HasAccepted");
            if (current is int i && i == 1) return true;
            key.SetValue("HasAccepted", 1, RegistryValueKind.DWord);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RebuildCommandWords()
    {
        var words = new List<string>
        {
            "jarvis", "ei jarvis", "hey jarvis", "oi jarvis",
            "abra", "abre", "abrir", "abra o", "abre o", "abrir o",
            "abra o site", "abrir o site",
            "toca", "toque", "tocar", "pesquise", "pesquisar",
            "pausa", "play", "continua", "proxima", "anterior",
            "aumenta o volume", "diminui o volume", "mudo",
            "bloqueia a tela", "dormir", "hibernar"
        };
        foreach (var b in _manager.All)
        {
            if (!string.IsNullOrWhiteSpace(b.Description))
                words.Add(b.Description.Trim());
            if (b.Category == "site" && Uri.TryCreate(b.Target, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
                words.Add(host.Split('.')[0]);
            }
        }
        _commandWords.Clear();
        _commandWords.AddRange(words.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private void SetStatus(string s) => StatusChanged?.Invoke(s);

    private void Speak(string text) => _speaker.Speak(text);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _manager.Changed -= RebuildCommandWords;
        try { _talkHook?.Dispose(); } catch { }
        try { _recognizer?.Dispose(); } catch { }
        _speaker.Dispose();
    }
}