using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Vox;

public static class Theme
{
    private static ResourceDictionary? _dark;
    private static ResourceDictionary? _light;

    public static bool IsSystemLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 1;
        }
        catch
        {
            return false;
        }
    }

    public static string Resolve(string theme)
    {
        return theme.Equals("light", StringComparison.OrdinalIgnoreCase) ? "light"
             : theme.Equals("dark", StringComparison.OrdinalIgnoreCase) ? "dark"
             : IsSystemLight() ? "light" : "dark";
    }

    public static void Preload()
    {
        _dark ??= Load("Themes/Dark.xaml");
        _light ??= Load("Themes/Light.xaml");
    }

    private static ResourceDictionary Load(string source)
        => new() { Source = new Uri(source, UriKind.Relative) };

    public static void Apply(string theme)
    {
        if (System.Windows.Application.Current == null)
            return;

        Preload();
        var dark = Resolve(theme) == "dark";
        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        var fresh = dark ? _dark! : _light!;

        if (merged.Count > 0 && ReferenceEquals(merged[0], fresh))
            return;

        var themeDict = merged.FirstOrDefault(d => d.Contains("BgBrush") && !ReferenceEquals(d, fresh));
        if (themeDict != null)
        {
            merged.Insert(0, fresh);
            merged.Remove(themeDict);
        }
        else
        {
            merged.Insert(0, fresh);
        }
    }
}
