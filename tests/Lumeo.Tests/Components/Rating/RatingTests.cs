using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Rating;

public class RatingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public RatingTests()
    {
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
    public void Renders_Default_Five_Stars()
    {
        var cut = _ctx.Render<Lumeo.Rating>();

        var buttons = cut.FindAll("button");
        Assert.Equal(5, buttons.Count);
    }

    [Fact]
    public void Max_Parameter_Changes_Star_Count()
    {
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.Max, 10));

        var buttons = cut.FindAll("button");
        Assert.Equal(10, buttons.Count);
    }

    [Fact]
    public void Container_Has_Inline_Flex_Class()
    {
        var cut = _ctx.Render<Lumeo.Rating>();

        var cls = cut.Find("[role='radiogroup']").GetAttribute("class");
        Assert.Contains("inline-flex", cls);
        Assert.Contains("items-center", cls);
    }

    [Fact]
    public void ReadOnly_Disables_All_Buttons()
    {
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.ReadOnly, true));

        var buttons = cut.FindAll("button");
        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.Class, "my-rating"));

        var cls = cut.Find("[role='radiogroup']").GetAttribute("class");
        Assert.Contains("my-rating", cls);
    }

    // --- #189: arrow keys change value instead of scrolling the page ---

    [Fact]
    public void Registers_PreventDefault_For_Arrow_Keys()
    {
        var cut = _ctx.Render<Lumeo.Rating>();
        var groupId = cut.Find("[role='radiogroup']").GetAttribute("id");

        var invocation = _module.VerifyInvoke("registerPreventDefaultKeys");
        Assert.Equal(groupId, invocation.Arguments[0]);
        var rules = Lumeo.Tests.Helpers.PreventDefaultRuleCapture.Parse(invocation.Arguments[1]);
        var keys = rules.Select(r => r.Key).ToList();
        Assert.Contains("ArrowUp", keys);
        Assert.Contains("ArrowDown", keys);
        Assert.Contains("ArrowLeft", keys);
        Assert.Contains("ArrowRight", keys);
    }

    [Fact]
    public void ReadOnly_Does_Not_Register_PreventDefault()
    {
        _ctx.Render<Lumeo.Rating>(p => p.Add(r => r.ReadOnly, true));

        Assert.Empty(_module.Invocations["registerPreventDefaultKeys"]);
    }

    [Fact]
    public void ArrowRight_Increments_Value()
    {
        double? changed = null;
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.Value, 2)
            .Add(r => r.ValueChanged, v => changed = v));

        cut.Find("[role='radiogroup']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal(3, changed);
    }

    [Fact]
    public void ArrowLeft_Decrements_Value()
    {
        double? changed = null;
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.Value, 2)
            .Add(r => r.ValueChanged, v => changed = v));

        cut.Find("[role='radiogroup']").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Equal(1, changed);
    }

    [Fact]
    public void AllowHalf_Steps_By_Half()
    {
        double? changed = null;
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.AllowHalf, true)
            .Add(r => r.Value, 2)
            .Add(r => r.ValueChanged, v => changed = v));

        cut.Find("[role='radiogroup']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal(2.5, changed);
    }

    [Fact]
    public void End_Key_Sets_Max()
    {
        double? changed = null;
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.Max, 5)
            .Add(r => r.Value, 1)
            .Add(r => r.ValueChanged, v => changed = v));

        cut.Find("[role='radiogroup']").KeyDown(new KeyboardEventArgs { Key = "End" });

        Assert.Equal(5, changed);
    }

    [Fact]
    public void Stars_Expose_Radio_Role_And_Checked_State()
    {
        var cut = _ctx.Render<Lumeo.Rating>(p => p.Add(r => r.Value, 2));

        var radios = cut.FindAll("[role='radio']");
        Assert.Equal(5, radios.Count);
        // ARIA single-selection: exactly the star at Value is checked, not every
        // filled star below it (the visual fill is driven by classes, not aria-checked).
        Assert.Equal("false", radios[0].GetAttribute("aria-checked"));
        Assert.Equal("true", radios[1].GetAttribute("aria-checked"));
        Assert.Equal("false", radios[2].GetAttribute("aria-checked"));
    }

    // --- #58: a role="radiogroup" must not report aria-checked="true" on more than
    // one role="radio" (single-selection contract). The visual "all stars up to Value
    // are filled" look is driven by CSS classes, but aria-checked previously mirrored
    // that fill, so Value=3 reported THREE checked radios. The fix marks only the
    // single star equal to the current value as checked. ---

    [Fact]
    public void RadioGroup_HasExactlyOneCheckedRadio()
    {
        var cut = _ctx.Render<Lumeo.Rating>(p => p.Add(r => r.Value, 3));

        var radios = cut.FindAll("[role='radio']");
        // Without the fix, stars 1, 2 AND 3 would all report aria-checked="true",
        // violating the radiogroup single-selection contract.
        var checkedCount = radios.Count(r => r.GetAttribute("aria-checked") == "true");
        Assert.Equal(1, checkedCount);

        // And the single checked radio must be the star at the current value (star 3).
        Assert.Equal("true", radios[2].GetAttribute("aria-checked"));
    }

    [Fact]
    public void RadioGroup_HalfValue_ChecksSingleStar()
    {
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.AllowHalf, true)
            .Add(r => r.Value, 2.5));

        var radios = cut.FindAll("[role='radio']");
        // A 2.5 rating selects star 3 (Ceiling) — and ONLY star 3.
        var checkedCount = radios.Count(r => r.GetAttribute("aria-checked") == "true");
        Assert.Equal(1, checkedCount);
        Assert.Equal("true", radios[2].GetAttribute("aria-checked"));
    }

    [Fact]
    public void RadioGroup_ZeroValue_HasNoCheckedRadio()
    {
        var cut = _ctx.Render<Lumeo.Rating>(p => p.Add(r => r.Value, 0));

        var radios = cut.FindAll("[role='radio']");
        // No selection yet: a radiogroup may legitimately report zero checked radios,
        // and crucially must never report more than one.
        Assert.DoesNotContain(radios, r => r.GetAttribute("aria-checked") == "true");
    }

    // --- #57: shrinking Max below current Value must not drop the widget out of
    // the tab order. The roving tabindex names the active star by Ceiling(Value);
    // if Max shrinks below it, that star is no longer rendered, so without the
    // clamp NO rendered star carries tabindex="0" and the widget becomes
    // keyboard-unreachable. The fix clamps the active index to Max so exactly one
    // tabstop survives (the last rendered star). ---

    [Fact]
    public void ShrinkingMax_BelowValue_KeepsOneTabstop()
    {
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.Value, 5)
            .Add(r => r.Max, 5));

        // Sanity: star 5 (index of Value) is the tabstop before the shrink.
        var before = cut.FindAll("button");
        Assert.Equal(5, before.Count);
        Assert.Single(before, b => b.GetAttribute("tabindex") == "0");

        // Shrink Max below the current Value. The previously-active star (5) no
        // longer exists.
        cut.Render(p => p.Add(r => r.Max, 3));

        var after = cut.FindAll("button");
        Assert.Equal(3, after.Count);

        // Exactly one rendered star must remain in the tab order, and it must be
        // the last rendered star (clamped to Max) — otherwise the widget is a
        // keyboard trap-out.
        Assert.Single(after, b => b.GetAttribute("tabindex") == "0");
        Assert.Equal("0", after[2].GetAttribute("tabindex"));
    }
}
