using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WBrush = System.Windows.Media.Brush;
using WBrushes = System.Windows.Media.Brushes;
using WApplication = System.Windows.Application;
using WMessageBox = System.Windows.MessageBox;
using K = System.Windows.Input.Key;

namespace Vox;

public partial class MainWindow : Window
{
    private readonly ShortcutManager _manager;
    private readonly VoiceAssistant _voice;
    private readonly ObservableCollection<HotkeyBinding> _commands = new();
    private readonly ICollectionView _view;
    private readonly DoubleAnimation _pulseAnim;
    private bool _micDown;
    private bool _navigating;

    // Add view state
    private string _addCategory = "app";
    private string _addModifiers = "";
    private string _addKey = "";
    private bool _capturingAddHotkey;
    private bool _addWinDown;
    private bool _addWasApp = true;
    private bool _addSuppressClear;

    // Settings view state
    private string _settingsTalkKey = "F9";
    private string _settingsMicrophoneId = "";
    private List<MicrophoneDevice> _settingsMicrophones = new();
    private bool _loadingMicrophones;
    private bool _capturingSettingsKey;

    // History
    private bool _historyLoaded;

    // Edit hotkey overlay
    private HotkeyBinding? _editBinding;
    private string _editModifiers = "";
    private string _editKey = "";
    private bool _capturingEditHotkey;
    private bool _editWinDown;

    // App picker overlay
    private readonly ObservableCollection<InstalledApp> _pickerApps = new();
    private readonly ICollectionView _pickerView;
    private static readonly SemaphoreSlim IconThrottle = new(4);
    private bool _pickerLoaded;

    public MainWindow(ShortcutManager manager, VoiceAssistant voice)
    {
        _manager = manager;
        _voice = voice;
        _navigating = true;
        InitializeComponent();
        _navigating = false;

        _view = CollectionViewSource.GetDefaultView(_commands);
        _view.Filter = o => Matches((HotkeyBinding)o);
        AppList.ItemsSource = _view;

        _pickerView = CollectionViewSource.GetDefaultView(_pickerApps);
        _pickerView.Filter = o => PickerMatches((InstalledApp)o);
        AppListBox.ItemsSource = _pickerView;

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
        _voice.CommandExecuted += OnVoiceCommandExecuted;

        _manager.Changed += RefreshAll;
        RefreshAll();

        var voiceSettings = Config.LoadVoice();
        var talkKey = voiceSettings.Enabled ? voiceSettings.TalkHotkey : "botão";
        VoiceStatusText.Text = $"Segure o botão ou a tecla {talkKey} e fale, ex: \"ei vox abra o youtube em coldplay paradise\" ou \"que horas são\"";

        DescriptionBox.TextChanged += (_, _) => UpdateAddPreview();
        TargetBox.TextChanged += (_, _) => { UpdateAddPreview(); AutoDetectType(); };
        UrlType.Checked += (_, _) => { if (!_addSuppressClear && _addWasApp && TargetBox.Text.Trim().Length > 0) { TargetBox.Text = ""; if (PreviewIcon != null) { PreviewIcon.HasIcon = false; PreviewIcon.Icon = null; } } _addWasApp = false; TargetBox.Placeholder = "Cole o endereço do site (ex: https://youtube.com)"; UpdateAddPreview(); };
        OpenType.Checked += (_, _) => { _addWasApp = true; TargetBox.Placeholder = "Cole um link ou informe o caminho do programa"; UpdateAddPreview(); };
        CommandType.Checked += (_, _) => { if (!_addSuppressClear && _addWasApp && TargetBox.Text.Trim().Length > 0) { TargetBox.Text = ""; if (PreviewIcon != null) { PreviewIcon.HasIcon = false; PreviewIcon.Icon = null; } } _addWasApp = false; TargetBox.Placeholder = "Digite o comando (ex: notepad, calc)"; UpdateAddPreview(); };

        HistoryList.ItemsSource = _voice.History;
        UpdateHistoryEmpty();

        LoadSettingsView();
        PreviewKeyDown += Main_PreviewKeyDown;
        PreviewKeyUp += Main_PreviewKeyUp;
    }

    public void NavigateToSettings() => ShowView(View.Settings);
    public void NavigateToHistory() => ShowView(View.History);
    public void NavigateToHome() => ShowView(View.List);

    private enum View { List, Add, Settings, History }

    private void ShowView(View view)
    {
        if (AppPickerOverlay != null && EditHotkeyOverlay != null)
        {
            if (view != View.Add) CloseAddCapture();
            if (view != View.Settings) CloseSettingsCapture();
            AppPickerOverlay.Visibility = Visibility.Collapsed;
            EditHotkeyOverlay.Visibility = Visibility.Collapsed;
        }

        if (ListViewRoot == null || AddViewRoot == null || SettingsViewRoot == null || HistoryViewRoot == null)
            return;
        ListViewRoot.Visibility = view == View.List ? Visibility.Visible : Visibility.Collapsed;
        AddViewRoot.Visibility = view == View.Add ? Visibility.Visible : Visibility.Collapsed;
        SettingsViewRoot.Visibility = view == View.Settings ? Visibility.Visible : Visibility.Collapsed;
        HistoryViewRoot.Visibility = view == View.History ? Visibility.Visible : Visibility.Collapsed;

        _navigating = true;
        NavHome.IsChecked = view == View.List;
        NavHistory.IsChecked = view == View.History;
        NavSettings.IsChecked = view == View.Settings;
        _navigating = false;

        if (view == View.Settings) LoadSettingsView();
        if (view == View.History) RefreshHistory();
        if (view == View.Add) Dispatcher.BeginInvoke(() => DescriptionBox.Focus(), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void NavHome_Checked(object sender, RoutedEventArgs e)
    {
        if (_navigating) return;
        ShowView(View.List);
    }

    private void NavHistory_Checked(object sender, RoutedEventArgs e)
    {
        if (_navigating) return;
        ShowView(View.History);
    }

    private void NavSettings_Checked(object sender, RoutedEventArgs e)
    {
        if (_navigating) return;
        ShowView(View.Settings);
    }

    private void BackToList_Click(object sender, RoutedEventArgs e) => ShowView(View.List);

    // ===== History =====
    private void RefreshHistory()
    {
        if (!_historyLoaded)
        {
            _historyLoaded = true;
            HistoryList.ItemsSource = null;
            HistoryList.ItemsSource = _voice.History;
        }
        UpdateHistoryEmpty();
        if (HistoryList.Items.Count > 0) HistoryList.ScrollIntoView(HistoryList.Items[0]);
    }

    private void UpdateHistoryEmpty()
    {
        EmptyHistory.Visibility = _voice.History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnVoiceCommandExecuted(string phrase)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_historyLoaded)
            {
                _historyLoaded = true;
                HistoryList.ItemsSource = null;
                HistoryList.ItemsSource = _voice.History;
            }
            UpdateHistoryEmpty();
            if (HistoryList.Items.Count > 0) HistoryList.ScrollIntoView(HistoryList.Items[0]);
        });
    }

    // ===== Voice mic =====
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
        try { Process.Start(new ProcessStartInfo("ms-settings:speech") { UseShellExecute = true }); }
        catch { VoiceStatusText.Text = "Não foi possível abrir as configurações"; }
    }

    public void SetVoiceStatus(string text) => VoiceStatusText.Text = text;

    private void SetMicVisual(bool listening)
    {
        MicButton.Background = listening
            ? (WBrush)WApplication.Current.FindResource("DangerBrush")
            : (WBrush)WApplication.Current.FindResource("AccentBrush");
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

    // ===== List =====
    private bool IsAppCategory(HotkeyBinding b) => (AppTabRadio.IsChecked == true) == (b.Category != "site");
    private bool MatchesText(HotkeyBinding b, string q) => b.Description.Contains(q, StringComparison.OrdinalIgnoreCase) || b.Target.Contains(q, StringComparison.OrdinalIgnoreCase);
    private bool Matches(HotkeyBinding b)
    {
        var q = SearchBox.Text.Trim();
        return IsAppCategory(b) && (q.Length == 0 || MatchesText(b, q));
    }

    private void RefreshAll()
    {
        _commands.Clear();
        foreach (var b in _manager.All) _commands.Add(b);
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
        if (searching && visible == 0) EmptySearchText.Text = $"para \"{q}\"";
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        UpdateEmptyStates();
        if (AppList == null) return;
        _view.Refresh();
        if (_view.IsEmpty) return;
        if (AppList.Items.Count > 0) AppList.ScrollIntoView(AppList.Items[0]);
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_view == null) return;
        _view.Refresh();
        UpdateEmptyStates();
    }

    private void Run_Click(object sender, RoutedEventArgs e) => _manager.Execute((HotkeyBinding)((FrameworkElement)sender).DataContext);

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2 && ((FrameworkElement)sender).DataContext is HotkeyBinding b)
            _manager.Execute(b);
    }

    private void Card_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && ((FrameworkElement)sender).DataContext is HotkeyBinding b)
        {
            e.Handled = true;
            _manager.Execute(b);
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var b = (HotkeyBinding)((FrameworkElement)sender).DataContext;
        OpenEditOverlay(b);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var b = (HotkeyBinding)((FrameworkElement)sender).DataContext;
        var res = WMessageBox.Show(this, $"Remover \"{b.Description}\"?", "Vox", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == System.Windows.MessageBoxResult.Yes) _manager.Remove(b);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        _addCategory = AppTabRadio.IsChecked == true ? "app" : "site";
        ResetAddForm();
        ShowView(View.Add);
    }

    // ===== Add view =====
    private void ResetAddForm()
    {
        AddTitleText.Text = _addCategory == "app" ? "Adicionar aplicativo" : "Adicionar site";
        DescriptionBox.Placeholder = "Ex: Abrir Spotify";
        TargetBox.Placeholder = _addCategory == "site" ? "Cole o endereço do site (ex: https://youtube.com)" : "Cole um link ou informe o caminho do programa";
        DescriptionBox.Text = "";
        TargetBox.Text = "";
        _addModifiers = "";
        _addKey = "";
        _capturingAddHotkey = false;
        _addWinDown = false;
        _addWasApp = _addCategory != "site";
        if (_addCategory == "site") UrlType.IsChecked = true; else OpenType.IsChecked = true;
        PreviewIcon.Category = _addCategory;
        HotkeyButtonText.Text = "Definir teclas";
        HotkeyButton.Background = WBrushes.Transparent;
        HotkeyButton.Foreground = (WBrush)FindResource("TextSecondaryBrush");
        HotkeyButton.BorderBrush = (WBrush)FindResource("BorderBrush");
        CaptureHint.Visibility = Visibility.Collapsed;
        HotkeyHint.Visibility = Visibility.Visible;
        if (InlinePreviewContainer != null) InlinePreviewContainer.Visibility = Visibility.Collapsed;
        if (InlinePreviewPanel != null) InlinePreviewPanel.Visibility = Visibility.Visible;
        UpdateAddKeycaps();
        UpdateAddPreview();
    }

    private void AddCancel_Click(object sender, RoutedEventArgs e)
    {
        CloseAddCapture();
        ShowView(View.List);
    }

    private void DefineHotkeyInline_Click(object sender, RoutedEventArgs e)
    {
        _capturingAddHotkey = !_capturingAddHotkey;
        if (_capturingAddHotkey)
        {
            HotkeyButtonText.Text = "Pressione as teclas…";
            HotkeyButton.BorderBrush = WBrushes.Transparent;
            HotkeyButton.Background = FindResource("AccentBrush") as WBrush;
            HotkeyButton.Foreground = WBrushes.White;
            CaptureHint.Visibility = Visibility.Visible;
            HotkeyHint.Visibility = Visibility.Collapsed;
            Activate();
        }
        else CloseAddCapture();
    }

    private void CloseAddCapture()
    {
        _capturingAddHotkey = false;
        _addWinDown = false;
        if (HotkeyButtonText != null) HotkeyButtonText.Text = "Definir teclas";
        if (HotkeyButton != null)
        {
            HotkeyButton.Background = WBrushes.Transparent;
            HotkeyButton.Foreground = (WBrush)FindResource("TextSecondaryBrush");
            HotkeyButton.BorderBrush = (WBrush)FindResource("BorderBrush");
        }
        if (CaptureHint != null) CaptureHint.Visibility = Visibility.Collapsed;
        if (HotkeyHint != null) HotkeyHint.Visibility = Visibility.Visible;
    }

    private void UpdateAddKeycaps()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_addModifiers)) parts.AddRange(_addModifiers.Split('+', StringSplitOptions.RemoveEmptyEntries));
        if (!string.IsNullOrWhiteSpace(_addKey)) parts.Add(_addKey);
        KeycapDisplay.ItemsSource = parts;
        PreviewKeycap.ItemsSource = parts;
        NoHotkeyText.Visibility = parts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (PreviewHotkeyLabel != null)
            PreviewHotkeyLabel.Text = parts.Count == 0 ? "Sem atalho" : string.Join(" + ", parts);
    }

    private void SyncAddChecks()
    {
        if (CheckUrl != null) CheckUrl.Visibility = UrlType.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (CheckApp != null) CheckApp.Visibility = OpenType.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (CheckCmd != null) CheckCmd.Visibility = CommandType.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateAddPreview()
    {
        if (PreviewName == null) return;
        var desc = DescriptionBox.Text.Trim();
        PreviewName.Text = desc.Length > 0 ? desc : "Abrir Spotify";
        PreviewIcon.AvatarChar = desc.Length > 0 ? char.ToUpperInvariant(desc[0]).ToString() : "A";
        var typeLabel = UrlType.IsChecked == true ? "Site" : OpenType.IsChecked == true ? "Aplicativo" : "Comando";
        if (PreviewTypeText != null) PreviewTypeText.Text = typeLabel;
        if (PreviewStatusText != null) PreviewStatusText.Text = "Pronto para usar";
        PreviewIcon.Category = UrlType.IsChecked == true ? "site" : "app";
        var hasDesc = desc.Length > 0;
        var hasTarget = TargetBox.Text.Trim().Length > 0;
        NameError.Visibility = hasDesc ? Visibility.Collapsed : Visibility.Visible;
        DescriptionBox.HasError = !hasDesc;
        TargetError.Visibility = hasTarget ? Visibility.Collapsed : Visibility.Visible;
        TargetBox.HasError = !hasTarget;
        ConfirmButton.IsEnabled = hasDesc && hasTarget;
        ReadyText.Text = hasDesc && hasTarget ? "Tudo pronto! Aperte Enter para adicionar" : hasDesc ? "Falta o destino — digite um link ou escolha um app" : hasTarget ? "Falta o nome do comando" : "Preencha o nome e o destino para continuar";
        ReadyText.Foreground = hasDesc && hasTarget ? (WBrush)FindResource("AccentBrush") : (WBrush)FindResource("TextSecondaryBrush");
        SyncAddChecks();
        UpdateAddProgress();
    }

    private void UpdateAddProgress()
    {
        bool hasName = DescriptionBox.Text.Trim().Length > 0;
        bool hasDestino = TargetBox.Text.Trim().Length > 0;
        bool hasTipo = UrlType.IsChecked == true || OpenType.IsChecked == true || CommandType.IsChecked == true;
        bool hasAtalho = !string.IsNullOrWhiteSpace(_addKey);
        int step = hasName && hasTipo && hasDestino ? (hasAtalho ? 4 : 3) : hasName && hasTipo ? 2 : hasName ? 1 : 0;

        void SetStep(Border circle, TextBlock label, int n, bool done, bool active)
        {
            if (circle == null || label == null) return;
            if (active || done)
            {
                circle.Background = (WBrush)FindResource("AccentBrush");
                circle.BorderBrush = null;
                circle.BorderThickness = new Thickness(0);
                var tb = circle.Child as TextBlock;
                if (tb != null) tb.Foreground = WBrushes.White;
                label.Foreground = (WBrush)FindResource("TextPrimaryBrush");
                label.FontWeight = FontWeights.Medium;
            }
            else
            {
                circle.Background = WBrushes.Transparent;
                circle.BorderBrush = (WBrush)FindResource("BorderBrush");
                circle.BorderThickness = new Thickness(1);
                var tb = circle.Child as TextBlock;
                if (tb != null) tb.Foreground = (WBrush)FindResource("TextTertiaryBrush");
                label.Foreground = (WBrush)FindResource("TextSecondaryBrush");
                label.FontWeight = FontWeights.Normal;
            }
            circle.Opacity = 1;
            label.Opacity = active || done ? 1 : 0.85;
        }

        SetStep(StepCircle1, StepLabel1, 1, step >= 1, step >= 0);
        SetStep(StepCircle2, StepLabel2, 2, step >= 2, step >= 1);
        SetStep(StepCircle3, StepLabel3, 3, step >= 3, step >= 2);
        SetStep(StepCircle4, StepLabel4, 4, step >= 4, step >= 3);
    }

    private void AutoDetectType()
    {
        var t = TargetBox.Text.Trim();
        if (t.Length == 0) return;
        _addSuppressClear = true;
        try
        {
            if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                UrlType.IsChecked = true;
            else if (t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || t.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || t.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                if (t.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)) CommandType.IsChecked = true; else OpenType.IsChecked = true;
            }
            else if (t.StartsWith("cmd ", StringComparison.OrdinalIgnoreCase) || t.StartsWith("powershell", StringComparison.OrdinalIgnoreCase) || t.Contains(" | ") || t.Contains("&&"))
                CommandType.IsChecked = true;
        }
        finally { _addSuppressClear = false; }
    }

    private void ConfirmAdd_Click(object sender, RoutedEventArgs e)
    {
        var desc = DescriptionBox.Text.Trim();
        var target = TargetBox.Text.Trim();
        if (desc.Length == 0 || target.Length == 0) { UpdateAddPreview(); return; }
        var action = UrlType.IsChecked == true ? "url" : OpenType.IsChecked == true ? "open" : "command";
        var binding = new HotkeyBinding { Category = _addCategory, Description = desc, Target = target, Action = action, Modifiers = _addModifiers, Key = _addKey };
        var err = _manager.Add(binding);
        if (err != null) { WMessageBox.Show(this, err, "Vox", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        ShowView(View.List);
    }

    // ===== App picker overlay =====
    private bool PickerMatches(InstalledApp a)
    {
        var q = AppSearchBox.Text.Trim();
        return q.Length == 0 || a.Name.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private async void PickAppInline_Click(object sender, RoutedEventArgs e)
    {
        AppPickerOverlay.Visibility = Visibility.Visible;
        AppSearchBox.Focus();
        if (!_pickerLoaded)
        {
            _pickerLoaded = true;
            await LoadPickerAppsAsync();
        }
    }

    private async Task LoadPickerAppsAsync()
    {
        AppPickerStatus.Text = "Carregando aplicativos instalados...";
        var apps = await AppCatalog.GetAppsAsync();
        foreach (var a in apps) _pickerApps.Add(a);
        _pickerView.Refresh();
        AppPickerStatus.Text = $"{apps.Count} aplicativos encontrados";
        var tasks = _pickerApps.Select(async a =>
        {
            await IconThrottle.WaitAsync();
            try { await AppCatalog.LoadIconAsync(a); }
            finally { IconThrottle.Release(); }
        }).ToList();
        await Task.WhenAll(tasks);
    }

    private void AppSearch_TextChanged(object sender, TextChangedEventArgs e) => _pickerView?.Refresh();

    private void AppList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ConfirmPicker();
    }

    private void ChooseApp_Click(object sender, RoutedEventArgs e) => ConfirmPicker();

    private async void ConfirmPicker()
    {
        if (AppListBox.SelectedItem is InstalledApp app)
        {
            DescriptionBox.Text = app.Name;
            TargetBox.Text = app.Target;
            OpenType.IsChecked = true;
            AppPickerOverlay.Visibility = Visibility.Collapsed;
            if (InlinePreviewContainer != null) InlinePreviewContainer.Visibility = Visibility.Visible;
            if (InlinePreviewPanel != null) InlinePreviewPanel.Visibility = Visibility.Visible;
            if (PreviewIcon != null)
            {
                PreviewIcon.Icon = app.Icon;
                PreviewIcon.HasIcon = app.HasIcon;
                PreviewIcon.AvatarChar = app.AvatarChar;
                PreviewIcon.Category = "app";
                if (!app.HasIcon)
                {
                    await AppCatalog.LoadIconAsync(app);
                    if (app.HasIcon)
                    {
                        PreviewIcon.Icon = app.Icon;
                        PreviewIcon.HasIcon = true;
                    }
                }
            }
        }
    }

    private void CloseAppPicker_Click(object sender, RoutedEventArgs e) => AppPickerOverlay.Visibility = Visibility.Collapsed;

    // ===== Edit hotkey overlay =====
    private void OpenEditOverlay(HotkeyBinding b)
    {
        _editBinding = b;
        _editModifiers = b.Modifiers;
        _editKey = b.Key;
        _capturingEditHotkey = false;
        _editWinDown = false;
        EditCommandLabel.Text = $"Comando: {b.Description}";
        UpdateEditDisplay();
        EditHotkeyOverlay.Visibility = Visibility.Visible;
        Focus();
    }

    private void CloseHotkeyOverlay_Click(object sender, RoutedEventArgs e)
    {
        EditHotkeyOverlay.Visibility = Visibility.Collapsed;
        _editBinding = null;
        _capturingEditHotkey = false;
    }

    private void SaveHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_editBinding == null) return;
        var err = _manager.Rebind(_editBinding, _editModifiers, _editKey);
        if (err != null) { WMessageBox.Show(this, err, "Vox", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        EditHotkeyOverlay.Visibility = Visibility.Collapsed;
        _editBinding = null;
    }

    private void UpdateEditDisplay()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_editModifiers)) parts.AddRange(_editModifiers.Split('+', StringSplitOptions.RemoveEmptyEntries));
        if (!string.IsNullOrWhiteSpace(_editKey)) parts.Add(_editKey);
        EditKeycapDisplay.ItemsSource = parts;
        EditPressHint.Visibility = parts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EditHintText.Text = "Dica: Ctrl, Alt, Shift ou Win + tecla  (ex.: Alt+V)";
        SaveHotkeyButton.IsEnabled = parts.Count > 0;
    }

    // ===== Settings view =====
    private void LoadSettingsView()
    {
        var s = Config.LoadVoice();
        _settingsTalkKey = string.IsNullOrWhiteSpace(s.TalkHotkey) ? "F9" : s.TalkHotkey;
        EnableVoiceCheck.IsChecked = s.Enabled;
        WakeWordCheck.IsChecked = s.Enabled && s.WakeWord;
        HotkeyDisplay.Text = _settingsTalkKey;
        SettingsHotkeyButton.IsEnabled = s.Enabled;
        WakeWordCheck.IsEnabled = s.Enabled;
        _settingsMicrophoneId = s.MicrophoneId ?? "";
        AutoStartCheck.IsChecked = App.IsAutoStart();
        var appearance = Config.LoadAppearance();
        if (appearance.Theme.Equals("dark", StringComparison.OrdinalIgnoreCase)) ThemeDark.IsChecked = true;
        else if (appearance.Theme.Equals("light", StringComparison.OrdinalIgnoreCase)) ThemeLight.IsChecked = true;
        else ThemeSystem.IsChecked = true;
        SetSettingsStatus("Dica: use a tecla global para falar com o Vox de qualquer lugar.");
        if (_settingsMicrophones.Count == 0) _ = RefreshMicrophonesAsync();
        else RefreshMicrophoneCombo();
    }

    private void SetSettingsStatus(string text)
    {
        StatusText.Text = text;
        StatusBorder.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task RefreshMicrophonesAsync()
    {
        if (_loadingMicrophones) return;
        _loadingMicrophones = true;
        RefreshMicrophonesButton.IsEnabled = false;
        try
        {
            var mics = await MicrophoneSelector.GetMicrophonesAsync();
            _settingsMicrophones = mics;
            RefreshMicrophoneCombo();
            if (mics.Count == 0) SetSettingsStatus("Nenhum microfone detectado. Conecte um microfone e clique em Atualizar.");
        }
        catch (Exception ex)
        {
            Logger.Error("RefreshMicrophones", ex);
            SetSettingsStatus("Não foi possível listar os microfones.");
        }
        finally { _loadingMicrophones = false; RefreshMicrophonesButton.IsEnabled = true; }
    }

    private void RefreshMicrophoneCombo()
    {
        var selected = _settingsMicrophoneId;
        MicrophoneCombo.Items.Clear();
        MicrophoneCombo.Items.Add(new MicrophoneDevice { Id = "", Name = "Padrão do Windows" });
        foreach (var m in _settingsMicrophones) MicrophoneCombo.Items.Add(m);
        if (_settingsMicrophones.Count == 0) { }
        else if (string.IsNullOrEmpty(selected)) MicrophoneCombo.SelectedIndex = 0;
        else MicrophoneCombo.SelectedItem = MicrophoneCombo.Items.Cast<MicrophoneDevice>().FirstOrDefault(m => string.Equals(m.Id, selected, StringComparison.OrdinalIgnoreCase)) ?? MicrophoneCombo.Items[0];
    }

    private void RefreshMicrophones_Click(object sender, RoutedEventArgs e) => _ = RefreshMicrophonesAsync();

    private void Microphone_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingMicrophones) return;
        if (MicrophoneCombo.SelectedItem is MicrophoneDevice m) _settingsMicrophoneId = m.Id;
    }

    private void EnableVoice_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = EnableVoiceCheck.IsChecked == true;
        SettingsHotkeyButton.IsEnabled = enabled;
        WakeWordCheck.IsEnabled = enabled;
        if (!enabled) { WakeWordCheck.IsChecked = false; CaptureHintBorder.Visibility = Visibility.Collapsed; CloseSettingsCapture(); }
    }

    private void CaptureKey_Click(object sender, RoutedEventArgs e)
    {
        if (EnableVoiceCheck.IsChecked != true) return;
        _capturingSettingsKey = true;
        CaptureHintBorder.Visibility = Visibility.Visible;
        SettingsHotkeyButton.Focus();
    }

    private void CloseSettingsCapture()
    {
        _capturingSettingsKey = false;
        if (CaptureHintBorder != null) CaptureHintBorder.Visibility = Visibility.Collapsed;
    }

    private static bool IsValidSettingsKey(K key) => (key >= K.A && key <= K.Z) || (key >= K.D0 && key <= K.D9) || (key >= K.F1 && key <= K.F24) || (key >= K.NumPad0 && key <= K.NumPad9) || key == K.Space;

    private void TestVoice_Click(object sender, RoutedEventArgs e)
    {
        _voice.TestSpeech();
        SetSettingsStatus("Ouvindo a voz de resposta...");
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var s = new VoiceSettings { Enabled = EnableVoiceCheck.IsChecked == true, TalkHotkey = _settingsTalkKey, WakeWord = WakeWordCheck.IsChecked == true, MicrophoneId = _settingsMicrophoneId };
        Config.SaveVoice(s);
        var theme = ThemeDark.IsChecked == true ? "dark" : ThemeLight.IsChecked == true ? "light" : "system";
        Config.SaveAppearance(new AppearanceSettings { Theme = theme });
        Theme.Apply(theme);
        App.SetAutoStart(AutoStartCheck.IsChecked == true);
        if (WApplication.Current is App app) app.ApplySettings();
        SetSettingsStatus("Configurações salvas!");
        var talkKey = s.Enabled ? s.TalkHotkey : "botão";
        VoiceStatusText.Text = $"Segure o botão ou a tecla {talkKey} e fale, ex: \"ei vox abra o youtube em coldplay paradise\" ou \"que horas são\"";
    }

    private void SettingsCancel_Click(object sender, RoutedEventArgs e) => ShowView(View.List);

    // ===== Global key handling =====
    private void Main_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (AppPickerOverlay.Visibility == Visibility.Visible && e.Key == K.Escape) { AppPickerOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
        if (EditHotkeyOverlay.Visibility == Visibility.Visible)
        {
            HandleEditHotkeyDown(e);
            return;
        }
        if (_capturingAddHotkey) { HandleAddHotkeyDown(e); return; }
        if (_capturingSettingsKey) { HandleSettingsHotkeyDown(e); return; }
        if (e.Key == K.Escape)
        {
            if (AddViewRoot.Visibility == Visibility.Visible || SettingsViewRoot.Visibility == Visibility.Visible || HistoryViewRoot.Visibility == Visibility.Visible)
            {
                ShowView(View.List);
                e.Handled = true;
            }
        }
    }

    private void Main_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == K.LWin || e.Key == K.RWin) { _addWinDown = false; _editWinDown = false; }
    }

    private void HandleAddHotkeyDown(System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == K.System ? e.SystemKey : e.Key;
        if (key == K.Escape) { CloseAddCapture(); return; }
        if (key == K.LWin || key == K.RWin) { _addWinDown = true; return; }
        if (key == K.LeftCtrl || key == K.RightCtrl || key == K.LeftShift || key == K.RightShift || key == K.LeftAlt || key == K.RightAlt) return;
        var name = FormatKey(key);
        if (name == null) return;
        var mods = new List<string>();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods.Add("Shift");
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods.Add("Alt");
        if (_addWinDown || Keyboard.IsKeyDown(K.LWin) || Keyboard.IsKeyDown(K.RWin)) mods.Add("Win");
        if (mods.Count == 0 && !name.StartsWith("F")) { CaptureHint.Text = "Use pelo menos uma tecla modificadora (Ctrl, Alt, Shift ou Win) + tecla"; return; }
        _addModifiers = string.Join("+", mods);
        _addKey = name;
        UpdateAddKeycaps();
        CloseAddCapture();
    }

    private void HandleSettingsHotkeyDown(System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == K.System ? e.SystemKey : e.Key;
        if (key == K.Escape) { CloseSettingsCapture(); return; }
        if (key is K.LeftCtrl or K.RightCtrl or K.LeftShift or K.RightShift or K.LeftAlt or K.RightAlt or K.LWin or K.RWin or K.System) return;
        if (!IsValidSettingsKey(key)) { SetSettingsStatus("Use uma tecla comum, como uma letra, número ou F1-F12."); return; }
        _settingsTalkKey = key.ToString();
        HotkeyDisplay.Text = _settingsTalkKey;
        CloseSettingsCapture();
        SetSettingsStatus($"Tecla para falar definida: {_settingsTalkKey}");
    }

    private void HandleEditHotkeyDown(System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == K.System ? e.SystemKey : e.Key;
        if (key == K.Escape) { EditHotkeyOverlay.Visibility = Visibility.Collapsed; _editBinding = null; return; }
        if (key == K.LWin || key == K.RWin) { _editWinDown = true; return; }
        if (key == K.LeftCtrl || key == K.RightCtrl || key == K.LeftShift || key == K.RightShift || key == K.LeftAlt || key == K.RightAlt) return;
        if (key == K.Enter && SaveHotkeyButton.IsEnabled) { SaveHotkey_Click(this, new RoutedEventArgs()); return; }
        var name = FormatKey(key);
        if (name == null) return;
        var mods = new List<string>();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods.Add("Shift");
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods.Add("Alt");
        if (_editWinDown || Keyboard.IsKeyDown(K.LWin) || Keyboard.IsKeyDown(K.RWin)) mods.Add("Win");
        if (mods.Count == 0 && !name.StartsWith("F")) { EditHintText.Text = "Use pelo menos uma tecla modificadora (Ctrl/Alt/Shift/Win)"; return; }
        _editModifiers = string.Join("+", mods);
        _editKey = name;
        EditHintText.Text = "Dica: Ctrl, Alt, Shift ou Win + tecla  (ex.: Alt+V)";
        UpdateEditDisplay();
    }

    private static string? FormatKey(K key)
    {
        if (key >= K.A && key <= K.Z) return key.ToString();
        if (key >= K.D0 && key <= K.D9) return key.ToString().Substring(1);
        if (key >= K.F1 && key <= K.F24) return key.ToString();
        return null;
    }

    // ===== Title bar =====
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
