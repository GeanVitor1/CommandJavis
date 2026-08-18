using System.Runtime.InteropServices;

namespace JarvisComando;

public sealed class GlobalTalkHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessageW(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    private readonly HookProc _proc;
    private readonly int _vk;
    private IntPtr _hook;
    private Thread? _thread;
    private uint _threadId;
    private bool _down;
    private bool _disposed;

    public event Action? TalkPressed;
    public event Action? TalkReleased;
    public event Action? HookFailed;

    public bool IsInstalled => _hook != IntPtr.Zero;

    public GlobalTalkHook(System.Windows.Forms.Keys key)
    {
        _vk = (int)key & 0xFF;
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_thread != null) return;
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            _hook = SetWindowsHookExW(WH_KEYBOARD_LL, _proc, GetModuleHandleW("user32.dll"), 0);
            if (_hook == IntPtr.Zero)
            {
                HookFailed?.Invoke();
                return;
            }
            MSG msg;
            while (GetMessageW(out msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
                if (_disposed)
                    break;
            }
        })
        { IsBackground = true, Name = "TalkHook" };
        _thread.Start();
    }

    public bool KeyEquals(System.Windows.Forms.Keys key) => ((int)key & 0xFF) == _vk;

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var msg = wParam.ToInt32();
            var isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            var isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

            if (kbd.vkCode == _vk)
            {
                if (isDown && !_down)
                {
                    _down = true;
                    TalkPressed?.Invoke();
                    return (IntPtr)1;
                }
                if (isUp && _down)
                {
                    _down = false;
                    TalkReleased?.Invoke();
                    return (IntPtr)1;
                }
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        _disposed = true;
        if (_thread != null)
            PostThreadMessageW(_threadId, 0x0012, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _thread?.Join(500);
    }
}