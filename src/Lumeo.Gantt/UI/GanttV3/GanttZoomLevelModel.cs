namespace Lumeo.GanttV3;

/// <summary>
/// Pure, static "which zoom levels are toolbar/zoom-control-representable, in
/// what order" logic — shared by <see cref="Lumeo.GanttNav"/>'s toggle group
/// and <see cref="Lumeo.GanttZoomControl"/>'s +/- stepper (design spec Phase
/// 3, T7 — "Floating zoom control ... steps through the active ZoomLevels
/// list"). Extracted from <see cref="Lumeo.GanttNav"/>'s own former private
/// members (a pure "extract method" refactor — <see cref="Lumeo.GanttNav"/>'s
/// EXTERNALLY observable behavior is unchanged, verified against its own
/// existing test suite) so the toolbar and the floating control can never
/// silently diverge on "what counts as a representable mode" the way two
/// independently-hand-maintained copies of the same list eventually would
/// (e.g. a future 6th <see cref="GanttViewMode"/> added to one list and
/// forgotten in the other).
/// </summary>
internal static class GanttZoomLevelModel
{
    /// <summary>
    /// The toolbar/zoom-control's DEFAULT set (no <c>ZoomLevels</c> parameter
    /// supplied) — v2 parity (same v2's <c>Gantt.razor</c> ZoomLevels default
    /// contract). Deliberately excludes <see cref="GanttViewMode.Quarter"/>
    /// (design spec Phase 3, T2) — see <see cref="RepresentableLevels"/>'s own
    /// remarks for why.
    /// </summary>
    internal static readonly IReadOnlyList<GanttViewMode> DefaultLevels =
    [
        GanttViewMode.Day, GanttViewMode.Week, GanttViewMode.Month, GanttViewMode.Year,
    ];

    /// <summary>
    /// Every zoom level with its OWN toolbar-representable label — used to
    /// FILTER a caller-supplied <c>ZoomLevels</c> list (design spec Phase 3,
    /// T2 — adding <see cref="GanttViewMode.Quarter"/> as a 5th DEFAULT
    /// toolbar button would silently change every existing v3 chart's
    /// default look/visual-snapshot baselines with no explicit opt-in; a
    /// consumer wanting Quarter reachable passes it in their OWN
    /// <c>ZoomLevels</c> list instead, which this set (not
    /// <see cref="DefaultLevels"/>) now allows through).
    /// </summary>
    internal static readonly IReadOnlyList<GanttViewMode> RepresentableLevels =
    [
        GanttViewMode.Day, GanttViewMode.Week, GanttViewMode.Month, GanttViewMode.Quarter, GanttViewMode.Year,
    ];

    /// <summary>
    /// The zoom levels a caller-supplied <c>ZoomLevels</c> parameter actually
    /// resolves to: the representable subset of <paramref name="zoomLevels"/>
    /// (in the CALLER's own order) when non-empty, else <see cref="DefaultLevels"/>.
    /// </summary>
    internal static IReadOnlyList<GanttViewMode> Resolve(IReadOnlyList<GanttViewMode>? zoomLevels) =>
        zoomLevels is { Count: > 0 }
            ? zoomLevels.Where(l => RepresentableLevels.Contains(l)).ToList()
            : DefaultLevels;

    /// <summary>The localization key for a representable zoom level's own label.</summary>
    internal static string LabelKey(GanttViewMode level) => level switch
    {
        GanttViewMode.Day => "Gantt.Day",
        GanttViewMode.Week => "Gantt.Week",
        GanttViewMode.Month => "Gantt.Month",
        GanttViewMode.Quarter => "Gantt.Quarter",
        GanttViewMode.Year => "Gantt.Year",
        _ => "Gantt.Day",
    };
}
