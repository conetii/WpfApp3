using System.IO;
using System.Text;
using WpfApp3.Models;

namespace WpfApp3.Services;

public sealed class DocumentFileService
{
    private const int DetectionSampleSize = 16384;
    private const int BinaryPreviewSize = 4096;
    private const int TextPreviewCharacterCount = 4096;
    private const int SearchBufferSize = 8192;
    private const long MaxDirectTextFileSize = int.MaxValue;
    private readonly Encoding _defaultEncoding = new UTF8Encoding(false);

    static DocumentFileService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public DocumentOpenResult Open(string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] sample = ReadBytes(stream, DetectionSampleSize);

        if (IsLikelyBinary(sample))
        {
            stream.Position = 0;
            byte[] previewBytes = ReadBytes(stream, BinaryPreviewSize);
            return new DocumentOpenResult(true, true, BuildBinaryPreview(previewBytes, stream.Length), null, stream.Length);
        }

        Encoding encoding = DetectEncoding(sample);

        if (stream.Length > MaxDirectTextFileSize)
        {
            stream.Position = 0;
            string preview = ReadTextPreview(stream, encoding, TextPreviewCharacterCount);
            return new DocumentOpenResult(false, true, BuildLargeTextPreview(preview, stream.Length), encoding, stream.Length);
        }

        stream.Position = 0;
        byte[] bytes = ReadBytes(stream, (int)stream.Length);
        string content = encoding.GetString(bytes);
        return new DocumentOpenResult(false, false, content, encoding, stream.Length);
    }

    public void SaveText(string filePath, string content, Encoding? encoding)
    {
        Encoding targetEncoding = encoding ?? _defaultEncoding;
        File.WriteAllText(filePath, content, targetEncoding);
    }

    public bool FileContainsText(string filePath, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        try
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] sample = ReadBytes(stream, DetectionSampleSize);

            if (IsLikelyBinary(sample))
            {
                return false;
            }

            stream.Position = 0;
            Encoding encoding = DetectEncoding(sample);

            using StreamReader reader = new(stream, encoding, true, SearchBufferSize, leaveOpen: false);
            return StreamContainsText(reader, query);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ReadBytes(Stream stream, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        byte[] buffer = new byte[count];
        int totalRead = 0;

        while (totalRead < count)
        {
            int read = stream.Read(buffer, totalRead, count - totalRead);

            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead == buffer.Length)
        {
            return buffer;
        }

        byte[] result = new byte[totalRead];
        Array.Copy(buffer, result, totalRead);
        return result;
    }

    private Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return new UTF8Encoding(true);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            return Encoding.UTF32;
        }

        UTF8Encoding utf8 = new(false, true);

        try
        {
            utf8.GetString(bytes);
            return new UTF8Encoding(false);
        }
        catch
        {
        }

        try
        {
            return Encoding.GetEncoding(1251);
        }
        catch
        {
            return _defaultEncoding;
        }
    }

    private static string ReadTextPreview(Stream stream, Encoding encoding, int characterCount)
    {
        using StreamReader reader = new(stream, encoding, true, DetectionSampleSize, leaveOpen: true);
        char[] buffer = new char[characterCount];
        int read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }

    private static bool StreamContainsText(TextReader reader, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        char[] buffer = new char[SearchBufferSize];
        string carryOver = string.Empty;

        while (true)
        {
            int read = reader.ReadBlock(buffer, 0, buffer.Length);

            if (read == 0)
            {
                return carryOver.Contains(query, StringComparison.CurrentCultureIgnoreCase);
            }

            string chunk = new(buffer, 0, read);

            if (carryOver.Length > 0)
            {
                chunk = carryOver + chunk;
            }

            if (chunk.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (query.Length == 1)
            {
                carryOver = string.Empty;
                continue;
            }

            int carryLength = Math.Min(query.Length - 1, chunk.Length);
            carryOver = chunk.Substring(chunk.Length - carryLength, carryLength);
        }
    }

    private static bool IsLikelyBinary(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return false;
        }

        int suspiciousCount = 0;

        foreach (byte currentByte in bytes)
        {
            if (currentByte == 0)
            {
                return true;
            }

            if (currentByte < 0x08 || (currentByte > 0x0D && currentByte < 0x20))
            {
                suspiciousCount++;
            }
        }

        return suspiciousCount > Math.Max(1, bytes.Length / 12);
    }

    private static string BuildBinaryPreview(byte[] bytes, long totalSize)
    {
        StringBuilder builder = new();

        for (int offset = 0; offset < bytes.Length; offset += 16)
        {
            int lineLength = Math.Min(16, bytes.Length - offset);
            builder.Append(offset.ToString("X8"));
            builder.Append("  ");

            for (int index = 0; index < 16; index++)
            {
                if (index < lineLength)
                {
                    builder.Append(bytes[offset + index].ToString("X2"));
                }
                else
                {
                    builder.Append("  ");
                }

                if (index < 15)
                {
                    builder.Append(' ');
                }
            }

            builder.Append("  ");

            for (int index = 0; index < lineLength; index++)
            {
                byte currentByte = bytes[offset + index];
                char character = currentByte >= 32 && currentByte <= 126 ? (char)currentByte : '.';
                builder.Append(character);
            }

            if (offset + lineLength < bytes.Length)
            {
                builder.AppendLine();
            }
        }

        if (totalSize > bytes.Length)
        {
            builder.AppendLine();
            builder.Append("...");
        }

        return builder.ToString();
    }

    private static string BuildLargeTextPreview(string preview, long totalSize)
    {
        StringBuilder builder = new();
        builder.AppendLine("[Read-only preview]");
        builder.Append("The file is too large to open safely in the editor (");
        builder.Append(FormatFileSize(totalSize));
        builder.AppendLine(").");
        builder.AppendLine("Only the beginning of the file is shown, and saving from this view is disabled.");

        if (!string.IsNullOrEmpty(preview))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.Append(preview);
            builder.AppendLine();
            builder.Append("...");
        }

        return builder.ToString();
    }

    private static string FormatFileSize(long size)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = size;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
