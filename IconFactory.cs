using System.Drawing;
using System.Drawing.Drawing2D;

namespace Vox;

public static class IconFactory
{
    public static Icon Create()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new LinearGradientBrush(
                new Rectangle(0, 0, 32, 32),
                Color.FromArgb(40, 130, 240),
                Color.FromArgb(20, 60, 140),
                45f);
            g.FillEllipse(brush, 2, 2, 28, 28);
            using var font = new Font("Segoe UI", 12f, FontStyle.Bold);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("J", font, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
        }
        IntPtr hIcon = bmp.GetHicon();
        using var tmp = Icon.FromHandle(hIcon);
        return (Icon)tmp.Clone();
    }
}
