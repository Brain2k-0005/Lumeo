using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.ToolCallCard;

/// <summary>
/// Battle-wave3 regressions for the ToolCallCard copy button:
///
/// #24 (state-on-data-change) — The Error and Output sections both passed
///   CopyTarget.Output to the copy-button fragment, so the transient "Copied"
///   flag set by copying the Output carried straight over to the Error slot when
///   the card was reused for an error result. Each slot now has its own
///   CopyTarget identity (Input/Output/Error).
///
/// #66 (lifecycle) — The "Copied" indicator latched forever despite the comment
///   calling it transient: nothing reset it. Copying now schedules a revert back
///   to "Copy" after a short delay, cancelled on dispose.
/// </summary>
public class ToolCallCardCopyStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    // A pure C# fake whose CopyToClipboard is a synchronous no-op, so the click's
    // _copied = target assignment is observable immediately after Click().
    private readonly TrackingInteropService _interop = new();

    public ToolCallCardCopyStateTests()
    {
        _ctx.AddLumeoServices();
        // Last interface registration wins, so the card resolves the fake.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.ToolCallCard> RenderSuccessCardWithOutput() =>
        _ctx.Render<L.ToolCallCard>(p => p
            .Add(c => c.ToolName, "search")
            .Add(c => c.Status, L.ToolCallCard.ToolCallStatus.Success)
            .Add(c => c.Output, "the result")
            .Add(c => c.DefaultOpen, true));

    // ── #24: the copied flag must not leak across the Output -> Error swap ──────

    [Fact]
    public void CopiedIndicator_DoesNotCarryFromOutputToError()
    {
        var cut = RenderSuccessCardWithOutput();

        // Copy from the Output slot -> that button shows the transient "Copied".
        cut.Find("button").Click();
        Assert.Contains("Copied", cut.Markup);

        // The SAME card instance is now reused for an error result (Output -> Error
        // swap). The private _copied flag persists across the re-render.
        cut.Render(p => p
            .Add(c => c.Status, L.ToolCallCard.ToolCallStatus.Error)
            .Add(c => c.ErrorMessage, "boom"));

        // The Error slot's copy button must NOT inherit the Output slot's "Copied"
        // state. Without the fix both slots share CopyTarget.Output, so the Error
        // button renders "Copied" even though nothing was copied from it.
        var btn = cut.Find("button");
        Assert.Equal("Copy to clipboard", btn.GetAttribute("aria-label"));
        Assert.DoesNotContain("Copied", cut.Markup);
    }

    // ── #66: the "Copied" affordance is transient and reverts to "Copy" ─────────

    [Fact]
    public void CopiedIndicator_RevertsToCopy_AfterTransientDelay()
    {
        var cut = RenderSuccessCardWithOutput();

        cut.Find("button").Click();
        // Immediately latches the "Copied" affordance.
        Assert.Contains("Copied", cut.Markup);

        // ...which is transient: it must revert to "Copy" on its own. Without the
        // revert the indicator sticks on "Copied" forever and this times out.
        cut.WaitForAssertion(() => Assert.DoesNotContain("Copied", cut.Markup));
    }
}
