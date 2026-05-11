using System.IO;
using System.Text;

namespace WpfApp3.Services;

public static class EncryptedStorageDebugLog
{
    private static readonly object SyncRoot = new();
    private static readonly UTF8Encoding Utf8 = new(false);

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WpfApp3",
        "Logs",
        "encrypted-storage.log");

    public static void Write(string source, string message)
    {
        try
        {
            string? directoryPath = Path.GetDirectoryName(LogPath);

            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}] {message}{Environment.NewLine}";

            lock (SyncRoot)
            {
                File.AppendAllText(LogPath, line, Utf8);
            }
        }
        catch
        {
        }
    }

    public static void WriteException(string source, Exception exception)
    {
        Write(source, $"{exception.GetType().FullName}: {Normalize(exception.Message)}");
        Write(source, Normalize(exception.ToString()));
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        return value.Replace("\r", "\\r").Replace("\n", "\\n");
    }
}