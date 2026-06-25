using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using L = Lumeo;

namespace Lumeo.Tests.Components.Resizable;

/// <summary>
/// #199 (edge-data) — A late-arriving / changed <c>SavedLayout</c> whose
/// <c>Sizes.Count</c> does not match the live registered panel count must be
/// IGNORED rather than copied verbatim. Copying a 2-entry snapshot while three
/// panels are rendered left a broken mixed layout: panels 0/1 took 70/30 (sum
/// 100) and the surplus panel fell through to GetPanelSize's equal-share
/// fallback, so the visible flex bases summed to ~150 instead of 100. The fix
/// validates the snapshot length against the registered panels in OnAfterRender
/// and falls through to the normal default seeding when they disagree.
/// </summary>
public class ResizableSavedLayoutMismatchTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ResizableSavedLayoutMismatchTests()
    {
        _ctx.AddLumeoServices();
        var version = typeof(Lumeo.Services.ComponentInteropService).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? typeof(Lumeo.Services.ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        var module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={version}");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<double>("getElementDimension", _ => true).SetResult(1000.0);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.ResizablePanelGroup> RenderGroup(
        L.ResizableLayout? savedLayout,
        (double Default, double Min, double Max, bool Collapsible)[] panels)
    {
        return _ctx.Render<L.ResizablePanelGroup>(p => p
            .Add(g => g.SavedLayout, savedLayout)
            .AddChildContent(b =>
            {
                var seq = 0;
                for (var i = 0; i < panels.Length; i++)
                {
                    var idx = i;
                    b.OpenComponent<L.ResizablePanel>(seq++);
                    b.AddAttribute(seq++, "Order", idx);
                    b.AddAttribute(seq++, "DefaultSize", panels[idx].Default);
                    b.AddAttribute(seq++, "MinSize", panels[idx].Min);
                    b.AddAttribute(seq++, "MaxSize", panels[idx].Max);
                    b.AddAttribute(seq++, "Collapsible", panels[idx].Collapsible);
                    b.AddAttribute(seq++, "ChildContent", (RenderFragment)(inner =>
                        inner.AddMarkupContent(0, $"<span data-testid='panel-{idx}'>panel {idx}</span>")));
                    b.CloseComponent();
                    if (idx < panels.Length - 1)
                    {
                        b.OpenComponent<L.ResizableHandle>(seq++);
                        b.AddAttribute(seq++, "PanelIndex", idx);
                        b.CloseComponent();
                    }
                }
            }));
    }

    private static double PanelFlexBasis(IRenderedComponent<L.ResizablePanelGroup> cut, int idx)
    {
        var style = cut.Find($"[data-testid='panel-{idx}']").ParentElement!.GetAttribute("style") ?? "";
        // style looks like: "flex: 33.33 1 0; min-width: 0; ...".
        var marker = "flex: ";
        var start = style.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = style.IndexOf(' ', start);
        return double.Parse(style.Substring(start, end - start),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void StaleSavedLayout_With_Fewer_Sizes_Is_Ignored_And_Sizes_Sum_To_100()
    {
        // Snapshot saved for 2 panels, but the host now renders 3 — the stale
        // snapshot must NOT be applied; instead the three panels seed from their
        // DefaultSizes (33.33 each, normalized to 100).
        var cut = RenderGroup(
            savedLayout: new L.ResizableLayout(new[] { 70.0, 30.0 }),
            panels: new[]
            {
                (50.0, 10.0, 90.0, false),
                (50.0, 10.0, 90.0, false),
                (50.0, 10.0, 90.0, false),
            });

        var s0 = PanelFlexBasis(cut, 0);
        var s1 = PanelFlexBasis(cut, 1);
        var s2 = PanelFlexBasis(cut, 2);

        // WITHOUT the fix: s0=70, s1=30, s2=50 (fallback) -> sum 150, broken.
        // WITH the fix: each panel ~33.33 -> sum 100.
        Assert.Equal(100.0, s0 + s1 + s2, precision: 1);
        Assert.Equal(s0, s1, precision: 1);
        Assert.Equal(s1, s2, precision: 1);
        // And the stale 70/30 split is gone.
        Assert.NotEqual(70.0, s0, precision: 1);
    }

    [Fact]
    public void MatchingSavedLayout_Is_Still_Applied()
    {
        // Normal path preserved: a snapshot whose length matches the panel count
        // is applied verbatim.
        var cut = RenderGroup(
            savedLayout: new L.ResizableLayout(new[] { 70.0, 30.0 }),
            panels: new[]
            {
                (50.0, 10.0, 90.0, false),
                (50.0, 10.0, 90.0, false),
            });

        Assert.Equal(70.0, PanelFlexBasis(cut, 0), precision: 1);
        Assert.Equal(30.0, PanelFlexBasis(cut, 1), precision: 1);
    }
}
