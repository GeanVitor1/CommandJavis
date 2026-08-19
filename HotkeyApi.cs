using System.Runtime.InteropServices;

namespace Vox;

public static class HotkeyApi
{
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static bool Register(IntPtr hWnd, int id, string modifiers, string key)
    {
        return RegisterHotKey(hWnd, id, ParseModifiers(modifiers), ParseKey(key));
    }

    public static void Unregister(IntPtr hWnd, int id)
    {
        UnregisterHotKey(hWnd, id);
    }

    private static uint ParseModifiers(string modifiers)
    {
        uint mods = 0;
        foreach (var part in modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            mods |= part.ToLowerInvariant() switch
            {
                "alt" => MOD_ALT,
                "ctrl" or "control" => MOD_CONTROL,
                "shift" => MOD_SHIFT,
                "win" or "windows" => MOD_WIN,
                _ => 0
            };
        }
        return mods | MOD_NOREPEAT;
    }

    private static uint ParseKey(string key)
    {
        if (Enum.TryParse<System.Windows.Forms.Keys>(key, true, out var k))
            return (uint)k & 0xFF;
        return 0;
    }
}
