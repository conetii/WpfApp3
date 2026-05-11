namespace WpfApp3.Models;

public sealed class StorageOperationException : Exception
{
    public StorageOperationException(string errorCode, string? message = null)
        : base(message ?? errorCode)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}