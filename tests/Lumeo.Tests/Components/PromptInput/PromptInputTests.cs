using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Lumeo.Tests.Components.PromptInput;

public class PromptInputTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PromptInputTests()
    {
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
}
