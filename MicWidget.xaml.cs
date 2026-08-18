using System.Windows;
using System.Windows.Threading;

namespace JarvisComando;

public partial class MicWidget : Window
{
    private readonly DispatcherTimer _pulse = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private bool _on;

    public MicWidget()
    {
        InitializeComponent();
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 48;
        _pulse.Tick += (_, _) =>
        {
            _on = !_on;
            MicIcon.Opacity = _on ? 1 : 0.35;
        };
    }

    public void ShowListening()
    {
        if (!_pulse.IsEnabled) _pulse.Start();
        Show();
    }

    public void HideListening()
    {
        _pulse.Stop();
        MicIcon.Opacity = 1;
        Hide();
    }
}