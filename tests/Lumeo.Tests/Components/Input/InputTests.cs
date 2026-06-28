using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Input;

public class InputTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InputTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Input_Element()
    {
        var cut = _ctx.Render<Lumeo.Input>();

        Assert.NotNull(cut.Find("input"));
    }

    [Fact]
    public void Clearable_Renders_Wrapper_Even_While_Empty()
    {
        // Regression: keying the wrapper branch off "has a value" recreated
        // the <input> element on the first typed character (and when deleting
        // the last one), dropping focus and caret mid-typing. The wrapper must
        // be stable; only the clear BUTTON is value-conditional.
        var empty = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Clearable, true)
            .Add(b => b.Value, ""));
        Assert.NotNull(empty.Find("div input"));
        Assert.Empty(empty.FindAll("button"));

        var filled = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Clearable, true)
            .Add(b => b.Value, "x"));
        Assert.NotNull(filled.Find("div input"));
        Assert.NotEmpty(filled.FindAll("button"));
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

    // --- Size variants ---

    [Fact]
    public void Size_Sm_Adds_H8_Class()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Size, Lumeo.Size.Sm));

        var cls = cut.Find("input").GetAttribute("class");
        Assert.Contains("h-8", cls);
    }

    [Fact]
    public void Size_Lg_Adds_H11_Class()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Size, Lumeo.Size.Lg));

        var cls = cut.Find("input").GetAttribute("class");
        Assert.Contains("h-11", cls);
    }

    // --- Clearable ---

    [Fact]
    public void Clearable_Shows_X_Button_When_Value_NonEmpty()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Value, "hello")
            .Add(b => b.Clearable, true));

        var clearBtn = cut.Find("button[aria-label='Clear']");
        Assert.NotNull(clearBtn);
    }

    [Fact]
    public void Clearable_No_Button_When_Value_Empty()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Value, "")
            .Add(b => b.Clearable, true));

        // When value is empty, the clearable button path is not rendered
        // and the bare input is rendered instead
        Assert.Empty(cut.FindAll("button"));
    }

    // --- ShowCount / MaxLength (parity with Textarea) ---

    [Fact]
    public void ShowCount_Renders_Character_Count()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.Value, "hello"));

        Assert.Contains("5", cut.Markup);
    }

    [Fact]
    public void ShowCount_With_MaxLength_Renders_Count_And_Max()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 100)
            .Add(t => t.Value, "hello"));

        Assert.Contains("5/100", cut.Markup);
    }

    [Fact]
    public void MaxLength_Forwarded_To_Input_Element()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p.Add(t => t.MaxLength, 42));
        Assert.Equal("42", cut.Find("input").GetAttribute("maxlength"));
    }

    [Fact]
    public void ShowCount_Over_Limit_Uses_Destructive_Color()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 3)
            .Add(t => t.Value, "hello")); // 5 > 3

        var counterDivs = cut.FindAll("div.text-end").ToList();
        Assert.Single(counterDivs);
        Assert.Contains("text-destructive", counterDivs[0].GetAttribute("class") ?? "");
    }

    [Fact]
    public void ShowCount_With_CountFormat_Uses_Custom_Format()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.Value, "ab")
            .Add(t => t.CountFormat, c => $"{c} chars"));

        Assert.Contains("2 chars", cut.Markup);
    }
}
