using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// ReUI parity — its event calendar has a <c>timeZone</c> prop that renders the whole grid in
/// any IANA zone. Lumeo had none, and every view is deliberately a wall-clock renderer that
/// never consults <see cref="TimeZoneInfo"/>.
///
/// <para>
/// That property is kept. The projection happens at the wrapper's edges instead, which makes
/// <see cref="DateTime.Kind"/> the deciding question: only values that denote a real instant
/// can be re-read in another zone. The single most important test here is the first one —
/// naive values, which is what virtually every existing caller passes, must not move.
/// </para>
/// </summary>
public class SchedulerTimeZoneTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerTimeZoneTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Tokyo is UTC+9 year-round — no DST, so the arithmetic in these tests is stable
    // whatever the machine running them thinks the date is.
    private static TimeZoneInfo Tokyo =>
        L.SchedulerTimeZoneProjection.Resolve("Asia/Tokyo")
        ?? throw new InvalidOperationException("Asia/Tokyo missing from this host's tz database");

    // ── The compatibility guarantee ─────────────────────────────────────────

    [Fact]
    public void A_Naive_Value_Never_Moves()
    {
        // The whole safety argument for shipping this: an Unspecified DateTime is a wall-clock
        // reading that is ALREADY in whatever zone the caller means. If setting a time zone
        // moved these, every existing consumer's events would silently shift on upgrade.
        var naive = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal(naive, L.SchedulerTimeZoneProjection.ToDisplay(naive, Tokyo));
    }

    [Fact]
    public void An_All_Day_Event_Never_Moves()
    {
        // An all-day event is a DATE. Shifting it by hours moves it onto a different day,
        // which is the one thing it must never do.
        var ev = new L.SchedulerEvent("e1", "Holiday",
            new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
            AllDay: true);

        Assert.Equal(ev, L.SchedulerTimeZoneProjection.ToDisplay(ev, Tokyo));
    }

    // ── Real instants ───────────────────────────────────────────────────────

    [Fact]
    public void A_Utc_Instant_Is_Read_In_The_Display_Zone()
    {
        var utc = new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc);

        var shown = L.SchedulerTimeZoneProjection.ToDisplay(utc, Tokyo);

        Assert.Equal(new DateTime(2026, 3, 11, 9, 0, 0), shown);
        Assert.Equal(DateTimeKind.Unspecified, shown.Kind);   // it is a wall-clock reading now
    }

    [Fact]
    public void A_Utc_Instant_Survives_The_Round_Trip()
    {
        var utc = new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc);

        var back = L.SchedulerTimeZoneProjection.FromDisplay(
            L.SchedulerTimeZoneProjection.ToDisplay(utc, Tokyo), DateTimeKind.Utc, Tokyo);

        Assert.Equal(utc, back);
        Assert.Equal(DateTimeKind.Utc, back.Kind);
    }

    [Fact]
    public void An_Edit_Comes_Back_In_The_Frame_It_Went_In_As()
    {
        // The views hand back naive wall-clock values; only the ORIGINAL event says what frame
        // the caller was using. Getting this wrong rewrites the caller's data into a different
        // frame on every single drag.
        var original = new L.SchedulerEvent("e1", "Standup",
            new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 11, 1, 0, 0, DateTimeKind.Utc));

        // Dragged an hour later in the displayed (Tokyo) grid: 09:00 -> 10:00.
        var edited = original with
        {
            Start = new DateTime(2026, 3, 11, 10, 0, 0),
            End = new DateTime(2026, 3, 11, 11, 0, 0),
        };

        var committed = L.SchedulerTimeZoneProjection.FromDisplay(edited, original, Tokyo);

        Assert.Equal(new DateTime(2026, 3, 11, 1, 0, 0, DateTimeKind.Utc), committed.Start);
        Assert.Equal(DateTimeKind.Utc, committed.Start.Kind);
    }

    // ── Degrading rather than failing ───────────────────────────────────────

    [Fact]
    public void An_Unknown_Zone_Degrades_To_No_Projection()
    {
        // A bad id must not fail the render — the grid falls back to the caller's own values.
        Assert.Null(L.SchedulerTimeZoneProjection.Resolve("Mars/Olympus_Mons"));
        Assert.Null(L.SchedulerTimeZoneProjection.Resolve(""));
        Assert.Null(L.SchedulerTimeZoneProjection.Resolve(null));
    }

    [Fact]
    public void A_Wall_Clock_Reading_That_Does_Not_Exist_Does_Not_Throw()
    {
        // Spring forward skips an hour. A drag can land inside the gap, and .NET throws on
        // converting one — which would surface as an unhandled exception on the circuit
        // rather than as a rejected drop.
        var berlin = L.SchedulerTimeZoneProjection.Resolve("Europe/Berlin");
        if (berlin is null) return;   // host without tz data — nothing to assert

        // 2026-03-29 02:30 local does not exist in Europe/Berlin.
        var skipped = new DateTime(2026, 3, 29, 2, 30, 0);

        var committed = L.SchedulerTimeZoneProjection.FromDisplay(skipped, DateTimeKind.Utc, berlin);

        Assert.Equal(DateTimeKind.Utc, committed.Kind);
    }

    [Fact]
    public void The_Js_Zone_Id_Is_Iana_Because_Intl_Understands_Nothing_Else()
    {
        // The now-indicator resolves the zone with Intl in the browser. A Windows id sent
        // across would be rejected there and the line would silently stay on the viewer's
        // own clock.
        var id = L.SchedulerTimeZoneProjection.JsZoneId(Tokyo);

        Assert.Equal("Asia/Tokyo", id);
        Assert.Null(L.SchedulerTimeZoneProjection.JsZoneId(null));
    }

    // ── Through the component ───────────────────────────────────────────────

    [Fact]
    public void The_Grid_Places_A_Utc_Event_At_Its_Zone_Local_Hour()
    {
        var events = new[]
        {
            new L.SchedulerEvent("e1", "Standup",
                new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 3, 11, 1, 0, 0, DateTimeKind.Utc)),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Day)
            .Add(c => c.InitialDate, new DateTime(2026, 3, 11))
            .Add(c => c.TimeZone, "Asia/Tokyo")
            .Add(c => c.Events, events));

        // The chip carries its own displayed time; in Tokyo that is 09:00, not 00:00.
        var chip = cut.FindAll("[data-event-id='e1']").First();
        var label = chip.GetAttribute("aria-label") ?? chip.TextContent;
        Assert.Contains("9", label, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_A_Zone_The_Component_Renders_Exactly_What_It_Always_Did()
    {
        var events = new[]
        {
            new L.SchedulerEvent("e1", "Standup",
                new DateTime(2026, 3, 11, 9, 0, 0),
                new DateTime(2026, 3, 11, 10, 0, 0)),
        };

        IRenderedComponent<L.Scheduler> RenderWith(string? tz) =>
            _ctx.Render<L.Scheduler>(p =>
            {
                p.Add(c => c.InitialView, L.SchedulerView.Day);
                p.Add(c => c.InitialDate, new DateTime(2026, 3, 11));
                p.Add(c => c.Events, events);
                if (tz is not null) p.Add(c => c.TimeZone, tz);
            });

        // Naive values, so even an explicit zone must produce identical chips.
        var plain = RenderWith(null).FindAll("[data-event-id='e1']").First().TextContent;
        var zoned = RenderWith("Asia/Tokyo").FindAll("[data-event-id='e1']").First().TextContent;

        Assert.Equal(plain, zoned);
    }
}
