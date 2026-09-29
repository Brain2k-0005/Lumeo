using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Textarea;

/// <summary>
/// Textarea used to write each keystroke straight into its <c>Value</c> parameter and
/// await the whole ValueChanged task, so a parent re-render carrying an older Value
/// overwrote the text, and an async handler held the circuit's event queue. Measured in a
/// real Blazor Server circuit (54-char sentence, 15 ms/key, handler doing
/// <c>await Task.Delay(30)</c>): 31-36 characters kept, 0/20 runs exact. It now keeps a
/// live value and follows Input's controlled / uncontrolled rules (ControlledValueEcho).
/// </summary>
public class TextareaAsyncEchoTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TextareaAsyncEchoTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static string? Text(IRenderedComponent<L.Textarea> cut) => cut.Find("textarea").GetAttribute("value");

    [Fact]
    public async Task StaleAsyncEcho_InEitherOrder_DoesNotClobberNewerText()
    {
        var gates = new Dictionary<string, TaskCompletionSource> { ["a"] = new(), ["ab"] = new(), ["abc"] = new() };
        var done = new Dictionary<string, TaskCompletionSource> { ["a"] = new(), ["ab"] = new(), ["abc"] = new() };
        string? stored = null;
        IRenderedComponent<L.Textarea>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            await gates[v!].Task;
            stored = v;
            cut!.Render(p => p.Add(i => i.Value, stored).Add(i => i.ValueChanged, callback));
            done[v!].SetResult();
        });

        cut = _ctx.Render<L.Textarea>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        var textarea = cut.Find("textarea");
        _ = textarea.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "a" });
        _ = textarea.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "ab" });
        _ = textarea.TriggerEventAsync("oninput", new ChangeEventArgs { Value = "abc" });
        Assert.Equal("abc", Text(cut));

        // Oldest first, then the newest, then the one in between arriving last.
        gates["a"].SetResult();
        await done["a"].Task;
        Assert.Equal("abc", Text(cut));

        gates["abc"].SetResult();
        await done["abc"].Task;
        Assert.Equal("abc", Text(cut));

        gates["ab"].SetResult();
        await done["ab"].Task;
        Assert.Equal("abc", Text(cut));
    }

    [Fact]
    public async Task ParentRerender_WithItsOldValue_WhileHandlerPending_KeepsTypedText()
    {
        var gate = new TaskCompletionSource();
        var done = new TaskCompletionSource();
        IRenderedComponent<L.Textarea>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, async (string? v) =>
        {
            await gate.Task;
            cut!.Render(p => p.Add(i => i.Value, v).Add(i => i.ValueChanged, callback));
            done.SetResult();
        });

        cut = _ctx.Render<L.Textarea>(p => p
            .Add(i => i.Value, "start")
            .Add(i => i.ValueChanged, callback));

        _ = cut.Find("textarea").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "startX" });
        cut.Render(p => p.Add(i => i.Value, "start").Add(i => i.ValueChanged, callback));
        Assert.Equal("startX", Text(cut));

        gate.SetResult();
        await done.Task;
        Assert.Equal("startX", Text(cut));
    }

    [Fact]
    public void SynchronousReject_RollsBack()
    {
        IRenderedComponent<L.Textarea>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, (string? _) =>
        {
            cut!.Render(p => p.Add(i => i.Value, "start").Add(i => i.ValueChanged, callback));
        });

        cut = _ctx.Render<L.Textarea>(p => p
            .Add(i => i.Value, "start")
            .Add(i => i.ValueChanged, callback));

        cut.Find("textarea").Input("start-rejected");

        Assert.Equal("start", Text(cut));
    }

    [Fact]
    public void Rejection_RenderedAfterTheEvent_RollsBack()
    {
        var cut = _ctx.Render<L.Textarea>(p => p
            .Add(i => i.Value, "start")
            .Add(i => i.ValueChanged, (string? _) => { }));

        cut.Find("textarea").Input("typed over start");
        cut.Render(p => p.Add(i => i.Value, "start").Add(i => i.ValueChanged, (string? _) => { }));

        Assert.Equal("start", Text(cut));
    }

    [Fact]
    public void SynchronousTransform_IsAdopted()
    {
        IRenderedComponent<L.Textarea>? cut = null;
        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, (string? v) =>
        {
            cut!.Render(p => p.Add(i => i.Value, v?.ToUpperInvariant()).Add(i => i.ValueChanged, callback));
        });

        cut = _ctx.Render<L.Textarea>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        cut.Find("textarea").Input("shout");

        Assert.Equal("SHOUT", Text(cut));
    }

    [Fact]
    public void ObserverOnly_NullValue_KeepsTypedText()
    {
        var cut = _ctx.Render<L.Textarea>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, (string? _) => { }));

        cut.Find("textarea").Input("hello");
        cut.Render(p => p.Add(i => i.Value, (string?)null).Add(i => i.ValueChanged, (string? _) => { }));

        Assert.Equal("hello", Text(cut));
    }

    [Fact]
    public void Uncontrolled_UnrelatedRerender_KeepsTypedText_ButARealChangeWins()
    {
        var cut = _ctx.Render<L.Textarea>(p => p.Add(i => i.Value, "seed"));

        cut.Find("textarea").Input("seed and more");
        cut.Render(p => p.Add(i => i.Value, "seed"));
        Assert.Equal("seed and more", Text(cut));

        cut.Render(p => p.Add(i => i.Value, "reset"));
        Assert.Equal("reset", Text(cut));
    }

    [Fact]
    public void Counter_Follows_The_Live_Text()
    {
        var cut = _ctx.Render<L.Textarea>(p => p
            .Add(i => i.ShowCount, true)
            .Add(i => i.MaxLength, 10)
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, (string? _) => { }));

        cut.Find("textarea").Input("hello");

        Assert.Contains("5/10", cut.Markup);
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
                builder.OpenComponent<L.Textarea>(0);
                builder.AddComponentParameter(1, nameof(L.Textarea.Value), (string?)null);
                builder.AddComponentParameter(2, nameof(L.Textarea.ValueChanged), handler);
                builder.CloseComponent();
            }))
            .Add(b => b.ErrorContent, (Exception ex) => $"<p id=\"err\">{ex.Message}</p>"));

        // The event must not wait for the async handler (and must not hang if it did).
        var dispatch = cut.Find("textarea").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "x" });
        Assert.True(dispatch.IsCompleted);
        await dispatch;
        Assert.Empty(cut.FindAll("#err"));

        gate.SetResult();
        await cut.WaitForAssertionAsync(() => Assert.Equal("boom from ValueChanged", cut.Find("#err").TextContent));
    }

    [Fact]
    public async Task SynchronousHandlerException_StillPropagatesFromTheEvent()
    {
        var cut = _ctx.Render<L.Textarea>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, (string? _) => throw new InvalidOperationException("sync boom")));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cut.Find("textarea").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "x" }));
        Assert.Equal("sync boom", ex.Message);
    }
}
