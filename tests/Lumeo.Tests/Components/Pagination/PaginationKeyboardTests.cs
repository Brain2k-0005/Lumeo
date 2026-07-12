using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Pagination;

/// <summary>
/// Data-driven Pagination renders native Previous/page-number/Next &lt;button&gt;s inside
/// a &lt;nav aria-label="pagination"&gt; — Enter/Space activation is free via the browser's
/// default button semantics, so .Click() exercises the exact handler a synthesized
/// keydown would run (PaginationTests already pins OnClick for the compositional
/// PaginationItem/Previous/Next primitives via that same mechanism). This file targets
/// the data-driven mode's own untested surface: Tab order across Previous -> pages ->
/// Next, page-button activation driving PageChanged, and the disabled/native-excluded
/// boundary state at page 1 / the last page.
/// </summary>
public class PaginationKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public PaginationKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Tab_Order_Reaches_Previous_Then_Each_Page_Then_Next_In_DOM_Order()
    {
        var cut = _ctx.Render<L.Pagination>(p => p
            .Add(x => x.Page, 2)
            .Add(x => x.TotalPages, 3));

        var buttons = cut.FindAll("button");
        // Previous, 1, 2, 3, Next — none carry a tabindex override, so the browser's
        // default source-order Tab sequence visits them in exactly this DOM order.
        Assert.Equal(5, buttons.Count);
        Assert.Equal("Go to previous page", buttons[0].GetAttribute("aria-label"));
        Assert.Equal("Go to next page", buttons[4].GetAttribute("aria-label"));
        foreach (var button in buttons)
            Assert.False(button.HasAttribute("tabindex"));
    }

    [Fact]
    public void Activating_A_Page_Button_Fires_PageChanged_With_That_Page()
    {
        int? changed = null;
        var cut = _ctx.Render<L.Pagination>(p => p
            .Add(x => x.Page, 1)
            .Add(x => x.TotalPages, 3)
            .Add(x => x.PageChanged, v => changed = v));

        cut.Find("button[aria-label='Go to page 2']").Click();

        Assert.Equal(2, changed);
    }

    [Fact]
    public void Previous_Is_Disabled_And_Not_Focusable_At_The_First_Page()
    {
        var cut = _ctx.Render<L.Pagination>(p => p
            .Add(x => x.Page, 1)
            .Add(x => x.TotalPages, 3));

        var previous = cut.Find("button[aria-label='Go to previous page']");
        Assert.True(previous.HasAttribute("disabled"));
    }

    [Fact]
    public void Next_Is_Disabled_And_Not_Focusable_At_The_Last_Page()
    {
        var cut = _ctx.Render<L.Pagination>(p => p
            .Add(x => x.Page, 3)
            .Add(x => x.TotalPages, 3));

        var next = cut.Find("button[aria-label='Go to next page']");
        Assert.True(next.HasAttribute("disabled"));
    }
}
