using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sheet;

/// <summary>
/// Sheet focus-management regressions (a11y, P1):
/// 1. Escape inside a Sheet nested in a Dialog must close only the Sheet —
///    the keydown must not bubble into the ancestor overlay's Escape handler.
/// 2. The focus trap is removed on both close paths (Open flipped false
///    externally, dispose-while-open) so focus returns to the trigger.
/// </summary>
public class SheetFocusManagementTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public SheetFocusManagementTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment SheetFragment(bool open, RenderFragment content) => builder =>
    {
        builder.OpenComponent<L.Sheet>(0);
        builder.AddAttribute(1, "Open", open);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.SheetContent>(0);
            b.AddAttribute(1, "ChildContent", content);
            b.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public void Escape_On_Sheet_Inside_Dialog_Does_Not_Close_Outer_Dialog()
    {
        var cut = _ctx.Render<NestedOverlayHost<L.Sheet, L.SheetContent>>();

        // Both the Dialog and the Sheet content render role="dialog"; the
        // Sheet is the nested (last in document order) one.
        var dialogs = cut.FindAll("[role='dialog']");
        Assert.Equal(2, dialogs.Count);

        dialogs[^1].KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(cut.Instance.InnerOpen);          // sheet handled Escape and closed
        Assert.True(cut.Instance.OuterOpen);           // outer dialog was never asked to close
        Assert.Single(cut.FindAll("[role='dialog']")); // only the outer dialog remains
    }

    [Fact]
    public void Closing_Sheet_Removes_The_Focus_Trap()
    {
        var cut = _ctx.Render<L.Sheet>(p => p
            .Add(s => s.Open, true)
            .AddChildContent<L.SheetContent>(cp => cp.AddChildContent("Body")));

        var setup = Assert.Single(_interop.FocusTrapSetups);
        Assert.Empty(_interop.FocusTrapRemovals);

        cut.Render(p => p.Add(s => s.Open, false));

        Assert.Equal(setup.ElementId, Assert.Single(_interop.FocusTrapRemovals));
    }

    [Fact]
    public void Disposing_Open_Sheet_Removes_The_Focus_Trap()
    {
        var cut = _ctx.Render<ConditionalRoot>(p => p
            .Add(x => x.Show, true)
            .AddChildContent(SheetFragment(open: true, inner => inner.AddContent(0, "Body"))));

        var setup = Assert.Single(_interop.FocusTrapSetups);
        Assert.Empty(_interop.FocusTrapRemovals);

        cut.Render(p => p.Add(x => x.Show, false));

        Assert.Equal(setup.ElementId, Assert.Single(_interop.FocusTrapRemovals));
    }
}
