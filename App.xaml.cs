using System.Windows;
using Microsoft.Win32;

namespace JarvisComando;

public partial class App : System.Windows.Application
{
    private const string AppMutex = "JarvisComando.SingleInstance";
    private const string ShowEvent = "JarvisComando.ShowSignal.v1";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private ShortcutManager? _manager;
    private VoiceAssistant? _voice;
    private MainWindow? _window;
    private MicWidget? _widget;
    private System.Windows.Forms.NotifyIcon? _tray;
    private SettingsWindow? _settings;
    private Thread? _waiter;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Logger.Error("AppDomain.UnhandledException", args.ExceptionObject as Exception ?? new Exception("objeto de exceção não-Exception"));
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error("UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        if (!TryAcquireMutex())
        {
            try
            {
                using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvent);
                signal.Set();
            }
            catch { }
            Shutdown();
            return;
        }

        StartShowWaiter();
        Toaster.EnsureRegistered();

        _manager = new ShortcutManager();
        _manager.Notification += msg => Notify("Jarvis Comando", msg);

        _voice = new VoiceAssistant(_manager);
        _voice.ListeningChanged += OnListeningChanged;

        ApplySettings();
        Theme.Apply(Config.LoadAppearance().Theme);

        _window = new MainWindow(_manager, _voice, OpenSettings);
        _window.Closing += (_, args) =>
        {
            if (!_exiting)
            {
                args.Cancel = true;
                _window.Hide();
            }
        };
        _window.IsVisibleChanged += (_, _) =>
        {
            if (_window.IsVisible)
                _widget?.HideListening();
        };

        _widget = new MicWidget();
        _widget.Hide();

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = IconFactory.Create(),
            Text = "Jarvis Comando",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _tray.DoubleClick += (_, _) => ShowWindow();

        _window.Show();
    }

    private bool TryAcquireMutex()
    {
        _mutex = new Mutex(true, AppMutex, out var createdNew);
        if (createdNew) return true;
        try { _mutex.ReleaseMutex(); } catch { }
        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    private void StartShowWaiter()
    {
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvent);
        _waiter = new Thread(() =>
        {
            while (!_exiting)
            {
                try { _showSignal.WaitOne(); } catch { break; }
                var dispatcher = Dispatcher;
                if (dispatcher == null || dispatcher.HasShutdownStarted)
                    continue;
                dispatcher.BeginInvoke(ShowWindow);
            }
        })
        { IsBackground = true, Name = "ShowWaiter" };
        _waiter.Start();
    }

    public void ApplySettings()
    {
        if (_voice == null) return;
        var voice = Config.LoadVoice();
        _voice.ConfigureTalkHotkey(voice.Enabled ? voice.TalkHotkey : "");
    }

    private void OnListeningChanged(bool listening)
    {
        if (_widget == null) return;
        var showWindow = _window != null && _window.IsVisible;
        if (listening && !showWindow)
            _widget.ShowListening();
        else
            _widget.HideListening();
    }

    private void Notify(string title, string message)
    {
        Toaster.Show(title, message);
        _tray?.ShowBalloonTip(3000, title, message, System.Windows.Forms.ToolTipIcon.Warning);
    }

    private System.Windows.Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Abrir Jarvis Comando", null, (_, _) => ShowWindow());
        menu.Items.Add("Configurações", null, (_, _) => OpenSettings());
        menu.Items.Add("Recarregar config", null, (_, _) => _manager?.ReloadFromDisk());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitApp());
        return menu;
    }

    private void OpenSettings()
    {
        if (_settings == null)
        {
            _settings = new SettingsWindow(_voice!) { Owner = _window };
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Owner = _window;
        _settings.Show();
        _settings.Activate();
    }

    private void ShowWindow()
    {
        if (_window == null) return;
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void ExitApp()
    {
        _exiting = true;
        _voice?.Dispose();
        _manager?.Dispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        try { _showSignal?.Set(); } catch { }
        _window?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        try { _mutex?.ReleaseMutex(); } catch { }
        base.OnExit(e);
    }

    public static bool IsAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue("JarvisComando") != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled)
                key.SetValue("JarvisComando", $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue("JarvisComando", false);
        }
        catch
        {
        }
    }
}