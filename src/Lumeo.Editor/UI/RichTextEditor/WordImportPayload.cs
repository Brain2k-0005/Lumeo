namespace Lumeo;

/// <summary>
/// Picked .docx file payload handed to <see cref="RichTextEditor.OnWordImportRequested"/>.
/// </summary>
public sealed record WordImportPayload(string FileName, byte[] Content);
