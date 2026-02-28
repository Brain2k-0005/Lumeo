using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Textarea;

public class TextareaTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public TextareaTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

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
}
