using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Scheduler;

/// <summary>
/// Real-browser coverage for <c>/components/scheduler</c>. Asserts PER-SECTION presence rather
/// than just a 200 response: a Blazor render exception anywhere in the tree silently drops
/// everything rendered after it — no console error, no crash banner — so a page that
/// half-renders otherwise looks fine.
///
/// <para>
/// Rewritten with the FullCalendar removal. The old suite had a section asserting the
/// wrapper's static scaffolding while deliberately NOT gating on its content, because that
/// section fetched five packages from esm.sh at runtime and the CDN was observably flaky
/// under load. Nothing on this page fetches anything now, so every demo can be asserted on
/// its real rendered DOM — which is what the last test here checks explicitly.
/// </para>
///
/// <para>
/// Timeout budget: this page boots roughly a dozen live view instances, each doing recurrence
/// expansion, grid layout and drag registration on init. On a Debug/interpreted WASM build
/// that settles within ~20s; 30s leaves headroom.
/// </para>
/// </summary>
public class SchedulerDocsPageRenderTests : PlaywrightTestBase
{
    private const float LongTimeoutMs = 30000;

    [Fact]
    public async Task Page_Header_And_Views_Section_Render()
    {
        await Goto("/components/scheduler");

        await Assertions.Expect(Page.Locator("h1", new() { HasTextString = "Scheduler" })).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(Page.Locator("#views")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
    }

    [Fact]
    public async Task Every_Demo_Card_Renders()
    {
        await Goto("/components/scheduler");

        // By ComponentDemo-computed slug id (Title, slugified), except the toolbar demo,
        // which carries an explicit id because "Scheduler" would collide with the API
        // reference's own #scheduler heading.
        string[] expectedCardIds =
        [
            "month-view",
            "week-view",
            "day-view",
            "agenda-view",
            "drop-validation-candrop",
            "scheduler-toolbar",
            "grid-interval-slotduration",
            "n-day-view-schedulerview-multiday",
            "display-time-zone",
            "resource-view",
        ];
        foreach (var id in expectedCardIds)
        {
            await Assertions.Expect(Page.Locator($"#{id}")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        }
    }

    [Fact]
    public async Task The_Standalone_View_Demos_Render_Live_Grids()
    {
        await Goto("/components/scheduler");

        // A real ARIA grid inside the card is proof the component initialized, not just that
        // the surrounding card chrome rendered.
        await Assertions.Expect(Page.Locator("#month-view [role='grid']")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(Page.Locator("#week-view [role='grid']")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        // The dense dataset's overlap cluster on "today" (5 same-day events, over the default
        // 3-lane budget) must overflow into a "+N more" affordance.
        await Assertions.Expect(Page.Locator("#month-view").GetByTestId("month-more-events")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        // Agenda view is a flat list, not a grid — assert its known event titles instead.
        await Assertions.Expect(Page.Locator("#agenda-view")).ToContainTextAsync("Daily stand-up", new() { Timeout = LongTimeoutMs });
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
    public async Task The_Scheduler_Toolbar_Demo_Drives_A_Live_Grid()
    {
        await Goto("/components/scheduler");

        var card = Page.Locator("#scheduler-toolbar");
        await Assertions.Expect(card).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        // Month is the initial view: a 42-cell grid.
        await Assertions.Expect(card.Locator("[role='grid']")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(card.Locator("[data-cell-date]")).ToHaveCountAsync(42, new() { Timeout = LongTimeoutMs });

        // The toolbar swaps the grid underneath it — the whole point of the component.
        await card.GetByRole(AriaRole.Button, new() { NameString = "Week" }).ClickAsync();
        await Assertions.Expect(card.Locator("[data-cell-date]")).ToHaveCountAsync(0, new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(card.Locator("[data-daycol]")).ToHaveCountAsync(7, new() { Timeout = LongTimeoutMs });
    }

    [Fact]
    public async Task The_N_Day_Demo_Shows_Its_Own_Window_Width()
    {
        await Goto("/components/scheduler");

        var card = Page.Locator("#n-day-view-schedulerview-multiday");
        await Assertions.Expect(card).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        // A rolling three-day window, and a toolbar entry labelled with that count.
        await Assertions.Expect(card.Locator("[data-daycol]")).ToHaveCountAsync(3, new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(card.GetByRole(AriaRole.Button, new() { NameString = "3 days" })).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
    }

    [Fact]
    public async Task The_Grid_Interval_Demo_Subdivides_The_Hour()
    {
        await Goto("/components/scheduler");

        var card = Page.Locator("#grid-interval-slotduration");
        await Assertions.Expect(card).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        // 15-minute rows over one day: 96 of them, against 24 on the default grid.
        await Assertions.Expect(card.Locator("[data-slot-minute]")).ToHaveCountAsync(96, new() { Timeout = LongTimeoutMs });
    }

    [Fact]
    public async Task The_Time_Zone_Demo_Reads_The_Same_Events_In_Two_Zones()
    {
        await Goto("/components/scheduler");

        var card = Page.Locator("#display-time-zone");
        await Assertions.Expect(card).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        // Two calendars, same events. Asserting the two zone labels plus both grids is
        // enough here — the actual hour arithmetic is pinned by unit tests, and hard-coding
        // clock times in an E2E test would make it fail twice a year on a DST boundary.
        await Assertions.Expect(card).ToContainTextAsync("Europe/Berlin");
        await Assertions.Expect(card).ToContainTextAsync("Asia/Tokyo");
        await Assertions.Expect(card.Locator("[role='grid']")).ToHaveCountAsync(2, new() { Timeout = LongTimeoutMs });
    }

    [Fact]
    public async Task The_Resource_Demo_Lays_Out_One_Lane_Per_Resource()
    {
        await Goto("/components/scheduler");

        var card = Page.Locator("#resource-view");
        await Assertions.Expect(card).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(card.Locator("[data-resourcecol]")).ToHaveCountAsync(3, new() { Timeout = LongTimeoutMs });
    }

    [Fact]
    public async Task Api_Reference_Covers_The_View_Components_And_CanDrop_Types()
    {
        await Goto("/components/scheduler");

        await Assertions.Expect(Page.Locator("#api-reference")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(Page.Locator("#first-party-views")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(Page.Locator("#scheduler-drop-result")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        var dropTable = Page.Locator("table", new() { HasTextString = "AcceptWith" });
        await Assertions.Expect(dropTable).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
    }

    [Fact]
    public async Task The_Page_Fetches_No_Calendar_Library()
    {
        // The removal's headline claim, asserted where it is actually observable: a real
        // browser loading the real page. Unit tests cannot see a network request.
        var thirdParty = new List<string>();
        Page.Request += (_, req) =>
        {
            if (req.Url.Contains("fullcalendar", StringComparison.OrdinalIgnoreCase))
                thirdParty.Add(req.Url);
        };

        await Goto("/components/scheduler");
        await Assertions.Expect(Page.Locator("#views")).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(Page.Locator("#resource-view [data-resourcecol]").First).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        Assert.Empty(thirdParty);
    }
}
