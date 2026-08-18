using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace JarvisComando;

public partial class MainWindow : Window
{
    private readonly ShortcutManager _manager;
    private readonly VoiceAssistant _voice;
    private readonly Action _openSettings;
    private readonly ObservableCollection<HotkeyBinding> _commands = new();
    private readonly ICollectionView _view;
    private readonly DoubleAnimation _pulseAnim;
    private bool _micDown;

    public MainWindow(ShortcutManager manager, VoiceAssistant voice, Action openSettings)
    {
        _manager = manager;
        _voice = voice;
        _openSettings = openSettings;
        InitializeComponent();

        _view = CollectionViewSource.GetDefaultView(_commands);
        _view.Filter = o => Matches((HotkeyBinding)o);
        AppList.ItemsSource = _view;

        _pulseAnim = new DoubleAnimation
        {
            From = 1,
            To = 1.18,
            Duration = TimeSpan.FromMilliseconds(350),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        _voice.StatusChanged += s => VoiceStatusText.Text = s;
        _voice.PrivacyFixRequested += () => EnableSpeechButton.Visibility = Visibility.Visible;

        _manager.Changed += RefreshAll;
        RefreshAll();
        var voiceSettings = Config.LoadVoice();
        var talkKey = voiceSettings.Enabled ? voiceSettings.TalkHotkey : "botão";
        VoiceStatusText.Text = $"Segure o botão ou a tecla {talkKey} e fale, ex: \"ei jarvis abra o youtube em coldplay paradise\"";
    }

    private async void MicButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _micDown = true;
        SetMicVisual(true);
        if (!await _voice.StartAsync() && _micDown)
        {
            _micDown = false;
            SetMicVisual(false);
        }
    }

    private async void MicButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_micDown) return;
        _micDown = false;
        SetMicVisual(false);
        await _voice.StopAsync();
    }

    private void MicButton_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_micDown) return;
        _micDown = false;
        SetMicVisual(false);
        _ = _voice.StopAsync();
    }

    private void EnableSpeech_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:speech") { UseShellExecute = true });
        }
        catch
        {
            VoiceStatusText.Text = "Não foi possível abrir as configurações";
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => _openSettings();

    private void SetMicVisual(bool listening)
    {
        MicButton.Background = listening
            ? (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("DangerBrush")
            : (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("AccentGradientBrush");
        var scale = (ScaleTransform)MicButton.RenderTransform;
        if (listening)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, _pulseAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, _pulseAnim);
        }
        else
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = scale.ScaleY = 1;
        }
    }

    private bool IsAppCategory(HotkeyBinding b) => (AppTabRadio.IsChecked == true) == (b.Category != "site");

    private bool MatchesText(HotkeyBinding b, string q)
        => b.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
        || b.Target.Contains(q, StringComparison.OrdinalIgnoreCase);

    private bool Matches(HotkeyBinding b)
    {
        var q = SearchBox.Text.Trim();
        return IsAppCategory(b) && (q.Length == 0 || MatchesText(b, q));
    }

    private void RefreshAll()
    {
        _commands.Clear();
        foreach (var b in _manager.All)
            _commands.Add(b);

        _view.Refresh();

        int total = _manager.All.Count;
        int active = _manager.All.Count(b => b.Id > 0);
        StatsText.Text = $"{active} de {total} ativos";
        AppTabRadio.Content = $"Aplicativos  ({_commands.Count(b => b.Category != "site")})";
        SiteTabRadio.Content = $"Sites  ({_commands.Count(b => b.Category == "site")})";

        UpdateEmptyStates();
        _ = LoadIconsAsync();
    }

    private async Task LoadIconsAsync()
    {
        var snapshot = _manager.All.ToList();
        await Task.WhenAll(snapshot.Select(IconLoader.LoadAsync));
    }

    private void UpdateEmptyStates()
    {
        if (EmptyApp == null || EmptySite == null || EmptySearch == null || AppTabRadio == null) return;
        bool appTab = AppTabRadio.IsChecked == true;
        string q = SearchBox.Text.Trim();
        bool searching = q.Length > 0;
        int categoryTotal = _commands.Count(IsAppCategory);
        int visible = searching ? _commands.Count(b => IsAppCategory(b) && MatchesText(b, q)) : categoryTotal;

        EmptyApp.Visibility = appTab && !searching && categoryTotal == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptySite.Visibility = !appTab && !searching && categoryTotal == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptySearch.Visibility = searching && visible == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (searching && visible == 0)
            EmptySearchText.Text = $"para \"{q}\"";
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        UpdateEmptyStates();
        if (AppList == null) return;
        _view.Refresh();
        if (_view.IsEmpty)
            return;
        if (AppList.Items.Count > 0)
            AppList.ScrollIntoView(AppList.Items[0]);
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_view == null) return;
        _view.Refresh();
        UpdateEmptyStates();
    }

    private void Run_Click(object sender, RoutedEventArgs e)
        => _manager.Execute((HotkeyBinding)((FrameworkElement)sender).DataContext);

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2 && ((FrameworkElement)sender).DataContext is HotkeyBinding b)
            _manager.Execute(b);
    }

    private void Card_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space &&
            ((FrameworkElement)sender).DataContext is HotkeyBinding b)
        {
            e.Handled = true;
            _manager.Execute(b);
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var b = (HotkeyBinding)((FrameworkElement)sender).DataContext;
        var win = new HotkeyCaptureWindow(b.Description, b.Modifiers, b.Key) { Owner = this };
        if (win.ShowDialog() != true)
            return;
        var err = _manager.Rebind(b, win.Modifiers, win.Key);
        if (err != null)
            System.Windows.MessageBox.Show(this, err, "Jarvis Comando", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var b = (HotkeyBinding)((FrameworkElement)sender).DataContext;
        var res = System.Windows.MessageBox.Show(this, $"Remover \"{b.Description}\"?", "Jarvis Comando",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
            _manager.Remove(b);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var category = AppTabRadio.IsChecked == true ? "app" : "site";
        var win = new AddCommandWindow(category) { Owner = this };
        if (win.ShowDialog() != true || win.Result == null)
            return;
        var err = _manager.Add(win.Result);
        if (err != null)
            System.Windows.MessageBox.Show(this, err, "Jarvis Comando", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
