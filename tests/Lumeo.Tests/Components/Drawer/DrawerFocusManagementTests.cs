using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

/// <summary>
/// Drawer focus-management regressions (a11y, P1):
/// 1. Escape inside a Drawer nested in a Dialog must close only the Drawer —
///    the keydown must not bubble into the ancestor overlay's Escape handler.
/// 2. The focus trap is removed on both close paths (Open flipped false
///    externally, dispose-while-open) so focus returns to the trigger.
/// </summary>
public class DrawerFocusManagementTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DrawerFocusManagementTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment DrawerFragment(bool open, RenderFragment content) => builder =>
    {
        builder.OpenComponent<L.Drawer>(0);
        builder.AddAttribute(1, "Open", open);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.DrawerContent>(0);
            b.AddAttribute(1, "ChildContent", content);
            b.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public void Escape_On_Drawer_Inside_Dialog_Does_Not_Close_Outer_Dialog()
    {
        var cut = _ctx.Render<NestedOverlayHost<L.Drawer, L.DrawerContent>>();

        // Both the Dialog and the Drawer content render role="dialog"; the
        // Drawer is the nested (last in document order) one.
        var dialogs = cut.FindAll("[role='dialog']");
        Assert.Equal(2, dialogs.Count);

        dialogs[^1].KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(cut.Instance.InnerOpen);          // drawer handled Escape and closed
        Assert.True(cut.Instance.OuterOpen);           // outer dialog was never asked to close
        Assert.Single(cut.FindAll("[role='dialog']")); // only the outer dialog remains
    }

    [Fact]
    public void Closing_Drawer_Removes_The_Focus_Trap()
    {
        var cut = _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, true)
            .AddChildContent<L.DrawerContent>(cp => cp.AddChildContent("Body")));

        var setup = Assert.Single(_interop.FocusTrapSetups);
        Assert.Empty(_interop.FocusTrapRemovals);

        cut.Render(p => p.Add(d => d.Open, false));

        Assert.Equal(setup.ElementId, Assert.Single(_interop.FocusTrapRemovals));
    }

    [Fact]
    public void Disposing_Open_Drawer_Removes_The_Focus_Trap()
    {
        var cut = _ctx.Render<ConditionalRoot>(p => p
            .Add(x => x.Show, true)
            .AddChildContent(DrawerFragment(open: true, inner => inner.AddContent(0, "Body"))));

        var setup = Assert.Single(_interop.FocusTrapSetups);
        Assert.Empty(_interop.FocusTrapRemovals);

        cut.Render(p => p.Add(x => x.Show, false));

        Assert.Equal(setup.ElementId, Assert.Single(_interop.FocusTrapRemovals));
    }
}
