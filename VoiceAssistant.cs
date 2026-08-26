using System.Media;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;

namespace Vox;

public class VoiceAssistant : IDisposable
{
    private const int SpeechPrivacyError = unchecked((int)0x80045509);
    private const string PrivacyKey = @"SOFTWARE\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy";
    private const double MinConfidence = 0.35;
    private static readonly string[] WakePhrases = { "ei vox", "hey vox", "oi vox", "ola vox", "e ai vox" };

    private static readonly string[] DayNames =
        { "domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado" };
    private static readonly string[] MonthNames =
        { "janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" };

    private readonly ShortcutManager _manager;
    private readonly VoiceSpeaker _speaker;
    private readonly List<string> _commandWords = new();
    private readonly List<string> _history = new();
    private SpeechRecognizer? _recognizer;
    private GlobalTalkHook? _talkHook;
    private Task<SpeechRecognitionResult>? _pending;
    private CancellationTokenSource? _timerCts;
    private bool _listening;
    private bool _privacyRetried;
    private bool _disposed;
    private bool _wakeEnabled;
    private bool _wakeLoopRunning;
    private DateTime _wakeCooldownUntil;
    private string _microphoneId = "";

    public event Action<string>? StatusChanged;
    public event Action<string>? PartialResult;
    public event Action? PrivacyFixRequested;
    public event Action<bool>? ListeningChanged;
    public event Action<string>? CommandExecuted;

    public IReadOnlyList<string> History => _history;

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

    public void SetWakeWordEnabled(bool enabled)
    {
        if (_wakeEnabled == enabled) return;
        _wakeEnabled = enabled;
        if (enabled && !_wakeLoopRunning)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.HasShutdownStarted)
                _ = dispatcher.BeginInvoke(() => _ = WakeLoopAsync());
        }
    }

    public void SetMicrophone(string deviceId)
    {
        if (string.Equals(deviceId, _microphoneId, StringComparison.OrdinalIgnoreCase))
            return;
        Logger.Info($"SetMicrophone: '{_microphoneId}' -> '{deviceId}' (reset recognizer)");
        _microphoneId = deviceId ?? "";
        ResetRecognizer();
    }

    private void ResetRecognizer()
    {
        Logger.Info("ResetRecognizer");
        try { _recognizer?.Dispose(); } catch (Exception ex) { Logger.Error("ResetRecognizer Dispose", ex); }
        _recognizer = null;
    }

    private async Task WakeLoopAsync()
    {
        _wakeLoopRunning = true;
        while (_wakeEnabled && !_disposed)
        {
            try
            {
                if (_listening || _speaker.IsSpeaking || DateTime.UtcNow < _wakeCooldownUntil)
                {
                    await Task.Delay(300);
                    continue;
                }
                if (_recognizer == null && !await EnsureReadyAsync())
                {
                    await Task.Delay(5000);
                    continue;
                }

                var result = await _recognizer!.RecognizeAsync().AsTask();
                if (_disposed || !_wakeEnabled) break;

                if (result?.Status == SpeechRecognitionResultStatus.Success &&
                    !string.IsNullOrWhiteSpace(result.Text) &&
                    result.RawConfidence >= MinConfidence &&
                    IsWakePhrase(result.Text))
                {
                    _wakeCooldownUntil = DateTime.UtcNow.AddSeconds(5);
                    var text = result.Text.Trim();
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher != null && !dispatcher.HasShutdownStarted)
                        _ = dispatcher.BeginInvoke(() =>
                        {
                            SetStatus($"Você disse: \"{text}\"");
                            _ = HandleCommandAsync(text);
                        });
                }
            }
            catch (Exception ex)
            {
                if (!_disposed)
                {
                    Logger.Error("WakeLoopAsync", ex);
                    await Task.Delay(3000);
                }
            }
        }
        _wakeLoopRunning = false;
    }

    private static bool IsWakePhrase(string text)
    {
        var t = RemoveAccents(text.ToLowerInvariant()).Trim();
        foreach (var w in WakePhrases)
            if (t == w || t.StartsWith(w + " ", StringComparison.Ordinal))
                return true;
        return t == "vox" || t.StartsWith("vox ", StringComparison.Ordinal);
    }

    private static string RemoveAccents(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString();
    }

    public void ExecutePhrase(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted) return;
        if (dispatcher.CheckAccess())
        {
            SetStatus($"Você disse: \"{phrase}\"");
            _ = HandleCommandAsync(phrase);
        }
        else
        {
            dispatcher.BeginInvoke(() =>
            {
                SetStatus($"Você disse: \"{phrase}\"");
                _ = HandleCommandAsync(phrase);
            });
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
        PlayStartFeedback();
        SetStatus("Ouvindo... fale agora");
        try
        {
            if (_wakeEnabled)
            {
                try { await _recognizer!.StopRecognitionAsync(); } catch { }
            }
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
        catch (Exception ex)
        {
            _listening = false;
            ListeningChanged?.Invoke(false);
            Logger.Error("StartAsync", ex);
            if (IsMicrophoneFailure(ex))
            {
                if (!_privacyRetried)
                {
                    _privacyRetried = true;
                    ResetRecognizer();
                    SetStatus("Microfone não disponível. Tentando novamente...");
                    await Task.Delay(500);
                    return await StartAsync();
                }
                SetStatus("Microfone não disponível. Abra as Configurações e selecione o microfone.");
                PrivacyFixRequested?.Invoke();
                return false;
            }
            SetStatus("Microfone não disponível");
            return false;
        }
    }

    private static bool IsMicrophoneFailure(Exception ex)
    {
        if (ex.HResult == unchecked((int)0x8004550B)) return true;
        if (ex.Message.Contains("microphone", StringComparison.OrdinalIgnoreCase)) return true;
        if (ex.Message.Contains("audio", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
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
        Exception? taskEx = null;
        try { result = await task; } catch (Exception ex) { taskEx = ex; Logger.Error("ProcessAsync await", ex); }
        if (!ReferenceEquals(task, _pending)) return;
        _pending = null;
        _listening = false;
        ListeningChanged?.Invoke(false);

        var status = result?.Status.ToString() ?? taskEx?.GetType().Name ?? "null";
        Logger.Info($"ProcessAsync: Status={status} Text='{result?.Text}' Confidence={result?.RawConfidence:0.00} HResult=0x{(taskEx?.HResult ?? 0):X8}");

        var text = result?.Text?.Trim();
        if (result == null || string.IsNullOrEmpty(text))
        {
            PlayEndFeedback();
            // Diagnóstico mais útil
            if (taskEx != null)
            {
                SetStatus($"Erro de microfone: {taskEx.Message}");
                Logger.Error("ProcessAsync task exception", taskEx);
                return;
            }
            if (result?.Status == SpeechRecognitionResultStatus.MicrophoneUnavailable)
            {
                SetStatus("Microfone não disponível — verifique nas Configurações > Microfone");
                return;
            }
            if (result?.Status == SpeechRecognitionResultStatus.AudioQualityFailure)
            {
                SetStatus("Qualidade de áudio muito baixa — fale mais perto do microfone");
                return;
            }
            if (result?.Status == SpeechRecognitionResultStatus.NetworkFailure)
            {
                SetStatus("Falha de rede no reconhecimento");
                return;
            }
            // Timeout por silêncio
            SetStatus("Não ouvi nada — clique no microfone e fale claramente em até 8s");
            return;
        }

        if (result.Status == SpeechRecognitionResultStatus.Success && result.RawConfidence < MinConfidence)
        {
            PlayEndFeedback();
            Logger.Info($"Baixa confianca: {result.RawConfidence:0.00} para '{text}' — threshold {MinConfidence}");
            SetStatus($"Não entendi bem (\"{text}\") — tente falar mais claro");
            Speak("Não entendi muito bem, tente falar mais claro");
            return;
        }

        if (result.Status != SpeechRecognitionResultStatus.Success)
        {
            PlayEndFeedback();
            SetStatus($"Reconhecimento falhou: {result.Status} — tente de novo");
            return;
        }

        PlayEndFeedback();
        SetStatus($"Você disse: \"{text}\"");
        await HandleCommandAsync(text);
    }

    private async Task HandleCommandAsync(string raw)
    {
        var cmd = CommandParser.Parse(raw, _manager.All);
        if (cmd == null)
        {
            SetStatus("Não reconheci esse comando");
            Speak("Não entendi, tente de novo");
            return;
        }

        CommandExecuted?.Invoke(raw);
        _history.Insert(0, raw);
        while (_history.Count > 20)
            _history.RemoveAt(_history.Count - 1);

        if (cmd.System is SystemCommand sys)
        {
            await HandleSystemCommandAsync(sys, cmd.SystemText, cmd.SystemNumber);
            return;
        }

        var binding = cmd.Binding!;
        if (cmd.Query != null && binding.Category == "site")
        {
            var url = SearchEngines.Build(binding.SearchTemplate, binding.Target, cmd.Query)
                ?? $"https://www.google.com/search?q={Uri.EscapeDataString(cmd.Query)}";
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

    private async Task HandleSystemCommandAsync(SystemCommand sys, string? text, int number)
    {
        switch (sys)
        {
            case SystemCommand.PlayPause:
            case SystemCommand.Next:
            case SystemCommand.Previous:
            case SystemCommand.VolumeUp:
            case SystemCommand.VolumeDown:
                SystemActions.Run(sys);
                SetStatus(SystemActions.Label(sys));
                Speak("Pronto");
                break;

            case SystemCommand.VolumeSet:
                SystemActions.Run(SystemCommand.VolumeSet, number);
                SetStatus($"Volume em {number}%");
                Speak($"Volume em {number} por cento");
                break;

            case SystemCommand.Mute:
                SystemActions.Run(SystemCommand.Mute);
                var muted = VolumeController.IsMuted();
                SetStatus(muted ? "Som mutado" : "Som ativado");
                Speak(muted ? "Som mutado" : "Som ativado");
                break;

            case SystemCommand.Lock:
            case SystemCommand.Sleep:
            case SystemCommand.Hibernate:
                if (ConfirmDestructive(SystemActions.Label(sys)))
                {
                    SystemActions.Run(sys);
                    SetStatus(SystemActions.Label(sys));
                    Speak("Pronto");
                }
                else
                {
                    SetStatus("Ação cancelada");
                    Speak("Cancelado");
                }
                break;

            case SystemCommand.Time:
                var time = FormatTime(DateTime.Now);
                SetStatus(time);
                Speak(time);
                break;

            case SystemCommand.Date:
                var date = FormatDate(DateTime.Now);
                SetStatus(date);
                Speak(date);
                break;

            case SystemCommand.Calc:
                var value = Calculator.Evaluate(text ?? "");
                if (value == null)
                {
                    SetStatus("Não consegui calcular isso");
                    Speak("Não consegui calcular isso");
                }
                else
                {
                    var s = FormatNumber(value.Value);
                    SetStatus($"{text} = {s}");
                    Speak($"O resultado é {s}");
                }
                break;

            case SystemCommand.Clipboard:
                SpeakClipboard();
                break;

            case SystemCommand.Theme:
                var theme = text ?? "system";
                Theme.Apply(theme);
                Config.SaveAppearance(new AppearanceSettings { Theme = theme });
                var themeLabel = theme == "dark" ? "escuro" : theme == "light" ? "claro" : "do sistema";
                SetStatus($"Tema {themeLabel} ativado");
                Speak($"Tema {themeLabel} ativado");
                break;

            case SystemCommand.ShowWindow:
                if (System.Windows.Application.Current is App app)
                    app.ActivateMainWindow();
                SetStatus("Aqui estou");
                Speak("Aqui estou");
                break;

            case SystemCommand.ReloadConfig:
                if (System.Windows.Application.Current is App reloadApp)
                    reloadApp.ReloadConfig();
                SetStatus("Configuração recarregada");
                Speak("Configuração recarregada");
                break;

            case SystemCommand.Timer:
                ScheduleTimer(number);
                break;

            case SystemCommand.Cancel:
                var hadTimer = CancelTimer();
                SetStatus(hadTimer ? "Lembrete cancelado" : "Nada para cancelar");
                Speak(hadTimer ? "Lembrete cancelado" : "Não há nada para cancelar");
                break;

            case SystemCommand.Screenshot:
                var file = SystemActions.CaptureScreen();
                if (file != null)
                {
                    SetStatus($"Print salvo em {file}");
                    Speak("Print salvo na pasta Imagens");
                }
                else
                {
                    SetStatus("Não consegui capturar a tela");
                    Speak("Não consegui capturar a tela");
                }
                break;

            case SystemCommand.ShowDesktop:
                WindowControl.ShowDesktop();
                SetStatus("Mostrando a área de trabalho");
                Speak("Pronto");
                break;

            case SystemCommand.CloseApp:
                CloseApp(text ?? "");
                break;

            case SystemCommand.MinimizeApp:
                MinimizeApp(text ?? "");
                break;

            case SystemCommand.MaximizeApp:
                MaximizeApp(text ?? "");
                break;

            case SystemCommand.FocusApp:
                FocusApp(text ?? "");
                break;

            case SystemCommand.Weather:
                await SpeakWeatherAsync();
                break;
        }
    }

    private void MinimizeApp(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Qual aplicativo devo minimizar?");
            Speak("Qual aplicativo devo minimizar?");
            return;
        }
        if (WindowControl.MinimizeAppByName(name))
        {
            SetStatus($"Minimizei {name}");
            Speak($"Minimizei {name}");
            return;
        }
        // fallback: tenta fechar? nao, informa nao encontrado
        SetStatus($"Não encontrei o {name} aberto");
        Speak($"Não encontrei o {name} aberto");
    }

    private void MaximizeApp(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Qual aplicativo devo maximizar?");
            Speak("Qual aplicativo devo maximizar?");
            return;
        }
        if (WindowControl.MaximizeAppByName(name))
        {
            SetStatus($"Maximizei {name}");
            Speak($"Maximizei {name}");
            return;
        }
        SetStatus($"Não encontrei o {name} aberto");
        Speak($"Não encontrei o {name} aberto");
    }

    private void FocusApp(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Qual aplicativo devo focar?");
            Speak("Qual aplicativo devo focar?");
            return;
        }
        if (name.IndexOf("vox", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (System.Windows.Application.Current is App voxApp) voxApp.ActivateMainWindow();
            SetStatus("Aqui estou");
            Speak("Aqui estou");
            return;
        }
        if (WindowControl.FocusAppByName(name))
        {
            SetStatus($"Mostrando {name}");
            Speak($"Mostrando {name}");
            return;
        }
        // Se nao esta aberto, tenta abrir via hotkey binding
        var b = _manager.All.FirstOrDefault(x => RemoveAccents(x.Description ?? "").ToLowerInvariant().Contains(RemoveAccents(name).ToLowerInvariant(), StringComparison.Ordinal));
        if (b != null)
        {
            _manager.Execute(b);
            SetStatus($"Abrindo {b.Description}");
            Speak($"Abrindo {b.Description}");
            return;
        }
        SetStatus($"Não encontrei o {name}");
        Speak($"Não encontrei o {name}");
    }

    private void CloseApp(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Qual aplicativo devo fechar?");
            Speak("Qual aplicativo devo fechar?");
            return;
        }
        if (name.IndexOf("vox", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            SetStatus("Não consigo me fechar por voz");
            Speak("Não posso me fechar por voz");
            return;
        }

        HotkeyBinding? match = null;
        foreach (var b in _manager.All)
        {
            if (b.Category != "app" || string.IsNullOrWhiteSpace(b.Target) ||
                !b.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;
            if (RemoveAccents(b.Description ?? "").ToLowerInvariant()
                .Contains(RemoveAccents(name).ToLowerInvariant(), StringComparison.Ordinal))
            {
                match = b;
                break;
            }
        }

        if (match != null && WindowControl.CloseProcessesByPath(match.Target))
        {
            SetStatus($"Fechei {match.Description}");
            Speak($"Fechei {match.Description}");
            return;
        }

        if (WindowControl.CloseAppByName(name))
        {
            SetStatus($"Fechei {name}");
            Speak($"Fechei {name}");
            return;
        }

        SetStatus($"Não encontrei o {name} aberto");
        Speak($"Não encontrei o {name} aberto");
    }

    private async Task SpeakWeatherAsync()
    {
        SetStatus("Consultando o clima...");
        var raw = await Task.Run(FetchWeather);
        if (raw == null)
        {
            SetStatus("Não consegui consultar o clima agora");
            Speak("Não consegui consultar o clima agora");
            return;
        }
        var spoken = raw
            .Replace("+", " ", StringComparison.Ordinal)
            .Replace("°C", " graus ", StringComparison.Ordinal)
            .Replace("°", " graus ", StringComparison.Ordinal)
            .Replace("%", " por cento ", StringComparison.Ordinal)
            .Replace("km/h", " quilômetros por hora ", StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();
        SetStatus($"Clima: {raw}");
        Speak($"Agora em {spoken}");
    }

    private static string? FetchWeather()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Vox/1.0");
            var raw = client.GetStringAsync("https://wttr.in/?format=%l:+%t+%h+%w").GetAwaiter().GetResult();
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }
        catch
        {
            return null;
        }
    }

    private bool ConfirmDestructive(string label)
    {
        Window? owner = null;
        var main = System.Windows.Application.Current?.MainWindow;
        if (main != null && main.IsVisible)
            owner = main;
        return ConfirmWindow.Show(owner, $"Executar \"{label}\"?");
    }

private void SpeakClipboard()
    {
        try
        {
            var text = System.Windows.Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("A área de transferência está vazia");
                Speak("A área de transferência está vazia");
                return;
            }
            if (text.Length > 600)
                text = text[..600] + "...";
            SetStatus($"Lendo a área de transferência ({text.Length} caracteres)");
            Speak(text);
        }
        catch
        {
            SetStatus("Não consegui acessar a área de transferência");
            Speak("Não consegui acessar a área de transferência");
        }
    }

    private static string FormatTime(DateTime now)
    {
        var h = now.Hour;
        var m = now.Minute;
        if (h == 0 && m == 0) return "É meia-noite";
        if (h == 12 && m == 0) return "É meio-dia";
        var hora = h == 1 ? "1 hora" : $"{h} horas";
        if (m == 0) return $"São {hora} em ponto";
        if (m == 30) return $"São {hora} e meia";
        return $"São {hora} e {m} minutos";
    }

private static string FormatDate(DateTime now)
    {
        var day = DayNames[(int)now.DayOfWeek];
        var month = MonthNames[now.Month - 1];
        return $"Hoje é {day}, {now.Day} de {month} de {now.Year}";
    }

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 1e-9)
            return ((long)Math.Round(value)).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        return value.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    }

    private void ScheduleTimer(int seconds)
    {
        CancelTimer();
        if (seconds <= 0) return;
        _timerCts = new CancellationTokenSource();
        var token = _timerCts.Token;
        var label = FormatDuration(seconds);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;
            _ = dispatcher.BeginInvoke(() =>
            {
                Toaster.Show("Vox", $"Lembrete: passaram {label}");
                Speak($"Passaram {label}");
                SetStatus($"Lembrete: {label}");
            });
        });

SetStatus($"Lembrete agendado para {label}");
        Speak($"Lembrete agendado para {label}");
    }

    private bool CancelTimer()
    {
        if (_timerCts == null) return false;
        _timerCts.Cancel();
        _timerCts.Dispose();
        _timerCts = null;
        return true;
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds % 3600 == 0) return seconds / 3600 + (seconds / 3600 == 1 ? " hora" : " horas");
        if (seconds % 60 == 0) return seconds / 60 + (seconds / 60 == 1 ? " minuto" : " minutos");
        return seconds + " segundos";
    }

public void TestSpeech()
    {
        Speak("Olá, eu sou o Vox. Comando de voz funcionando.");
        SetStatus("Testando a voz de resposta");
    }

    private async Task<bool> EnsureReadyAsync()
    {
        if (_recognizer != null) return true;
        try
        {
            if (!string.IsNullOrWhiteSpace(_microphoneId))
            {
                var ok = MicrophoneSelector.SetDefaultCaptureDevice(_microphoneId);
                Logger.Info($"SetDefaultCaptureDevice({ShortId(_microphoneId)}) -> {ok}");
                if (!ok)
                {
                    SetStatus("Não consegui trocar o microfone. Verifique a seleção em Configurações.");
                    // continua mesmo se falhou, tenta com padrão do Windows
                }
            }

            var tag = PickLanguageTag();
            Logger.Info($"EnsureReady: idioma escolhido={tag} grammarLangs={string.Join(",", SpeechRecognizer.SupportedGrammarLanguages.Select(l=>l.LanguageTag))}");
            if (tag == null)
            {
                SetStatus("Nenhum idioma de fala instalado — instale pt-BR em Configurações > Idioma");
                return false;
            }

            var rec = new SpeechRecognizer(new Language(tag));
            rec.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(8);
            rec.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.2);
            rec.Timeouts.BabbleTimeout = TimeSpan.FromSeconds(10);

            rec.HypothesisGenerated += (_, args) =>
            {
                var partial = args.Hypothesis?.Text;
                if (string.IsNullOrWhiteSpace(partial)) return;
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.HasShutdownStarted) return;
                dispatcher.BeginInvoke(() => PartialResult?.Invoke($"Ouvindo: {partial}"));
            };

            rec.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "vox"));
            SpeechRecognitionCompilationResult compiledDict;
            try
            {
                compiledDict = await rec.CompileConstraintsAsync();
                Logger.Info($"Compile dictation: {compiledDict.Status}");
                if (compiledDict.Status != SpeechRecognitionResultStatus.Success)
                    throw new InvalidOperationException($"dictation compile {compiledDict.Status}");
            }
            catch (Exception exDict)
            {
                Logger.Info($"Dictation falhou ({exDict.Message}), tentando ListConstraint com {_commandWords.Count} palavras");
                rec.Constraints.Clear();
                rec.Constraints.Add(new SpeechRecognitionListConstraint(_commandWords, "comandos"));
                var compiled = await rec.CompileConstraintsAsync();
                Logger.Info($"Compile list: {compiled.Status}");
                if (compiled.Status != SpeechRecognitionResultStatus.Success)
                {
                    rec.Dispose();
                    SetStatus($"Reconhecimento de voz indisponível ({compiled.Status})");
                    return false;
                }
            }

            _recognizer = rec;
            Logger.Info("Recognizer pronto com sucesso");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("EnsureReadyAsync", ex);
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

    private static void PlayStartFeedback()
    {
        // Troca Beep (som de erro) por Asterisk mais suave; usuário relatou "barulho no windows"
        try { SystemSounds.Asterisk.Play(); } catch { }
    }

    private static void PlayEndFeedback()
    {
        try { SystemSounds.Asterisk.Play(); } catch { }
    }

    private static string ShortId(string? id) => string.IsNullOrEmpty(id) ? "(vazio)" : id.Length > 50 ? id.Substring(id.Length - 50) : id;

    private void RebuildCommandWords()
    {
        var words = new List<string>
        {
            "vox", "ei vox", "hey vox", "oi vox",
            "abra", "abre", "abrir", "abra o", "abre o", "abrir o",
            "abra o site", "abrir o site",
            "toca", "toque", "tocar", "pesquise", "pesquisar",
            "pausa", "play", "continua", "proxima", "anterior",
            "aumenta o volume", "diminui o volume", "mudo",
            "bloqueia a tela", "dormir", "hibernar",
            "que horas sao", "que horas", "que dia e hoje", "que dia e",
            "quanto e", "calcule", "calcula",
            "volume", "volume maximo", "volume minimo", "sem volume",
            "tema escuro", "tema claro", "tema do sistema", "modo escuro", "modo claro",
            "recarregue o config", "recarregar o config",
            "mostre o vox", "abra o vox", "abre o vox",
            "leia a area de transferencia", "clipboard",
"lembre em", "me lembre em", "alarme em", "timer de",
            "cancelar", "cancele", "cancela",
            "segundos", "minutos", "horas", "meia hora",
            "tire um print", "tira um print", "captura de tela", "screenshot",
            "fecha o", "feche o", "fechar o", "fecha", "feche", "fechar",
            "minimize tudo", "minimize as janelas", "mostre a area de trabalho", "area de trabalho",
            "previsao do tempo", "que clima faz", "como esta o tempo", "clima", "tempo hoje"
        };
        foreach (var b in _manager.All)
        {
            var baseNames = new List<string>();
            if (!string.IsNullOrWhiteSpace(b.Description))
            {
                var desc = b.Description.Trim();
                words.Add(desc);
                baseNames.Add(desc);
            }
            if (b.Category == "site" && Uri.TryCreate(b.Target, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).Split('.')[0];
                words.Add(host);
                baseNames.Add(host);
            }
            if (b.Description != null && b.Description.Contains("visual studio code", StringComparison.OrdinalIgnoreCase))
                baseNames.Add("vs code");

            // Variáveis automáticas: ao criar "abroba", já funciona "abra abroba", "feche abroba", "pesquise abroba" sem config extra
            foreach (var n in baseNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var lower = n.ToLowerInvariant();
                words.Add($"abra {lower}");
                words.Add($"abrir {lower}");
                words.Add($"abre {lower}");
                words.Add($"feche {lower}");
                words.Add($"fecha {lower}");
                words.Add($"fechar {lower}");
                words.Add($"pesquise {lower}");
                words.Add($"pesquise no {lower}");
                words.Add($"procure {lower}");
                words.Add($"tocar {lower}");
                words.Add($"minimizar {lower}");
                words.Add($"minimizar o {lower}");
                words.Add($"minimizar a {lower}");
                words.Add($"minimize {lower}");
                words.Add($"maximizar {lower}");
                words.Add($"maximizar o {lower}");
                words.Add($"restaurar {lower}");
                words.Add($"focar {lower}");
                words.Add($"foca {lower}");
                words.Add($"mostrar {lower}");
                words.Add($"mostra {lower}");
                words.Add($"traga {lower}");
            }
        }
        _commandWords.Clear();
        _commandWords.AddRange(words.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private void SetStatus(string s)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.HasShutdownStarted && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => StatusChanged?.Invoke(s));
            return;
        }
        StatusChanged?.Invoke(s);
    }

    private void Speak(string text) => _speaker.Speak(text);

public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wakeEnabled = false;
        _manager.Changed -= RebuildCommandWords;
        try { _talkHook?.Dispose(); } catch { }
        try { _recognizer?.Dispose(); } catch { }
        _speaker.Dispose();
    }
}


