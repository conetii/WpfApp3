using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfApp3.Models;

namespace WpfApp3.Services;

public sealed class EncryptedStorageCliService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _scriptPath = Path.Combine(AppContext.BaseDirectory, "Python", "storage_cli.py");

    public void CreateArchive(string archivePath, string password)
    {
        Invoke("create", archivePath, null, null, password);
    }

    public void UnlockArchive(string archivePath, string outputPath, string password)
    {
        Invoke("unlock", archivePath, null, outputPath, password);
    }

    public void PackArchive(string archivePath, string inputPath, string password)
    {
        Invoke("pack", archivePath, inputPath, null, password);
    }

    public void VerifyArchive(string archivePath, string password)
    {
        Invoke("verify", archivePath, null, null, password);
    }

    private void Invoke(string command, string archivePath, string? inputPath, string? outputPath, string password)
    {
        EncryptedStorageDebugLog.Write(
            "Cli",
            $"Invoke start; command={command}; archive={archivePath}; input={inputPath ?? "<null>"}; output={outputPath ?? "<null>"}; script={_scriptPath}; scriptExists={File.Exists(_scriptPath)}; baseDir={AppContext.BaseDirectory}");

        if (!File.Exists(_scriptPath))
        {
            EncryptedStorageDebugLog.Write("Cli", "Invoke failed before start; reason=cli_missing");
            throw new StorageOperationException("cli_missing");
        }

        using Process process = new();

        try
        {
            process.StartInfo = BuildStartInfo("py", true, command, archivePath, inputPath, outputPath);
            process.Start();
            EncryptedStorageDebugLog.Write("Cli", "Process started with py -3");
        }
        catch (Win32Exception firstException)
        {
            EncryptedStorageDebugLog.Write("Cli", $"py launcher start failed; message={EncryptedStorageDebugLog.Normalize(firstException.Message)}");

            try
            {
                process.StartInfo = BuildStartInfo("python", false, command, archivePath, inputPath, outputPath);
                process.Start();
                EncryptedStorageDebugLog.Write("Cli", "Process started with python");
            }
            catch (Win32Exception exception)
            {
                EncryptedStorageDebugLog.Write("Cli", $"python fallback start failed; message={EncryptedStorageDebugLog.Normalize(exception.Message)}");
                throw new StorageOperationException("python_not_found", exception.Message);
            }
        }

        process.StandardInput.WriteLine(password);
        process.StandardInput.Close();

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(120000))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
            }

            EncryptedStorageDebugLog.Write("Cli", $"Process timeout; command={command}");
            throw new StorageOperationException("cli_timeout");
        }

        EncryptedStorageDebugLog.Write(
            "Cli",
            $"Process exit; command={command}; exitCode={process.ExitCode}; stdout={EncryptedStorageDebugLog.Normalize(output)}; stderr={EncryptedStorageDebugLog.Normalize(error)}");

        CliResponse? response = null;

        if (!string.IsNullOrWhiteSpace(output))
        {
            try
            {
                response = JsonSerializer.Deserialize<CliResponse>(output, JsonOptions);
                EncryptedStorageDebugLog.Write(
                    "Cli",
                    $"Response parsed; command={command}; ok={response?.Ok.ToString() ?? "<null>"}; errorCode={response?.ErrorCode ?? "<null>"}; message={EncryptedStorageDebugLog.Normalize(response?.Message)}");
            }
            catch (Exception exception)
            {
                EncryptedStorageDebugLog.Write("Cli", $"JSON parse failed; message={EncryptedStorageDebugLog.Normalize(exception.Message)}");
            }
        }

        if (process.ExitCode == 0 && response?.Ok == true)
        {
            EncryptedStorageDebugLog.Write("Cli", $"Invoke success; command={command}");
            return;
        }

        string errorCode = response?.ErrorCode
            ?? (!string.IsNullOrWhiteSpace(error) ? "cli_failed" : "unknown_cli_error");

        string message = response?.Message ?? error;
        EncryptedStorageDebugLog.Write("Cli", $"Invoke failed; command={command}; errorCode={errorCode}; message={EncryptedStorageDebugLog.Normalize(message)}");
        throw new StorageOperationException(errorCode, message);
    }

    private ProcessStartInfo BuildStartInfo(string executableName, bool usePyLauncher, string command, string archivePath, string? inputPath, string? outputPath)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executableName,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };

        if (usePyLauncher)
        {
            startInfo.ArgumentList.Add("-3");
        }

        startInfo.ArgumentList.Add(_scriptPath);
        startInfo.ArgumentList.Add(command);
        startInfo.ArgumentList.Add("--archive");
        startInfo.ArgumentList.Add(archivePath);

        if (!string.IsNullOrWhiteSpace(inputPath))
        {
            startInfo.ArgumentList.Add("--input");
            startInfo.ArgumentList.Add(inputPath);
        }

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(outputPath);
        }

        return startInfo;
    }

    private sealed class CliResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}