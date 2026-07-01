using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using L = Lumeo;

namespace Lumeo.Tests.Components.Resizable;

/// <summary>
/// Battle-test #108 (state-on-data-change): adding/removing a panel at runtime
/// must NOT full-reseed the group back to the default equal split — that wipes
/// the user's dragged / collapsed layout. ResizablePanelGroup.OnAfterRender
/// previously re-seeded _sizes from the panel DefaultSizes whenever the
/// registered panel count diverged from _sizes.Count; the fix seeds only ONCE
/// (the _seeded guard) and otherwise reconciles the count in place, preserving
/// the survivors' relative ratios.
/// </summary>
public class ResizablePanelCountChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ResizablePanelCountChangeTests()
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

    // Builds the group's ChildContent (panels + interleaved handles) from a panel
    // spec so the same group can be re-rendered with a different panel count.
    private static RenderFragment BuildPanels((double Default, double Min, double Max)[] panels) => b =>
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
    };

    private IRenderedComponent<L.ResizablePanelGroup> RenderGroup((double Default, double Min, double Max)[] panels) =>
        _ctx.Render<L.ResizablePanelGroup>(p => p
            .Add(g => g.Direction, L.Orientation.Horizontal)
            .AddChildContent(BuildPanels(panels)));

    private static string PanelStyle(IRenderedComponent<L.ResizablePanelGroup> cut, int idx) =>
        cut.Find($"[data-testid='panel-{idx}']").ParentElement!.GetAttribute("style") ?? "";

    private static readonly (double Default, double Min, double Max) P = (50.0, 10.0, 90.0);

    // --- GROWTH: a dragged layout survives adding a panel ---

    [Fact]
    public async Task Adding_A_Panel_Preserves_The_Dragged_Layout()
    {
        // Two panels, 50/50.
        var cut = RenderGroup(new[] { P, P });

        // Drag the leading panel +100px (= +10% on the 1000px group) -> 60/40.
        var context = cut.FindComponent<L.ResizablePanel>().Instance.Context;
        await cut.InvokeAsync(() => context.OnHandleDrag.InvokeAsync((0, 100.0)));
        Assert.Contains("flex: 60.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 40.00 1 0", PanelStyle(cut, 1));

        // Consumer adds a third panel at runtime.
        cut.Render(p => p.AddChildContent(BuildPanels(new[] { P, P, P })));

        // WITHOUT the fix the group full-reseeds to an equal split, so panel 0
        // would snap to 33.33. WITH the fix the incumbents are scaled to make
        // room for the newcomer's 33.33 share while keeping their 60:40 ratio:
        //   newcomer = 50/150 = 33.33; scale = (100-33.33)/100 = 0.6667
        //   60*0.6667 = 40.00, 40*0.6667 = 26.67.
        Assert.Contains("flex: 40.00 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 26.67 1 0", PanelStyle(cut, 1));
        Assert.Contains("flex: 33.33 1 0", PanelStyle(cut, 2));
    }

    // The dragged leading panel must NOT snap back to the default equal split
    // when a panel is appended. (Tightest single assertion of the regression:
    // before the fix panel 0 reverts to 33.33; after the fix it stays at 40.00.)
    [Fact]
    public async Task Adding_A_Panel_Does_Not_Reset_The_Leading_Panel_To_The_Default_Split()
    {
        var cut = RenderGroup(new[] { P, P });

        var context = cut.FindComponent<L.ResizablePanel>().Instance.Context;
        await cut.InvokeAsync(() => context.OnHandleDrag.InvokeAsync((0, 100.0))); // -> 60/40

        cut.Render(p => p.AddChildContent(BuildPanels(new[] { P, P, P })));

        Assert.DoesNotContain("flex: 33.33 1 0", PanelStyle(cut, 0));
        Assert.Contains("flex: 40.00 1 0", PanelStyle(cut, 0));
    }
}
