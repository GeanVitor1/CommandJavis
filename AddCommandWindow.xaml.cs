using System.Windows;
using System.Windows.Input;

namespace JarvisComando;

public partial class AddCommandWindow : Window
{
    private readonly string _category;
    private string _modifiers = "";
    private string _key = "";

    public HotkeyBinding? Result { get; private set; }

    public AddCommandWindow(string category)
    {
        _category = category;
        InitializeComponent();
        TitleText.Text = category == "app" ? "Adicionar aplicativo" : "Adicionar site";
        TargetBox.Placeholder = category == "site" ? "https://www.exemplo.com" : "C:\\caminho\\para\\programa.exe";
        PickAppButton.Visibility = category == "app" ? Visibility.Visible : Visibility.Collapsed;
        if (category == "site") UrlType.IsChecked = true; else OpenType.IsChecked = true;
        UpdateKeycaps();
        Loaded += (_, _) => DescriptionBox.Focus();
    }

    private async void PickApp_Click(object sender, RoutedEventArgs e)
    {
        var win = new AppPickerWindow { Owner = this };
        if (win.ShowDialog() != true || win.Result == null)
            return;
        DescriptionBox.Text = win.Result.Name;
        TargetBox.Text = win.Result.Target;
        if (string.IsNullOrWhiteSpace(_key))
            DefineHotkey_Click(this, new RoutedEventArgs());
    }

    private void DefineHotkey_Click(object sender, RoutedEventArgs e)
    {
        var desc = DescriptionBox.Text.Trim();
        var win = new HotkeyCaptureWindow(desc.Length > 0 ? desc : "novo comando", _modifiers, _key) { Owner = this };
        if (win.ShowDialog() != true)
            return;
        _modifiers = win.Modifiers;
        _key = win.Key;
        UpdateKeycaps();
    }

    private void UpdateKeycaps()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_modifiers))
            parts.AddRange(_modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries));
        if (!string.IsNullOrWhiteSpace(_key))
            parts.Add(_key);
        KeycapDisplay.ItemsSource = parts;
        NoHotkeyText.Visibility = parts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var desc = DescriptionBox.Text.Trim();
        var target = TargetBox.Text.Trim();
        if (desc.Length == 0 || target.Length == 0)
        {
            System.Windows.MessageBox.Show(this, "Preencha a descrição e o destino.", "Jarvis Comando",
                MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
