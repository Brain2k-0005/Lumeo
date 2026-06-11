using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using L = Lumeo;

namespace Lumeo.Tests.Components.Resizable;

/// <summary>
/// Regression tests for Resizable being drag-dead by default, keyboard-inert
/// handles, and reject-instead-of-clamp constraint handling:
///  - without group-level DefaultSizes, _sizes stayed empty so HandleDrag
///    always early-returned and ResizablePanel.DefaultSize was never consumed;
///    the group now seeds sizes from panel registrations after the first render;
///  - ResizableHandle had role="separator" and tabindex="0" but no keydown
///    handler and no aria-value*/aria-orientation (mirrors SplitterDivider now);
///  - drags that overshot a panel's min/max were rejected, stalling short of
///    the limit; they now clamp exactly to the boundary.
/// </summary>
public class ResizableInteractionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ResizableInteractionTests()
    {
        _ctx.AddLumeoServices();
        // HandleDrag converts pixel deltas to % of the group dimension via
        // getElementDimension — pin it to 1000px so 10px == 1%. The service
        // imports components.js with a version cache-buster, so the module must
        // be set up with the exact same identifier the service computes.
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
        double[]? groupDefaults = null,
        (double Default, double Min, double Max)[]? panels = null,
        L.Orientation direction = L.Orientation.Horizontal)
    {
        panels ??= new[] { (50.0, 10.0, 90.0), (50.0, 10.0, 90.0) };
        return _ctx.Render<L.ResizablePanelGroup>(p => p
            .Add(g => g.Direction, direction)
            .Add(g => g.DefaultSizes, groupDefaults)
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

    // --- Seeding from panel DefaultSize ---

    [Fact]
    public void Panels_Seed_Group_Sizes_From_Their_DefaultSize()
    {
        var cut = RenderGroup(panels: new[] { (30.0, 10.0, 90.0), (70.0, 10.0, 90.0) });

        Assert.Contains("flex: 30.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 70.00 1 0", PanelStyle(cut, 1));
    }

    [Fact]
    public void Unspecified_Panel_Sizes_Fall_Back_To_An_Equal_Split()
    {
        // All three panels keep the DefaultSize parameter default (50) —
        // normalized to a third each.
        var cut = RenderGroup(panels: new[] { (50.0, 10.0, 90.0), (50.0, 10.0, 90.0), (50.0, 10.0, 90.0) });

        Assert.Contains("flex: 33.33 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 33.33 1 0", PanelStyle(cut, 1));
        Assert.Contains("flex: 33.33 1 0", PanelStyle(cut, 2));
    }

    [Fact]
    public void Panel_DefaultSizes_Are_Normalized_To_100_Percent()
    {
        var cut = RenderGroup(panels: new[] { (60.0, 10.0, 90.0), (60.0, 10.0, 90.0) });

        Assert.Contains("flex: 50.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 50.00 1 0", PanelStyle(cut, 1));
    }

    [Fact]
    public void Group_DefaultSizes_Take_Precedence_Over_Panel_DefaultSize()
    {
        var cut = RenderGroup(
            groupDefaults: new[] { 20.0, 80.0 },
            panels: new[] { (30.0, 10.0, 90.0), (70.0, 10.0, 90.0) });

        Assert.Contains("flex: 20.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 80.00 1 0", PanelStyle(cut, 1));
    }

    // --- Keyboard resize on the handle ---

    [Fact]
    public void ArrowRight_On_Horizontal_Handle_Grows_The_Leading_Panel()
    {
        var cut = RenderGroup(); // 50/50; 10px step on 1000px == 1%

        cut.Find("[role='separator']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Contains("flex: 51.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 49.00 1 0", PanelStyle(cut, 1));
    }

    [Fact]
    public void ArrowLeft_On_Horizontal_Handle_Shrinks_The_Leading_Panel()
    {
        var cut = RenderGroup();

        cut.Find("[role='separator']").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Contains("flex: 49.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 51.00 1 0", PanelStyle(cut, 1));
    }

    [Fact]
    public void ArrowDown_On_Vertical_Handle_Grows_The_Leading_Panel()
    {
        var cut = RenderGroup(direction: L.Orientation.Vertical);

        cut.Find("[role='separator']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Contains("flex: 51.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 49.00 1 0", PanelStyle(cut, 1));
    }

    [Fact]
    public void Unrelated_Keys_Do_Not_Resize()
    {
        var cut = RenderGroup();

        cut.Find("[role='separator']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Contains("flex: 50.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 50.00 1 0", PanelStyle(cut, 1));
    }

    // --- Handle ARIA contract ---

    [Fact]
    public void Handle_Exposes_Separator_Aria_Values()
    {
        var cut = RenderGroup(panels: new[] { (30.0, 20.0, 80.0), (70.0, 25.0, 90.0) });

        var handle = cut.Find("[role='separator']");
        Assert.Equal("30", handle.GetAttribute("aria-valuenow"));
        // min = max(own 20, 100 - trailing max 90) = 20
        Assert.Equal("20", handle.GetAttribute("aria-valuemin"));
        // max = min(own 80, 100 - trailing min 25) = 75
        Assert.Equal("75", handle.GetAttribute("aria-valuemax"));
        // A divider between horizontally-arranged panels is a vertical bar.
        Assert.Equal("vertical", handle.GetAttribute("aria-orientation"));
    }

    [Fact]
    public void Vertical_Handle_Has_Horizontal_Aria_Orientation()
    {
        var cut = RenderGroup(direction: L.Orientation.Vertical);

        Assert.Equal("horizontal", cut.Find("[role='separator']").GetAttribute("aria-orientation"));
    }

    // --- Clamping at min/max ---

    [Fact]
    public async Task Drag_Overshooting_The_Min_Clamps_To_The_Boundary()
    {
        var cut = RenderGroup(panels: new[] { (50.0, 45.0, 90.0), (50.0, 10.0, 90.0) });
        var context = cut.FindComponent<L.ResizablePanel>().Instance.Context;

        // -300px on a 1000px group = -30% -> raw newLeft 20 violates MinSize 45,
        // so the drag must land exactly ON 45/55 (previously: rejected, stuck at 50/50).
        await cut.InvokeAsync(() => context.OnHandleDrag.InvokeAsync((0, -300.0)));

        Assert.Contains("flex: 45.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 55.00 1 0", PanelStyle(cut, 1));
    }

    [Fact]
    public async Task Drag_Overshooting_The_Max_Clamps_To_The_Boundary()
    {
        var cut = RenderGroup(); // min 10 / max 90 on both panels

        var context = cut.FindComponent<L.ResizablePanel>().Instance.Context;

        // +800px = +80% -> raw newLeft 130; clamp to min(own max 90, 100 - trailing min 10) = 90.
        await cut.InvokeAsync(() => context.OnHandleDrag.InvokeAsync((0, 800.0)));

        Assert.Contains("flex: 90.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 10.00 1 0", PanelStyle(cut, 1));
    }
}
