using System.Windows;
using System.Windows.Input;

namespace Vox;

public partial class SettingsWindow : Window
{
    private readonly VoiceAssistant _voice;
    private bool _capturing;
    private string _talkKey = "F9";

    public SettingsWindow(VoiceAssistant voice)
    {
        _voice = voice;
        InitializeComponent();
        var s = Config.LoadVoice();
        _talkKey = string.IsNullOrWhiteSpace(s.TalkHotkey) ? "F9" : s.TalkHotkey;
        EnableVoiceCheck.IsChecked = s.Enabled;
        WakeWordCheck.IsChecked = s.Enabled && s.WakeWord;
        HotkeyDisplay.Text = _talkKey;
        HotkeyButton.IsEnabled = s.Enabled;
        WakeWordCheck.IsEnabled = s.Enabled;
        AutoStartCheck.IsChecked = App.IsAutoStart();
        var appearance = Config.LoadAppearance();
        if (appearance.Theme.Equals("dark", StringComparison.OrdinalIgnoreCase))
            ThemeDark.IsChecked = true;
        else if (appearance.Theme.Equals("light", StringComparison.OrdinalIgnoreCase))
            ThemeLight.IsChecked = true;
        else
            ThemeSystem.IsChecked = true;
        KeyDown += OnKeyDown;
        StatusText.Text = "Dica: use a tecla global para falar com o Vox de qualquer lugar.";
    }

    private void EnableVoice_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = EnableVoiceCheck.IsChecked == true;
        HotkeyButton.IsEnabled = enabled;
        WakeWordCheck.IsEnabled = enabled;
        if (!enabled)
        {
            WakeWordCheck.IsChecked = false;
            CaptureHint.Visibility = Visibility.Collapsed;
        }
    }

    private void CaptureKey_Click(object sender, RoutedEventArgs e)
    {
        if (EnableVoiceCheck.IsChecked != true) return;
        _capturing = true;
        CaptureHint.Visibility = Visibility.Visible;
        HotkeyButton.Focus();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_capturing) return;

        if (e.Key == Key.Escape)
        {
            _capturing = false;
            CaptureHint.Visibility = Visibility.Collapsed;
            e.Handled = true;
            return;
        }

        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!IsValidKey(key))
        {
            StatusText.Text = "Use uma tecla comum, como uma letra, número ou F1-F12.";
            return;
        }

        _talkKey = key.ToString();
        HotkeyDisplay.Text = _talkKey;
        _capturing = false;
        CaptureHint.Visibility = Visibility.Collapsed;
        StatusText.Text = $"Tecla para falar definida: {_talkKey}";
        e.Handled = true;
    }

    private static bool IsValidKey(Key key)
        => (key >= Key.A && key <= Key.Z)
        || (key >= Key.D0 && key <= Key.D9)
        || (key >= Key.F1 && key <= Key.F24)
        || (key >= Key.NumPad0 && key <= Key.NumPad9)
        || key == Key.Space;

    private void TestVoice_Click(object sender, RoutedEventArgs e)
    {
        _voice.TestSpeech();
        StatusText.Text = "Ouvindo a voz de resposta...";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = new VoiceSettings
        {
            Enabled = EnableVoiceCheck.IsChecked == true,
            TalkHotkey = _talkKey,
            WakeWord = WakeWordCheck.IsChecked == true
        };
        Config.SaveVoice(s);
        var theme = ThemeDark.IsChecked == true ? "dark"
            : ThemeLight.IsChecked == true ? "light" : "system";
        Config.SaveAppearance(new AppearanceSettings { Theme = theme });
        Theme.Apply(theme);
        App.SetAutoStart(AutoStartCheck.IsChecked == true);
        if (System.Windows.Application.Current is App app)
            app.ApplySettings();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}