using System.Diagnostics.CodeAnalysis;
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

    // TComponent is rendered later via <DynamicComponent Type="..."> (OverlayProvider.razor),
    // which needs every member preserved regardless of what the trimmer can see reached
    // through normal static usage. [DynamicallyAccessedMembers(All)] on the generic
    // parameter propagates that requirement to typeof(TComponent) wherever it flows —
    // through OverlayInstance.ComponentType — down to the DynamicComponent render (#354).
    Task<OverlayResult> ShowDialogAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(
        string? title = null,
        OverlayParameters? parameters = null,
        OverlayOptions? options = null) where TComponent : IComponent;

    Task<OverlayResult> ShowSheetAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(
        string? title = null,
        Lumeo.Side side = Lumeo.Side.Right,
        SheetSize size = SheetSize.Default,
        OverlayParameters? parameters = null,
        OverlayOptions? options = null) where TComponent : IComponent;

    Task<OverlayResult> ShowDrawerAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(
        string? title = null,
        OverlayParameters? parameters = null,
        OverlayOptions? options = null) where TComponent : IComponent;

    Task<OverlayResult> ShowAlertDialogAsync(AlertDialogOptions alertOptions);

    void Close(string overlayId, object? result = null);
    void Cancel(string overlayId);
}
