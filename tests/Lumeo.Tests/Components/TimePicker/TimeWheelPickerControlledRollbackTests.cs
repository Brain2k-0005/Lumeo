using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TimePicker;

/// <summary>
/// Regression tests for the controlled-component rollback fix on TimeWheelPicker.
/// When the picker is used in controlled mode (ValueChanged bound back to a real
/// Value) and the parent vetoes a wheel commit by re-rendering with the original
/// Value unchanged, both the highlighted hour/minute cells AND the column scroll
/// position must roll back to the bound value rather than keeping the
/// optimistically-committed selection. Mirrors SwitchControlledRollbackTests.
///
/// Drives commits through the CommitSelectionForTest seam (see
/// TimeWheelPickerUncontrolledCommitTests) since the real scroll path depends on
/// JS scroll offsets + a timer debounce bUnit cannot reproduce.
/// </summary>
public class TimeWheelPickerControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public TimeWheelPickerControlledRollbackTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const int ItemHeight = 40; // must match TimeWheelPicker's h-10 rows

    // --- Controlled: veto rolls back both the highlight and the scroll position ---

    [Fact]
    public async Task Controlled_Veto_Rolls_Back_Highlighted_Selection_And_RescrollsColumns()
    {
        // Parent starts bound at 10:00 and vetoes every commit by re-rendering
        // with its own state unchanged (always re-renders with Value=10:00).
        var parentState = new TimeSpan(10, 0, 0);
        IRenderedComponent<L.TimeWheelPicker>? cut = null;

        var callback = EventCallback.Factory.Create<TimeSpan?>(_ctx, (TimeSpan? incoming) =>
        {
            // Veto: do NOT adopt `incoming` into parentState; re-render with the
            // original bound value, exactly as a controlled parent would when it
            // rejects the change (e.g. failed validation).
            cut!.Render(p =>
            {
                p.Add(c => c.Use24Hour, true);
                p.Add(c => c.Value, parentState);   // still 10:00
                p.Add(c => c.ValueChanged, EventCallback.Factory.Create<TimeSpan?>(_ctx, (_) => { }));
            });
        });

        cut = _ctx.Render<L.TimeWheelPicker>(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, parentState)
            .Add(c => c.ValueChanged, callback));

        cut.WaitForAssertion(() => Assert.Equal(2, _interop.WheelScrollToCallCount));
        Assert.Contains(cut.FindAll(".font-semibold"), e => e.TextContent.Trim() == "10");

        // User settles the wheels on 11:00 — drives the same RaiseChange commit
        // the debounced scroll handlers perform. The parent vetoes via the
        // callback above.
        await cut.InvokeAsync(() => cut.Instance.CommitSelectionForTest(new TimeSpan(11, 0, 0)));

        // After the veto the highlighted hour must have rolled back to 10, not
        // stayed at the optimistically-committed 11.
        var highlighted = cut.FindAll(".font-semibold");
        Assert.Contains(highlighted, e => e.TextContent.Trim() == "10");
        Assert.DoesNotContain(highlighted, e => e.TextContent.Trim() == "11");

        // The wheel columns must also have been re-scrolled back to 10:00 — not
        // left stranded at the rejected 11:00 position.
        cut.WaitForAssertion(() => Assert.Equal(4, _interop.WheelScrollToCallCount));
        Assert.Equal(10 * ItemHeight, _interop.WheelScrollToTops[2]); // hour 10
        Assert.Equal(0 * ItemHeight, _interop.WheelScrollToTops[3]);  // minute 00
    }

    // --- Controlled: accepted commit keeps the new value, no redundant re-scroll ---

    [Fact]
    public async Task Controlled_Accepted_Commit_Keeps_New_Value_Without_Redundant_Rescroll()
    {
        // Parent accepts every commit by writing the emitted value back into its
        // own bound state and re-rendering with it.
        var parentState = new TimeSpan(10, 0, 0);
        IRenderedComponent<L.TimeWheelPicker>? cut = null;

        EventCallback<TimeSpan?> callback = default;
        callback = EventCallback.Factory.Create<TimeSpan?>(_ctx, (TimeSpan? incoming) =>
        {
            parentState = incoming ?? parentState;
            cut!.Render(p =>
            {
                p.Add(c => c.Use24Hour, true);
                p.Add(c => c.Value, parentState);
                p.Add(c => c.ValueChanged, callback);
            });
        });

        cut = _ctx.Render<L.TimeWheelPicker>(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, parentState)
            .Add(c => c.ValueChanged, callback));

        cut.WaitForAssertion(() => Assert.Equal(2, _interop.WheelScrollToCallCount));

        await cut.InvokeAsync(() => cut.Instance.CommitSelectionForTest(new TimeSpan(11, 0, 0)));

        // Parent accepted — the highlighted hour should now be 11.
        Assert.Contains(cut.FindAll(".font-semibold"), e => e.TextContent.Trim() == "11");

        // The accepted echo must NOT trigger a redundant programmatic re-scroll
        // (we're already positioned there from the user's own commit).
        Assert.Equal(2, _interop.WheelScrollToCallCount);
    }

    // --- Controlled: a genuine external reset after a commit is still adopted ---

    [Fact]
    public async Task Controlled_Programmatic_Reset_After_Commit_Is_Adopted_And_Rescrolls()
    {
        // Start bound at 10:00 with a real two-way binding (callback writes
        // back), commit to 11:00 (accepted), then the parent independently
        // resets the bound value to 09:30 WITHOUT a further user commit
        // (simulates an external data reload / form reset).
        var parentState = new TimeSpan(10, 0, 0);
        IRenderedComponent<L.TimeWheelPicker>? cut = null;

        EventCallback<TimeSpan?> callback = default;
        callback = EventCallback.Factory.Create<TimeSpan?>(_ctx, (TimeSpan? incoming) =>
        {
            parentState = incoming ?? parentState;
            cut!.Render(p =>
            {
                p.Add(c => c.Use24Hour, true);
                p.Add(c => c.Value, parentState);
                p.Add(c => c.ValueChanged, callback);
            });
        });

        cut = _ctx.Render<L.TimeWheelPicker>(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, parentState)
            .Add(c => c.ValueChanged, callback));
        cut.WaitForAssertion(() => Assert.Equal(2, _interop.WheelScrollToCallCount));

        await cut.InvokeAsync(() => cut.Instance.CommitSelectionForTest(new TimeSpan(11, 0, 0)));
        var scrollsAfterCommit = _interop.WheelScrollToCallCount;

        // External reset, independent of any wheel commit.
        cut.Render(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, new TimeSpan(9, 30, 0))
            .Add(c => c.ValueChanged, callback));

        cut.WaitForAssertion(() => Assert.Equal(scrollsAfterCommit + 2, _interop.WheelScrollToCallCount));
        Assert.Contains(cut.FindAll(".font-semibold"), e => e.TextContent.Trim() == "09");
        Assert.Contains(cut.FindAll(".font-semibold"), e => e.TextContent.Trim() == "30");
    }

    // --- Controlled: a GENUINE clear-to-null veto (Value WAS non-null, parent explicitly nulls it
    //     out) rolls back — distinct from the #68 "Value has always been null" observer-only case,
    //     which intentionally keeps the local commit (round-16 Codex finding) ---

    [Fact]
    public async Task Controlled_Genuine_Clear_To_Null_After_Commit_Resets_The_Wheel()
    {
        // Start bound at a real, non-null 10:00 with a genuine two-way binding. Commit to 11:00, then
        // the parent's handler explicitly REJECTS by clearing its own state to null and re-rendering
        // with Value=null — a real, distinguishable decision (Value demonstrably HAD a value and now
        // demonstrably doesn't), not an ambiguous "hasn't propagated" re-render.
        var parentState = (TimeSpan?)new TimeSpan(10, 0, 0);
        IRenderedComponent<L.TimeWheelPicker>? cut = null;

        var callback = EventCallback.Factory.Create<TimeSpan?>(_ctx, (TimeSpan? incoming) =>
        {
            parentState = null; // genuine veto: clear, don't adopt `incoming`
            cut!.Render(p =>
            {
                p.Add(c => c.Use24Hour, true);
                p.Add(c => c.Value, parentState);
                p.Add(c => c.ValueChanged, EventCallback.Factory.Create<TimeSpan?>(_ctx, (_) => { }));
            });
        });

        cut = _ctx.Render<L.TimeWheelPicker>(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, parentState)
            .Add(c => c.ValueChanged, callback));
        cut.WaitForAssertion(() => Assert.Equal(2, _interop.WheelScrollToCallCount));
        Assert.Contains(cut.FindAll(".font-semibold"), e => e.TextContent.Trim() == "10");

        await cut.InvokeAsync(() => cut.Instance.CommitSelectionForTest(new TimeSpan(11, 0, 0)));

        // After the genuine clear, the wheel must NOT keep showing the rejected 11:00 commit — it
        // resets to the TimeSpan.Zero default (hour 00), not the stale optimistic value.
        var highlighted = cut.FindAll(".font-semibold");
        Assert.DoesNotContain(highlighted, e => e.TextContent.Trim() == "11");
        Assert.Contains(highlighted, e => e.TextContent.Trim() == "00");
    }
}
