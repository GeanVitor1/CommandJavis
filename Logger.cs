using System.IO;

namespace JarvisComando;

public static class Logger
{
    private static readonly object Gate = new();
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JarvisComando", "error.log");

    public static void Error(string context, Exception ex)
    {
        try
        {
            lock (Gate)
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex}\r\n");
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > 1_000_000)
                    File.WriteAllText(FilePath, $"# log rotacionado em {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");
            }
        }
        catch
        {
            // logging nunca pode derrubar o app
        }
    }
}