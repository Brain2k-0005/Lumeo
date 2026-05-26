namespace Lumeo;

/// <summary>
/// Argument passed to a <see cref="FileViewer.CustomRenderers"/> fragment. The
/// fetched <see cref="Text"/> is only populated for text-based kinds
/// (Markdown, Code, JSON, CSV, Text); for binary kinds (Image, Video, Audio,
/// PDF) it is <c>null</c> and renderers should consume <see cref="Src"/>.
/// </summary>
public sealed record FileViewerRenderContext(
    string Src,
    FileKind Kind,
    string? Text,
    string? FileName);
