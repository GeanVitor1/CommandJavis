using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Vox;

public partial class ConfirmWindow : Window
{
    private const int AutoCancelSeconds = 8;
    private readonly DispatcherTimer _timer;
    private int _remaining = AutoCancelSeconds;

    public ConfirmWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        CountdownText.Text = $"Cancelando automaticamente em {_remaining}s...";
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _remaining--;
            CountdownText.Text = _remaining > 0
                ? $"Cancelando automaticamente em {_remaining}s..."
                : "Ação cancelada";
            if (_remaining <= 0)
            {
                _timer.Stop();
                DialogResult = false;
                Close();
            }
        };
        _timer.Start();
        Loaded += (_, _) => Activate();
    }

    public static bool Show(Window? owner, string message)
    {
        var win = new ConfirmWindow(message) { Owner = owner };
        return win.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        DialogResult = false;
        Close();
    }
}