using Microsoft.AspNetCore.Components;

namespace Lumeo.Services;

/// <summary>
/// Provides methods to show and close overlay components (Dialog, Sheet, Drawer, AlertDialog).
/// Inject this interface in consumers to enable mocking in tests.
/// </summary>
public interface IOverlayService
{
    event Action<OverlayInstance>? OnShow;
    event Action<string, object?, bool>? OnClose;

    Task<OverlayResult> ShowDialogAsync<TComponent>(
        string? title = null,
        OverlayParameters? parameters = null,
        OverlayOptions? options = null) where TComponent : IComponent;

    Task<OverlayResult> ShowSheetAsync<TComponent>(
        string? title = null,
        SheetSide side = SheetSide.Right,
        SheetSize size = SheetSize.Default,
        OverlayParameters? parameters = null,
        OverlayOptions? options = null) where TComponent : IComponent;

    Task<OverlayResult> ShowDrawerAsync<TComponent>(
        string? title = null,
        OverlayParameters? parameters = null,
        OverlayOptions? options = null) where TComponent : IComponent;

    Task<OverlayResult> ShowAlertDialogAsync(AlertDialogOptions alertOptions);

    void Close(string overlayId, object? result = null);
    void Cancel(string overlayId);
}
