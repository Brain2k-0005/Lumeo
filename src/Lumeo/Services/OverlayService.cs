using Microsoft.AspNetCore.Components;

namespace Lumeo.Services;

public sealed class OverlayService
{
    public event Action<OverlayInstance>? OnShow;
    public event Action<string, object?>? OnClose;

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
        OnClose?.Invoke(overlayId, result);
    }

    public void Cancel(string overlayId)
    {
        OnClose?.Invoke(overlayId, null);
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
