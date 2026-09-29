using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Input;

/// <summary>
/// Regression coverage for the lost-keystroke bug: a controlled <see cref="L.Input"/>
/// whose <c>ValueChanged</c> handler stores the value only AFTER an <c>await</c> can
/// have its late re-render arrive carrying an OLDER value than one the user has since
/// typed (a faster/later keystroke's own push already went out and was accepted).
/// Before the fix, OnParametersSet only compared the incoming Value against the
/// SINGLE latest <c>_lastPushed</c>, so that stale echo looked like a genuine
/// external change and was adopted — silently reverting (and re-displaying) an
/// older value, clobbering what the user had already typed.
///
/// Measured before the fix: typing a 54-char sentence at 15ms/key against a
/// ValueChanged handler that does <c>await Task.Delay(30)</c> before storing kept
/// only 34-39 of the 54 characters, in a real Blazor Server circuit at both 0ms and
/// 150ms simulated RTT.
///
/// The fix tracks an ordered <c>_pushHistory</c> of every value this component has
/// pushed since the parent last caught up, plus a <c>_dispatching</c> flag that is
/// true only for the synchronous portion of a push. A parameter set while
/// dispatching is the parent's synchronous, authoritative reaction (today's exact
/// accept/reject/transform semantics apply). A parameter set while NOT dispatching
/// is checked against the push history: an OLDER entry is a stale async echo
/// (ignored), the newest entry means caught up, and anything else is a genuine
/// external change (adopted).
/// </summary>
public class InputAsyncEchoTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InputAsyncEchoTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task StaleAsyncEchoOfOlderKeystroke_DoesNotClobberNewerOne()
    {
        var tcsA = new TaskCompletionSource();
        var tcsB = new TaskCompletionSource();
        // Signaled by the callback itself once its post-await work (storing the
        // value + re-rendering) is done. HandleInput deliberately does NOT block
        // its own completion on a still-pending ValueChanged task (see
        // Input.razor's AwaitOrObserve remarks — blocking there would backlog
        // Blazor Server's per-circuit event queue at real typing speed), so the
        // Task TriggerEventAsync returns can no longer be used to know the
        // callback's continuation has run; these dedicated signals stand in for
        // that.
        var doneA = new TaskCompletionSource();
        var doneB = new TaskCompletionSource();
        string? stored = null;
        IRenderedComponent<L.Input>? cut = null;
        EventCallback<string?> callback = default;

        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            // Mirrors the real bug: the handler stores the value only AFTER an
            // await, so a slower-to-arrive-but-earlier-pushed keystroke's storage
            // can complete (and re-render the parent) later than a faster one.
            if (v == "a") await tcsA.Task;
            else if (v == "ab") await tcsB.Task;
            stored = v;
            cut!.Render(p => p
                .Add(i => i.Value, stored)
                .Add(i => i.ValueChanged, callback));
            if (v == "a") doneA.SetResult();
            else if (v == "ab") doneB.SetResult();
        });

        cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        var input = cut.Find("input");

        // Raise "a" (first handler starts, suspends on tcsA), then "ab" while the
        // first handler's TaskCompletionSource is still pending (second handler
        // starts, suspends on tcsB) — exactly the out-of-order sequence from the
        // bug report.
        _ = input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "a" });
        _ = input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "ab" });

        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));

        // Complete the FIRST (older) push's handler first — its late echo carries
        // the STALE value "a", which must be ignored.
        tcsA.SetResult();
        await doneA.Task;

        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));

        // Now the second (newer) push's handler catches up with "ab" — this must
        // be recognised as caught-up, not as yet another external change.
        tcsB.SetResult();
        await doneB.Task;

        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));
        Assert.Equal("ab", stored);
    }

    [Fact]
    public void SynchronousReject_StillRollsBack()
    {
        // A parent that reacts SYNCHRONOUSLY (no await) and keeps its own value
        // must still win — this is the "dispatching" branch, unchanged from
        // today's semantics.
        IRenderedComponent<L.Input>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, (string? _) =>
        {
            cut!.Render(p => p
                .Add(i => i.Value, "start")
                .Add(i => i.ValueChanged, callback));
        });

        cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, "start")
            .Add(i => i.ValueChanged, callback));

        cut.Find("input").Input("start-rejected");

        Assert.Equal("start", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void SynchronousTransform_IsAdopted()
    {
        // A parent that synchronously uppercases the pushed value must have that
        // transform reflected in the display.
        IRenderedComponent<L.Input>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, (string? v) =>
        {
            cut!.Render(p => p
                .Add(i => i.Value, v?.ToUpperInvariant())
                .Add(i => i.ValueChanged, callback));
        });

        cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        cut.Find("input").Input("shout");

        Assert.Equal("SHOUT", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task ExternalChangeAfterCatchUp_IsAdopted()
    {
        // Once the parent has genuinely caught up to the last pushed value, a
        // LATER, unrelated external change (e.g. a reset button) must still win.
        var tcs = new TaskCompletionSource();
        var done = new TaskCompletionSource();
        string? stored = null;
        IRenderedComponent<L.Input>? cut = null;
        EventCallback<string?> callback = default;

        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            await tcs.Task;
            stored = v;
            cut!.Render(p => p
                .Add(i => i.Value, stored)
                .Add(i => i.ValueChanged, callback));
            done.SetResult();
        });

        cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        // HandleInput does not block its own completion on a still-pending
        // ValueChanged task (see AwaitOrObserve's remarks), so `done` -- not the
        // dispatch's own Task -- is what tells us the callback's post-await
        // continuation actually ran.
        _ = cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "typed" });
        tcs.SetResult();
        await done.Task;

        Assert.Equal("typed", cut.Find("input").GetAttribute("value"));
        Assert.Equal("typed", stored);

        // A totally unrelated external reset, well after catch-up.
        cut.Render(p => p
            .Add(i => i.Value, "reset")
            .Add(i => i.ValueChanged, callback));

        Assert.Equal("reset", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Clear_StillWorks()
    {
        string? lastPushed = "unset";
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Clearable, true)
            .Add(i => i.Value, "hello")
            .Add(i => i.ValueChanged, (string? v) => lastPushed = v));

        cut.Find("button[aria-label='Clear']").Click();

        Assert.Equal("", cut.Find("input").GetAttribute("value"));
        Assert.Equal("", lastPushed);
    }

    [Fact]
    public void PushHistory_Is_Bounded_So_A_Sufficiently_Old_Echo_Is_Adopted_Not_Ignored()
    {
        // Cap is ~64: push 65 values with none of their handlers ever completing
        // (so nothing ever "catches up" and clears the history), which evicts the
        // very first one. A later echo of that evicted value is then
        // indistinguishable from a genuine external change and IS adopted — the
        // documented, deliberate limit of the bounded history.
        var neverCompletes = new TaskCompletionSource();
        EventCallback<string?> callback = EventCallback.Factory.Create<string?>(_ctx, async (string? _) =>
        {
            await neverCompletes.Task;
        });

        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        var input = cut.Find("input");
        for (var i = 0; i < 65; i++)
        {
            _ = input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = $"v{i}" });
        }

        Assert.Equal("v64", cut.Find("input").GetAttribute("value"));

        // v0 has fallen out of the bounded history.
        cut.Render(p => p
            .Add(i => i.Value, "v0")
            .Add(i => i.ValueChanged, callback));

        Assert.Equal("v0", cut.Find("input").GetAttribute("value"));

        neverCompletes.TrySetResult();
    }
}
