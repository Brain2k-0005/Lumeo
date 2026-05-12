namespace Lumeo;

/// <summary>
/// Represents a file or folder node in the FileManager tree.
/// </summary>
public class FileSystemNode
{
    /// <summary>Unique identifier for this node. Used as the navigation key for CurrentPath.</summary>
    public string Id { get; set; } = "";

    /// <summary>Display name (file or folder name).</summary>
    public string Name { get; set; } = "";

    /// <summary>True if this node is a folder; false if it is a file.</summary>
    public bool IsFolder { get; set; }

    /// <summary>File size in bytes. Null for folders.</summary>
    public long? Size { get; set; }

    /// <summary>Last-modified timestamp.</summary>
    public DateTime? Modified { get; set; }

    /// <summary>
    /// Full display path (optional). If not provided, the FileManager derives
    /// path breadcrumbs from the tree structure.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Child nodes. Non-null means children are already loaded.
    /// Null means lazy-loading is possible (if <c>LoadChildren</c> is set).
    /// An empty list means an empty folder.
    /// </summary>
    public List<FileSystemNode>? Children { get; set; }

    /// <summary>
    /// Optional Lucide icon name override. When set, overrides the
    /// automatic icon selection based on file extension.
    /// </summary>
    public string? IconName { get; set; }

    /// <summary>Arbitrary consumer payload attached to this node.</summary>
    public object? Tag { get; set; }
}
