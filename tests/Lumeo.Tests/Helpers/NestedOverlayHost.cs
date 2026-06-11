using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Lumeo.Tests.Helpers;

/// <summary>
/// Two-way-bound nesting harness: an outer <see cref="Lumeo.Dialog"/> hosting
/// an inner overlay (root + content component pair), with both Open states
/// bound to properties on this host — mirroring how consumers use
/// <c>@bind-Open</c>. Because the EventCallback receivers are this component,
/// dismissals trigger a real re-render loop and the markup reflects the open
/// state, unlike fire-and-forget callbacks created on the test context.
/// Works for any overlay pair following the Open/OpenChanged/ChildContent
/// convention (Dialog, AlertDialog, Sheet, Drawer).
/// </summary>
public sealed class NestedOverlayHost<TInnerRoot, TInnerContent> : ComponentBase
    where TInnerRoot : IComponent
    where TInnerContent : IComponent
{
    public bool OuterOpen { get; private set; } = true;
    public bool InnerOpen { get; private set; } = true;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<Lumeo.Dialog>(0);
        builder.AddAttribute(1, "Open", OuterOpen);
        builder.AddAttribute(2, "OpenChanged", EventCallback.Factory.Create<bool>(this, v => OuterOpen = v));
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<Lumeo.DialogContent>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(mid =>
            {
                mid.OpenComponent<TInnerRoot>(0);
                mid.AddAttribute(1, "Open", InnerOpen);
                mid.AddAttribute(2, "OpenChanged", EventCallback.Factory.Create<bool>(this, v => InnerOpen = v));
                mid.AddAttribute(3, "ChildContent", (RenderFragment)(ib =>
                {
                    ib.OpenComponent<TInnerContent>(0);
                    ib.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Inner body")));
                    ib.CloseComponent();
                }));
                mid.CloseComponent();
            }));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    }
}
