using System.Linq;
using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// Round-2 P2 (typeahead scope). A printable key handled by an OPEN
/// ContextMenuSubContent must not also reach the root ContextMenuContent: the
/// SubContent is a DOM descendant of the root menu, so without keydown
/// stopPropagation the key bubbles up and the ROOT typeahead fires too, jumping
/// focus back to a root item and yanking it out of the submenu. The fix scopes
/// keydown to the submenu (mirroring MenubarSubContent). The typeahead calls are
/// observed through TrackingInteropService: each carries the container id it
/// searched, so a leaked root call is visible as a call against the root
/// (context-menu-content-*) container.
/// </summary>
public class ContextMenuSubTypeaheadScopeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ContextMenuSubTypeaheadScopeTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Open root menu → root item "Root Apple" + a sub whose content holds "Sub Banana".
    private static RenderFragment OpenMenuWithSub => builder =>
    {
        builder.OpenComponent<L.ContextMenu>(0);
        builder.AddAttribute(1, "Open", true);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.ContextMenuContent>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(content =>
            {
                content.OpenComponent<L.ContextMenuItem>(0);
                content.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Root Apple")));
                content.CloseComponent();

                content.OpenComponent<L.ContextMenuSub>(2);
                content.AddAttribute(3, "ChildContent", (RenderFragment)(sub =>
                {
                    sub.OpenComponent<L.ContextMenuSubTrigger>(0);
                    sub.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "More")));
                    sub.CloseComponent();

                    sub.OpenComponent<L.ContextMenuSubContent>(2);
                    sub.AddAttribute(3, "ChildContent", (RenderFragment)(sc =>
                    {
                        sc.OpenComponent<L.ContextMenuItem>(0);
                        sc.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Sub Banana")));
                        sc.CloseComponent();
                    }));
                    sub.CloseComponent();
                }));
                content.CloseComponent();
            }));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public void Printable_Key_In_Open_Submenu_Does_Not_Trigger_Root_Typeahead()
    {
        var cut = _ctx.Render(OpenMenuWithSub);

        // Hover the sub-trigger to open the submenu, then locate the SUB content panel
        // (its id is context-sub-content-*, distinct from the root context-menu-content-*).
        cut.Find("button[aria-haspopup='menu']").MouseEnter();
        var subContent = cut.FindAll("[role='menu']").First(m => (m.Id ?? "").StartsWith("context-sub-content"));

        subContent.KeyDown(new KeyboardEventArgs { Key = "b" });

        // Exactly one typeahead call, against the SUB container — the key must not have
        // bubbled to the root menu's handler. Pre-fix (no stopPropagation) a second call
        // lands against the root context-menu-content-* container.
        var call = Assert.Single(_interop.TypeaheadCalls);
        Assert.StartsWith("context-sub-content-", call.ContainerId);
        Assert.DoesNotContain(_interop.TypeaheadCalls, c => c.ContainerId.StartsWith("context-menu-content-"));
    }
}
