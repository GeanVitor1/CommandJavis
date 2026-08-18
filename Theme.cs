using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace JarvisComando;

public static class Theme
{
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

    public static void Apply(string theme)
    {
        if (System.Windows.Application.Current == null)
            return;

        var dark = Resolve(theme) == "dark";
        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        var themeDict = merged.FirstOrDefault(d => d.Contains("BgBrush"));
        if (themeDict == null)
            return;

        themeDict.Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
    }
}