using System.IO;
using WpfApp3.Models;

namespace WpfApp3.Services;

public sealed class StorageService
{
    private readonly DocumentFileService _documentFileService = new();

    public IReadOnlyList<StorageNode> LoadTree(string rootPath, string searchText)
    {
        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        string query = searchText.Trim();
        List<StorageNode> nodes = [];

        foreach (string directoryPath in Directory.GetDirectories(rootPath).OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
        {
            StorageNode? directoryNode = BuildDirectoryNode(directoryPath, query);

            if (directoryNode is not null)
            {
                nodes.Add(directoryNode);
            }
        }

        foreach (string filePath in Directory.GetFiles(rootPath).OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
        {
            if (MatchesFile(filePath, query))
            {
                nodes.Add(new StorageNode(Path.GetFileName(filePath), filePath, false));
            }
        }

        return nodes;
    }

    public string SuggestNewFilePath(string rootPath, string? selectedPath, string baseFileName)
    {
        string targetDirectory = GetTargetDirectory(rootPath, selectedPath);
        string candidateName = NormalizeFileName(baseFileName, ".txt");
        string candidatePath = Path.Combine(targetDirectory, candidateName);
        int index = 1;
        string baseName = Path.GetFileNameWithoutExtension(candidateName);
        string extension = Path.GetExtension(candidateName);

        while (File.Exists(candidatePath))
        {
            candidatePath = Path.Combine(targetDirectory, $"{baseName} {index}{extension}");
            index++;
        }

        return candidatePath;
    }

    public string BuildFilePath(string rootPath, string? selectedPath, string fileName)
    {
        string targetDirectory = GetTargetDirectory(rootPath, selectedPath);
        return Path.Combine(targetDirectory, NormalizeFileName(fileName, ".txt"));
    }

    public string BuildRenamedFilePath(string currentFilePath, string fileName)
    {
        string? directory = Path.GetDirectoryName(currentFilePath);
        string targetDirectory = string.IsNullOrWhiteSpace(directory) ? string.Empty : directory;
        return Path.Combine(targetDirectory, NormalizeFileName(fileName, Path.GetExtension(currentFilePath)));
    }

    public bool StorageExists(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
    }

    private StorageNode? BuildDirectoryNode(string directoryPath, string query)
    {
        List<StorageNode> childNodes = [];

        foreach (string childDirectoryPath in Directory.GetDirectories(directoryPath).OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
        {
            StorageNode? childDirectoryNode = BuildDirectoryNode(childDirectoryPath, query);

            if (childDirectoryNode is not null)
            {
                childNodes.Add(childDirectoryNode);
            }
        }

        foreach (string filePath in Directory.GetFiles(directoryPath).OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
        {
            if (MatchesFile(filePath, query))
            {
                childNodes.Add(new StorageNode(Path.GetFileName(filePath), filePath, false));
            }
        }

        bool matchesDirectoryName = string.IsNullOrWhiteSpace(query)
            || Path.GetFileName(directoryPath).Contains(query, StringComparison.CurrentCultureIgnoreCase);

        if (!matchesDirectoryName && childNodes.Count == 0)
        {
            return null;
        }

        return new StorageNode(Path.GetFileName(directoryPath), directoryPath, true, childNodes);
    }

    private bool MatchesFile(string filePath, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        if (Path.GetFileName(filePath).Contains(query, StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        return _documentFileService.FileContainsText(filePath, query);
    }

    private static string GetTargetDirectory(string rootPath, string? selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return rootPath;
        }

        if (Directory.Exists(selectedPath))
        {
            return selectedPath;
        }

        string? directory = Path.GetDirectoryName(selectedPath);
        return string.IsNullOrWhiteSpace(directory) ? rootPath : directory;
    }

    private static string NormalizeFileName(string fileName, string defaultExtension)
    {
        string sanitized = SanitizeFileName(Path.GetFileName(fileName.Trim()));

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Untitled";
        }

        if (!Path.HasExtension(sanitized) && !string.IsNullOrWhiteSpace(defaultExtension))
        {
            sanitized += defaultExtension.StartsWith('.') ? defaultExtension : "." + defaultExtension;
        }

        return sanitized;
    }

    private static string SanitizeFileName(string fileName)
    {
        string sanitized = fileName;

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        return sanitized;
    }
}
