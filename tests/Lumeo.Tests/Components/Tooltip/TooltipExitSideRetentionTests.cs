using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tooltip;

/// <summary>
/// Minor (Codex/CodeRabbit) — tooltip side desync through the exit window. Cleanup()
/// used to null <c>_resolvedSide</c> immediately on close, so during the zoom-out the
/// frozen box kept its collision-flipped position while data-side / ArrowClasses /
/// ArrowStyle snapped back to the unflipped requested side. The fix retains the resolved
/// side through the exit and clears it only once the box actually unmounts (FinishExit).
///
/// bUnit can't run floating-ui, so the collision flip is simulated by stubbing the
/// <c>positionFixed</c> JS return (same technique as
/// <see cref="TooltipArrowFollowsSideTests"/>).
/// </summary>
public class TooltipExitSideRetentionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TooltipExitSideRetentionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class TooltipSideProbe : ComponentBase
    {
        [Parameter] public L.Side Side { get; set; } = L.Side.Top;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "HideDelay", 0);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "Side", Side);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Tooltip text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    // The interop imports components.js with a version cache-buster, so stub THAT path.
    private void StubCollisionFlipTo(string resolved)
    {
        var v = typeof(Lumeo.Services.ComponentInteropService).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(Lumeo.Services.ComponentInteropService).Assembly.GetName().Version?.ToString()
                ?? "0";
        var module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        module.Setup<string>("positionFixed", _ => true).SetResult(resolved);
    }

    [Fact]
    public void Resolved_Flipped_Side_Is_Retained_Through_The_Exit_Window()
    {
        // A preferred-Top tooltip that collision-flips to render BELOW its trigger keeps
        // data-side="bottom" while OPEN. On close the box freezes for the zoom-out; its
        // arrow + data-side must STAY on the flipped (bottom) edge through the exit —
        // not snap back to the unflipped requested Top mid-fade.
        StubCollisionFlipTo("bottom");

        var cut = _ctx.Render<TooltipSideProbe>(p => p.Add(x => x.Side, L.Side.Top));
        cut.Find("div").MouseEnter(new MouseEventArgs());

        Assert.Equal("bottom", cut.Find("[role='tooltip']").GetAttribute("data-side")); // flip applied while open

        // Close → the tooltip enters its exit window (still mounted, data-state=closed).
        cut.Find("div").MouseLeave(new MouseEventArgs());

        // Force a re-render during the exit window (AFTER the close-time Cleanup ran).
        // Pre-fix Cleanup nulled _resolvedSide, so this render snapped data-side back to
        // "top" while the frozen box stayed below the trigger (the desync). Post-fix the
        // resolved side is retained until the box actually unmounts.
        cut.Render(p => p.Add(x => x.Side, L.Side.Top));

        var exiting = cut.Find("[role='tooltip']");
        Assert.Equal("closed", exiting.GetAttribute("data-state"));
        Assert.Equal("bottom", exiting.GetAttribute("data-side"));
        var arrowClass = exiting.QuerySelector(".rotate-45")!.GetAttribute("class") ?? "";
        Assert.Contains("bottom-full", arrowClass);   // arrow stays on the trigger-facing edge
        Assert.DoesNotContain("top-full", arrowClass); // not snapped back to the requested side
    }
}
