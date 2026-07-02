using Microsoft.AspNetCore.Components;

namespace Lumeo.Services;

public sealed class OverlayService : IOverlayService
{
    /// <summary>Base z-index assigned to the first overlay. Subsequent overlays
    /// stack at <c>BaseZIndex + n * Step</c>. The backdrop sits at the assigned
    /// value; the content sits one tier above (Z + 1) so a nested overlay's
    /// backdrop always lands above the parent's content.</summary>
    public const int BaseZIndex = 50;

    /// <summary>Distance between consecutive overlay tiers. <c>Step</c> = 10
    /// leaves room for backdrop (Z) + content (Z+1) without colliding with
    /// the next overlay's backdrop.</summary>
    public const int Step = 10;

    // Active overlay id → assigned backdrop tier. Replaces the old monotonic
    // counter, which leaked: closing an overlay just decremented the count, so a
    // newly opened overlay could be handed a tier a still-open overlay already
    // owned (out-of-order close → z-index collision / stacking bug, #228).
    private readonly Dictionary<string, int> _activeTiers = new();

    public event Action<OverlayInstance>? OnShow;
    public event Action<string, object?, bool>? OnClose;

    public Task<OverlayResult> ShowDialogAsync<TComponent>(
        string? title = null,
        OverlayParameters? parameters = null,
        OverlayOptions? options = null) where TComponent : IComponent
    {
        return ShowAsync(OverlayType.Dialog, typeof(TComponent), title, parameters, options);
    }

    public Task<OverlayResult> ShowSheetAsync<TComponent>(
        string? title = null,
        Lumeo.Side side = Lumeo.Side.Right,
        SheetSize size = SheetSize.Default,
        OverlayParameters? parameters = null,
        OverlayOptions? options = null) where TComponent : IComponent
    {
        options ??= new OverlayOptions();
        // Precedence: the legacy `size` parameter wins only when explicitly set
        // to a non-default value; otherwise defer to the unified options.Size so
        // ShowSheetAsync(options: new() { Size = OverlaySize.Xl }) takes effect
        // instead of being overwritten back to Default by the omitted parameter.
        var effectiveSize = size != SheetSize.Default ? OverlaySizeConvert.FromSheet(size) : options.Size;
        options = options with { SheetSide = side, Size = effectiveSize };
        return ShowAsync(OverlayType.Sheet, typeof(TComponent), title, parameters, options);
    }

    public Task<OverlayResult> ShowDrawerAsync<TComponent>(
        string? title = null,
        OverlayParameters? parameters = null,
        OverlayOptions? options = null) where TComponent : IComponent
    {
        return ShowAsync(OverlayType.Drawer, typeof(TComponent), title, parameters, options);
    }

    public Task<OverlayResult> ShowAlertDialogAsync(AlertDialogOptions alertOptions)
    {
        var tcs = new TaskCompletionSource<OverlayResult>();
        var id = Guid.NewGuid().ToString("N");
        var instance = new OverlayInstance
        {
            Id = id,
            Type = OverlayType.AlertDialog,
            Title = alertOptions.Title,
            AlertOptions = alertOptions,
            ZIndex = AllocateZIndex(id),
            Tcs = tcs
        };
        OnShow?.Invoke(instance);
        return tcs.Task;
    }

    public void Close(string overlayId, object? result = null)
    {
        ReleaseZIndex(overlayId);
        OnClose?.Invoke(overlayId, result, false);
    }

    public void Cancel(string overlayId)
    {
        ReleaseZIndex(overlayId);
        OnClose?.Invoke(overlayId, null, true);
    }

    private int AllocateZIndex(string overlayId)
    {
        // A newly opened overlay must stack ABOVE every currently-open overlay,
        // so allocate one tier above the current maximum (or the base tier when
        // none are open). Tracking per-id (not a bare counter) makes the release
        // exact: closing one overlay frees only its tier and, once they're all
        // closed, the max resets to the base — no unbounded drift, no collision
        // when overlays close out of order (#228).
        var tier = (_activeTiers.Count == 0 ? BaseZIndex : _activeTiers.Values.Max()) + Step;
        _activeTiers[overlayId] = tier;
        return tier;
    }

    private void ReleaseZIndex(string overlayId)
    {
        _activeTiers.Remove(overlayId);
    }

    private Task<OverlayResult> ShowAsync(
        OverlayType type,
        Type componentType,
        string? title,
        OverlayParameters? parameters,
        OverlayOptions? options)
    {
        var tcs = new TaskCompletionSource<OverlayResult>();
        var id = Guid.NewGuid().ToString("N");
        var instance = new OverlayInstance
        {
            Id = id,
            Type = type,
            ComponentType = componentType,
            Title = title,
            Parameters = parameters,
            Options = options ?? new OverlayOptions(),
            ZIndex = AllocateZIndex(id),
            Tcs = tcs
        };
        OnShow?.Invoke(instance);
        return tcs.Task;
    }
}

public enum OverlayType { Dialog, Sheet, Drawer, AlertDialog }

/// <summary>Unified overlay content size as exposed through
/// <see cref="OverlayService"/>. Drives both <c>DialogContent.DialogSize</c>
/// and <c>SheetContent.SheetSize</c> from a single <see cref="OverlayOptions.Size"/>
/// value, so the same options record can size a Dialog or a Sheet. Kept in the
/// service layer (not the UI namespace) so consumers don't take a hard UI
/// dependency. <c>Full</c> is layout-intent (entire viewport).</summary>
public enum OverlaySize { Sm, Default, Lg, Xl, Full }

/// <summary>Sheet size as exposed through <see cref="OverlayService"/>. Mirrors
/// <c>SheetContent.SheetSize</c> — kept distinct so the service layer doesn't
/// take a hard dependency on the UI namespace. <c>Full</c> is layout-intent
/// (entire viewport) and intentionally NOT folded into <see cref="Lumeo.Size"/>.
/// <para>Retained as the parameter type of
/// <see cref="OverlayService.ShowSheetAsync{T}"/> and as the backing type of the
/// obsolete <see cref="OverlayOptions.SheetSize"/> alias; new code should prefer
/// <see cref="OverlaySize"/>.</para></summary>
public enum SheetSize { Sm, Default, Lg, Xl, Full }

/// <summary>1:1 conversions between the legacy <see cref="SheetSize"/> and the
/// unified <see cref="OverlaySize"/>. The member sets are identical, so the maps
/// are total and lossless — they exist only to bridge the obsolete
/// <see cref="OverlayOptions.SheetSize"/> alias and the
/// <see cref="OverlayService.ShowSheetAsync{T}"/> parameter onto the canonical
/// <see cref="OverlayOptions.Size"/>.</summary>
internal static class OverlaySizeConvert
{
    public static OverlaySize FromSheet(SheetSize s) => s switch
    {
        SheetSize.Sm => OverlaySize.Sm,
        SheetSize.Lg => OverlaySize.Lg,
        SheetSize.Xl => OverlaySize.Xl,
        SheetSize.Full => OverlaySize.Full,
        _ => OverlaySize.Default,
    };

    public static SheetSize ToSheet(OverlaySize s) => s switch
    {
        OverlaySize.Sm => SheetSize.Sm,
        OverlaySize.Lg => SheetSize.Lg,
        OverlaySize.Xl => SheetSize.Xl,
        OverlaySize.Full => SheetSize.Full,
        _ => SheetSize.Default,
    };
}

public sealed record OverlayResult
{
    public bool Cancelled { get; init; }
    public object? Data { get; init; }

    public T? GetData<T>() => Data is T typed ? typed : default;

    public static OverlayResult Ok(object? data = null) => new() { Data = data };
    public static OverlayResult CancelResult() => new() { Cancelled = true };
}

public sealed class OverlayParameters
{
    private readonly Dictionary<string, object> _parameters = new();

    public OverlayParameters Add(string name, object value)
    {
        _parameters[name] = value;
        return this;
    }

    public T? Get<T>(string name) =>
        _parameters.TryGetValue(name, out var value) && value is T typed ? typed : default;

    public Dictionary<string, object> ToDictionary() => new(_parameters);
}

// CS0618 is disabled across this record because the synthesized record members
// (Equals / GetHashCode / PrintMembers) enumerate every public property,
// including the obsolete SheetSize / MobileSheetSize aliases below — that
// generated reference would otherwise trip -warnaserror. The aliases stay fully
// usable by consumers; only the in-record generated access is suppressed.
#pragma warning disable CS0618
public record OverlayOptions
{
    /// <summary>Extra CSS classes merged onto the overlay's content element.
    /// <b>Applies to:</b> Dialog, Sheet, Drawer.</summary>
    public string? Class { get; init; }

    /// <summary>Suppress dismissal via backdrop click / Escape.
    /// <b>Applies to:</b> Dialog, Sheet, Drawer.</summary>
    public bool PreventClose { get; init; }

    /// <summary>Whether the shell renders its top-end close (X) button.
    /// <c>null</c> (default) keeps the legacy coupling — the X shows whenever
    /// <see cref="PreventClose"/> is <c>false</c>. Set <c>true</c> to force the X
    /// even on a modal overlay (<c>PreventClose=true</c>: backdrop/Escape stay
    /// disabled, but the X still offers an explicit way out — it routes through
    /// the same dismiss guard, so an <c>OnBeforeClose</c> veto still applies), or
    /// <c>false</c> to hide it and own the close chrome yourself.
    /// <b>Applies to:</b> Dialog, Sheet. (Drawer has no X by design — drag handle.)</summary>
    public bool? ShowCloseButton { get; init; }

    /// <summary>Edge the sheet slides in from. <b>Applies to:</b> Sheet.</summary>
    public Lumeo.Side SheetSide { get; init; } = Lumeo.Side.Right;

    /// <summary>Overlay content size. <b>Applies to:</b> Dialog, Sheet.
    /// (Drawer sizes to its content and ignores this.) For a Dialog it maps to
    /// the <c>max-w-*</c> preset (<c>Sm</c>→<c>max-w-sm</c> … <c>Xl</c>→<c>max-w-4xl</c>,
    /// <c>Full</c>→near-viewport); for a Sheet it maps to the side-appropriate
    /// width (Left/Right) or height (Top/Bottom). Replaces the Sheet-only
    /// <see cref="SheetSize"/> as the single size knob for service overlays.</summary>
    public OverlaySize Size { get; init; } = OverlaySize.Default;

    /// <summary>Legacy Sheet-only size alias. <b>Applies to:</b> Sheet.
    /// Reads/writes <see cref="Size"/> via a 1:1 mapping; setting either sets the
    /// same backing value. Kept for source compatibility with pre-3.6 callers.</summary>
    [Obsolete("Use Size instead — the unified size property now drives both Dialog and Sheet. SheetSize remains as a 1:1 mapping alias onto Size and may be removed in a future major version.")]
    public SheetSize SheetSize
    {
        get => OverlaySizeConvert.ToSheet(Size);
        init => Size = OverlaySizeConvert.FromSheet(value);
    }

    /// <summary>
    /// When true, a Sheet opened via <see cref="OverlayService.ShowSheetAsync{T}"/>
    /// can be dismissed by swiping in the direction opposite to its
    /// <see cref="SheetSide"/> (e.g. swipe-down on a Bottom sheet). Ignored for
    /// non-Sheet overlay types. Drawers already enable swipe by default.
    /// Default false to preserve the existing programmatic-open behaviour.
    /// <b>Applies to:</b> Sheet.
    /// </summary>
    public bool SwipeToClose { get; init; }

    // --- Responsive mobile overrides (2.1.3) -------------------------------
    // When the viewport width is below MobileBreakpoint, the Mobile* fields
    // take precedence over the desktop SheetSide/SheetSize/SwipeToClose. The
    // OverlayProvider reads these via IResponsiveService and re-renders the
    // SheetContent reactively when the viewport crosses the breakpoint
    // (e.g. user rotates a tablet). All four fields are nullable — null means
    // "no responsive switch, just use the desktop value at every size".

    /// <summary>Viewport width threshold (CSS pixels) below which the
    /// <c>Mobile*</c> overrides apply. Default 768 (Tailwind <c>md</c>).
    /// Set to null to disable responsive switching even if the
    /// <c>Mobile*</c> fields are populated.
    /// <b>Applies to:</b> Dialog, Sheet, Drawer (gates every <c>Mobile*</c> override).</summary>
    public int? MobileBreakpoint { get; init; } = 768;

    /// <summary>Sheet side to use when the viewport is below
    /// <see cref="MobileBreakpoint"/>. Null = use <see cref="SheetSide"/> at all
    /// sizes. Typical pattern: <c>Lumeo.Side.Right</c> on desktop,
    /// <c>Lumeo.Side.Bottom</c> on mobile.
    /// <b>Applies to:</b> Sheet.</summary>
    public Lumeo.Side? MobileSheetSide { get; init; }

    /// <summary>Overlay size to use when the viewport is below
    /// <see cref="MobileBreakpoint"/>. Null = use <see cref="Size"/> at all
    /// sizes. Typical pattern: <c>OverlaySize.Default</c> on desktop,
    /// <c>OverlaySize.Full</c> on mobile.
    /// <b>Applies to:</b> Dialog, Sheet.</summary>
    public OverlaySize? MobileSize { get; init; }

    /// <summary>Legacy Sheet-only alias for <see cref="MobileSize"/>.
    /// <b>Applies to:</b> Sheet. Reads/writes <see cref="MobileSize"/> via a 1:1
    /// mapping (null round-trips to null). Kept for source compatibility with
    /// pre-3.6 callers.</summary>
    [Obsolete("Use MobileSize instead — it drives both Dialog and Sheet below MobileBreakpoint. MobileSheetSize remains as a 1:1 mapping alias onto MobileSize and may be removed in a future major version.")]
    public SheetSize? MobileSheetSize
    {
        get => MobileSize is { } s ? OverlaySizeConvert.ToSheet(s) : null;
        init => MobileSize = value is { } v ? OverlaySizeConvert.FromSheet(v) : null;
    }

    /// <summary>SwipeToClose override below <see cref="MobileBreakpoint"/>.
    /// Null = use <see cref="SwipeToClose"/> at all sizes. Typical pattern:
    /// off on desktop, on for mobile bottom-sheet pull-down.
    /// <b>Applies to:</b> Sheet.</summary>
    public bool? MobileSwipeToClose { get; init; }

    /// <summary>When true and the viewport width is below
    /// <see cref="MobileBreakpoint"/>, the overlay is forced full-screen
    /// (full width + full height, no corner radius). Works across Dialog,
    /// Sheet and Drawer overlays — replaces the consumer-side
    /// <c>max-md:!h-full max-md:!w-full max-md:!max-w-full</c> Tailwind
    /// override chain that was previously the only way to get a
    /// full-screen Dialog on mobile. For Sheets, this is equivalent to
    /// setting <see cref="MobileSize"/> to <see cref="OverlaySize.Full"/>
    /// but composes more naturally for the Sheet+Dialog mixed case.
    /// <b>Applies to:</b> Dialog, Sheet, Drawer.</summary>
    public bool MobileFullscreen { get; init; }

    /// <summary>
    /// Wrap the rendered component in a scrollable body region inside the
    /// overlay's chrome (Sheet only — Dialog and Drawer manage their own
    /// scroll). Default <c>true</c> so consumers can pass a long form to
    /// <see cref="OverlayService.ShowSheetAsync{T}"/> without rolling their
    /// own <c>overflow-y-auto</c> wrapper — and without hitting the focus-
    /// ring clip that bare <c>overflow-y: auto</c> introduces (per spec,
    /// <c>overflow-y: auto</c> promotes <c>overflow-x</c> to <c>auto</c> too,
    /// which clips inputs' box-shadow focus rings at the left/right edge).
    /// The built-in wrapper uses the <c>px-1 -mx-1</c> trick to give focus
    /// rings 4px breathing room without shifting the visual padding. Set to
    /// <c>false</c> for sheets whose content sets its own scrolling strategy
    /// (e.g. an embedded <c>PdfViewer</c> that already paints inside a fixed
    /// canvas).
    /// <b>Applies to:</b> Sheet.
    /// </summary>
    public bool ScrollableBody { get; init; } = true;
}
#pragma warning restore CS0618

/// <summary>
/// Sheet-shaped overlay options. <b>Semantic</b> marker — at the call site
/// you get clearer intent ("this configures a Sheet") and IDE navigation,
/// but the existing <see cref="OverlayService.ShowDialogAsync{T}"/> /
/// <see cref="OverlayService.ShowDrawerAsync{T}"/> still accept any
/// <see cref="OverlayOptions"/> via standard subtype polymorphism, so
/// nothing prevents passing this record to the wrong service overload —
/// the unused sheet-only properties stay silent no-ops there.
/// Compile-time enforcement requires narrowing the service-method
/// signatures to the typed record (planned for 4.0 as a breaking change).
/// </summary>
public sealed record SheetOverlayOptions : OverlayOptions { }

/// <summary>Dialog-shaped overlay options. Semantic marker (same caveat as
/// <see cref="SheetOverlayOptions"/>) — typed for documentation and IDE
/// auto-complete, not enforced at the service overload boundary.</summary>
public sealed record DialogOverlayOptions : OverlayOptions { }

/// <summary>Drawer-shaped overlay options. Drawer enables swipe-to-close by
/// default; the inherited <see cref="OverlayOptions.SwipeToClose"/> is
/// ignored for drawers. Semantic marker (see <see cref="SheetOverlayOptions"/>
/// for the enforcement caveat).</summary>
public sealed record DrawerOverlayOptions : OverlayOptions { }

public sealed record AlertDialogOptions
{
    public string Title { get; init; } = "Are you sure?";
    public string? Description { get; init; }
    public string ConfirmText { get; init; } = "Continue";
    public string CancelText { get; init; } = "Cancel";
    public bool IsDestructive { get; init; }
}

public sealed class OverlayInstance
{
    public required string Id { get; init; }
    public required OverlayType Type { get; init; }
    public Type? ComponentType { get; init; }
    public string? Title { get; init; }
    public OverlayParameters? Parameters { get; init; }
    public OverlayOptions Options { get; init; } = new();
    public AlertDialogOptions? AlertOptions { get; init; }
    /// <summary>Backdrop z-index for this overlay. Content sits at
    /// <c>ZIndex + 1</c> so a nested overlay's backdrop (allocated later
    /// with a strictly larger value) lands above the parent's content.
    /// Assigned by <see cref="OverlayService"/> at registration time.</summary>
    public int ZIndex { get; init; } = OverlayService.BaseZIndex;
    internal TaskCompletionSource<OverlayResult> Tcs { get; init; } = default!;
}
