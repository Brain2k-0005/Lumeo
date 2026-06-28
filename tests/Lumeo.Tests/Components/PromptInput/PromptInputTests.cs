using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Lumeo.Tests.Components.PromptInput;

public class PromptInputTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _module;

    public PromptInputTests()
    {
        // The lib appends ?v=<assembly-version> to the components.js import URL;
        // mirror it (RatingTests pattern) so this handle captures the module-scoped
        // ai.autosize invocations the component issues through the interop service.
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        _module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        _module.Mode = JSRuntimeMode.Loose;

        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Textarea_With_Placeholder()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Placeholder, "Type here..."));

        var ta = cut.Find("textarea");
        Assert.Equal("Type here...", ta.GetAttribute("placeholder"));
    }

    [Fact]
    public void Default_Placeholder_Is_Ask_Anything()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>();

        Assert.Equal("Ask anything\u2026", cut.Find("textarea").GetAttribute("placeholder"));
    }

    [Fact]
    public void Value_Is_Bound_To_Textarea()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Value, "Hello"));

        Assert.Equal("Hello", cut.Find("textarea").GetAttribute("value"));
    }

    [Fact]
    public async Task ValueChanged_Fires_On_Input()
    {
        string? captured = null;
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => captured = v)));

        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "new text" });

        Assert.Equal("new text", captured);
    }

    [Fact]
    public void IsLoading_Disables_Textarea()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.IsLoading, true));

        Assert.True(cut.Find("textarea").HasAttribute("disabled"));
    }

    [Fact]
    public void DisableSendOnEmpty_True_Disables_Send_When_Empty()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.DisableSendOnEmpty, true));

        var btn = cut.Find("button[aria-label='Send']");
        Assert.True(btn.HasAttribute("disabled"));
    }

    [Fact]
    public void DisableSendOnEmpty_False_Enables_Send_When_Empty()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.DisableSendOnEmpty, false));

        var btn = cut.Find("button[aria-label='Send']");
        Assert.False(btn.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Enter_Triggers_OnSend()
    {
        string? sent = null;
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Value, "hi")
            .Add(x => x.OnSend, EventCallback.Factory.Create<string>(this, v => sent = v)));

        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("hi", sent);
    }

    [Fact]
    public async Task Shift_Enter_Does_Not_Trigger_OnSend()
    {
        var fired = false;
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Value, "hi")
            .Add(x => x.OnSend, EventCallback.Factory.Create<string>(this, _ => fired = true)));

        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", ShiftKey = true });

        Assert.False(fired);
    }

    [Fact]
    public async Task Send_Button_Click_Triggers_OnSend()
    {
        string? sent = null;
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Value, "msg")
            .Add(x => x.OnSend, EventCallback.Factory.Create<string>(this, v => sent = v)));

        await cut.Find("button[aria-label='Send']").ClickAsync(new MouseEventArgs());

        Assert.Equal("msg", sent);
    }

    [Fact]
    public void LeadingContent_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.LeadingContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='lead'>L</span>"))));

        Assert.NotNull(cut.Find("[data-testid='lead']"));
    }

    [Fact]
    public void TrailingContent_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.TrailingContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='trail'>T</span>"))));

        Assert.NotNull(cut.Find("[data-testid='trail']"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Class, "pi-x"));

        Assert.Contains("pi-x", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "pi"
            }));

        Assert.Equal("pi", cut.Find("div").GetAttribute("data-testid"));
    }

    // ── #304: Stop button ────────────────────────────────────────────────────

    [Fact]
    public void Loading_With_OnStop_Renders_Stop_Button_Not_Send()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.OnStop, EventCallback.Factory.Create(this, () => { })));

        Assert.NotNull(cut.Find("button[aria-label='Stop']"));
        Assert.Empty(cut.FindAll("button[aria-label='Send']"));
    }

    [Fact]
    public void Loading_Without_OnStop_Keeps_Disabled_Send()
    {
        // Back-compat: no Stop handler → original disabled-spinner Send button.
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.IsLoading, true));

        Assert.Empty(cut.FindAll("button[aria-label='Stop']"));
        Assert.True(cut.Find("button[aria-label='Send']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Stop_Button_Click_Invokes_OnStop()
    {
        var stopped = false;
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.IsLoading, true)
            .Add(x => x.OnStop, EventCallback.Factory.Create(this, () => stopped = true)));

        await cut.Find("button[aria-label='Stop']").ClickAsync(new MouseEventArgs());

        Assert.True(stopped);
    }

    [Fact]
    public void Not_Loading_Renders_Send_Even_With_OnStop_Bound()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.IsLoading, false)
            .Add(x => x.OnStop, EventCallback.Factory.Create(this, () => { })));

        Assert.NotNull(cut.Find("button[aria-label='Send']"));
        Assert.Empty(cut.FindAll("button[aria-label='Stop']"));
    }

    // ── #304: attachments ────────────────────────────────────────────────────

    [Fact]
    public void Attachments_Slot_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Attachments, (RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='att'>file.png</span>"))));

        Assert.NotNull(cut.Find("[data-testid='att']"));
    }

    [Fact]
    public void Attach_Button_Renders_When_Enabled()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.ShowAttachButton, true));

        Assert.NotNull(cut.Find("button[aria-label='Attach file']"));
    }

    [Fact]
    public void Attach_Button_Hidden_By_Default()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>();

        Assert.Empty(cut.FindAll("button[aria-label='Attach file']"));
    }

    [Fact]
    public async Task Attach_Button_Click_Invokes_OnAttach()
    {
        var attached = false;
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.ShowAttachButton, true)
            .Add(x => x.OnAttach, EventCallback.Factory.Create(this, () => attached = true)));

        await cut.Find("button[aria-label='Attach file']").ClickAsync(new MouseEventArgs());

        Assert.True(attached);
    }

    // ── #304: character counter ──────────────────────────────────────────────

    [Fact]
    public void Counter_Hidden_By_Default()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p.Add(x => x.Value, "hello"));

        // No counter element rendered when neither MaxLength nor ShowCounter set.
        Assert.DoesNotContain("aria-live=\"polite\"", cut.Markup);
    }

    [Fact]
    public void Counter_Shows_Count_Over_Limit_When_MaxLength_Set()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Value, "hello")
            .Add(x => x.MaxLength, 100));

        Assert.Contains("5/100", cut.Markup);
        Assert.Equal("100", cut.Find("textarea").GetAttribute("maxlength"));
    }

    [Fact]
    public void Counter_Turns_Destructive_Near_Limit()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Value, new string('a', 96))
            .Add(x => x.MaxLength, 100));

        // 96/100 is within the last 10% → destructive color class applied.
        var counter = cut.FindAll("span").First(s => (s.TextContent ?? "").Contains("96/100"));
        Assert.Contains("text-destructive", counter.GetAttribute("class"));
    }

    [Fact]
    public void ShowCounter_Shows_Raw_Count_Without_MaxLength()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Value, "abc")
            .Add(x => x.ShowCounter, true));

        Assert.Contains(">3<", cut.Markup);
        Assert.False(cut.Find("textarea").HasAttribute("maxlength"));
    }

    // ── Wave 3 #16: external Value change re-runs autosize ────────────────────

    [Fact]
    public void External_Value_Change_Reruns_Autosize()
    {
        var cut = _ctx.Render<Lumeo.PromptInput>(p => p
            .Add(x => x.Value, "first line\nsecond line\nthird line"));

        // First render autosizes the textarea exactly once. Wait for the call to
        // be recorded so the baseline is taken AFTER the first-render reflow.
        cut.WaitForAssertion(() => Assert.NotEmpty(_module.Invocations["ai.autosize"]));
        var initialCount = _module.Invocations["ai.autosize"].Count;

        // The consumer clears the value from OUTSIDE the textarea (the clear-on-send
        // pattern). Without the fix, autosize never re-runs for an external Value
        // change, so the textarea keeps its stale multi-line height. The fix
        // re-invokes ai.autosize from the non-first-render OnAfterRenderAsync branch.
        cut.Render(p => p.Add(x => x.Value, ""));

        cut.WaitForAssertion(() =>
            Assert.True(_module.Invocations["ai.autosize"].Count > initialCount,
                $"ai.autosize should re-run after an external Value change (initial {initialCount})."));
    }
}
