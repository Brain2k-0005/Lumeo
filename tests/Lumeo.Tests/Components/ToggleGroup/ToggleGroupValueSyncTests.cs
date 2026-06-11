using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ToggleGroup;

/// <summary>
/// Regression: clearing a bound <c>Value</c> / <c>SelectedValues</c> back to
/// null never cleared the internal selection — the item stayed visually
/// pressed. Uncontrolled usage (never binding) must keep working: an
/// internally-toggled selection survives unrelated parent re-renders.
/// </summary>
public class ToggleGroupValueSyncTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToggleGroupValueSyncTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment TwoItems() => b =>
    {
        b.OpenComponent<L.ToggleGroupItem>(0);
        b.AddAttribute(1, "Value", "a");
        b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
        b.CloseComponent();

        b.OpenComponent<L.ToggleGroupItem>(3);
        b.AddAttribute(4, "Value", "b");
        b.AddAttribute(5, "ChildContent", (RenderFragment)(c => c.AddContent(0, "B")));
        b.CloseComponent();
    };

    [Fact]
    public void Clearing_Bound_Value_To_Null_Clears_Selection()
    {
        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, "a")
            .Add(g => g.ChildContent, TwoItems()));

        Assert.Contains(cut.FindAll("button"), b => b.GetAttribute("aria-pressed") == "true");

        cut.Render(p => p.Add(g => g.Value, null));

        Assert.All(cut.FindAll("button"), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }

    [Fact]
    public void Clearing_Bound_SelectedValues_To_Null_Clears_Selection()
    {
        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Multiple)
            .Add(g => g.SelectedValues, new[] { "a", "b" })
            .Add(g => g.ChildContent, TwoItems()));

        Assert.All(cut.FindAll("button"), b => Assert.Equal("true", b.GetAttribute("aria-pressed")));

        cut.Render(p => p.Add(g => g.SelectedValues, (IEnumerable<string>?)null));

        Assert.All(cut.FindAll("button"), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }

    [Fact]
    public void Uncontrolled_Selection_Survives_Unrelated_ReRender()
    {
        // Never binds Value — internal toggling must not be wiped by a parent
        // re-render that re-sets parameters with Value still null.
        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.ChildContent, TwoItems()));

        cut.FindAll("button")[0].Click();
        Assert.Equal("true", cut.FindAll("button")[0].GetAttribute("aria-pressed"));

        cut.Render(p => p.Add(g => g.Class, "reparam"));

        Assert.Equal("true", cut.FindAll("button")[0].GetAttribute("aria-pressed"));
    }
}
