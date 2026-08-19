using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Vox;

public static class Toaster
{
    public const string Aumid = "Vox.App";

    private static bool _ready;
    private static bool _tried;

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("0000010B-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;

        public PROPERTYKEY(Guid g, uint p) { fmtid = g; pid = p; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1, wReserved2, wReserved3;
        public IntPtr data1;
        public IntPtr data2;
    }

    private const ushort VT_LPWSTR = 31;
    private static readonly PROPERTYKEY PKEY_AppUserModelID =
        new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    private static Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");
    private static Guid IID_IShellLinkW = new("000214F9-0000-0000-C000-000000000046");
    private static Guid IID_IPersistFile = new("0000010B-0000-0000-C000-000000000046");
    private static Guid IID_IPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid riid, out IntPtr ppv);

    public static void EnsureRegistered()
    {
        if (_tried) return;
        _tried = true;
        try
        {
            var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var lnk = Path.Combine(startMenu, "Vox.lnk");
            var exe = CurrentExePath();
            if (File.Exists(lnk))
            {
                var target = GetShortcutTarget(lnk);
                if (string.Equals(target, exe, StringComparison.OrdinalIgnoreCase))
                {
                    _ready = true;
                    return;
                }
                try { File.Delete(lnk); } catch { }
            }
            CreateShortcut(lnk, exe);
            _ready = File.Exists(lnk);
        }
        catch (Exception ex)
        {
            Logger.Error("Toaster.EnsureRegistered", ex);
            _ready = false;
        }
    }

    public static void Show(string title, string message)
    {
        if (!_ready) return;
        try
        {
            var xml = "<toast><visual><binding template='ToastGeneric'>"
                    + $"<text>{XmlEscape(title)}</text><text>{XmlEscape(message)}</text>"
                    + "</binding></visual></toast>";
            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);
            var toast = new Windows.UI.Notifications.ToastNotification(doc);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier(Aumid).Show(toast);
        }
        catch (Exception ex)
        {
            Logger.Error("Toaster.Show", ex);
        }
    }

    private static string XmlEscape(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string CurrentExePath()
        => Environment.ProcessPath
        ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
        ?? throw new InvalidOperationException("exe path não encontrado");

    private static IntPtr CreateShellLinkPtr()
    {
        var hr = CoCreateInstance(ref CLSID_ShellLink, IntPtr.Zero, 0x1, ref IID_IShellLinkW, out var ptr);
        if (hr != 0)
            throw new COMException($"CoCreateInstance ShellLink falhou (0x{hr:X8})", hr);
        return ptr;
    }

    private static string? GetShortcutTarget(string lnk)
    {
        var ptr = IntPtr.Zero;
        try
        {
            ptr = CreateShellLinkPtr();
            var link = (IShellLinkW)Marshal.GetObjectForIUnknown(ptr);
            var persist = (IPersistFile)link;
            persist.Load(lnk, 0);
            var sb = new StringBuilder(1024);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            return sb.ToString();
        }
        catch
        {
            return null;
        }
        finally
        {
            if (ptr != IntPtr.Zero) Marshal.Release(ptr);
        }
    }

    private static void CreateShortcut(string lnk, string exe)
    {
        var ptr = IntPtr.Zero;
        var storePtr = IntPtr.Zero;
        var persistPtr = IntPtr.Zero;
        try
        {
            ptr = CreateShellLinkPtr();
            var link = (IShellLinkW)Marshal.GetObjectForIUnknown(ptr);
            link.SetPath(exe);
            link.SetWorkingDirectory(Path.GetDirectoryName(exe) ?? "");

            Marshal.QueryInterface(ptr, in IID_IPropertyStore, out storePtr);
            var store = (IPropertyStore)Marshal.GetObjectForIUnknown(storePtr);
            var pv = new PROPVARIANT { vt = VT_LPWSTR, data1 = Marshal.StringToCoTaskMemUni(Aumid) };
            try
            {
                var key = PKEY_AppUserModelID;
                store.SetValue(ref key, ref pv);
                store.Commit();
            }
            finally
            {
                Marshal.FreeCoTaskMem(pv.data1);
            }

            Marshal.QueryInterface(ptr, in IID_IPersistFile, out persistPtr);
            var persist = (IPersistFile)Marshal.GetObjectForIUnknown(persistPtr);
            persist.Save(lnk, false);
        }
        finally
        {
            if (persistPtr != IntPtr.Zero) Marshal.Release(persistPtr);
            if (storePtr != IntPtr.Zero) Marshal.Release(storePtr);
            if (ptr != IntPtr.Zero) Marshal.Release(ptr);
        }
    }
}