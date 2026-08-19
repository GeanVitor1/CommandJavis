using System.Windows;
using System.Windows.Input;
using K = System.Windows.Input.Key;

namespace Vox;

public partial class HotkeyCaptureWindow : Window
{
    private bool _winDown;

    public string Modifiers { get; private set; } = "";
    public string Key { get; private set; } = "";

    public HotkeyCaptureWindow(string description, string currentModifiers, string currentKey)
    {
        InitializeComponent();
        CommandLabel.Text = $"Comando: {description}";
        Modifiers = currentModifiers;
        Key = currentKey;
        UpdateDisplay();
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        Loaded += (_, _) => Activate();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == K.System ? e.SystemKey : e.Key;

        if (key == K.Escape)
        {
            DialogResult = false;
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
        if (key == K.Enter && SaveButton.IsEnabled)
        {
            DialogResult = true;
            return;
        }

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
            HintText.Text = "Use pelo menos uma tecla modificadora (Ctrl/Alt/Shift/Win)";
            return;
        }

        Modifiers = string.Join("+", mods);
        Key = name;
        HintText.Text = "Dica: Ctrl, Alt, Shift ou Win + tecla  (ex.: Alt+V)";
        UpdateDisplay();
        SaveButton.IsEnabled = true;
    }

    private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == K.LWin || e.Key == K.RWin)
            _winDown = false;
    }

    private static string? FormatKey(K key)
    {
        if (key >= K.A && key <= K.Z) return key.ToString();
        if (key >= K.D0 && key <= K.D9) return key.ToString().Substring(1);
        if (key >= K.F1 && key <= K.F24) return key.ToString();
        return null;
    }

    private void UpdateDisplay()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Modifiers))
            parts.AddRange(Modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries));
        if (!string.IsNullOrWhiteSpace(Key))
            parts.Add(Key);
        KeycapDisplay.ItemsSource = parts;
        PressHint.Visibility = parts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = parts.Count > 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

