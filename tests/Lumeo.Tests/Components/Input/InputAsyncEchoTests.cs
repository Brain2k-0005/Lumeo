using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Input;

/// <summary>
/// Regression coverage for the lost-keystroke bug: a controlled <see cref="L.Input"/>
/// whose <c>ValueChanged</c> handler stores the value only AFTER an <c>await</c> gets
/// re-rendered late, and possibly out of order, with values it has already moved past.
/// Adopting such a stale echo overwrote what the user had typed since.
///
/// Measured in a real Blazor Server circuit (54-char sentence, 15 ms/key, handler doing
/// <c>await Task.Delay(30)</c>): origin/master kept 34-39 characters. The first fix
/// (history cleared on catch-up) kept 51-54 and 0/20 exact once the parent also passed a
/// RenderFragment. The rules now live in <c>ControlledValueEcho</c>: a pushed value is an
/// echo for as long as the handler that received it is still running; an unchanged Value
/// while a push is pending is not a verdict; a synchronous handler's verdict applies as
/// before.
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

    [Fact]
    public async Task OlderEcho_ArrivingAfterTheNewerEcho_IsIgnored()
    {
        // The residual loss seen in the E2E probe: two handlers whose awaits finish in the
        // same timer tick run their continuations newest-first. The newer echo lands (and
        // looks like "caught up"), then the older one follows. It must not be adopted.
        var gates = new Dictionary<string, TaskCompletionSource> { ["a"] = new(), ["ab"] = new() };
        var done = new Dictionary<string, TaskCompletionSource> { ["a"] = new(), ["ab"] = new() };
        string? stored = null;
        IRenderedComponent<L.Input>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            await gates[v!].Task;
            stored = v;
            cut!.Render(p => p.Add(i => i.Value, stored).Add(i => i.ValueChanged, callback));
            done[v!].SetResult();
        });

        cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        var input = cut.Find("input");
        _ = input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "a" });
        _ = input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "ab" });

        gates["ab"].SetResult();
        await done["ab"].Task;
        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));

        gates["a"].SetResult();
        await done["a"].Task;
        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task ParentRerender_WithItsOldValue_WhileHandlerPending_KeepsTypedText()
    {
        // A parent passing a RenderFragment (or any parameter Blazor can't prove unchanged)
        // hands Input its parameters on every render, including the one it makes right
        // after the handler's first await, still carrying its OLD Value. That render is
        // not a rejection.
        var gate = new TaskCompletionSource();
        var done = new TaskCompletionSource();
        string? stored = "start";
        IRenderedComponent<L.Input>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            await gate.Task;
            stored = v;
            cut!.Render(p => p.Add(i => i.Value, stored).Add(i => i.ValueChanged, callback));
            done.SetResult();
        });

        cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, stored)
            .Add(i => i.ValueChanged, callback));

        _ = cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "startX" });
        cut.Render(p => p.Add(i => i.Value, "start").Add(i => i.ValueChanged, callback));
        Assert.Equal("startX", cut.Find("input").GetAttribute("value"));

        gate.SetResult();
        await done.Task;
        Assert.Equal("startX", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task ParentRerender_InsideDispatch_BeforeItsFirstAwait_KeepsTypedText()
    {
        // Same as above, but the old-Value render happens synchronously inside the
        // ValueChanged call (as it does when the push itself runs after an await, e.g. with
        // an async OnInput). Only once InvokeAsync returns a pending task is it known not
        // to be a synchronous rejection.
        var gate = new TaskCompletionSource();
        var done = new TaskCompletionSource();
        IRenderedComponent<L.Input>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            cut!.Render(p => p.Add(i => i.Value, "start").Add(i => i.ValueChanged, callback));
            await gate.Task;
            cut!.Render(p => p.Add(i => i.Value, v).Add(i => i.ValueChanged, callback));
            done.SetResult();
        });

        cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, "start")
            .Add(i => i.ValueChanged, callback));

        _ = cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "startX" });
        Assert.Equal("startX", cut.Find("input").GetAttribute("value"));

        gate.SetResult();
        await done.Task;
        Assert.Equal("startX", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task OnceTheHandlerFinished_AnOlderPushedValue_IsAdopted()
    {
        // A pushed value only counts as an echo while its handler runs. Afterwards the
        // parent re-supplying it is its real state (e.g. an async rejection back to it).
        var gate = new TaskCompletionSource();
        var done = new TaskCompletionSource();
        IRenderedComponent<L.Input>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            if (v == "ab") await gate.Task;
            cut!.Render(p => p.Add(i => i.Value, v).Add(i => i.ValueChanged, callback));
            if (v == "ab") done.SetResult();
        });

        cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        var input = cut.Find("input");
        await input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "a" });
        _ = input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "ab" });
        gate.SetResult();
        await done.Task;
        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));

        cut.Render(p => p.Add(i => i.Value, "a").Add(i => i.ValueChanged, callback));
        Assert.Equal("a", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task AsyncHandlerException_ReachesTheEnclosingErrorBoundary()
    {
        // HandleInput does not await a pending ValueChanged task, so an exception the
        // handler throws after its await must still reach Blazor's error handling (here an
        // ErrorBoundary), not disappear into a background task.
        var gate = new TaskCompletionSource();
        var handler = EventCallback.Factory.Create<string?>(_ctx, async (string? _) =>
        {
            await gate.Task;
            throw new InvalidOperationException("boom from ValueChanged");
        });

        var cut = _ctx.Render<ErrorBoundary>(p => p
            .Add(b => b.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<L.Input>(0);
                builder.AddComponentParameter(1, nameof(L.Input.Value), (string?)null);
                builder.AddComponentParameter(2, nameof(L.Input.ValueChanged), handler);
                builder.CloseComponent();
            }))
            .Add(b => b.ErrorContent, (Exception ex) => $"<p id=\"err\">{ex.Message}</p>"));

        // The event must not wait for the async handler (and must not hang if it did).
        var dispatch = cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "x" });
        Assert.True(dispatch.IsCompleted);
        await dispatch;
        Assert.Empty(cut.FindAll("#err"));

        gate.SetResult();
        await cut.WaitForAssertionAsync(() => Assert.Equal("boom from ValueChanged", cut.Find("#err").TextContent));
    }

    [Fact]
    public async Task AsyncHandlerCancellation_IsNotReportedAsAnError()
    {
        // Blazor ignores a cancelled event-handler task; the background observer does too.
        var gate = new TaskCompletionSource();
        var finished = new TaskCompletionSource();
        var handler = EventCallback.Factory.Create<string?>(_ctx, async (string? _) =>
        {
            try
            {
                await gate.Task;
                throw new OperationCanceledException();
            }
            finally
            {
                finished.SetResult();
            }
        });

        var cut = _ctx.Render<ErrorBoundary>(p => p
            .Add(b => b.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<L.Input>(0);
                builder.AddComponentParameter(1, nameof(L.Input.Value), (string?)null);
                builder.AddComponentParameter(2, nameof(L.Input.ValueChanged), handler);
                builder.CloseComponent();
            }))
            .Add(b => b.ErrorContent, (Exception ex) => $"<p id=\"err\">{ex.Message}</p>"));

        // The event must not wait for the async handler (and must not hang if it did).
        var dispatch = cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "x" });
        Assert.True(dispatch.IsCompleted);
        await dispatch;
        gate.SetResult();
        await finished.Task;
        await cut.InvokeAsync(() => { });

        Assert.Empty(cut.FindAll("#err"));
        Assert.Equal("x", cut.Find("input").GetAttribute("value"));
    }
}
