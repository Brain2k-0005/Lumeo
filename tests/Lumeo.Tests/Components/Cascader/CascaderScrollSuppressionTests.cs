using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Cascader;

/// <summary>
/// Codex P2 — the roving-focus navigation keys HandleKeyDown consumes (ArrowUp/Down/
/// Left/Right, Home, End) had no browser-default preventDefault registration, so on a
/// scrollable page/panel those same keys also scrolled the page while the roving focus
/// moved between columns. Mirrors TreeSelect.OpenDropdown's RegisterPreventDefaultKeys call.
/// </summary>
public class CascaderScrollSuppressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public CascaderScrollSuppressionTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Cascader.CascaderOption> BuildOptions() =>
    [
        new() { Label = "Fruit", Value = "fruit", Children = [new() { Label = "Apple", Value = "apple" }] },
    ];

    [Fact]
    public void Opening_The_Panel_Registers_Scroll_Suppression_For_The_Roving_Keys()
    {
        var cut = _ctx.Render<L.Cascader>(p => p.Add(c => c.Options, BuildOptions()));
        cut.Find("button").Click(); // open

        var contentId = cut.Find("[tabindex='-1']").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(contentId));
        cut.WaitForAssertion(() => Assert.Contains(contentId, _interop.RegisterPreventDefaultKeysElementIds));
    }
}

/// <summary>
/// Codex P2 — a focused option &lt;button&gt; synthesizes its own native click on
/// Enter/Space (standard browser keyboard-activation behaviour), which fires the
/// button's own @onclick="SelectOption" a SECOND time on top of the bubbled keydown
/// that HandleKeyDown's "Enter"/" " case already handles — double-invoking a leaf
/// selection's ValueChanged. bUnit cannot simulate the native click synthesis itself
/// (it isn't a real browser), so this asserts the OBSERVABLE JS interop call instead:
/// Enter and Space must be included in the registered preventDefault rule set, which
/// is what suppresses that native synthesis in a real browser. Uses the REAL
/// ComponentInteropService (not TrackingInteropService) so the rule LIST itself —
/// not just the target element id — is inspectable via bUnit's JSInterop.Invocations.
/// </summary>
public class CascaderActivationKeySuppressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CascaderActivationKeySuppressionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Cascader.CascaderOption> BuildOptions() =>
    [
        new() { Label = "Fruit", Value = "fruit", Children = [new() { Label = "Apple", Value = "apple" }] },
    ];

    [Fact]
    public void Opening_The_Panel_Registers_PreventDefault_For_Enter_And_Space()
    {
        var cut = _ctx.Render<L.Cascader>(p => p.Add(c => c.Options, BuildOptions()));
        cut.Find("button").Click(); // open

        var reg = Assert.Single(_ctx.JSInterop.Invocations,
            i => i.Identifier == "registerPreventDefaultKeys");
        var rules = (IReadOnlyList<Lumeo.Services.PreventDefaultKeyRule>)reg.Arguments[1]!;
        var keys = rules.Select(r => r.Key).ToList();

        Assert.Contains("Enter", keys);
        Assert.Contains(" ", keys);
    }
}
