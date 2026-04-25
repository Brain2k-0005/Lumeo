namespace Lumeo;

/// <summary>
/// Metadata about an image the user dropped, pasted, or picked through the
/// editor's image button. Returned to consumers via
/// <see cref="RichTextEditor.OnImageRequested"/>; consumer is responsible for
/// uploading the actual bytes (typically via a paired <c>InputFile</c>) and
/// returning a final URL.
/// </summary>
public sealed record EditorImageRequest(string FileName, string MimeType, long Size);
