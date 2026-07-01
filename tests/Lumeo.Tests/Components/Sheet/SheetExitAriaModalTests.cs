using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sheet;

/// <summary>
/// Battle-wave2 #185 (keyboard-a11y) — during the exit phase the Sheet panel stays
/// mounted (via the <c>_exiting</c> latch) so the slide/fade-out can play, but its focus
/// trap + scroll lock were already torn down by Cleanup(). It must therefore stop
/// advertising itself as a modal: while exiting (<c>Context.IsOpen == false</c>) the
/// <c>role=dialog</c> panel now renders <c>aria-modal="false"</c> instead of the stale
/// <c>aria-modal="true"</c>, so the ARIA state matches the now-non-trapping panel.
///
/// bUnit cannot observe real focus, so the test asserts the OBSERVABLE markup change:
/// the <c>aria-modal</c> attribute flips from "true" (open) to "false" (exiting) on the
/// same mounted dialog element.
/// </summary>
public class SheetExitAriaModalTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SheetExitAriaModalTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Sheet> RenderSheet(bool open)
    {
        return _ctx.Render<L.Sheet>(p => p
            .Add(s => s.Open, open)
            .Add(s => s.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetContent>(0);
                b.AddAttribute(1, "Side", L.Side.Right);
                b.AddAttribute(2, "Animation", L.SheetContent.SheetAnimation.Slide);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body")));
                b.CloseComponent();
            })));
    }

    [Fact]
    public void Open_Panel_Is_Modal()
    {
        var cut = RenderSheet(open: true);
        var dialog = cut.Find("[role='dialog']");
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
    }

    [Fact]
    public void Exiting_Panel_Drops_AriaModal()
    {
        // Open, then close. The panel lingers (slide-out) but its focus trap + scroll
        // lock were torn down by Cleanup() — so it must no longer claim aria-modal=true.
        var cut = RenderSheet(open: true);
        Assert.Equal("true", cut.Find("[role='dialog']").GetAttribute("aria-modal"));

        cut.Render(p => p.Add(s => s.Open, false));

        // Still mounted for the exit animation, but now advertised as non-modal.
        cut.WaitForAssertion(() =>
        {
            var dialog = cut.Find("[role='dialog']");
            // sanity: confirm we are in the exit phase (panel kept mounted, sliding out)
            Assert.Contains("animate-slide-out-to-right", dialog.GetAttribute("class") ?? "");
            Assert.Equal("false", dialog.GetAttribute("aria-modal"));
        });
    }
}
