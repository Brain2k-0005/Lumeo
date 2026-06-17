using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.OtpInput;

public class OtpInputTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OtpInputTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Rendering ---

    [Fact]
    public void Renders_Correct_Number_Of_Inputs_With_Default_Length()
    {
        var cut = _ctx.Render<L.OtpInput>();
        Assert.Equal(6, cut.FindAll("input").Count);
    }

    [Fact]
    public void Renders_Correct_Number_Of_Inputs_With_Custom_Length()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p.Add(c => c.Length, 4));
        Assert.Equal(4, cut.FindAll("input").Count);
    }

    [Fact]
    public void Input_Has_Type_Text()
    {
        var cut = _ctx.Render<L.OtpInput>();
        var inputs = cut.FindAll("input");
        Assert.All(inputs, i => Assert.Equal("text", i.GetAttribute("type")));
    }

    [Fact]
    public void Input_Has_Maxlength_One()
    {
        var cut = _ctx.Render<L.OtpInput>();
        var inputs = cut.FindAll("input");
        Assert.All(inputs, i => Assert.Equal("1", i.GetAttribute("maxlength")));
    }

    [Fact]
    public void Input_Has_Inputmode_Numeric()
    {
        var cut = _ctx.Render<L.OtpInput>();
        var inputs = cut.FindAll("input");
        Assert.All(inputs, i => Assert.Equal("numeric", i.GetAttribute("inputmode")));
    }

    [Fact]
    public void Input_Has_Autocomplete_One_Time_Code()
    {
        var cut = _ctx.Render<L.OtpInput>();
        var inputs = cut.FindAll("input");
        Assert.All(inputs, i => Assert.Equal("one-time-code", i.GetAttribute("autocomplete")));
    }

    [Fact]
    public void Inputs_Have_Unique_Ids()
    {
        var cut = _ctx.Render<L.OtpInput>();
        var ids = cut.FindAll("input").Select(i => i.GetAttribute("id")).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // --- Value Display ---

    [Fact]
    public void Renders_Initial_Value_In_Inputs()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "12"));

        var inputs = cut.FindAll("input");
        Assert.Equal("1", inputs[0].GetAttribute("value"));
        Assert.Equal("2", inputs[1].GetAttribute("value"));
        Assert.Equal("", inputs[2].GetAttribute("value"));
        Assert.Equal("", inputs[3].GetAttribute("value"));
    }

    [Fact]
    public void Empty_Value_Shows_Empty_Inputs()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Value, ""));

        var inputs = cut.FindAll("input");
        Assert.All(inputs, i => Assert.Equal("", i.GetAttribute("value") ?? ""));
    }

    // --- ValueChanged Callback ---

    [Fact]
    public void ValueChanged_Fires_When_Input_Entered()
    {
        string? changedValue = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "")
            .Add(c => c.ValueChanged, v => changedValue = v));

        cut.FindAll("input")[0].Input(new ChangeEventArgs { Value = "5" });

        Assert.NotNull(changedValue);
        Assert.Contains("5", changedValue);
    }

    [Fact]
    public void ValueChanged_Fires_With_Updated_String()
    {
        string? changedValue = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 6)
            .Add(c => c.Value, "123")
            .Add(c => c.ValueChanged, v => changedValue = v));

        cut.FindAll("input")[3].Input(new ChangeEventArgs { Value = "4" });

        Assert.Equal("1234", changedValue);
    }

    [Fact]
    public void ValueChanged_Uses_Last_Char_Of_Multi_Char_Input()
    {
        string? changedValue = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "")
            .Add(c => c.InputMode, "Alphanumeric")
            .Add(c => c.ValueChanged, v => changedValue = v));

        cut.FindAll("input")[0].Input(new ChangeEventArgs { Value = "abc" });

        Assert.Contains("c", changedValue);
    }

    // --- OnComplete Callback ---

    [Fact]
    public void OnComplete_Fires_When_All_Inputs_Filled()
    {
        string? completedValue = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 3)
            .Add(c => c.Value, "12")
            .Add(c => c.OnComplete, v => completedValue = v));

        cut.FindAll("input")[2].Input(new ChangeEventArgs { Value = "3" });

        Assert.Equal("123", completedValue);
    }

    [Fact]
    public void OnComplete_Not_Fired_When_Not_All_Inputs_Filled()
    {
        string? completedValue = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "")
            .Add(c => c.OnComplete, v => completedValue = v));

        cut.FindAll("input")[0].Input(new ChangeEventArgs { Value = "1" });

        Assert.Null(completedValue);
    }

    // --- Backspace Key Handling ---

    [Fact]
    public void Backspace_On_Empty_Input_Fires_ValueChanged()
    {
        string? changedValue = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "12")
            .Add(c => c.ValueChanged, v => changedValue = v));

        // Index 2 is empty, backspace should clear index 1
        cut.FindAll("input")[2].KeyDown(new KeyboardEventArgs { Key = "Backspace" });

        Assert.NotNull(changedValue);
    }

    [Fact]
    public void Backspace_On_First_Empty_Input_Does_Not_Fire_ValueChanged()
    {
        string? changedValue = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "")
            .Add(c => c.ValueChanged, v => changedValue = v));

        // Index 0 is empty and there's no previous input
        cut.FindAll("input")[0].KeyDown(new KeyboardEventArgs { Key = "Backspace" });

        Assert.Null(changedValue);
    }

    // --- CSS / Classes ---

    [Fact]
    public void Container_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.OtpInput>();
        var cls = cut.Find("div").GetAttribute("class") ?? "";
        Assert.Contains("flex", cls);
        Assert.Contains("items-center", cls);
        Assert.Contains("gap-2", cls);
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p.Add(c => c.Class, "my-otp"));
        var cls = cut.Find("div").GetAttribute("class") ?? "";
        Assert.Contains("my-otp", cls);
        Assert.Contains("flex", cls);
    }

    [Fact]
    public void Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "my-otp" }));
        Assert.Equal("my-otp", cut.Find("div").GetAttribute("data-testid"));
    }

    [Fact]
    public void Input_Has_Styling_Classes()
    {
        var cut = _ctx.Render<L.OtpInput>();
        var inputCls = cut.FindAll("input")[0].GetAttribute("class") ?? "";
        Assert.Contains("rounded-md", inputCls);
        Assert.Contains("border", inputCls);
        Assert.Contains("text-center", inputCls);
    }

    // --- #178: arrow nav, clean rejection, no interior-space padding ---

    [Fact]
    public void Each_Cell_Has_Aria_Label()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p.Add(c => c.Length, 3));
        var inputs = cut.FindAll("input");
        Assert.Equal("Digit 1 of 3", inputs[0].GetAttribute("aria-label"));
        Assert.Equal("Digit 3 of 3", inputs[2].GetAttribute("aria-label"));
    }

    [Fact]
    public void Rejected_Char_Does_Not_Change_Value()
    {
        string? changed = null;
        var changeCount = 0;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.InputMode, "Numeric")
            .Add(c => c.Value, "")
            .Add(c => c.ValueChanged, v => { changed = v; changeCount++; }));

        // Letters are invalid in Numeric mode — the value stays empty and
        // ValueChanged must not fire.
        cut.FindAll("input")[0].Input(new ChangeEventArgs { Value = "x" });

        Assert.Null(changed);
        Assert.Equal(0, changeCount);
    }

    [Fact]
    public void Rejected_Char_Reverts_The_Cell_To_Model_Value()
    {
        // The bound value attribute follows the (empty) model, so the painted
        // reject is discarded rather than lingering.
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.InputMode, "Numeric"));

        cut.FindAll("input")[0].Input(new ChangeEventArgs { Value = "x" });

        Assert.Equal("", cut.FindAll("input")[0].GetAttribute("value") ?? "");
    }

    [Fact]
    public void Arrow_Keys_Do_Not_Throw_Or_Mutate()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "12"));

        cut.FindAll("input")[1].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        cut.FindAll("input")[1].KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        cut.FindAll("input")[1].KeyDown(new KeyboardEventArgs { Key = "Home" });
        cut.FindAll("input")[1].KeyDown(new KeyboardEventArgs { Key = "End" });

        Assert.Equal("1", cut.FindAll("input")[0].GetAttribute("value"));
        Assert.Equal("2", cut.FindAll("input")[1].GetAttribute("value"));
    }

    [Fact]
    public void Arrow_Keys_Do_Not_Fire_ValueChanged()
    {
        string? changed = "sentinel";
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "12")
            .Add(c => c.ValueChanged, v => changed = v));

        cut.FindAll("input")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("sentinel", changed);
    }

    [Fact]
    public void Clearing_Middle_Cell_Does_Not_Leave_Interior_Space()
    {
        string? changed = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "123")
            .Add(c => c.ValueChanged, v => changed = v));

        // Backspace at the middle (index 1) clears that cell. The emitted value
        // must collapse to gap-free "13", never "1 3" with an interior space.
        cut.FindAll("input")[1].KeyDown(new KeyboardEventArgs { Key = "Backspace" });

        Assert.NotNull(changed);
        Assert.DoesNotContain(" ", changed);
        Assert.Equal("13", changed);
    }

    [Fact]
    public void Paste_Does_Not_Pad_With_Trailing_Spaces()
    {
        string? changed = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 6)
            .Add(c => c.ValueChanged, v => changed = v));

        // Drive the paste handler the way the JS interop callback would.
        var otp = cut.Instance;
        cut.InvokeAsync(() =>
            (Task)typeof(L.OtpInput)
                .GetMethod("HandlePaste", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(otp, ["123"])!);

        Assert.Equal("123", changed);
        Assert.Equal(3, changed!.Length); // no padding to Length
    }
}
