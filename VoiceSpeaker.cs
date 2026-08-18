using System.IO;
using System.Windows.Media;
using Windows.Media.SpeechSynthesis;

namespace JarvisComando;

public class VoiceSpeaker : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private readonly MediaPlayer _player = new();
    private readonly object _gate = new();
    private string? _filePath;

    public VoiceSpeaker()
    {
        var voice = PickBestVoice();
        if (voice != null)
            _synth.Voice = voice;
        _player.MediaEnded += (_, _) => DeleteCurrentFile();
        _player.MediaFailed += (_, _) => DeleteCurrentFile();
    }

    public void Speak(string text)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        dispatcher.BeginInvoke(async () =>
        {
            try
            {
                using var ss = await _synth.SynthesizeTextToStreamAsync(text);
                var path = Path.Combine(Path.GetTempPath(), $"jarvis_{Guid.NewGuid():N}.wav");
                using (var fs = File.Create(path))
                using (var source = ss.AsStreamForRead())
                    source.CopyTo(fs);

                lock (_gate)
                {
                    _player.Stop();
                    DeleteCurrentFile();
                    _filePath = path;
                    _player.Open(new Uri(path));
                    _player.Play();
                }
            }
            catch
            {
            }
        });
    }

    private void DeleteCurrentFile()
    {
        var path = _filePath;
        _filePath = null;
        if (path == null) return;
        try { File.Delete(path); } catch { }
    }

    private static VoiceInformation? PickBestVoice()
    {
        var voices = SpeechSynthesizer.AllVoices
            .Where(v => v.Language.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (voices.Count == 0)
            voices = SpeechSynthesizer.AllVoices.ToList();
        if (voices.Count == 0)
            return null;

        var natural = voices.FirstOrDefault(v =>
            v.DisplayName.Contains("Natural", StringComparison.OrdinalIgnoreCase) ||
            v.Description.Contains("Natural", StringComparison.OrdinalIgnoreCase));
        if (natural != null)
            return natural;

        return voices.FirstOrDefault(v => v.DisplayName.Contains("Maria", StringComparison.OrdinalIgnoreCase))
            ?? voices.FirstOrDefault(v => v.Gender == VoiceGender.Female)
            ?? voices.First();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _player.Stop();
            _player.Close();
            DeleteCurrentFile();
        }
        _synth.Dispose();
    }
}