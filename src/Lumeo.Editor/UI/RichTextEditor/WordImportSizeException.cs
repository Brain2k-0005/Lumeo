namespace Lumeo;

/// <summary>
/// Thrown when a Word document upload exceeds the configured size limit.
/// Lightweight, reusable across the JS-side guard and the .NET-side decode guard.
/// </summary>
public sealed class WordImportSizeException : Exception
{
    public long ActualBytes { get; }
    public long LimitBytes { get; }

    public WordImportSizeException(long actualBytes, long limitBytes)
        : base($"Word document is {actualBytes:N0} bytes which exceeds the {limitBytes:N0}-byte import limit.")
    {
        ActualBytes = actualBytes;
        LimitBytes = limitBytes;
    }
}
