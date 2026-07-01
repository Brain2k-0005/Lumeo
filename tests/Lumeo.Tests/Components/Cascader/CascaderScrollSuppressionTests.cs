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
