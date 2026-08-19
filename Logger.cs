using System.IO;

namespace Vox;

public static class Logger
{
    private static readonly object Gate = new();
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Vox", "error.log");

    public static void Error(string context, Exception ex)
    {
        try
        {
            lock (Gate)
            {
                WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex}");
            }
        }
        catch
        {
            // logging nunca pode derrubar o app
        }
    }

    public static void Info(string message)
    {
        try
        {
            lock (Gate)
            {
                WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
            }
        }
        catch
        {
        }
    }

    private static void WriteLine(string line)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        RotateIfNeeded();
        File.AppendAllText(FilePath, line + "\r\n");
    }

    private static void RotateIfNeeded()
    {
        const long maxSize = 1_000_000;
        if (!File.Exists(FilePath))
            return;
        var fi = new FileInfo(FilePath);
        if (fi.Length <= maxSize)
            return;
        try
        {
            var backup = FilePath + ".1";
            if (File.Exists(backup))
                File.Delete(backup);
            File.Move(FilePath, backup);
        }
        catch
        {
            // se não conseguir rotacionar, segue anexando
        }
    }
}