using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Vox;

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
    Hibernate,
    VolumeSet,
    Time,
    Date,
    Calc,
    Clipboard,
    Theme,
    ShowWindow,
    ReloadConfig,
    Timer,
    Cancel,
    Screenshot,
    CloseApp,
    ShowDesktop,
    Weather
}

public static class SystemActions
{
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const double VolumeStep = 0.05;

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public INPUTUNION U;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll")]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public static void Run(SystemCommand command) => Run(command, 0);

    public static void Run(SystemCommand command, int value)
    {
        switch (command)
        {
            case SystemCommand.PlayPause: SendMediaKey(VK_MEDIA_PLAY_PAUSE); break;
            case SystemCommand.Next: SendMediaKey(VK_MEDIA_NEXT_TRACK); break;
            case SystemCommand.Previous: SendMediaKey(VK_MEDIA_PREV_TRACK); break;
            case SystemCommand.VolumeUp: StepVolume(+VolumeStep); break;
            case SystemCommand.VolumeDown: StepVolume(-VolumeStep); break;
            case SystemCommand.VolumeSet: VolumeController.SetLevel(Math.Clamp(value, 0, 100) / 100f); break;
            case SystemCommand.Mute: VolumeController.SetMuted(!VolumeController.IsMuted()); break;
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
            SystemCommand.VolumeSet => "Volume ajustado",
            SystemCommand.Mute => "Som mutado",
            SystemCommand.Lock => "Tela bloqueada",
            SystemCommand.Sleep => "Colocando o PC para dormir",
            SystemCommand.Hibernate => "Hibernando",
            SystemCommand.Time => "Hora",
            SystemCommand.Date => "Data",
            SystemCommand.Calc => "Cálculo",
            SystemCommand.Clipboard => "Área de transferência",
            SystemCommand.Theme => "Tema alterado",
            SystemCommand.ShowWindow => "Mostrando o Vox",
            SystemCommand.ReloadConfig => "Configuração recarregada",
            SystemCommand.Timer => "Lembrete agendado",
            SystemCommand.Cancel => "Ação cancelada",
            SystemCommand.Screenshot => "Captura de tela",
            SystemCommand.CloseApp => "Fechando aplicativo",
            SystemCommand.ShowDesktop => "Mostrando a área de trabalho",
            SystemCommand.Weather => "Clima",
            _ => "Pronto"
        };
    }

    private static void SendMediaKey(byte vk)
    {
        var down = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk } }
        };
        var up = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } }
        };
        SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
    }

    private static void StepVolume(double delta)
    {
        var current = VolumeController.GetLevel();
        VolumeController.SetLevel((float)Math.Clamp(current + delta, 0.0, 1.0));
    }

    public static string? CaptureScreen()
    {
        try
        {
            var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            using var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);

            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Vox");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"vox-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
            return file;
        }
        catch
        {
            return null;
        }
    }
}

public static class WindowControl
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private const uint WM_CLOSE = 0x0010;
    private const byte VK_LWIN = 0x5B;
    private const byte VK_D = 0x44;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public static void ShowDesktop()
    {
        var inputs = new[]
        {
            new SystemActions.INPUT { type = INPUT_KEYBOARD, U = new SystemActions.INPUTUNION { ki = new SystemActions.KEYBDINPUT { wVk = VK_LWIN } } },
            new SystemActions.INPUT { type = INPUT_KEYBOARD, U = new SystemActions.INPUTUNION { ki = new SystemActions.KEYBDINPUT { wVk = VK_D } } },
            new SystemActions.INPUT { type = INPUT_KEYBOARD, U = new SystemActions.INPUTUNION { ki = new SystemActions.KEYBDINPUT { wVk = VK_D, dwFlags = KEYEVENTF_KEYUP } } },
            new SystemActions.INPUT { type = INPUT_KEYBOARD, U = new SystemActions.INPUTUNION { ki = new SystemActions.KEYBDINPUT { wVk = VK_LWIN, dwFlags = KEYEVENTF_KEYUP } } }
        };
        SystemActions.SendInput(4, inputs, Marshal.SizeOf<SystemActions.INPUT>());
    }

    public static bool CloseAppByName(string name)
    {
        var normalized = Normalize(name);
        if (normalized.Length == 0) return false;

        IntPtr best = IntPtr.Zero;
        var bestScore = 0;
        var ownPid = Environment.ProcessId;

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == ownPid) return true;

            var score = 0;
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                var procName = Normalize(proc.ProcessName);
                if (procName.Length > 0 && procName.Contains(normalized))
                    score = Math.Max(score, 20 + normalized.Length);
            }
            catch
            {
            }

            var len = GetWindowTextLength(hWnd);
            if (len > 0)
            {
                var sb = new StringBuilder(len + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                var title = Normalize(sb.ToString());
                if (title.Length > 0 && title.Contains(normalized))
                    score = Math.Max(score, 10 + normalized.Length);
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = hWnd;
            }
            return true;
        }, IntPtr.Zero);

        if (best == IntPtr.Zero) return false;
        PostMessage(best, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        return true;
    }

    public static bool CloseProcessesByPath(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return false;
        exePath = Environment.ExpandEnvironmentVariables(exePath);
        var closed = false;
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.HasExited) continue;
                    var main = proc.MainModule?.FileName;
                    if (main == null || !string.Equals(main, exePath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!proc.CloseMainWindow())
                        proc.Kill();
                    closed = true;
                }
                catch
                {
                }
                finally
                {
                    try { proc.Dispose(); } catch { }
                }
            }
        }
        catch
        {
        }
        return closed;
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(ch) || ch == ' ')
                sb.Append(ch);
        }
        return sb.ToString().Trim();
    }
}

public static class VolumeController
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator
    {
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IMMDevice ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr pClient);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate([In] ref Guid iid, [In] uint dwClsCtx, [In] IntPtr pActivationParams, out IntPtr ppInterface);
        [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IntPtr ppProperties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        [PreserveSig] int GetState(out int pdwState);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, IntPtr pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, IntPtr pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, IntPtr pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, IntPtr pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute(int bMute, IntPtr pguidEventContext);
        [PreserveSig] int GetMute(out int pbMute);
        [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        [PreserveSig] int VolumeStepUp();
        [PreserveSig] int VolumeStepDown();
        [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float pflMinVolumeDB, out float pflMaxVolumeDB, out float pflIncrement);
    }

    private static readonly Guid IID_IAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    private const int S_OK = 0;

    public static float GetLevel()
    {
        try
        {
            using var vol = GetVolume();
            vol.Check(vol._volume.GetMasterVolumeLevelScalar(out var level));
            return level;
        }
        catch
        {
            return 0f;
        }
    }

    public static void SetLevel(float level)
    {
        try
        {
            using var vol = GetVolume();
            vol.Check(vol._volume.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), IntPtr.Zero));
        }
        catch
        {
            // volume indisponível
        }
    }

    public static bool IsMuted()
    {
        try
        {
            using var vol = GetVolume();
            vol.Check(vol._volume.GetMute(out var muted));
            return muted != 0;
        }
        catch
        {
            return false;
        }
    }

    public static void SetMuted(bool muted)
    {
        try
        {
            using var vol = GetVolume();
            vol.Check(vol._volume.SetMute(muted ? 1 : 0, IntPtr.Zero));
        }
        catch
        {
            // sem endpoint de áudio
        }
    }

    private static ComVolume GetVolume()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        try
        {
            if (enumerator.GetDefaultAudioEndpoint(0, 0, out var device) < S_OK)
                throw new COMException("Sem dispositivo de áudio padrão");

            var iid = IID_IAudioEndpointVolume;
            if (device.Activate(ref iid, 1, IntPtr.Zero, out var volumePtr) < S_OK || volumePtr == IntPtr.Zero)
                throw new COMException("Não foi possível ativar o volume");

            var volume = (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(volumePtr);
            Marshal.Release(volumePtr);
            return new ComVolume(volume);
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private sealed class ComVolume : IDisposable
    {
        public readonly IAudioEndpointVolume _volume;

        public ComVolume(IAudioEndpointVolume volume) => _volume = volume;

        public void Check(int hr)
        {
            if (hr < S_OK)
                Marshal.ThrowExceptionForHR(hr);
        }

        public void Dispose() => Marshal.ReleaseComObject(_volume);
    }
}