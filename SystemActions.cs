using System.Runtime.InteropServices;

namespace JarvisComando;

public enum SystemCommand
{
    PlayPause,
    Next,
    Previous,
    VolumeUp,
    VolumeDown,
    Mute,
    Lock,
    Sleep,
    Hibernate
}

public static class SystemActions
{
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const byte VK_VOLUME_UP = 0xAF;
    private const byte VK_VOLUME_DOWN = 0xAE;
    private const byte VK_VOLUME_MUTE = 0xAD;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll")]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public static void Run(SystemCommand command)
    {
        switch (command)
        {
            case SystemCommand.PlayPause: Press(VK_MEDIA_PLAY_PAUSE); break;
            case SystemCommand.Next: Press(VK_MEDIA_NEXT_TRACK); break;
            case SystemCommand.Previous: Press(VK_MEDIA_PREV_TRACK); break;
            case SystemCommand.VolumeUp:
                for (int i = 0; i < 5; i++) Press(VK_VOLUME_UP);
                break;
            case SystemCommand.VolumeDown:
                for (int i = 0; i < 5; i++) Press(VK_VOLUME_DOWN);
                break;
            case SystemCommand.Mute: Press(VK_VOLUME_MUTE); break;
            case SystemCommand.Lock: LockWorkStation(); break;
            case SystemCommand.Sleep: SetSuspendState(false, false, false); break;
            case SystemCommand.Hibernate: SetSuspendState(true, false, false); break;
        }
    }

    public static string Label(SystemCommand command)
    {
        return command switch
        {
            SystemCommand.PlayPause => "Tocando/Pausando",
            SystemCommand.Next => "Próxima faixa",
            SystemCommand.Previous => "Faixa anterior",
            SystemCommand.VolumeUp => "Volume aumentado",
            SystemCommand.VolumeDown => "Volume diminuído",
            SystemCommand.Mute => "Som mutado",
            SystemCommand.Lock => "Tela bloqueada",
            SystemCommand.Sleep => "Colocando o PC para dormir",
            SystemCommand.Hibernate => "Hibernando",
            _ => "Pronto"
        };
    }

    private static void Press(byte vk) => keybd_event(vk, 0, 0, 0);
}