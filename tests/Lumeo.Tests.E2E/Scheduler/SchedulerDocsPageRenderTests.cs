using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Scheduler;

/// <summary>
/// Real-browser coverage for <c>/components/scheduler</c> — the docs page rebuilt to show
/// realistic, dense event data (overlaps, multi-day spans, all-day events, recurrence) across
/// both Scheduler engines. Asserts PER-SECTION presence rather than just a 200 response: a
/// Blazor render exception anywhere in the tree silently drops everything rendered after it
/// (no console error, no visible crash banner — see the "First-party view engine" section's
/// history before this task, which vanished from the DOM with zero errors under a too-short
/// wait budget), so a page that half-renders can otherwise look fine.
///
/// Timeout budget: measured locally, the first-party engine section (5 live component
/// instances: SchedulerMonthView x2, SchedulerTimeGridView x2, SchedulerAgendaView) settles
/// within ~20s of navigation on a Debug/interpreted WASM build — noticeably slower than the
/// FullCalendar-backed demos alone (~8-10s) because each view does synchronous recurrence
/// expansion + month/week grid layout + JS interop drag registration on init. 30s is used
/// below to leave headroom.
///
/// The FullCalendar-backed "Scheduler (FullCalendar-backed, default)" section is asserted only
/// for its STATIC scaffolding (heading + demo card titles), not for FullCalendar's own rendered
/// DOM. That section fetches 5 FullCalendar packages from esm.sh at runtime; even with a clean
/// network path this project's own vendoring fix (tracked separately, PR #396 on
/// feat/gantt-v3-promote — NOT merged as of this test) hasn't landed, and locally the CDN calls
/// are observably flaky under load (esm.sh returned intermittent 408s here once ~13 concurrent
/// Scheduler-family components were booting on one page). Asserting deep FullCalendar content
/// here would make this suite flaky for a reason unrelated to Lumeo's own code.
/// </summary>
public class SchedulerDocsPageRenderTests : PlaywrightTestBase
{
    private const float LongTimeoutMs = 30000;

    [Fact]
    public async Task Page_Header_And_Choosing_An_Engine_Table_Render()
    {
        await Goto("/components/scheduler");

        await Assertions.Expect(Page.Locator("h1", new() { HasTextString = "Scheduler" })).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(Page.Locator("#choosing-an-engine")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        var table = Page.Locator("table", new() { HasTextString = "Multi-calendar split / overlay panes" });
        await Assertions.Expect(table).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(table).ToContainTextAsync("Drop validation");
    }

    [Fact]
    public async Task First_Party_Engine_Section_Renders_All_Five_Demo_Cards_With_Live_Grids()
    {
        await Goto("/components/scheduler");

        await Assertions.Expect(Page.Locator("#first-party-engine")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        // Every first-party demo card, by its ComponentDemo-computed slug id (Title, slugified).
        string[] expectedCardIds =
        [
            "month-view-first-party-engine",
            "week-view-first-party-engine",
            "day-view-first-party-engine",
            "agenda-view-first-party-engine",
            "drop-validation-candrop",
        ];
        foreach (var id in expectedCardIds)
        {
            await Assertions.Expect(Page.Locator($"#{id}")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        }

        // The month and week demos each render a real ARIA grid (SchedulerMonthView /
        // SchedulerTimeGridView both use role="grid") — proof the component actually
        // initialized, not just that the surrounding card chrome rendered.
        var monthGrid = Page.Locator("#month-view-first-party-engine [role='grid']");
        await Assertions.Expect(monthGrid).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        var weekGrid = Page.Locator("#week-view-first-party-engine [role='grid']");
        await Assertions.Expect(weekGrid).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        // The dense dataset's overlap cluster on "today" (5 same-day events, over the
        // default 3-lane budget) must overflow into a "+N more" affordance.
        var monthCard = Page.Locator("#month-view-first-party-engine");
        await Assertions.Expect(monthCard.GetByTestId("month-more-events")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        // Agenda view is a flat list, not a grid — assert its known event titles instead.
        var agendaCard = Page.Locator("#agenda-view-first-party-engine");
        await Assertions.Expect(agendaCard).ToContainTextAsync("Daily stand-up", new() { Timeout = LongTimeoutMs });
    }

    [Fact]
    public async Task CanDrop_Demo_Explains_The_Three_Way_Gate_And_Renders_A_Live_Grid()
    {
        await Goto("/components/scheduler");

        var card = Page.Locator("#drop-validation-candrop");
        await Assertions.Expect(card).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(card).ToContainTextAsync("15th");
        await Assertions.Expect(card).ToContainTextAsync("22nd");
        await Assertions.Expect(card.Locator("[role='grid']")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(card).ToContainTextAsync("Last CanDrop check");
    }

    [Fact]
    public async Task FullCalendar_Section_Static_Scaffolding_Renders_All_Eight_Demo_Cards()
    {
        // Static scaffolding only (see class-level remarks) — this section's actual FullCalendar
        // content depends on a CDN fetch this test deliberately does not gate on.
        await Goto("/components/scheduler");

        await Assertions.Expect(Page.Locator("#fullcalendar-engine")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        string[] expectedCardIds =
        [
            "month-view",
            "week-view-timed-all-day-overlaps",
            "day-view",
            "list-view-agenda",
            "click-to-create",
            "drag-to-reschedule",
            "recurring-events",
            "resource-color-coding",
        ];
        foreach (var id in expectedCardIds)
        {
            await Assertions.Expect(Page.Locator($"#{id}")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        }
    }

    [Fact]
    public async Task Api_Reference_Covers_Both_Engines_Including_CanDrop_Types()
    {
        await Goto("/components/scheduler");

        await Assertions.Expect(Page.Locator("#api-reference")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(Page.Locator("#first-party-views")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(Page.Locator("#scheduler-drop-result")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        var dropTable = Page.Locator("table", new() { HasTextString = "AcceptWith" });
        await Assertions.Expect(dropTable).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
    }
}
