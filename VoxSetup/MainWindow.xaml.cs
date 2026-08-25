using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;

namespace VoxSetup;

public partial class MainWindow : Window
{
    private bool _installing;
    private string _installedDest = "";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var def = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vox");
        PathBox.Text = def;
        UpdateSpaceCheck();
    }

    private void UpdateSpaceCheck()
    {
        try
        {
            var drive = Path.GetPathRoot(PathBox.Text) ?? "C:\\";
            var di = new DriveInfo(drive);
            var free = di.AvailableFreeSpace / (1024 * 1024 * 1024.0);
            SpaceCheck.Text = $"Espaço necessário: ~260 MB · Livre em {drive} {free:F1} GB · Instalação para o usuário atual";
        }
        catch { }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Escolha onde instalar o Vox",
            InitialDirectory = ExpandPath(PathBox.Text)
        };
        if (dlg.ShowDialog() == true)
        {
            PathBox.Text = dlg.FolderName;
            UpdateSpaceCheck();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void SuccessClose_Click(object sender, RoutedEventArgs e) => Close();

    private void SuccessOpen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exe = Path.Combine(_installedDest, "Vox.exe");
            if (!File.Exists(exe)) exe = Path.Combine(ExpandPath(PathBox.Text.Trim()), "Vox.exe");
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! });
        }
        catch (Exception ex)
        {
            ShowError($"Instalado, mas falha ao abrir:\n{ex.Message}");
            return;
        }
        Close();
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_installing) return;

        var dest = ExpandPath(PathBox.Text.Trim());
        if (string.IsNullOrWhiteSpace(dest))
        {
            ShowError("Escolha uma pasta válida.");
            return;
        }

        try { Directory.CreateDirectory(dest); }
        catch (Exception ex) { ShowError($"Não foi possível criar a pasta:\n{ex.Message}"); return; }

        // verifica drive
        try
        {
            var root = Path.GetPathRoot(dest)!;
            var drive = new DriveInfo(root);
            if (drive.AvailableFreeSpace < 350L * 1024 * 1024)
            {
                ShowError($"Espaço insuficiente em {root} — precisa de ~260 MB livres.");
                return;
            }
        }
        catch { }

        _installing = true;
        InstallBtn.IsEnabled = false;
        InstallBtn.Content = "Instalando...";
        ProgressPanel.Visibility = Visibility.Visible;
        ErrorPanel.Visibility = Visibility.Collapsed;
        SetProgress(4, "Preparando instalação...");

        try
        {
            // 1) fecha Vox se rodando
            SetProgress(6, "Verificando instância em execução...");
            await Task.Run(() => StopVox());

            // 2) localiza payload.zip
            SetProgress(10, "Localizando pacote...");
            var payloadPath = await Task.Run(() => FindPayload());
            if (payloadPath == null)
            {
                // tenta recurso embutido
                var embedded = await TryExtractEmbeddedPayloadAsync();
                if (embedded == null)
                    throw new FileNotFoundException("payload.zip não encontrado. Re-baixe o instalador ou execute de dentro da pasta do projeto (VoxSetup/payload.zip).");
                payloadPath = embedded;
            }
            DetailText.Text = Path.GetFileName(payloadPath) + $" · {new FileInfo(payloadPath).Length / (1024*1024):F0} MB";
            SetProgress(14, "Arquivos localizados");

            // 3) extrai para temp
            SetProgress(18, "Extraindo pacote...");
            var tmp = Path.Combine(Path.GetTempPath(), "VoxSetup_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tmp);
            try
            {
                await Task.Run(() => ZipFile.ExtractToDirectory(payloadPath, tmp, true));
            }
            catch (Exception ex) { throw new InvalidOperationException($"Falha ao extrair payload.zip: {ex.Message}", ex); }

            SetProgress(55, "Copiando arquivos...");
            DetailText.Text = $"Destino: {dest}";

            // 4) copia para destino com retry
            await Task.Run(() => CopyWithRetry(tmp, dest));

            // limpa temp
            try { Directory.Delete(tmp, true); } catch { }
            try { if (payloadPath.StartsWith(Path.GetTempPath())) File.Delete(payloadPath); } catch { }

            SetProgress(82, "Criando atalhos...");

            // 5) atalhos — captura estado da UI antes do Task.Run (evita cross-thread)
            var wantStartMenu = StartMenuCheck.IsChecked == true;
            var wantDesktop = DesktopCheck.IsChecked == true;
            var wantAutostart = AutostartCheck.IsChecked == true;

            await Task.Run(() =>
            {
                var exe = Path.Combine(dest, "Vox.exe");
                if (wantStartMenu) CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Vox", exe);
                // StartMenu Programs subfolder
                try
                {
                    var programs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Vox");
                    Directory.CreateDirectory(programs);
                    CreateShortcut(programs, "Vox", exe);
                }
                catch { }

                if (wantDesktop)
                    CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Vox", exe);

                // Startup lnk if autostart checked (alternative to registry)
                var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var startupLnk = Path.Combine(startup, "Vox.lnk");
                if (wantAutostart)
                    CreateShortcut(startup, "Vox", exe);
                else
                    try { if (File.Exists(startupLnk)) File.Delete(startupLnk); } catch { }

                // Registry Run key
                try
                {
                    var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)
                                 ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                    if (wantAutostart)
                        runKey?.SetValue("Vox", $"\"{exe}\"");
                    else
                        try { runKey?.DeleteValue("Vox", false); } catch { }
                    runKey?.Close();
                }
                catch { }
            });

            SetProgress(92, "Finalizando...");
            await Task.Delay(300);

            SetProgress(100, "Pronto! Vox instalado com sucesso.");
            DetailText.Text = $"Instalado em {dest}";
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00D68F"));
            PercentText.Text = "✓";

            InstallBtn.Content = "Instalado";
            InstallBtn.IsEnabled = false;
            _installing = false;
            _installedDest = dest;
            SuccessPathText.Text = dest;
            SuccessOverlay.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message + (ex.InnerException != null ? $"\n{ex.InnerException.Message}" : ""));
            SetProgress(0, "Falha na instalação");
            InstallBtn.IsEnabled = true;
            InstallBtn.Content = "Tentar novamente";
            _installing = false;
        }
    }

    private void SetProgress(int percent, string status)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = status;
            PercentText.Text = $"{percent}%";
            ProgressFill.Width = Math.Max(0, (ActualWidth - 56) * percent / 100.0);
            // fallback width calculation for progress bar container (520 - 56)
            var containerWidth = 464; // 520 - 28*2
            try { containerWidth = (int)(ProgressPanel.ActualWidth > 20 ? ProgressPanel.ActualWidth : 464); } catch { }
            ProgressFill.Width = containerWidth * percent / 100.0;
        });
    }

    private void ShowError(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            ErrorText.Text = msg;
            ErrorPanel.Visibility = Visibility.Visible;
        });
    }

    private static string ExpandPath(string p)
    {
        if (p.Contains("%")) p = Environment.ExpandEnvironmentVariables(p);
        return p;
    }

    private static void StopVox()
    {
        try
        {
            foreach (var pr in Process.GetProcessesByName("Vox"))
            {
                try { if (!pr.HasExited) pr.CloseMainWindow(); } catch { }
            }
            Thread.Sleep(800);
            foreach (var pr in Process.GetProcessesByName("Vox"))
            {
                try { if (!pr.HasExited) pr.Kill(); } catch { }
            }
            Thread.Sleep(400);
            for (int i = 0; i < 20; i++)
            {
                if (Process.GetProcessesByName("Vox").Length == 0) break;
                Thread.Sleep(250);
            }
        }
        catch { }
    }

    private static string? FindPayload()
    {
        // 1) ao lado do exe do setup
        var exeDir = AppContext.BaseDirectory;
        var cands = new[]
        {
            Path.Combine(exeDir, "payload.zip"),
            Path.Combine(exeDir, "VoxSetup", "payload.zip"),
            Path.Combine(Directory.GetCurrentDirectory(), "payload.zip"),
            Path.Combine(Directory.GetCurrentDirectory(), "VoxSetup", "payload.zip"),
            Path.Combine(exeDir, "..", "VoxSetup", "payload.zip"),
            Path.Combine(exeDir, "..", "..", "VoxSetup", "payload.zip"),
            "VoxSetup/payload.zip",
            "payload.zip",
            Path.Combine(exeDir, "..", "publish", "payload.zip"),
        };
        foreach (var c in cands)
        {
            try { var full = Path.GetFullPath(c); if (File.Exists(full)) return full; } catch { }
        }
        // procura recursiva perto do exe (OneDrive)
        try
        {
            var root = Path.GetFullPath(Path.Combine(exeDir, ".."));
            var found = Directory.GetFiles(root, "payload.zip", SearchOption.AllDirectories).FirstOrDefault();
            if (found != null) return found;
        }
        catch { }
        return null;
    }

    private async Task<string?> TryExtractEmbeddedPayloadAsync()
    {
        // tenta recurso embutido: VoxSetup.payload.zip
        var asm = Assembly.GetExecutingAssembly();
        var names = asm.GetManifestResourceNames();
        var resName = names.FirstOrDefault(n => n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase));
        if (resName == null) return null;
        var tmp = Path.Combine(Path.GetTempPath(), "vox_payload_" + Guid.NewGuid().ToString("N")[..6] + ".zip");
        await using (var s = asm.GetManifestResourceStream(resName)!)
        await using (var fs = File.Create(tmp))
            await s.CopyToAsync(fs);
        return tmp;
    }

    private static void CopyWithRetry(string src, string dest)
    {
        // copia recursiva com retry para lock de exe
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                CopyDirectory(src, dest);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                StopVox();
                Thread.Sleep(600);
            }
        }
        CopyDirectory(src, dest);
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            var dst = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            // não sobrescreve config.json se já existe (preserva user)
            if (rel.Equals("config.json", StringComparison.OrdinalIgnoreCase) && File.Exists(dst))
                continue;
            File.Copy(file, dst, true);
        }
    }

    private static void CreateShortcut(string folder, string name, string target)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var lnk = Path.Combine(folder, name + ".lnk");
            var ws = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            dynamic sc = ws!.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, ws, new object[] { lnk })!;
            sc.TargetPath = target;
            sc.WorkingDirectory = Path.GetDirectoryName(target)!;
            sc.Description = "Vox — assistente local";
            sc.IconLocation = target;
            sc.Save();
        }
        catch { }
    }
}
