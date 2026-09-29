using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InputMask;

/// <summary>
/// InputMask counterpart to <c>Input.InputAsyncEchoTests</c>: InputMask shadows its bound
/// <c>Value</c> with its own live pair (<c>_rawValue</c>/<c>_displayValue</c>), so it had
/// the same lost-keystroke bug with a ValueChanged handler that stores the value only
/// after an await, and uses the same <c>ControlledValueEcho</c> rules.
/// </summary>
public class InputMaskAsyncEchoTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public InputMaskAsyncEchoTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string Mask = "AA"; // two letter slots -- enough for "a" / "ab"

    [Fact]
    public async Task StaleAsyncEchoOfOlderKeystroke_DoesNotClobberNewerOne()
    {
        var tcsA = new TaskCompletionSource();
        var tcsB = new TaskCompletionSource();
        string? stored = null;
        IRenderedComponent<L.InputMask>? cut = null;
        EventCallback<string?> callback = default;
        // Signaled by the callback itself once its post-await continuation has
        // run -- HandleInput no longer blocks its own completion on a
        // still-pending ValueChanged task (see AwaitOrObserve's remarks), so the
        // dispatch's own Task can no longer be used for this.
        var doneA = new TaskCompletionSource();
        var doneB = new TaskCompletionSource();

        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            if (v == "a") await tcsA.Task;
            else if (v == "ab") await tcsB.Task;
            stored = v;
            cut!.Render(p => p
                .Add(i => i.Mask, Mask)
                .Add(i => i.Value, stored)
                .Add(i => i.ValueChanged, callback));
            if (v == "a") doneA.SetResult();
            else if (v == "ab") doneB.SetResult();
        });

        cut = _ctx.Render<L.InputMask>(p => p
            .Add(i => i.Mask, Mask)
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        var input = cut.Find("input");

        _ = input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "a" });
        _ = input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "ab" });

        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));

        tcsA.SetResult();
        await doneA.Task;

        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));

        tcsB.SetResult();
        await doneB.Task;

        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));
        Assert.Equal("ab", stored);
    }

    [Fact]
    public void SynchronousReject_StillRollsBack()
    {
        IRenderedComponent<L.InputMask>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, (string? _) =>
        {
            cut!.Render(p => p
                .Add(i => i.Mask, Mask)
                .Add(i => i.Value, "ab")
                .Add(i => i.ValueChanged, callback));
        });

        cut = _ctx.Render<L.InputMask>(p => p
            .Add(i => i.Mask, Mask)
            .Add(i => i.Value, "ab")
            .Add(i => i.ValueChanged, callback));

        cut.Find("input").Input("ac");

        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task ExternalChangeAfterCatchUp_IsAdopted()
    {
        var tcs = new TaskCompletionSource();
        var done = new TaskCompletionSource();
        string? stored = null;
        IRenderedComponent<L.InputMask>? cut = null;
        EventCallback<string?> callback = default;

        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            await tcs.Task;
            stored = v;
            cut!.Render(p => p
                .Add(i => i.Mask, Mask)
                .Add(i => i.Value, stored)
                .Add(i => i.ValueChanged, callback));
            done.SetResult();
        });

        cut = _ctx.Render<L.InputMask>(p => p
            .Add(i => i.Mask, Mask)
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        // HandleInput no longer blocks its own completion on a still-pending
        // ValueChanged task, so `done` (not the dispatch's own Task) is what
        // tells us the callback's post-await continuation actually ran.
        _ = cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "ab" });
        tcs.SetResult();
        await done.Task;

        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));

        cut.Render(p => p
            .Add(i => i.Mask, Mask)
            .Add(i => i.Value, "zz")
            .Add(i => i.ValueChanged, callback));

        Assert.Equal("zz", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void PushHistory_Is_Bounded_So_A_Sufficiently_Old_Echo_Is_Adopted_Not_Ignored()
    {
        var longMask = new string('*', 70);
        var neverCompletes = new TaskCompletionSource();
        EventCallback<string?> callback = EventCallback.Factory.Create<string?>(_ctx, async (string? _) =>
        {
            await neverCompletes.Task;
        });

        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(i => i.Mask, longMask)
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        var input = cut.Find("input");
        for (var i = 0; i < 65; i++)
        {
            // Grow the raw text by one significant char per push (v0.."v" * 65)
            // so each push is distinguishable and fits the long mask.
            _ = input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = new string('v', i + 1) });
        }

        Assert.Equal(new string('v', 65), cut.Find("input").GetAttribute("value"));

        // The very first push ("v", a single char) has fallen out of the bounded
        // history -- a later echo of it is adopted rather than ignored.
        cut.Render(p => p
            .Add(i => i.Mask, longMask)
            .Add(i => i.Value, "v")
            .Add(i => i.ValueChanged, callback));

        Assert.Equal("v", cut.Find("input").GetAttribute("value"));

        neverCompletes.TrySetResult();
    }

    [Fact]
    public async Task OlderEcho_ArrivingAfterTheNewerEcho_IsIgnored()
    {
        var gates = new Dictionary<string, TaskCompletionSource> { ["a"] = new(), ["ab"] = new() };
        var done = new Dictionary<string, TaskCompletionSource> { ["a"] = new(), ["ab"] = new() };
        IRenderedComponent<L.InputMask>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            await gates[v!].Task;
            cut!.Render(p => p.Add(i => i.Mask, Mask).Add(i => i.Value, v).Add(i => i.ValueChanged, callback));
            done[v!].SetResult();
        });

        cut = _ctx.Render<L.InputMask>(p => p
            .Add(i => i.Mask, Mask)
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        var input = cut.Find("input");
        _ = input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "a" });
        _ = input.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "ab" });

        gates["ab"].SetResult();
        await done["ab"].Task;
        gates["a"].SetResult();
        await done["a"].Task;

        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task ParentRerender_WithItsOldValue_WhileHandlerPending_KeepsTypedText()
    {
        var gate = new TaskCompletionSource();
        var done = new TaskCompletionSource();
        IRenderedComponent<L.InputMask>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            // InputMask pushes after its caret interop call, so a parent's pre-await
            // re-render lands inside the ValueChanged call.
            cut!.Render(p => p.Add(i => i.Mask, Mask).Add(i => i.Value, "a").Add(i => i.ValueChanged, callback));
            await gate.Task;
            cut!.Render(p => p.Add(i => i.Mask, Mask).Add(i => i.Value, v).Add(i => i.ValueChanged, callback));
            done.SetResult();
        });

        cut = _ctx.Render<L.InputMask>(p => p
            .Add(i => i.Mask, Mask)
            .Add(i => i.Value, "a")
            .Add(i => i.ValueChanged, callback));

        _ = cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "ab" });
        cut.Render(p => p.Add(i => i.Mask, Mask).Add(i => i.Value, "a").Add(i => i.ValueChanged, callback));
        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));

        gate.SetResult();
        await done.Task;
        Assert.Equal("ab", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task AsyncHandlerException_ReachesTheEnclosingErrorBoundary()
    {
        var gate = new TaskCompletionSource();
        var handler = EventCallback.Factory.Create<string?>(_ctx, async (string? _) =>
        {
            await gate.Task;
            throw new InvalidOperationException("boom from ValueChanged");
        });

        var cut = _ctx.Render<ErrorBoundary>(p => p
            .Add(b => b.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<L.InputMask>(0);
                builder.AddComponentParameter(1, nameof(L.InputMask.Mask), Mask);
                builder.AddComponentParameter(2, nameof(L.InputMask.Value), (string?)null);
                builder.AddComponentParameter(3, nameof(L.InputMask.ValueChanged), handler);
                builder.CloseComponent();
            }))
            .Add(b => b.ErrorContent, (Exception ex) => $"<p id=\"err\">{ex.Message}</p>"));

        // The event must not wait for the async handler (and must not hang if it did).
        var dispatch = cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "a" });
        Assert.True(dispatch.IsCompleted);
        await dispatch;
        Assert.Empty(cut.FindAll("#err"));

        gate.SetResult();
        await cut.WaitForAssertionAsync(() => Assert.Equal("boom from ValueChanged", cut.Find("#err").TextContent));
    }
}
