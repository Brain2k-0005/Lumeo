using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.MegaMenu;

/// <summary>
/// Regression test for battle-test #59 (state-on-data-change).
///
/// When a MegaMenuItem is flipped Disabled=true while its panel is open (the
/// item is the active one), the trigger button would render disabled AND keep
/// the panel content mounted — a panel stranded over a non-interactive trigger.
/// OnParametersSet re-reported Disabled to the registry but never reconciled the
/// parent's ActiveItemId, so _isOpen stayed true.
///
/// The fix: in MegaMenuItem.OnParametersSetAsync, when Disabled becomes true and
/// this item is the active one, clear ActiveItemId so the panel closes.
/// </summary>
public class MegaMenuDisabledReconcileTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MegaMenuDisabledReconcileTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Flipping_Disabled_True_On_Active_Item_Closes_Open_Panel()
    {
        var cut = _ctx.Render<MegaMenuDisabledHost>(p => p.Add(h => h.ProductsDisabled, false));

        // Open the panel via the trigger.
        cut.Find("button").Click();
        Assert.Contains("Analytics", cut.Markup);

        // Flip the active item Disabled while the panel is open. The SAME
        // MegaMenuItem instance receives the changed parameter (host re-render,
        // no cascading value re-provided through Render).
        cut.Render(p => p.Add(h => h.ProductsDisabled, true));

        // The panel must close (no stranded panel over a disabled trigger) and
        // the trigger must render disabled + aria-expanded="false".
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Analytics", cut.Markup);
            var button = cut.Find("button");
            Assert.True(button.HasAttribute("disabled"));
            Assert.Equal("false", button.GetAttribute("aria-expanded"));
        });
    }

    [Fact]
    public void Item_That_Was_Never_Open_Stays_Closed_When_Disabled()
    {
        // Sanity: flipping Disabled on an item that was never opened is a no-op
        // for the panel (it was already closed); the normal path is undisturbed.
        var cut = _ctx.Render<MegaMenuDisabledHost>(p => p.Add(h => h.ProductsDisabled, false));
        Assert.DoesNotContain("Analytics", cut.Markup);

        cut.Render(p => p.Add(h => h.ProductsDisabled, true));

        Assert.DoesNotContain("Analytics", cut.Markup);
        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }
}
