using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Segmented;

/// <summary>
/// Options (data-driven) mode coverage for Segmented.ItemClass and
/// SegmentedOption.Class (#segmented-item-class), mirroring
/// SegmentedItemClassTests' compound-mode coverage. Merge order is the same in
/// both modes: base item classes -> Segmented.ItemClass -> the item's own Class
/// (SegmentedOption.Class here) — last wins any Tailwind conflict.
/// </summary>
public class SegmentedOptionClassTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SegmentedOptionClassTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Segmented.SegmentedOption> Options(string? aClass = null) =>
    [
        new() { Label = "Day", Value = "day", Class = aClass },
        new() { Label = "Week", Value = "week" }
    ];

    [Fact]
    public void SegmentedOption_Class_Is_Merged_With_Base_Classes()
    {
        var cut = _ctx.Render<L.Segmented>(p => p
            .Add(s => s.Value, "day")
            .Add(s => s.Options, Options("my-option-class")));

        var day = cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Day"));
        var cls = day.GetAttribute("class") ?? "";

        Assert.Contains("my-option-class", cls);
        Assert.Contains("inline-flex", cls);
        Assert.Contains("rounded-md", cls);
    }

    [Fact]
    public void SegmentedOption_Class_Resolves_Tailwind_Conflicts_Last_Wins()
    {
        var cut = _ctx.Render<L.Segmented>(p => p
            .Add(s => s.Value, "day")
            .Add(s => s.Options, Options("rounded-none")));

        var day = cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Day"));
        var cls = day.GetAttribute("class") ?? "";

        Assert.Contains("rounded-none", cls);
        Assert.DoesNotContain("rounded-md", cls);
    }

    [Fact]
    public void Segmented_ItemClass_Applied_To_Every_Option_Button()
    {
        var cut = _ctx.Render<L.Segmented>(p => p
            .Add(s => s.Value, "day")
            .Add(s => s.ItemClass, "control-wide-accent")
            .Add(s => s.Options, Options()));

        foreach (var button in cut.FindAll("button[role='radio']"))
            Assert.Contains("control-wide-accent", button.GetAttribute("class"));
    }

    [Fact]
    public void SegmentedOption_Class_Wins_Over_Segmented_ItemClass_On_Conflict()
    {
        var cut = _ctx.Render<L.Segmented>(p => p
            .Add(s => s.Value, "day")
            .Add(s => s.ItemClass, "rounded-none")
            .Add(s => s.Options, Options("rounded-full")));

        var day = cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Day"));
        var week = cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Week"));

        Assert.Contains("rounded-full", day.GetAttribute("class"));
        Assert.DoesNotContain("rounded-none", day.GetAttribute("class"));
        // Week has no option-level Class, so it keeps the control-wide ItemClass.
        Assert.Contains("rounded-none", week.GetAttribute("class"));
    }

    [Fact]
    public void AriaChecked_Variant_Classes_Land_On_Active_And_Inactive_Options_Alike()
    {
        var cut = _ctx.Render<L.Segmented>(p => p
            .Add(s => s.Value, "day")
            .Add(s => s.ItemClass, "aria-checked:bg-primary aria-checked:text-primary-foreground")
            .Add(s => s.Options, Options()));

        var active = cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Day"));
        var inactive = cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Week"));

        Assert.Equal("true", active.GetAttribute("aria-checked"));
        Assert.Contains("aria-checked:bg-primary", active.GetAttribute("class"));
        Assert.Contains("aria-checked:text-primary-foreground", active.GetAttribute("class"));

        Assert.Equal("false", inactive.GetAttribute("aria-checked"));
        Assert.Contains("aria-checked:bg-primary", inactive.GetAttribute("class"));
        Assert.Contains("aria-checked:text-primary-foreground", inactive.GetAttribute("class"));
    }
}
