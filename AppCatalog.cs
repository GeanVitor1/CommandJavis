using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace JarvisComando;

public class InstalledApp : INotifyPropertyChanged
{
    public string Name { get; init; } = "";
    public string AppId { get; init; } = "";

    public bool IsUwp => AppId.Contains('!');

    public string Target => IsUwp ? "shell:AppsFolder\\" + AppId : AppId;

    public string AvatarChar => string.IsNullOrWhiteSpace(Name)
        ? "?"
        : Name.Trim().Substring(0, 1).ToUpperInvariant();

    private ImageSource? _icon;

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasIcon)));
        }
    }

    public bool HasIcon => Icon != null;

    public event PropertyChangedEventHandler? PropertyChanged;
}

public static class AppCatalog
{
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromDays(7);
    private static readonly string CacheFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JarvisComando", "apps.json");

    private static List<InstalledApp>? _cache;
    private static Dictionary<string, string>? _shortcuts;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    public static async Task<List<InstalledApp>> GetAppsAsync()
    {
        if (_cache != null)
            return _cache;

        var cached = TryLoadCache();
        if (cached != null)
        {
            _cache = cached;
            await Task.Run(BuildShortcutIndex);
            return _cache;
        }

        var list = new List<InstalledApp>();
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -Command \"[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; Get-StartApps | Select-Object Name, AppID | ConvertTo-Json -Compress\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var json = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();
                list = Parse(json);
            }
        }
        catch
        {
            list = new List<InstalledApp>();
        }

        if (list.Count > 0)
            SaveCache(list);

        _cache = list;
        await Task.Run(BuildShortcutIndex);
        return _cache;
    }

    private static List<InstalledApp>? TryLoadCache()
    {
        try
        {
            if (!File.Exists(CacheFile))
                return null;
            var stamp = File.GetLastWriteTimeUtc(CacheFile);
            if (DateTime.UtcNow - stamp > CacheMaxAge)
                return null;
            var doc = JsonDocument.Parse(File.ReadAllText(CacheFile));
            var list = new List<InstalledApp>();
            foreach (var el in doc.RootElement.GetProperty("apps").EnumerateArray())
            {
                var name = el.TryGetProperty("name", out var n) ? n.GetString() : null;
                var id = el.TryGetProperty("appId", out var i) ? i.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
                    list.Add(new InstalledApp { Name = name.Trim(), AppId = id.Trim() });
            }
            return list.Count > 0 ? list : null;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveCache(List<InstalledApp> apps)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
            var payload = new { apps = apps.Select(a => new { name = a.Name, appId = a.AppId }) };
            var json = JsonSerializer.Serialize(payload);
            var tmp = CacheFile + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, CacheFile, overwrite: true);
        }
        catch
        {
            // cache é opcional
        }
    }

    private static List<InstalledApp> Parse(string json)
    {
        var list = new List<InstalledApp>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var name = el.TryGetProperty("Name", out var n) ? n.GetString() : null;
                var id = el.TryGetProperty("AppID", out var i) ? i.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id))
                    continue;
                if (id.Contains("Microsoft.AutoGenerated", StringComparison.OrdinalIgnoreCase))
                    continue;
                list.Add(new InstalledApp { Name = name.Trim(), AppId = id.Trim() });
            }
        }
        catch
        {
            // saída inesperada do PowerShell
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<InstalledApp>();
        foreach (var a in list.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (seen.Add(a.AppId))
                result.Add(a);
        }
        return result;
    }

    private static void BuildShortcutIndex()
    {
        if (_shortcuts != null)
            return;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                var dirs = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
                };
                foreach (var dir in dirs)
                {
                    if (!Directory.Exists(dir))
                        continue;
                    foreach (var lnk in Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories))
                    {
                        try
                        {
                            dynamic sc = shell.CreateShortcut(lnk);
                            string? name = Path.GetFileNameWithoutExtension(lnk);
                            string? target = sc.TargetPath as string;
                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(target))
                                continue;
                            if (!map.TryGetValue(name, out var existing) || IsBetterTarget(target, existing))
                                map[name] = target;
                        }
                        catch
                        {
                            // atalho inválido
                        }
                    }
                }
            }
        }
        catch
        {
            // sem índice de atalhos
        }
        _shortcuts = map;
    }

    private static bool IsBetterTarget(string candidate, string current)
    {
        bool candUpdate = candidate.Contains("Update.exe", StringComparison.OrdinalIgnoreCase);
        bool curUpdate = current.Contains("Update.exe", StringComparison.OrdinalIgnoreCase);
        return candUpdate != curUpdate && !candUpdate;
    }

    public static async Task LoadIconAsync(InstalledApp app)
    {
        try
        {
            if (app.Icon != null)
                return;
            var icon = await Task.Run(() => ResolveIcon(app));
            if (icon != null)
                app.Icon = icon;
        }
        catch
        {
            // letra como fallback
        }
    }

    private static ImageSource? ResolveIcon(InstalledApp app)
    {
        if (app.IsUwp)
        {
            using var bmp = ShellIcons.Get("shell:AppsFolder\\" + app.AppId);
            return bmp == null ? null : IconLoader.FromBitmap(bmp);
        }

        ImageSource? icon = null;

        if (_shortcuts != null && _shortcuts.TryGetValue(app.Name, out var lnkTarget))
            icon = ExtractFromPath(lnkTarget);

        if (icon == null)
            icon = ExtractFromPath(app.AppId);

        if (icon == null)
        {
            var squirrel = TrySquirrel(app.AppId);
            if (squirrel != null)
                icon = ExtractFromPath(squirrel);
        }

        if (icon == null)
        {
            var guidPath = TryGuidPath(app.AppId);
            if (guidPath != null)
                icon = ExtractFromPath(guidPath);
        }

        if (icon == null)
        {
            var resolved = ResolveAppPaths(app.AppId);
            if (resolved != null)
                icon = ExtractFromPath(resolved);
        }

        if (icon == null)
        {
            using var bmp = ShellIcons.Get(app.AppId);
            icon = bmp == null ? null : IconLoader.FromBitmap(bmp);
        }

        return icon;
    }

    private static ImageSource? ExtractFromPath(string path)
    {
        if (!File.Exists(path))
            return null;
        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
        if (icon == null)
            return null;
        using var bmp = icon.ToBitmap();
        return IconLoader.FromBitmap(bmp);
    }

    private static string? TrySquirrel(string appId)
    {
        if (!appId.StartsWith("com.squirrel.", StringComparison.OrdinalIgnoreCase))
            return null;
        var parts = appId.Split('.');
        var name = parts[^1];
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name);
        if (!Directory.Exists(dir))
            return null;
        return Directory.GetDirectories(dir, "app-*")
            .Select(d => Path.Combine(d, name + ".exe"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
    }

    private static string? TryGuidPath(string appId)
    {
        if (!appId.StartsWith("{"))
            return null;
        var idx = appId.IndexOf('}');
        if (idx < 0 || idx + 1 >= appId.Length)
            return null;
        var guidStr = appId.Substring(0, idx + 1);
        if (!Guid.TryParse(guidStr, out var guid))
            return null;
        try
        {
            SHGetKnownFolderPath(ref guid, 0, IntPtr.Zero, out var ptr);
            string? basePath;
            try
            {
                basePath = Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
            if (string.IsNullOrWhiteSpace(basePath))
                return null;
            var rel = appId.Substring(idx + 1).TrimStart('\\');
            return Path.Combine(basePath, rel);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveAppPaths(string name)
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var sub in new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths"
            })
            {
                try
                {
                    using var key = hive.OpenSubKey(sub + "\\" + name);
                    if (key?.GetValue(null) is string v)
                    {
                        v = v.Trim().Trim('"');
                        if (File.Exists(v))
                            return v;
                    }
                }
                catch
                {
                    // segue tentando
                }
            }
        }
        return null;
    }
}
