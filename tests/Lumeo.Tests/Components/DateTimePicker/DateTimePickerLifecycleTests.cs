using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

/// <summary>
/// Battle-test LIFECYCLE regression for DateTimePicker.
///
/// n=148 — the time-column prevent-default-keys handlers are registered on the
/// popover-open transition; the SECONDS column is registered only `if (ShowSeconds)`.
/// The teardown (UnregisterNavKeys, reached on close AND on DisposeAsync) used to
/// gate the seconds-column unregister on the LIVE ShowSeconds. So toggling
/// ShowSeconds false while the popover stayed open left the seconds column's
/// handler-map entry registered forever — a leak of the generated column id.
///
/// The fix snapshots whether the seconds column was registered (_secondsNavRegistered
/// at open time) and unregisters off that snapshot, not the live ShowSeconds.
///
/// These assert on the recorded JSInterop register/unregister calls (loose mock),
/// mirroring DateTimePickerKeyboardNavTests.
/// </summary>
public class DateTimePickerLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DateTimePickerLifecycleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DateTimePicker> RenderOpen(
        Action<ComponentParameterCollectionBuilder<L.DateTimePicker>>? extra = null)
    {
        var cut = _ctx.Render<L.DateTimePicker>(p =>
        {
            p.Add(c => c.Use24Hour, true);
            p.Add(c => c.ShowSeconds, true);
            extra?.Invoke(p);
        });
        cut.Find("button[type='button']").Click(); // open the popover
        return cut;
    }

    // The third listbox (index 2) is the seconds column when ShowSeconds is true:
    // hours = 0, minutes = 1, seconds = 2.
    private string SecondsColumnId(IRenderedComponent<L.DateTimePicker> cut)
        => cut.FindAll("[role='listbox']")[2].GetAttribute("id")!;

    private bool Recorded(string identifier, string elementId) => _ctx.JSInterop.Invocations
        .Any(i => i.Identifier == identifier
            && i.Arguments.Count > 0 && (i.Arguments[0] as string) == elementId);

    [Fact]
    public void Opening_With_ShowSeconds_Registers_The_Seconds_Column()
    {
        var cut = RenderOpen();
        var secondsColId = SecondsColumnId(cut);

        Assert.True(Recorded("registerPreventDefaultKeys", secondsColId),
            "seconds column should register its nav-key prevent-default handler on open");
    }

    [Fact]
    public async Task Disposing_After_ShowSeconds_Toggled_Off_Still_Unregisters_The_Seconds_Column()
    {
        var cut = RenderOpen();
        var secondsColId = SecondsColumnId(cut);
        Assert.True(Recorded("registerPreventDefaultKeys", secondsColId)); // sanity: it was registered

        // Toggle ShowSeconds off WHILE the popover stays open. The seconds column
        // stops rendering, but its JS handler-map entry was registered on open and
        // must still be torn down on teardown.
        cut.Render(p =>
        {
            p.Add(c => c.Use24Hour, true);
            p.Add(c => c.ShowSeconds, false);
        });

        await cut.Instance.DisposeAsync();

        // Pre-fix: UnregisterNavKeys read the live ShowSeconds (now false) and skipped
        // the seconds column — no unregister was ever recorded => leak. Post-fix the
        // captured _secondsNavRegistered snapshot drives the unregister.
        Assert.True(Recorded("unregisterPreventDefaultKeys", secondsColId),
            "the seconds column registered on open must be unregistered on dispose even after ShowSeconds was toggled off");
    }

    [Fact]
    public async Task Dispose_While_Open_Does_Not_Throw()
    {
        var cut = RenderOpen();

        var ex = await Record.ExceptionAsync(async () => await cut.Instance.DisposeAsync());

        Assert.Null(ex);
    }
}
