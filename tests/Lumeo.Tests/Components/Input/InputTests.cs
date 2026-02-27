using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Input;

public class InputTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public InputTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Input_Element()
    {
        var cut = _ctx.Render<Lumeo.Input>();

        Assert.NotNull(cut.Find("input"));
    }

    [Fact]
    public void Renders_With_Default_Value()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Value, "hello"));

        var input = cut.Find("input");
        Assert.Equal("hello", input.GetAttribute("value"));
    }

    [Fact]
    public void Renders_Without_Value_By_Default()
    {
        var cut = _ctx.Render<Lumeo.Input>();

        var input = cut.Find("input");
        Assert.True(string.IsNullOrEmpty(input.GetAttribute("value")));
    }

    [Fact]
    public void Has_Base_Classes()
    {
        var cut = _ctx.Render<Lumeo.Input>();

        var cls = cut.Find("input").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("h-9", cls);
        Assert.Contains("w-full", cls);
        Assert.Contains("rounded-md", cls);
        Assert.Contains("border", cls);
        Assert.Contains("border-input", cls);
        Assert.Contains("bg-transparent", cls);
        Assert.Contains("px-3", cls);
        Assert.Contains("py-1", cls);
        Assert.Contains("text-sm", cls);
    }

    [Fact]
    public void OnInput_Event_Fires_On_Input()
    {
        ChangeEventArgs? receivedArgs = null;
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.OnInput, args => { receivedArgs = args; }));

        cut.Find("input").Input("new value");

        Assert.NotNull(receivedArgs);
        Assert.Equal("new value", receivedArgs.Value?.ToString());
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Class, "my-input-class"));

        var cls = cut.Find("input").GetAttribute("class");
        Assert.Contains("my-input-class", cls);
        Assert.Contains("flex", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-input",
                ["placeholder"] = "Enter text",
                ["type"] = "email"
            }));

        var input = cut.Find("input");
        Assert.Equal("my-input", input.GetAttribute("data-testid"));
        Assert.Equal("Enter text", input.GetAttribute("placeholder"));
        Assert.Equal("email", input.GetAttribute("type"));
    }

    [Fact]
    public void Disabled_Attribute_Is_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["disabled"] = true
            }));

        Assert.NotNull(cut.Find("input[disabled]"));
    }
}
