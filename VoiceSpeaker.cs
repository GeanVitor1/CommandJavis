using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;

namespace Vox;

public class VoiceSpeaker : IDisposable
{
    private const string VoiceName = "faber";
    private static readonly string VoiceDir = Path.Combine(AppContext.BaseDirectory, "voice");
    private static readonly string PiperExe = Path.Combine(VoiceDir, "piper.exe");
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vox", "TtsCache");
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromDays(30);

    private readonly MediaPlayer _player = new();
    private readonly object _gate = new();
    private string? _filePath;
    private bool _disposed;
    private bool _isSpeaking;

    public bool IsSpeaking
    {
        get { lock (_gate) return _isSpeaking; }
    }

    public VoiceSpeaker()
    {
        if (File.Exists(PiperExe))
            Logger.Info($"Piper TTS disponível em: {VoiceDir}");
        else
            Logger.Info("Piper TTS não encontrado; resposta de voz indisponível");
        _player.MediaEnded += (_, _) => { SetSpeaking(false); DeleteCurrentFile(); };
        _player.MediaFailed += (_, _) => { SetSpeaking(false); DeleteCurrentFile(); };
        CleanupOldCache();
    }

    private void SetSpeaking(bool value)
    {
        lock (_gate) _isSpeaking = value;
    }

    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted) return;

        dispatcher.BeginInvoke(async () =>
        {
            if (_disposed) return;
            string? path = null;
            try
            {
                path = await Task.Run(() => GetOrSynthesize(text));
                if (path == null || !File.Exists(path)) return;

                lock (_gate)
                {
                    if (_disposed) return;
                    _player.Stop();
                    DeleteCurrentFile();
                    _filePath = path;
                    _player.Open(new Uri(path));
                    _player.Play();
                    _isSpeaking = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("VoiceSpeaker.Speak", ex);
                if (path != null)
                {
                    try { File.Delete(path); } catch { }
                }
            }
        });
    }

    private static string? GetOrSynthesize(string text)
    {
        var cachePath = GetCachePath(text);
        if (cachePath != null && File.Exists(cachePath))
            return cachePath;

        var synthesized = SynthesizePiper(text);
        if (synthesized == null || cachePath == null)
            return synthesized;

        try
        {
            Directory.CreateDirectory(CacheDir);
            File.Copy(synthesized, cachePath, overwrite: true);
            File.Delete(synthesized);
            return cachePath;
        }
        catch
        {
            return synthesized;
        }
    }

    private static string? GetCachePath(string text)
    {
        try
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(VoiceName + "|" + text));
            var name = Convert.ToHexString(bytes)[..32];
            return Path.Combine(CacheDir, name + ".wav");
        }
        catch
        {
            return null;
        }
    }

    private static string? SynthesizePiper(string text)
    {
        var model = Path.Combine(VoiceDir, VoiceName + ".onnx");
        var config = Path.Combine(VoiceDir, VoiceName + ".onnx.json");
        if (!File.Exists(model) || !File.Exists(config))
        {
            Logger.Info($"Modelo '{VoiceName}' não encontrado em {VoiceDir}");
            return null;
        }
        var path = Path.Combine(Path.GetTempPath(), $"vox_{Guid.NewGuid():N}.wav");
        try
        {
            var psi = new ProcessStartInfo(PiperExe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false)
            };
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(model);
            psi.ArgumentList.Add("--config");
            psi.ArgumentList.Add(config);
            psi.ArgumentList.Add("--output_file");
            psi.ArgumentList.Add(path);

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            var stderrTask = proc.StandardError.ReadToEndAsync();
            proc.StandardInput.Write(text);
            proc.StandardInput.Close();

            if (!proc.WaitForExit(30000))
            {
                try { proc.Kill(); } catch { }
                Logger.Info("piper atingiu o tempo limite");
                return null;
            }
            var err = stderrTask.GetAwaiter().GetResult();
            if (proc.ExitCode != 0)
            {
                Logger.Info($"piper saiu com código {proc.ExitCode}: {err}");
                return null;
            }
            return File.Exists(path) ? path : null;
        }
        catch (Exception ex)
        {
            Logger.Error("VoiceSpeaker.SynthesizePiper", ex);
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            return null;
        }
    }

    private static void CleanupOldCache()
    {
        try
        {
            if (!Directory.Exists(CacheDir)) return;
            var cutoff = DateTime.UtcNow - CacheMaxAge;
            foreach (var file in Directory.EnumerateFiles(CacheDir, "*.wav"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                        File.Delete(file);
                }
                catch
                {
                    // arquivo em uso; ignora
                }
            }
        }
        catch
        {
            // cache é opcional
        }
    }

    private void DeleteCurrentFile()
    {
        var path = _filePath;
        _filePath = null;
        if (path == null) return;
        try { File.Delete(path); } catch { }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _player.Stop();
            _player.Close();
            DeleteCurrentFile();
        }
    }
}