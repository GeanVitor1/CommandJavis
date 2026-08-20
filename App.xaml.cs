using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Vox;

public partial class App : System.Windows.Application
{
    private const string AppMutex = "Vox.SingleInstance";
    private const string ShowEvent = "Vox.ShowSignal.v1";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private ShortcutManager? _manager;
    private VoiceAssistant? _voice;
    private MainWindow? _window;
    private MicWidget? _widget;
    private System.Windows.Forms.NotifyIcon? _tray;
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
            if (ShouldAutoUpdateRunningInstance())
            {
                Shutdown();
                return;
            }
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
        _manager.Notification += msg => Notify("Vox", msg);

        _voice = new VoiceAssistant(_manager);
        _voice.ListeningChanged += OnListeningChanged;
        _voice.PartialResult += text =>
        {
            _widget?.SetPartial(text);
            if (_window is { IsVisible: true })
                _window.SetVoiceStatus(text);
        };

        ApplySettings();
        Theme.Apply(Config.LoadAppearance().Theme);

        _window = new MainWindow(_manager, _voice);
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
            Text = "Vox",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _tray.DoubleClick += (_, _) => ShowWindow();

        ShowMainWindowWithRecovery();
    }

    private void ShowMainWindowWithRecovery()
    {
        if (_window == null) return;
        try { _window.Show(); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Visibility"))
        {
            Logger.Error("ShowWindow.VerifyCanShow", ex);
            try { _window.Close(); } catch { }
            if (_manager == null || _voice == null) return;
            _window = new MainWindow(_manager, _voice);
            _window.Closing += (_, args) =>
            {
                if (!_exiting) { args.Cancel = true; _window.Hide(); }
            };
            _window.IsVisibleChanged += (_, _) => { if (_window.IsVisible) _widget?.HideListening(); };
            _window.Show();
        }
    }

    private static string InstalledExePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vox", "Vox.exe");

    private bool ShouldAutoUpdateRunningInstance()
    {
        try
        {
            var installed = InstalledExePath;
            var current = Environment.ProcessPath ?? "";
            if (string.IsNullOrWhiteSpace(installed) || string.IsNullOrWhiteSpace(current))
                return false;
            if (!File.Exists(installed))
                return false;
            var installedFull = System.IO.Path.GetFullPath(installed);
            var currentFull = System.IO.Path.GetFullPath(current);
            if (string.Equals(installedFull, currentFull, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!IsInstalledNewerThanCurrent(currentFull, installedFull))
                return false;
            return TryReplaceInstalledAndRestart(installedFull);
        }
        catch (Exception ex)
        {
            Logger.Error("ShouldAutoUpdateRunningInstance", ex);
            return false;
        }
    }

    private static bool IsInstalledNewerThanCurrent(string currentPath, string installedPath)
    {
        try
        {
            var cur = new FileInfo(currentPath);
            var inst = new FileInfo(installedPath);
            if (!cur.Exists || !inst.Exists) return false;
            if (inst.LastWriteTimeUtc > cur.LastWriteTimeUtc.AddSeconds(2)) return true;
            if (inst.Length != cur.Length) return true;
            var curVer = FileVersionInfo.GetVersionInfo(currentPath).FileVersion;
            var instVer = FileVersionInfo.GetVersionInfo(installedPath).FileVersion;
            if (!string.IsNullOrWhiteSpace(curVer) && !string.IsNullOrWhiteSpace(instVer) &&
                string.Compare(instVer, curVer, StringComparison.OrdinalIgnoreCase) != 0)
                return true;
            return false;
        }
        catch { return false; }
    }

    private static bool TryReplaceInstalledAndRestart(string installedPath)
    {
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("Vox"))
            {
                try
                {
                    var exe = p.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(exe) &&
                        string.Equals(System.IO.Path.GetFullPath(exe), installedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        p.CloseMainWindow();
                    }
                }
                catch { }
            }
            for (int i = 0; i < 40; i++)
            {
                var still = System.Diagnostics.Process.GetProcessesByName("Vox").Any(p =>
                {
                    try { return string.Equals(System.IO.Path.GetFullPath(p.MainModule?.FileName ?? ""), installedPath, StringComparison.OrdinalIgnoreCase); }
                    catch { return false; }
                });
                if (!still) break;
                Thread.Sleep(100);
            }
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("Vox"))
            {
                try
                {
                    var exe = p.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(exe) &&
                        string.Equals(System.IO.Path.GetFullPath(exe), installedPath, StringComparison.OrdinalIgnoreCase))
                        p.Kill();
                }
                catch { }
            }
            Thread.Sleep(400);
            var psi = new System.Diagnostics.ProcessStartInfo(installedPath) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("TryReplaceInstalledAndRestart", ex);
            return false;
        }
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
        _voice.SetWakeWordEnabled(voice.Enabled && voice.WakeWord);
        _voice.SetMicrophone(voice.Enabled ? voice.MicrophoneId ?? "" : "");
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
        menu.Items.Add("Abrir Vox", null, (_, _) => ShowWindow());
        menu.Items.Add("Configurações", null, (_, _) => OpenSettings());
        menu.Items.Add("Recarregar config", null, (_, _) => _manager?.ReloadFromDisk());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitApp());
        return menu;
    }

    private void OpenSettings()
    {
        ShowWindow();
        _window?.NavigateToSettings();
    }

    public void OpenHistory()
    {
        ShowWindow();
        _window?.NavigateToHistory();
    }

    private void ShowWindow()
    {
        if (_window == null) return;
        try
        {
            if (!_window.IsVisible) ShowMainWindowWithRecovery();
            else _window.Show();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Visibility"))
        {
            Logger.Error("ShowWindow.VerifyCanShow", ex);
            try { _window.Close(); } catch { }
            if (_manager == null || _voice == null) return;
            _window = new MainWindow(_manager, _voice);
            _window.Closing += (_, args) =>
            {
                if (!_exiting) { args.Cancel = true; _window.Hide(); }
            };
            _window.IsVisibleChanged += (_, _) => { if (_window.IsVisible) _widget?.HideListening(); };
            _window.Show();
        }
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void ActivateMainWindow() => ShowWindow();

    public void ReloadConfig() => _manager?.ReloadFromDisk();

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
            return key?.GetValue("Vox") != null;
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
                key.SetValue("Vox", $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue("Vox", false);
        }
        catch
        {
        }
    }
}
