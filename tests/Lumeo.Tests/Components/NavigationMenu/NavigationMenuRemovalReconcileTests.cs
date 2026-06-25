using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.NavigationMenu;

/// <summary>
/// Regression test for battle-test #60 (state-on-data-change).
///
/// When the active NavigationMenuItem (the one whose submenu is open) is removed
/// from the tree, NavigationMenu._activeItemId was only ever set to null by user
/// handlers. NavigationMenuItem.Dispose() unregistered the trigger but never
/// reconciled the open-submenu state, so the Viewport/Indicator — which gate
/// purely on `Context.ActiveItemId is not null` — stayed stuck open forever,
/// referencing a no-longer-rendered item.
///
/// The fix: NavigationMenuItem.Dispose() calls NavigationMenu.NotifyItemRemoved,
/// which clears _activeItemId (and re-renders) when the removed id is the active
/// one — scoped to the specific removal so an unrelated list churn can't disturb
/// an open item.
/// </summary>
public class NavigationMenuRemovalReconcileTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NavigationMenuRemovalReconcileTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Removing_The_Active_Item_Closes_The_Viewport()
    {
        var cut = _ctx.Render<NavigationMenuRemovalHost>(p => p.Add(h => h.ShowSecond, true));

        // Open the SECOND item's submenu so _activeItemId points at the item we
        // are about to remove.
        cut.FindAll("button")[1].Click();
        Assert.Contains("viewport-open", cut.Markup);
        Assert.Contains("Services content", cut.Markup);

        // Remove the active item via a real host parameter (host re-render, no
        // cascading value re-provided through Render). Its Dispose must reconcile
        // the open state.
        cut.Render(p => p.Add(h => h.ShowSecond, false));

        // The Viewport (gated on ActiveItemId is not null) must close instead of
        // staying stuck open on the removed item.
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("viewport-open", cut.Markup);
            Assert.DoesNotContain("Services content", cut.Markup);
        });
    }

    [Fact]
    public void Removing_A_NonActive_Item_Leaves_The_Open_Submenu_Untouched()
    {
        var cut = _ctx.Render<NavigationMenuRemovalHost>(p => p.Add(h => h.ShowSecond, true));

        // Open the FIRST item's submenu (the active item is item one).
        cut.FindAll("button")[0].Click();
        Assert.Contains("viewport-open", cut.Markup);
        Assert.Contains("Products content", cut.Markup);

        // Remove the SECOND (non-active) item. The open submenu of item one must
        // survive — NotifyItemRemoved is scoped to the removed id only.
        cut.Render(p => p.Add(h => h.ShowSecond, false));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("viewport-open", cut.Markup);
            Assert.Contains("Products content", cut.Markup);
        });
    }
}
