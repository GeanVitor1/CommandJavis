using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace JarvisComando;

public partial class AppPickerWindow : Window
{
    private readonly ObservableCollection<InstalledApp> _apps = new();
    private readonly ICollectionView _view;
    private static readonly SemaphoreSlim IconThrottle = new(4);
    private bool _loaded;

    public InstalledApp? Result { get; private set; }

    public AppPickerWindow()
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_apps);
        _view.Filter = o => Matches((InstalledApp)o);
        AppListBox.ItemsSource = _view;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                DialogResult = false;
        };
        Loaded += async (_, _) =>
        {
            if (!_loaded)
            {
                _loaded = true;
                await LoadAppsAsync();
            }
        };
    }

    private bool Matches(InstalledApp a)
    {
        var q = SearchBox.Text.Trim();
        return q.Length == 0 || a.Name.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadAppsAsync()
    {
        StatusText.Text = "Carregando aplicativos instalados...";
        var apps = await AppCatalog.GetAppsAsync();
        foreach (var a in apps)
            _apps.Add(a);
        _view.Refresh();
        StatusText.Text = $"{apps.Count} aplicativos encontrados";
        var tasks = _apps.Select(async a =>
        {
            await IconThrottle.WaitAsync();
            try { await AppCatalog.LoadIconAsync(a); }
            finally { IconThrottle.Release(); }
        }).ToList();
        await Task.WhenAll(tasks);
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => _view?.Refresh();

    private void AppList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            Confirm();
    }

    private void Choose_Click(object sender, RoutedEventArgs e) => Confirm();

    private void Confirm()
    {
        if (AppListBox.SelectedItem is InstalledApp app)
        {
            Result = app;
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
