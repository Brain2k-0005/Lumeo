using System.ComponentModel;

namespace Lumeo;

/// <summary>Whether a <see cref="FlowHandle"/> starts connections (<see cref="Source"/>) or accepts them (<see cref="Target"/>).</summary>
public enum FlowHandleType
{
    /// <summary>An outgoing port — edges start here.</summary>
    Source,
    /// <summary>An incoming port — edges end here.</summary>
    Target,
}

/// <summary>
/// The PHYSICAL side of a node a handle sits on, and the direction an edge leaves/enters it.
/// Physical, not logical: <see cref="Left"/> stays left under <c>dir="rtl"</c> (the canvas is a
/// geometric surface, not a text flow).
/// </summary>
public enum FlowPosition
{
    /// <summary>The node's left edge.</summary>
    Left,
    /// <summary>The node's right edge.</summary>
    Right,
    /// <summary>The node's top edge.</summary>
    Top,
    /// <summary>The node's bottom edge.</summary>
    Bottom,
}

/// <summary>How an edge's path is routed between its two anchors.</summary>
public enum FlowEdgeType
{
    /// <summary>A cubic Bézier that leaves/enters each anchor along its handle's direction.</summary>
    Bezier,
    /// <summary>An orthogonal route with rounded corners.</summary>
    SmoothStep,
    /// <summary>An orthogonal route with sharp corners.</summary>
    Step,
    /// <summary>A straight line.</summary>
    Straight,
}

/// <summary>The pattern <see cref="FlowBackground"/> draws.</summary>
public enum FlowBackgroundVariant
{
    /// <summary>A dot at every grid intersection.</summary>
    Dots,
    /// <summary>Continuous grid lines.</summary>
    Lines,
    /// <summary>A small cross at every grid intersection.</summary>
    Cross,
}

/// <summary>
/// One node on a <see cref="FlowCanvas"/>. Immutable — update with <c>with</c>, the canvas emits a
/// new list through <c>NodesChanged</c> on every committed move.
/// </summary>
/// <param name="Id">Stable identifier; edges reference nodes by it.</param>
/// <param name="X">Left edge in flow coordinates.</param>
/// <param name="Y">Top edge in flow coordinates.</param>
/// <param name="Type">Your own discriminator for <c>NodeTemplate</c> (e.g. "trigger", "action").</param>
/// <param name="Data">Arbitrary payload for the template.</param>
/// <param name="Width">Fixed width in flow units; <c>null</c> = sized by its content and measured.</param>
/// <param name="Height">Fixed height in flow units; <c>null</c> = sized by its content and measured.</param>
/// <param name="Draggable">Per-node drag switch (ANDed with the canvas' own switches).</param>
/// <param name="Selectable">Per-node selection switch.</param>
/// <param name="Connectable">Per-node connect switch (phase 2).</param>
/// <param name="Deletable">Per-node delete switch (phase 2).</param>
/// <param name="ZIndex">Explicit stacking order; <c>null</c> = document order.</param>
public sealed record FlowNode(
    string Id, double X, double Y,
    string? Type = null,
    object? Data = null,
    double? Width = null, double? Height = null,
    bool Draggable = true, bool Selectable = true, bool Connectable = true,
    bool Deletable = true, int? ZIndex = null);

/// <summary>An edge between two nodes, drawn as one SVG path.</summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="Source">The source node's <see cref="FlowNode.Id"/>.</param>
/// <param name="Target">The target node's <see cref="FlowNode.Id"/>.</param>
/// <param name="SourceHandle">The source <see cref="FlowHandle"/>'s <c>Id</c>; <c>null</c> = the node's first source handle, or its right edge.</param>
/// <param name="TargetHandle">The target <see cref="FlowHandle"/>'s <c>Id</c>; <c>null</c> = the node's first target handle, or its left edge.</param>
/// <param name="Label">Optional label (rendered from phase 2).</param>
/// <param name="Type">Path routing.</param>
/// <param name="Animated">Dash animation (phase 2).</param>
/// <param name="Dashed">Dashed stroke (phase 2).</param>
/// <param name="Deletable">Per-edge delete switch (phase 2).</param>
/// <param name="MarkerEnd">End marker id (phase 2).</param>
/// <param name="Data">Arbitrary payload.</param>
public sealed record FlowEdge(
    string Id, string Source, string Target,
    string? SourceHandle = null, string? TargetHandle = null,
    string? Label = null, FlowEdgeType Type = FlowEdgeType.Bezier,
    bool Animated = false, bool Dashed = false, bool Deletable = true,
    string? MarkerEnd = "arrow", object? Data = null);

/// <summary>
/// The pan/zoom state: a flow point <c>p</c> is drawn at pane-local <c>p * Zoom + (X, Y)</c>.
/// </summary>
/// <param name="X">Horizontal translation in pane pixels.</param>
/// <param name="Y">Vertical translation in pane pixels.</param>
/// <param name="Zoom">Scale factor (1 = 100%).</param>
public readonly record struct FlowViewport(double X, double Y, double Zoom);

/// <summary>A point, in whichever space the member that returns it documents.</summary>
public readonly record struct FlowPoint(double X, double Y);

/// <summary>An axis-aligned rectangle in flow coordinates.</summary>
public readonly record struct FlowRect(double X, double Y, double Width, double Height)
{
    /// <summary>The right edge.</summary>
    public double Right => X + Width;
    /// <summary>The bottom edge.</summary>
    public double Bottom => Y + Height;
}

/// <summary>A generated edge path plus the point its label is centred on.</summary>
/// <param name="D">The SVG path data (culture-invariant, 3 decimals).</param>
/// <param name="LabelX">Label anchor X in flow coordinates.</param>
/// <param name="LabelY">Label anchor Y in flow coordinates.</param>
public readonly record struct FlowEdgePath(string D, double LabelX, double LabelY);

/// <summary>A new connection proposed by dragging between two handles (phase 2).</summary>
public sealed record FlowConnection(string Source, string? SourceHandle, string Target, string? TargetHandle);

/// <summary>One node's committed position after a drag or keyboard move.</summary>
/// <param name="Id">The node's id.</param>
/// <param name="X">New left edge in flow coordinates.</param>
/// <param name="Y">New top edge in flow coordinates.</param>
public sealed record FlowNodeChange(string Id, double X, double Y);

/// <summary>The selected node and edge ids.</summary>
public sealed record FlowSelection(IReadOnlySet<string> NodeIds, IReadOnlySet<string> EdgeIds);

/// <summary>What <c>NodeTemplate</c> receives for each node.</summary>
/// <param name="Node">The node being rendered.</param>
/// <param name="Selected">The node is selected.</param>
/// <param name="Dragging">A pointer drag is moving the node right now.</param>
/// <param name="Canvas">The owning canvas (for its imperative methods).</param>
public sealed record FlowNodeContext(FlowNode Node, bool Selected, bool Dragging, FlowCanvas Canvas);

/// <summary>What an edge label template receives (phase 2).</summary>
public sealed record FlowEdgeContext(FlowEdge Edge, bool Selected, double LabelX, double LabelY);

/// <summary>Arguments for <c>FlowCanvas.OnNodeContextMenu</c>.</summary>
/// <param name="Node">The node that was right-clicked.</param>
/// <param name="ClientX">Pointer X in viewport (client) pixels — ready for positioning a menu.</param>
/// <param name="ClientY">Pointer Y in viewport (client) pixels.</param>
public sealed record FlowNodeContextMenuEventArgs(FlowNode Node, double ClientX, double ClientY);

/// <summary>
/// Interop payload: one node's measured size and its handles, reported by the engine.
/// Not meant for application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record FlowNodeMeasurement(string Id, double Width, double Height, FlowHandleMeasurement[]? Handles);

/// <summary>
/// Interop payload: a handle's centre relative to its node's top-left corner, in flow units.
/// <paramref name="Type"/> is "source"/"target", <paramref name="Position"/> "left"/"right"/"top"/"bottom".
/// Not meant for application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record FlowHandleMeasurement(string? Id, string Type, string Position, double X, double Y);
