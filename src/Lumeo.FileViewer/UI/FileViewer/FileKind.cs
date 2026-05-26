namespace Lumeo;

/// <summary>
/// Kind of content <see cref="FileViewer"/> renders. Drives which renderer is
/// invoked and which icon / aria-label is shown in the toolbar.
/// <c>Auto</c> is the default and triggers detection from MIME / extension /
/// HEAD content-type; everything else short-circuits detection.
/// </summary>
public enum FileKind
{
    Auto,
    Pdf,
    Image,
    Video,
    Audio,
    Markdown,
    Code,
    Json,
    Csv,
    Text,
    Unknown,
}
