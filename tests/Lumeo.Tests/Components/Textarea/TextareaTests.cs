using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Textarea;

public class TextareaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TextareaTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Textarea_Element()
    {
        var cut = _ctx.Render<Lumeo.Textarea>();

        Assert.NotNull(cut.Find("textarea"));
    }

    [Fact]
    public void Renders_With_Default_Value()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.Value, "hello"));

        var textarea = cut.Find("textarea");
        Assert.Equal("hello", textarea.GetAttribute("value"));
    }

    [Fact]
    public void Renders_Without_Value_By_Default()
    {
        var cut = _ctx.Render<Lumeo.Textarea>();

        var textarea = cut.Find("textarea");
        Assert.True(string.IsNullOrEmpty(textarea.GetAttribute("value")));
    }

    [Fact]
    public void Has_Base_Classes()
    {
        var cut = _ctx.Render<Lumeo.Textarea>();

        var cls = cut.Find("textarea").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("min-h-[60px]", cls);
        Assert.Contains("w-full", cls);
        Assert.Contains("rounded-md", cls);
        Assert.Contains("border", cls);
        Assert.Contains("border-input", cls);
        Assert.Contains("bg-transparent", cls);
        Assert.Contains("px-3", cls);
        Assert.Contains("py-2", cls);
        Assert.Contains("text-sm", cls);
    }

    [Fact]
    public void OnInput_Event_Fires()
    {
        ChangeEventArgs? receivedArgs = null;
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.OnInput, args => { receivedArgs = args; }));

        cut.Find("textarea").Input("new value");

        Assert.NotNull(receivedArgs);
        Assert.Equal("new value", receivedArgs.Value?.ToString());
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.Class, "my-textarea-class"));

        var cls = cut.Find("textarea").GetAttribute("class");
        Assert.Contains("my-textarea-class", cls);
        Assert.Contains("flex", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-textarea",
                ["placeholder"] = "Enter text",
                ["rows"] = "5"
            }));

        var textarea = cut.Find("textarea");
        Assert.Equal("my-textarea", textarea.GetAttribute("data-testid"));
        Assert.Equal("Enter text", textarea.GetAttribute("placeholder"));
        Assert.Equal("5", textarea.GetAttribute("rows"));
    }

    [Fact]
    public void Disabled_Attribute_Is_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.AdditionalAttributes, new Dictionary<string, object>
            {
                ["disabled"] = true
            }));

        Assert.NotNull(cut.Find("textarea[disabled]"));
    }

    // --- ShowCount ---

    [Fact]
    public void ShowCount_Renders_Character_Count()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.Value, "hello"));

        // Should show "5" somewhere in the markup (the character count)
        Assert.Contains("5", cut.Markup);
    }

    [Fact]
    public void ShowCount_With_MaxLength_Renders_Count_And_Max()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 100)
            .Add(t => t.Value, "hello"));

        Assert.Contains("5/100", cut.Markup);
    }

    // --- Resize ---

    [Fact]
    public void Resize_None_Adds_Resize_None_Class()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.Resize, Lumeo.Textarea.TextareaResize.None));

        var cls = cut.Find("textarea").GetAttribute("class");
        Assert.Contains("resize-none", cls);
    }

    [Fact]
    public void Default_Resize_Has_Resize_Y_Class()
    {
        var cut = _ctx.Render<Lumeo.Textarea>();

        var cls = cut.Find("textarea").GetAttribute("class");
        Assert.Contains("resize-y", cls);
    }

    // --- #175: over-limit count warning ---

    [Fact]
    public void Count_Turns_Destructive_When_Over_MaxLength()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 5)
            .Add(t => t.Value, "toolong"));

        var counter = cut.Find("div.text-right");
        Assert.Contains("text-destructive", counter.GetAttribute("class"));
        Assert.Contains("7/5", counter.TextContent);
    }

    [Fact]
    public void Count_Is_Muted_When_Within_MaxLength()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 5)
            .Add(t => t.Value, "ok"));

        var counter = cut.Find("div.text-right");
        Assert.Contains("text-muted-foreground", counter.GetAttribute("class"));
        Assert.DoesNotContain("text-destructive", counter.GetAttribute("class"));
    }

    [Fact]
    public void ShowCount_Drops_Hard_Maxlength_So_Over_Limit_Is_Reachable()
    {
        // With a counter the native maxlength cap must be removed, otherwise the
        // browser blocks typing past the limit and the warning never shows.
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 5));

        Assert.False(cut.Find("textarea").HasAttribute("maxlength"));
    }

    [Fact]
    public void Without_Count_MaxLength_Stays_A_Hard_Cap()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.MaxLength, 5));

        Assert.Equal("5", cut.Find("textarea").GetAttribute("maxlength"));
    }

    [Fact]
    public void Over_Limit_Marks_Textarea_Invalid()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 3)
            .Add(t => t.Value, "abcd"));

        Assert.Equal("true", cut.Find("textarea").GetAttribute("aria-invalid"));
    }
}
