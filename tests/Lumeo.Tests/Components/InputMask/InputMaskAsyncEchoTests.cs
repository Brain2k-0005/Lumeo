using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InputMask;

/// <summary>
/// InputMask counterpart to <c>Input.InputAsyncEchoTests</c>: InputMask shadows its
/// bound <c>Value</c> with its own live pair (<c>_rawValue</c>/<c>_displayValue</c>)
/// plus a <c>_lastPushed</c>/<c>_lastValueParam</c> controlled/uncontrolled guard —
/// the exact same pattern Input had, so it carries the exact same lost-keystroke bug
/// when its ValueChanged handler stores the value only after an await, and gets the
/// same fix (a bounded <c>_pushHistory</c> plus a <c>_dispatching</c> flag).
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
}
