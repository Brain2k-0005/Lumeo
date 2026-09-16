using System.Collections.Generic;
using System.Linq;
using Bunit;
using Lumeo;
using Lumeo.Docs.Pages.Demos;
using Lumeo.Docs.Services;
using Lumeo.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Docs.Tests.Demos;

// Regression coverage for the saas-audit.md fix pass on the Northlight demo
// (docs/Lumeo.Docs/Pages/Demos/SaasDemo.razor). Each test guards one behavioural
// finding from the audit — see the fix report for the findings that couldn't be
// covered this way (they depend on the DataGrid's internal virtualization/search,
// which needs a real browser to measure).
public class SaasDemoTests
{
    // await using (not a shared IDisposable field): KeyboardShortcutService/
    // ResponsiveService only implement IAsyncDisposable, and a sync ctx.Dispose()
    // throws trying to dispose them — mirrors HomePageTests' NewContext() pattern.
    private static BunitContext NewContext()
    {
        var ctx = new BunitContext();

        // Loose mode: KeyboardShortcutService/ComponentInteropService/ResponsiveService
        // all make real JS interop calls (module imports, resize listener registration)
        // on OnAfterRenderAsync that bUnit has no browser to satisfy — same pattern
        // DataGridPlaygroundPageAccessibilityTests uses to render a DataGrid-bearing page.
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddLumeo();
        // DynamicIcon (used throughout the demo shell and every grid cell) resolves
        // through these two, same as every other docs page test.
        ctx.Services.AddSingleton<IconService>();
        ctx.Services.AddSingleton<DynamicIconResolver>();
        return ctx;
    }

    // ---- Row 7: URL routing per view ----

    [Fact]
    public async Task Renders_the_dashboard_by_default_with_no_route_segment()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<SaasDemo>();

        Assert.Contains("Monthly recurring revenue", cut.Markup);
    }

    [Fact]
    public async Task A_route_segment_deep_links_directly_into_that_view()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<SaasDemo>(p => p.Add(c => c.ViewParam, "board"));

        Assert.Contains("Drag cards between columns", cut.Markup);
        Assert.DoesNotContain("Monthly recurring revenue", cut.Markup);
    }

    [Fact]
    public async Task Clicking_a_nav_item_pushes_the_matching_url_segment()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<SaasDemo>();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        var boardNav = cut.FindAll("button").First(b => b.TextContent.Trim() == "Board");
        boardNav.Click();

        Assert.EndsWith("/demos/saas/board", nav.Uri);
    }

    // ---- Row 8: Settings Cancel must restore the previous values ----

    [Fact]
    public async Task Cancel_restores_the_profile_fields_after_a_failed_save()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<SaasDemo>(p => p.Add(c => c.ViewParam, "settings"));

        var nameInput = cut.FindAll("input").First(i => i.GetAttribute("value") == "Ava Chen");
        nameInput.Input(string.Empty);

        var saveButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Save changes");
        saveButton.Click();
        Assert.Contains("Name is required.", cut.Markup);

        var cancelButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Cancel");
        cancelButton.Click();

        Assert.DoesNotContain("Name is required.", cut.Markup);
        Assert.Contains("value=\"Ava Chen\"", cut.Markup);
    }

    // ---- Row 9: "New card" must actually add a card ----

    [Fact]
    public async Task New_card_button_adds_a_real_card_to_the_todo_column()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<SaasDemo>(p => p.Add(c => c.ViewParam, "board"));

        Assert.DoesNotContain("New task", cut.Markup);

        var newCardButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "New card");
        newCardButton.Click();

        Assert.Contains("New task", cut.Markup);
    }

    // ---- Row 6: low-priority Customer columns hide on narrow viewports ----

    [Fact]
    public async Task Mobile_breakpoint_hides_low_priority_customer_columns()
    {
        await using var ctx = NewContext();
        var fake = new FakeResponsiveService();
        ctx.Services.AddSingleton<IResponsiveService>(fake);

        var cut = ctx.Render<SaasDemo>(p => p.Add(c => c.ViewParam, "customers"));

        // Desktop (the fake's default): all columns present, including the
        // lower-priority ones the audit found forced a horizontal scroll on phones.
        Assert.Contains("Region", ColumnHeaderTitles(cut));

        // OnViewportChanged (SaasDemo.razor) mutates the column-visibility fields
        // (_seatsColVisible etc.) synchronously before it dispatches its own
        // StateHasChanged via InvokeAsync — only that dispatch is async, so waiting for
        // it via WaitForAssertion polling was unnecessary and flaked under a loaded test
        // machine. The fields are already updated by the time SetMobile returns; a plain
        // cut.Render() forces a fresh render off the already-mutated state, deterministically.
        fake.SetMobile(true);
        cut.Render();
        var headers = ColumnHeaderTitles(cut);
        Assert.DoesNotContain("Owner", headers);
        Assert.DoesNotContain("Seats", headers);
        Assert.DoesNotContain("Signed up", headers);
        // Company/Plan (the two columns the audit says survive on a phone) stay.
        Assert.Contains("Company", headers);
        Assert.Contains("Plan", headers);
    }

    // ---- "n of m accounts" follows the grid's own search, not just the chip filter ----
    // (5.10.3: DataGrid gained FilteredRowCount/TotalRowCount + OnRowCountChanged so this line
    // no longer has to be worded to avoid contradicting the grid's own "No rows match the
    // current filters" empty state — see DataGridPage.razor's "Showing n of m" example.)

    [Fact]
    public async Task Customers_header_shows_the_chip_filtered_baseline_as_n_of_m()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<SaasDemo>(p => p.Add(c => c.ViewParam, "customers"));

        var total = NorthlightData.Customers.Count;

        // No grid-level search/filter active yet: n == m == the full (chip="All") baseline,
        // matching what the old "@FilteredCustomers.Count accounts" text showed for "All".
        Assert.Contains($"{total} of {total} accounts", cut.Markup);
    }

    [Fact]
    public async Task Customers_header_narrows_with_the_grids_own_search_while_m_stays_the_chip_baseline()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<SaasDemo>(p => p.Add(c => c.ViewParam, "customers"));

        var total = NorthlightData.Customers.Count;
        Assert.Contains($"{total} of {total} accounts", cut.Markup);

        // A single company's name: DataGrid's built-in global search matches any column's
        // formatted value, so this narrows the grid's OWN filtered count (n) without touching
        // the chip filter, which is exactly the gap FilteredRowCount/TotalRowCount closes — the
        // old text only ever reflected the chip filter and could not follow this at all.
        var needle = NorthlightData.Customers[0].Company;
        var searchInput = cut.FindAll("input").First(i => i.GetAttribute("placeholder") == "Search…");
        searchInput.Input(needle);

        // OnRowCountChanged dispatches through SafeAsyncDispatcher (fire-and-forget from the
        // grid's synchronous client-mode filter path), so the demo's own fields can land a
        // render after the input event completes — WaitForAssertion covers that gap, mirroring
        // the ViewportChanged case above.
        cut.WaitForAssertion(
            () => Assert.DoesNotContain($"{total} of {total} accounts", cut.Markup),
            TimeSpan.FromSeconds(10));

        // m (the chip-filtered baseline, "All") is unchanged by the grid's own search.
        Assert.Contains($"of {total} accounts", cut.Markup);
    }

    private static IReadOnlyList<string> ColumnHeaderTitles(IRenderedComponent<SaasDemo> cut) =>
        cut.FindAll("[role='columnheader']").Select(h => h.TextContent.Trim()).ToList();

    private sealed class FakeResponsiveService : IResponsiveService
    {
        public double Width { get; private set; } = 1920;
        public double Height { get; private set; } = 1080;
        public Breakpoint Current { get; private set; } = Breakpoint.Xl;
        public bool IsMobile { get; private set; }
        public bool IsTablet { get; private set; }
        public bool IsDesktop { get; private set; } = true;
        public event Action<ViewportInfo>? ViewportChanged;
        public ValueTask EnsureInitialisedAsync() => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void SetMobile(bool mobile)
        {
            IsMobile = mobile;
            IsDesktop = !mobile;
            Width = mobile ? 375 : 1920;
            Current = mobile ? Breakpoint.Xs : Breakpoint.Xl;
            ViewportChanged?.Invoke(new ViewportInfo(Width, Height, Current));
        }
    }
}
