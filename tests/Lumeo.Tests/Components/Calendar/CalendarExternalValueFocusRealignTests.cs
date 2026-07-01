using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Calendar;

/// <summary>
/// Regression (#75, keyboard-a11y): the roving-tabindex / aria target
/// (_focusedDate) was not realigned when the bound Value changed externally to a
/// different day *within the already-displayed month*. Because the displayed
/// month did not change, EnsureFocusedDate — which only re-seeds when
/// _focusedDate falls OUT of the displayed window — kept the single tabbable day
/// button (tabindex=0) on the stale previous date. After an external move from
/// June 15 → June 20 the next Tab into the grid would land on the 15th, not the
/// newly-selected 20th. The fix realigns _focusedDate to the new anchor inside
/// OnParametersSet's real-anchor-change branch.
/// </summary>
public class CalendarExternalValueFocusRealignTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CalendarExternalValueFocusRealignTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>The single day button currently holding the roving tabindex.</summary>
    private static IElement Tabbable(IRenderedComponent<L.Calendar> cut)
        => cut.Find("[role='gridcell'] button[tabindex='0']");

    [Fact]
    public void External_Value_change_within_month_realigns_roving_tabindex()
    {
        var cut = _ctx.Render<L.Calendar>(p => p.Add(c => c.Value, new DateOnly(2024, 6, 15)));

        // Initially the roving-tabindex day is the selected day (15).
        Assert.Single(cut.FindAll("[role='gridcell'] button[tabindex='0']"));
        Assert.Equal("15", Tabbable(cut).TextContent.Trim());

        // External Value change to another day in the SAME displayed month.
        cut.Render(p => p.Add(c => c.Value, new DateOnly(2024, 6, 20)));

        // The roving target must follow the new value, not stay parked on the 15th.
        Assert.Single(cut.FindAll("[role='gridcell'] button[tabindex='0']"));
        Assert.Equal("20", Tabbable(cut).TextContent.Trim());
    }

    [Fact]
    public void External_RangeStart_change_within_month_realigns_roving_tabindex()
    {
        var cut = _ctx.Render<L.Calendar>(p => p
            .Add(c => c.IsRange, true)
            .Add(c => c.RangeStart, new DateOnly(2024, 6, 10)));

        Assert.Equal("10", Tabbable(cut).TextContent.Trim());

        // External RangeStart change within the displayed month (anchor => RangeStart).
        cut.Render(p => p.Add(c => c.RangeStart, new DateOnly(2024, 6, 18)));

        Assert.Single(cut.FindAll("[role='gridcell'] button[tabindex='0']"));
        Assert.Equal("18", Tabbable(cut).TextContent.Trim());
    }
}
