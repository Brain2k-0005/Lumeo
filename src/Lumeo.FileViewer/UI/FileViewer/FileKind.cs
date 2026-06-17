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

    /// <summary>
    /// Office document (Word / Excel / PowerPoint, legacy and OOXML). There is
    /// no safe, dependency-free way to render these inline in the browser, so
    /// <see cref="FileViewer"/> shows a clear "preview unavailable" panel with a
    /// download (and optional open-in-online-viewer) action rather than a broken
    /// blank frame. Detecting it as its own kind — instead of <see cref="Unknown"/> —
    /// lets the fallback message name the format and offer the right CTA.
    /// </summary>
    Office,

    Unknown,
}
