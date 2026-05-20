using Microsoft.AspNetCore.Components;

namespace Lumeo.Services;

public sealed class OverlayService : IOverlayService
{
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
        SheetSide side = SheetSide.Right,
        SheetSize size = SheetSize.Default,
        OverlayParameters? parameters = null,
        OverlayOptions? options = null) where TComponent : IComponent
    {
        options ??= new OverlayOptions();
        options = options with { SheetSide = side, SheetSize = size };
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
            Tcs = tcs
        };
        OnShow?.Invoke(instance);
        return tcs.Task;
    }

    public void Close(string overlayId, object? result = null)
    {
        OnClose?.Invoke(overlayId, result, false);
    }

    public void Cancel(string overlayId)
    {
        OnClose?.Invoke(overlayId, null, true);
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
            Tcs = tcs
        };
        OnShow?.Invoke(instance);
        return tcs.Task;
    }
}

public enum OverlayType { Dialog, Sheet, Drawer, AlertDialog }

public enum SheetSide { Top, Right, Bottom, Left }

public enum SheetSize { Sm, Default, Lg, Xl, Full }

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

public sealed record OverlayOptions
{
    public string? Class { get; init; }
    public bool PreventClose { get; init; }
    public SheetSide SheetSide { get; init; } = SheetSide.Right;
    public SheetSize SheetSize { get; init; } = SheetSize.Default;
    /// <summary>
    /// When true, a Sheet opened via <see cref="OverlayService.ShowSheetAsync{T}"/>
    /// can be dismissed by swiping in the direction opposite to its
    /// <see cref="SheetSide"/> (e.g. swipe-down on a Bottom sheet). Ignored for
    /// non-Sheet overlay types. Drawers already enable swipe by default.
    /// Default false to preserve the existing programmatic-open behaviour.
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
    /// <c>Mobile*</c> fields are populated.</summary>
    public int? MobileBreakpoint { get; init; } = 768;

    /// <summary>Sheet side to use when the viewport is below
    /// <see cref="MobileBreakpoint"/>. Null = use <see cref="SheetSide"/> at all
    /// sizes. Typical pattern: <c>SheetSide.Right</c> on desktop,
    /// <c>SheetSide.Bottom</c> on mobile.</summary>
    public SheetSide? MobileSheetSide { get; init; }

    /// <summary>Sheet size to use when the viewport is below
    /// <see cref="MobileBreakpoint"/>. Null = use <see cref="SheetSize"/> at all
    /// sizes. Typical pattern: <c>SheetSize.Default</c> on desktop,
    /// <c>SheetSize.Full</c> on mobile.</summary>
    public SheetSize? MobileSheetSize { get; init; }

    /// <summary>SwipeToClose override below <see cref="MobileBreakpoint"/>.
    /// Null = use <see cref="SwipeToClose"/> at all sizes. Typical pattern:
    /// off on desktop, on for mobile bottom-sheet pull-down.</summary>
    public bool? MobileSwipeToClose { get; init; }
}

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
    internal TaskCompletionSource<OverlayResult> Tcs { get; init; } = default!;
}
