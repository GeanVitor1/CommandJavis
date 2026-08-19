using System.Runtime.InteropServices;

namespace Vox;

[ComImport]
[Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemImageFactory
{
    [PreserveSig]
    int GetImage(NativeSize size, int flags, out IntPtr phbm);
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeSize
{
    public int cx;
    public int cy;
}

internal static class ShellIcons
{
    private static readonly Guid Iid = new("BCC18B79-BA16-442F-80C4-8A59C30C463B");

    private const int SIIGBF_BIGGERSIZEOK = 0x01;
    private const int SIIGBF_ICONONLY = 0x04;
    private const int SIIGBF_SCALEUP = 0x100;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    public static System.Drawing.Bitmap? Get(string path, int size = 32)
    {
        try
        {
            var guid = Iid;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref guid, out var obj);
            var factory = (IShellItemImageFactory)obj;
            var hbm = IntPtr.Zero;
            int hr = factory.GetImage(
                new NativeSize { cx = size, cy = size },
                SIIGBF_BIGGERSIZEOK | SIIGBF_ICONONLY | SIIGBF_SCALEUP,
                out hbm);
            if (hr != 0 || hbm == IntPtr.Zero)
                return null;
            try
            {
                return System.Drawing.Image.FromHbitmap(hbm);
            }
            finally
            {
                DeleteObject(hbm);
            }
        }
        catch
        {
            return null;
        }
    }
}
