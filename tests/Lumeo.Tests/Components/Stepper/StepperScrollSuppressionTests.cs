using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Stepper;

/// <summary>
/// Codex P2 — focused Stepper tab buttons handle Home/End (and the arrow keys) as
/// roving-focus commands, but nothing suppressed the browser default, so on a page
/// or scroll container Home/End also jumped the scroll position while moving focus
/// — breaking keyboard navigation for long forms. Registers RegisterPreventDefaultKeys
/// against the stable tablist id, mirroring the other roving-tabindex widgets
/// (ToggleGroup, TreeSelect, Cascader).
/// </summary>
public class StepperScrollSuppressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StepperScrollSuppressionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Mounting_Registers_Scroll_Suppression_For_The_Roving_Keys()
    {
        var cut = _ctx.Render<L.Stepper>(p => p
            .Add(s => s.ActiveStep, 0)
            .AddChildContent(b =>
            {
                b.OpenComponent<L.StepperStep>(0);
                b.AddAttribute(1, "Title", "Step 1");
                b.CloseComponent();
                b.OpenComponent<L.StepperStep>(2);
                b.AddAttribute(3, "Title", "Step 2");
                b.CloseComponent();
            }));

        var tablistId = cut.Find("[role='tablist']").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(tablistId));

        var reg = Assert.Single(_ctx.JSInterop.Invocations,
            i => i.Identifier == "registerPreventDefaultKeys");
        Assert.Equal(tablistId, reg.Arguments[0]);

        var rules = (IReadOnlyList<Lumeo.Services.PreventDefaultKeyRule>)reg.Arguments[1]!;
        var keys = rules.Select(r => r.Key).ToList();
        Assert.Contains("Home", keys);
        Assert.Contains("End", keys);
        Assert.Contains("ArrowLeft", keys);
        Assert.Contains("ArrowRight", keys);
    }
}
