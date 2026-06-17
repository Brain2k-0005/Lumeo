using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using L = Lumeo;

namespace Lumeo.Tests.Components.Resizable;

/// <summary>
/// #257 — Collapsible panels, persisted layout (SavedLayout round-trip) and an
/// OnLayout callback (mirrors DataGrid/Tabs SavedLayout).
/// </summary>
public class ResizableLayoutCollapseTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ResizableLayoutCollapseTests()
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
        L.ResizableLayout? savedLayout = null,
        EventCallback<L.ResizableLayout> onLayout = default,
        (double Default, double Min, double Max, bool Collapsible)[]? panels = null)
    {
        panels ??= new[] { (50.0, 10.0, 90.0, false), (50.0, 10.0, 90.0, false) };
        return _ctx.Render<L.ResizablePanelGroup>(p => p
            .Add(g => g.SavedLayout, savedLayout)
            .Add(g => g.OnLayout, onLayout)
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

    private static string PanelStyle(IRenderedComponent<L.ResizablePanelGroup> cut, int idx) =>
        cut.Find($"[data-testid='panel-{idx}']").ParentElement!.GetAttribute("style") ?? "";

    // --- SavedLayout ---

    [Fact]
    public void SavedLayout_Seeds_Panel_Sizes()
    {
        var cut = RenderGroup(savedLayout: new L.ResizableLayout(new[] { 70.0, 30.0 }));

        Assert.Contains("flex: 70.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 30.00 1 0", PanelStyle(cut, 1));
    }

    [Fact]
    public async Task OnLayout_Fires_After_Drag_With_Current_Sizes()
    {
        L.ResizableLayout? captured = null;
        var cut = RenderGroup(onLayout: EventCallback.Factory.Create<L.ResizableLayout>(this, l => captured = l));

        var context = cut.FindComponent<L.ResizablePanel>().Instance.Context;
        await cut.InvokeAsync(() => context.OnHandleDrag.InvokeAsync((0, 100.0))); // +10%

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Sizes.Count);
        Assert.Equal(60.0, captured.Sizes[0], precision: 1);
        Assert.Equal(40.0, captured.Sizes[1], precision: 1);
    }

    [Fact]
    public async Task SavedLayout_RoundTrips_Through_OnLayout()
    {
        // Drag, capture the emitted layout, then re-render a fresh group with it
        // as SavedLayout — the sizes must be restored.
        L.ResizableLayout? captured = null;
        var cut = RenderGroup(onLayout: EventCallback.Factory.Create<L.ResizableLayout>(this, l => captured = l));
        var context = cut.FindComponent<L.ResizablePanel>().Instance.Context;
        await cut.InvokeAsync(() => context.OnHandleDrag.InvokeAsync((0, 250.0))); // +25% -> 75/25

        Assert.NotNull(captured);

        var restored = RenderGroup(savedLayout: captured);
        Assert.Contains("flex: 75.00 1 0", PanelStyle(restored, 0));
        Assert.Contains("flex: 25.00 1 0", PanelStyle(restored, 1));
    }

    // --- Collapsible ---

    [Fact]
    public async Task Collapsible_Panel_Collapses_On_Over_Drag()
    {
        var cut = RenderGroup(panels: new[] { (50.0, 20.0, 90.0, true), (50.0, 10.0, 90.0, false) });
        var context = cut.FindComponent<L.ResizablePanel>().Instance.Context;

        // Drag the leading panel far below its min — collapses to 0.
        await cut.InvokeAsync(() => context.OnHandleDrag.InvokeAsync((0, -500.0)));

        Assert.Contains("flex: 0.00 1 0", PanelStyle(cut, 0));
        Assert.True(context.IsPanelCollapsed(0));
    }

    [Fact]
    public async Task ToggleCollapse_Collapses_And_Restores()
    {
        var cut = RenderGroup(panels: new[] { (60.0, 20.0, 90.0, true), (40.0, 10.0, 90.0, false) });
        var context = cut.FindComponent<L.ResizablePanel>().Instance.Context;

        await cut.InvokeAsync(() => context.ToggleCollapse.InvokeAsync(0));
        Assert.True(context.IsPanelCollapsed(0));
        Assert.Contains("flex: 0.00 1 0", PanelStyle(cut, 0));

        await cut.InvokeAsync(() => context.ToggleCollapse.InvokeAsync(0));
        Assert.False(context.IsPanelCollapsed(0));
        // Restored to the pre-collapse size (60).
        Assert.Contains("flex: 60.00 1 0", PanelStyle(cut, 0));
    }

    [Fact]
    public async Task NonCollapsible_Panel_Clamps_Instead_Of_Collapsing()
    {
        var cut = RenderGroup(panels: new[] { (50.0, 20.0, 90.0, false), (50.0, 10.0, 90.0, false) });
        var context = cut.FindComponent<L.ResizablePanel>().Instance.Context;

        await cut.InvokeAsync(() => context.OnHandleDrag.InvokeAsync((0, -500.0)));

        // Clamps at MinSize 20, never collapses.
        Assert.Contains("flex: 20.00 1 0", PanelStyle(cut, 0));
        Assert.False(context.IsPanelCollapsed(0));
    }

    [Fact]
    public async Task Collapsed_Panel_Marks_Data_Attribute()
    {
        var cut = RenderGroup(panels: new[] { (50.0, 20.0, 90.0, true), (50.0, 10.0, 90.0, false) });
        var context = cut.FindComponent<L.ResizablePanel>().Instance.Context;

        await cut.InvokeAsync(() => context.ToggleCollapse.InvokeAsync(0));

        var panel0 = cut.Find("[data-testid='panel-0']").ParentElement!;
        Assert.Equal("true", panel0.GetAttribute("data-collapsed"));
    }
}
