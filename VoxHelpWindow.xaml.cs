using System.Windows;

namespace Vox;

public partial class VoxHelpWindow : Window
{
    private readonly VoiceAssistant _voice;
    private bool _historyLoaded;

    public VoxHelpWindow(VoiceAssistant voice)
    {
        _voice = voice;
        InitializeComponent();

        HistoryList.ItemsSource = _voice.History;
        EmptyHistory.Visibility = _voice.History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _voice.CommandExecuted += OnCommandExecuted;

        if (_voice.History.Count > 0)
            HistoryList.ScrollIntoView(HistoryList.Items[0]);
    }

    private void OnCommandExecuted(string phrase)
    {
        if (!_historyLoaded)
        {
            _historyLoaded = true;
            HistoryList.ItemsSource = null;
            HistoryList.ItemsSource = _voice.History;
        }
        EmptyHistory.Visibility = _voice.History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (HistoryList.Items.Count > 0)
            HistoryList.ScrollIntoView(HistoryList.Items[0]);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}