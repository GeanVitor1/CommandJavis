using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WBrush = System.Windows.Media.Brush;
using WBrushes = System.Windows.Media.Brushes;
using K = System.Windows.Input.Key;

namespace Vox;

public partial class AddCommandWindow : Window
{
    private readonly string _category;
    private string _modifiers = "";
    private string _key = "";
    private bool _capturingHotkey;
    private bool _winDown;

    public HotkeyBinding? Result { get; private set; }

    public AddCommandWindow(string category)
    {
        _category = category;
        InitializeComponent();
        TitleText.Text = category == "app" ? "Adicionar aplicativo" : "Adicionar site";
        DescriptionBox.Placeholder = "Ex: Abrir Spotify";
        TargetBox.Placeholder = category == "site"
            ? "Cole o endereço do site (ex: https://youtube.com)"
            : "Cole um link ou informe o caminho do programa";
        if (category == "site") UrlType.IsChecked = true; else OpenType.IsChecked = true;
        PreviewIcon.Category = category;
        UrlType.Checked += (_, _) => UpdatePreview();
        OpenType.Checked += (_, _) => UpdatePreview();
        CommandType.Checked += (_, _) => UpdatePreview();
        UpdateKeycaps();
        UpdatePreview();
        DescriptionBox.TextChanged += (_, _) => UpdatePreview();
        TargetBox.TextChanged += (_, _) => { UpdatePreview(); AutoDetectType(); };
        PreviewKeyDown += Window_PreviewKeyDown;
        PreviewKeyUp += Window_PreviewKeyUp;
        Loaded += (_, _) => DescriptionBox.Focus();
    }

    private async void PickApp_Click(object sender, RoutedEventArgs e)
    {
        var win = new AppPickerWindow { Owner = this };
        if (win.ShowDialog() != true || win.Result == null)
            return;
        DescriptionBox.Text = win.Result.Name;
        TargetBox.Text = win.Result.Target;
        OpenType.IsChecked = true;
        if (string.IsNullOrWhiteSpace(_key))
            DefineHotkey_Click(this, new RoutedEventArgs());
    }

    private void DefineHotkey_Click(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = !_capturingHotkey;
        if (_capturingHotkey)
        {
            HotkeyButtonText.Text = "Pressione as teclas...";
            HotkeyButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
            HotkeyButton.Background = FindResource("AccentGradientBrush") as System.Windows.Media.Brush;
            HotkeyButton.Foreground = System.Windows.Media.Brushes.White;
            CaptureHint.Visibility = Visibility.Visible;
            HotkeyHint.Visibility = Visibility.Collapsed;
            Activate();
        }
        else
        {
            StopCapture();
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_capturingHotkey)
            return;

        e.Handled = true;
        var key = e.Key == K.System ? e.SystemKey : e.Key;

        if (key == K.Escape)
        {
            StopCapture();
            return;
        }
        if (key == K.LWin || key == K.RWin)
        {
            _winDown = true;
            return;
        }
        if (key == K.LeftCtrl || key == K.RightCtrl ||
            key == K.LeftShift || key == K.RightShift ||
            key == K.LeftAlt || key == K.RightAlt)
            return;

        var name = FormatKey(key);
        if (name == null)
            return;

        var mods = new List<string>();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods.Add("Shift");
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods.Add("Alt");
        if (_winDown || Keyboard.IsKeyDown(K.LWin) || Keyboard.IsKeyDown(K.RWin)) mods.Add("Win");

        if (mods.Count == 0 && !name.StartsWith("F"))
        {
            CaptureHint.Text = "Use pelo menos uma tecla modificadora (Ctrl, Alt, Shift ou Win) + tecla";
            return;
        }

        _modifiers = string.Join("+", mods);
        _key = name;
        UpdateKeycaps();
        UpdatePreview();
        StopCapture();
    }

    private void Window_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == K.LWin || e.Key == K.RWin)
            _winDown = false;
    }

    private void StopCapture()
    {
        _capturingHotkey = false;
        _winDown = false;
        HotkeyButtonText.Text = "Definir teclas";
        HotkeyButton.Background = WBrushes.Transparent;
        HotkeyButton.Foreground = (WBrush)FindResource("TextSecondaryBrush");
        HotkeyButton.BorderBrush = (WBrush)FindResource("BorderBrush");
        CaptureHint.Visibility = Visibility.Collapsed;
        HotkeyHint.Visibility = Visibility.Visible;
    }

    private static string? FormatKey(K key)
    {
        if (key >= K.A && key <= K.Z) return key.ToString();
        if (key >= K.D0 && key <= K.D9) return key.ToString().Substring(1);
        if (key >= K.F1 && key <= K.F24) return key.ToString();
        return null;
    }

    private void AutoDetectType()
    {
        var t = TargetBox.Text.Trim();
        if (t.Length == 0)
            return;
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            UrlType.IsChecked = true;
        }
        else if (t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                 t.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                 t.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            if (t.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                CommandType.IsChecked = true;
            else
                OpenType.IsChecked = true;
        }
        else if (t.StartsWith("cmd ", StringComparison.OrdinalIgnoreCase) ||
                 t.StartsWith("powershell", StringComparison.OrdinalIgnoreCase) ||
                 t.Contains(" | ") ||
                 t.Contains("&&"))
        {
            CommandType.IsChecked = true;
        }
    }

    private void UpdateKeycaps()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_modifiers))
            parts.AddRange(_modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries));
        if (!string.IsNullOrWhiteSpace(_key))
            parts.Add(_key);
        KeycapDisplay.ItemsSource = parts;
        PreviewKeycap.ItemsSource = parts;
        NoHotkeyText.Visibility = parts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PreviewHotkeyLabel.Text = parts.Count == 0 ? "Sem atalho" : string.Join(" + ", parts);
    }

    private void SyncChecks()
    {
        CheckUrl.Visibility = UrlType.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        CheckApp.Visibility = OpenType.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        CheckCmd.Visibility = CommandType.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateProgress()
    {
        bool hasName = DescriptionBox.Text.Trim().Length > 0;
        bool hasDestino = TargetBox.Text.Trim().Length > 0;
        bool hasTipo = UrlType.IsChecked == true || OpenType.IsChecked == true || CommandType.IsChecked == true;
        bool hasAtalho = !string.IsNullOrWhiteSpace(_key);
        int step = hasName && hasDestino && hasTipo ? (hasAtalho ? 4 : 3) : hasName && hasDestino ? 2 : hasName ? 1 : 0;
        void SetStep(Border circle, TextBlock label, bool done, bool active)
        {
            if (done || active)
            {
                circle.Background = (WBrush)FindResource("AccentGradientBrush");
                circle.BorderBrush = null;
                circle.BorderThickness = new Thickness(0);
                if (circle.Child is TextBlock tb) tb.Foreground = WBrushes.White;
                label.Foreground = (WBrush)FindResource("TextPrimaryBrush");
                label.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                circle.Background = WBrushes.Transparent;
                circle.BorderBrush = (WBrush)FindResource("BorderBrush");
                circle.BorderThickness = new Thickness(1);
                if (circle.Child is TextBlock tb) tb.Foreground = (WBrush)FindResource("TextSecondaryBrush");
                label.Foreground = (WBrush)FindResource("TextSecondaryBrush");
                label.FontWeight = FontWeights.Normal;
            }
        }
        SetStep(StepCircle1, StepLabel1, step >= 1, step >= 0);
        SetStep(StepCircle2, StepLabel2, step >= 2, step >= 1);
        SetStep(StepCircle3, StepLabel3, step >= 3, step >= 2);
        SetStep(StepCircle4, StepLabel4, step >= 4, step >= 3);
    }

    private void UpdatePreview()
    {
        var desc = DescriptionBox.Text.Trim();
        PreviewName.Text = desc.Length > 0 ? desc : "Abrir Spotify";
        PreviewIcon.AvatarChar = desc.Length > 0
            ? char.ToUpperInvariant(desc[0]).ToString()
            : "A";

        var typeLabel = UrlType.IsChecked == true ? "Site" : OpenType.IsChecked == true ? "Aplicativo" : "Comando";
        PreviewTypeText.Text = typeLabel;
        PreviewStatusText.Text = "Pronto para usar";

        var hasDesc = desc.Length > 0;
        var hasTarget = TargetBox.Text.Trim().Length > 0;

        NameError.Visibility = hasDesc ? Visibility.Collapsed : Visibility.Visible;
        DescriptionBox.HasError = !hasDesc;
        TargetError.Visibility = hasTarget ? Visibility.Collapsed : Visibility.Visible;
        TargetBox.HasError = !hasTarget;

        ConfirmButton.IsEnabled = hasDesc && hasTarget;
        ReadyText.Text = hasDesc && hasTarget
            ? "Tudo pronto! Aperte Enter para adicionar"
            : hasDesc
                ? "Falta o destino — digite um link ou escolha um app"
                : hasTarget
                    ? "Falta o nome do comando"
                    : "Preencha o nome e o destino para continuar";
        ReadyText.Foreground = hasDesc && hasTarget
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        SyncChecks();
        UpdateProgress();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var desc = DescriptionBox.Text.Trim();
        var target = TargetBox.Text.Trim();
        if (desc.Length == 0 || target.Length == 0)
        {
            UpdatePreview();
            return;
        }

        var action = UrlType.IsChecked == true ? "url" : OpenType.IsChecked == true ? "open" : "command";
        Result = new HotkeyBinding
        {
            Category = _category,
            Description = desc,
            Target = target,
            Action = action,
            Modifiers = _modifiers,
            Key = _key
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
