using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Segmented;

public class SegmentedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public SegmentedTests()
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

    private static List<Lumeo.Segmented.SegmentedOption> CreateOptions() =>
    [
        new() { Label = "Day", Value = "day" },
        new() { Label = "Week", Value = "week" },
        new() { Label = "Month", Value = "month" }
    ];

    [Fact]
    public void Renders_Options_As_Buttons()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions()));

        var buttons = cut.FindAll("button[role='radio']");
        Assert.Equal(3, buttons.Count);
    }

    [Fact]
    public void Container_Has_RadioGroup_Role()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions()));

        Assert.Equal("radiogroup", cut.Find("div").GetAttribute("role"));
    }

    [Fact]
    public void Active_Option_Has_AriaChecked_True()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions())
            .Add(s => s.Value, "week"));

        var weekButton = cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Week"));
        Assert.Equal("true", weekButton.GetAttribute("aria-checked"));
    }

    [Fact]
    public void Block_True_Adds_FullWidth_Class()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions())
            .Add(s => s.Block, true));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("w-full", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Class, "my-segmented"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-segmented", cls);
    }

    // --- #185: roving tabindex + arrow-key contract ---

    [Fact]
    public void Roving_Tabindex_Only_Selected_Is_Tabbable()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions())
            .Add(s => s.Value, "week"));

        var buttons = cut.FindAll("button[role='radio']");
        Assert.Equal("-1", buttons[0].GetAttribute("tabindex")); // Day
        Assert.Equal("0", buttons[1].GetAttribute("tabindex"));  // Week (selected)
        Assert.Equal("-1", buttons[2].GetAttribute("tabindex")); // Month
    }

    [Fact]
    public void No_Selection_First_Item_Is_Tabbable()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions()));

        var buttons = cut.FindAll("button[role='radio']");
        Assert.Equal("0", buttons[0].GetAttribute("tabindex"));
        Assert.Equal("-1", buttons[1].GetAttribute("tabindex"));
    }

    [Fact]
    public void ArrowRight_Selects_Next_Option()
    {
        string? changed = null;
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions())
            .Add(s => s.Value, "day")
            .Add(s => s.ValueChanged, v => changed = v));

        cut.Find("[role='radiogroup']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("week", changed);
    }

    [Fact]
    public void ArrowLeft_Wraps_To_Last_Option()
    {
        string? changed = null;
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions())
            .Add(s => s.Value, "day")
            .Add(s => s.ValueChanged, v => changed = v));

        cut.Find("[role='radiogroup']").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Equal("month", changed);
    }

    [Fact]
    public void Arrow_Skips_Disabled_Option()
    {
        List<Lumeo.Segmented.SegmentedOption> opts =
        [
            new() { Label = "Day", Value = "day" },
            new() { Label = "Week", Value = "week", Disabled = true },
            new() { Label = "Month", Value = "month" }
        ];
        string? changed = null;
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, opts)
            .Add(s => s.Value, "day")
            .Add(s => s.ValueChanged, v => changed = v));

        cut.Find("[role='radiogroup']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("month", changed); // week is skipped
    }

    [Fact]
    public void End_Key_Selects_Last_Option()
    {
        string? changed = null;
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions())
            .Add(s => s.Value, "day")
            .Add(s => s.ValueChanged, v => changed = v));

        cut.Find("[role='radiogroup']").KeyDown(new KeyboardEventArgs { Key = "End" });

        Assert.Equal("month", changed);
    }

    [Fact]
    public void Registers_PreventDefault_For_Arrow_Keys()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions()));
        var groupId = cut.Find("[role='radiogroup']").GetAttribute("id");

        var invocation = _module.VerifyInvoke("registerPreventDefaultKeys");
        Assert.Equal(groupId, invocation.Arguments[0]);
        var rules = Assert.IsAssignableFrom<IReadOnlyList<PreventDefaultKeyRule>>(invocation.Arguments[1]);
        Assert.Contains("ArrowRight", rules.Select(r => r.Key));
    }
}
