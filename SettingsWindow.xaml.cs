using System.Windows;
using System.Windows.Input;

namespace Vox;

public partial class SettingsWindow : Window
{
    private readonly VoiceAssistant _voice;
    private bool _capturing;
    private string _talkKey = "F9";
    private string _microphoneId = "";
    private List<MicrophoneDevice> _microphones = new();
    private bool _loadingMicrophones;

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
        _microphoneId = s.MicrophoneId ?? "";
        AutoStartCheck.IsChecked = App.IsAutoStart();
        var appearance = Config.LoadAppearance();
        if (appearance.Theme.Equals("dark", StringComparison.OrdinalIgnoreCase))
            ThemeDark.IsChecked = true;
        else if (appearance.Theme.Equals("light", StringComparison.OrdinalIgnoreCase))
            ThemeLight.IsChecked = true;
        else
            ThemeSystem.IsChecked = true;
        KeyDown += OnKeyDown;
        SetStatus("Dica: use a tecla global para falar com o Vox de qualquer lugar.");
        RefreshMicrophonesAsync();
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
        StatusBorder.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void RefreshMicrophonesAsync()
    {
        if (_loadingMicrophones) return;
        _loadingMicrophones = true;
        RefreshMicrophonesButton.IsEnabled = false;
        try
        {
            var mics = await MicrophoneSelector.GetMicrophonesAsync();
            _microphones = mics;
            var selected = _microphoneId;
            var previous = MicrophoneCombo.SelectedItem;

            MicrophoneCombo.Items.Clear();
            MicrophoneCombo.Items.Add(new MicrophoneDevice { Id = "", Name = "Padrão do Windows" });
            foreach (var m in mics)
                MicrophoneCombo.Items.Add(m);

            if (mics.Count == 0)
                SetStatus("Nenhum microfone detectado. Conecte um microfone e clique em Atualizar.");
            else if (previous == null && string.IsNullOrEmpty(selected))
                MicrophoneCombo.SelectedIndex = 0;
            else
                MicrophoneCombo.SelectedItem = MicrophoneCombo.Items
                    .Cast<MicrophoneDevice>()
                    .FirstOrDefault(m => string.Equals(m.Id, selected, StringComparison.OrdinalIgnoreCase))
                    ?? MicrophoneCombo.Items[0];
        }
        catch (Exception ex)
        {
            Logger.Error("RefreshMicrophones", ex);
            SetStatus("Não foi possível listar os microfones.");
        }
        finally
        {
            _loadingMicrophones = false;
            RefreshMicrophonesButton.IsEnabled = true;
        }
    }

    private void RefreshMicrophones_Click(object sender, RoutedEventArgs e) => RefreshMicrophonesAsync();

    private void Microphone_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loadingMicrophones) return;
        if (MicrophoneCombo.SelectedItem is MicrophoneDevice m)
            _microphoneId = m.Id;
    }

    private void EnableVoice_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = EnableVoiceCheck.IsChecked == true;
        HotkeyButton.IsEnabled = enabled;
        WakeWordCheck.IsEnabled = enabled;
        if (!enabled)
        {
            WakeWordCheck.IsChecked = false;
            CaptureHintBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void CaptureKey_Click(object sender, RoutedEventArgs e)
    {
        if (EnableVoiceCheck.IsChecked != true) return;
        _capturing = true;
        CaptureHintBorder.Visibility = Visibility.Visible;
        HotkeyButton.Focus();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_capturing) return;

        if (e.Key == Key.Escape)
        {
            _capturing = false;
            CaptureHintBorder.Visibility = Visibility.Collapsed;
            e.Handled = true;
            return;
        }

        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!IsValidKey(key))
        {
            SetStatus("Use uma tecla comum, como uma letra, número ou F1-F12.");
            return;
        }

        _talkKey = key.ToString();
        HotkeyDisplay.Text = _talkKey;
        _capturing = false;
        CaptureHintBorder.Visibility = Visibility.Collapsed;
        SetStatus($"Tecla para falar definida: {_talkKey}");
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
        SetStatus("Ouvindo a voz de resposta...");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = new VoiceSettings
        {
            Enabled = EnableVoiceCheck.IsChecked == true,
            TalkHotkey = _talkKey,
            WakeWord = WakeWordCheck.IsChecked == true,
            MicrophoneId = _microphoneId
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
