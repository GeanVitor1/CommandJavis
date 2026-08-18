using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JarvisComando;

public static class IconLoader
{
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JarvisComando", "Icons");

    public static async Task LoadAsync(HotkeyBinding b)
    {
        try
        {
            if (b.Icon != null)
                return;

            ImageSource? icon = null;
            if (!string.IsNullOrWhiteSpace(b.IconPath))
                icon = await LoadFromFieldAsync(b.IconPath);
            else if (b.Action.Equals("url", StringComparison.OrdinalIgnoreCase))
                icon = await LoadSiteIconAsync(b.Target);
            else
                icon = await Task.Run(() => LoadAppIcon(b));

            if (icon != null)
                b.Icon = icon;
        }
        catch (Exception ex)
        {
            Logger.Error("IconLoader.LoadAsync", ex);
        }
    }

    private static async Task<ImageSource?> LoadFromFieldAsync(string value)
    {
        if (value.StartsWith("http://") || value.StartsWith("https://"))
            return await DownloadAsync(value);
        return await Task.Run(() => LoadAppIconFromPath(Environment.ExpandEnvironmentVariables(value)));
    }

    private static async Task<ImageSource?> LoadSiteIconAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var host = uri.Host;
        var bytes = await LoadFaviconAsync(host);
        if (bytes == null)
        {
            var baseDomain = GetBaseDomain(host);
            if (baseDomain != null && !baseDomain.Equals(host, StringComparison.OrdinalIgnoreCase))
            {
                host = baseDomain;
                bytes = await LoadFaviconAsync(host);
            }
        }
        if (bytes == null)
            return null;

        return FromBytes(bytes);
    }

    private static async Task<byte[]?> LoadFaviconAsync(string host)
    {
        var cacheFile = Path.Combine(CacheDir, Sanitize(host) + ".png");
        if (File.Exists(cacheFile))
        {
            try
            {
                return await File.ReadAllBytesAsync(cacheFile);
            }
            catch
            {
                // ignora e baixa de novo
            }
        }

        var bytes = await DownloadBytesAsync($"https://www.google.com/s2/favicons?domain={host}&sz=64");
        if (bytes == null)
            return null;

        try
        {
            Directory.CreateDirectory(CacheDir);
            await File.WriteAllBytesAsync(cacheFile, bytes);
        }
        catch
        {
            // cache opcional
        }
        return bytes;
    }

    private static string? GetBaseDomain(string host)
    {
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2)
            return null;
        return string.Join('.', parts.Skip(parts.Length - 2));
    }

    private static ImageSource? LoadAppIcon(HotkeyBinding b)
    {
        var target = Environment.ExpandEnvironmentVariables(b.Target);

        if (target.Contains("Discord", StringComparison.OrdinalIgnoreCase) &&
            target.Contains("Update.exe", StringComparison.OrdinalIgnoreCase))
        {
            var discordDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord");
            if (Directory.Exists(discordDir))
            {
                var exe = Directory.GetDirectories(discordDir, "app-*")
                    .Select(d => Path.Combine(d, "Discord.exe"))
                    .Where(File.Exists)
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();
                if (exe != null)
                    return LoadAppIconFromPath(exe);
            }
        }

        return LoadAppIconFromPath(target);
    }

    private static ImageSource? LoadAppIconFromPath(string path)
    {
        if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            using var shellBmp = ShellIcons.Get(path);
            return shellBmp == null ? null : FromBitmap(shellBmp);
        }
        if (!File.Exists(path))
            return null;
        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
        if (icon == null)
            return null;
        using var bmp = icon.ToBitmap();
        return FromBitmap(bmp);
    }

    public static ImageSource FromBitmap(System.Drawing.Bitmap bmp)
    {
        var hbmp = bmp.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
            return FromBytes(ms.ToArray());
        }
        finally
        {
            DeleteObject(hbmp);
        }
    }

    private static async Task<ImageSource?> DownloadAsync(string url)
    {
        var bytes = await DownloadBytesAsync(url);
        return bytes == null ? null : FromBytes(bytes);
    }

    private static async Task<byte[]?> DownloadBytesAsync(string url)
    {
        try
        {
            using var resp = await Http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return null;
            return await resp.Content.ReadAsByteArrayAsync();
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource FromBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }

    private static string Sanitize(string host)
    {
        return string.Concat(host.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
    }
}
