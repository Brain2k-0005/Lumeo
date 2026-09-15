using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.OtpInput;

/// <summary>
/// Field report #464, finding 2: type five digits, then Backspace repeatedly —
/// the first Backspace clears a digit, then focus goes nowhere until the user
/// clicks back into the field. The C# deletion logic is correct (value shrinks,
/// the right previous cell is targeted, in the right order); what breaks is
/// WHEN the focus request is issued relative to the render.
///
/// Root cause: `OtpInput.razor` keys each cell on
/// `{index}:{GetChar(index)}:{_cellNonce[index]}`, so a cell whose character
/// changes gets its `&lt;input&gt;` destroyed and recreated on the next render.
/// The pre-fix code called `Interop.FocusElement(...)` directly inside the
/// keydown handler, BEFORE Blazor's implicit post-handler render has run — so
/// the call is issued against DOM state that is about to change underneath it,
/// racing the render that the focus target actually depends on.
///
/// bUnit can't reproduce the real SignalR/browser timing, but it CAN prove the
/// ORDERING claim precisely: at the moment `Interop.FocusElement` fires, does
/// the rendered markup already reflect the edit that triggered it? Pre-fix: no
/// (the handler calls FocusElement before any render happens, so the DOM
/// snapshot is still stale). Post-fix (focus deferred to OnAfterRenderAsync):
/// yes, every time — OnAfterRenderAsync only runs after a render has been
/// applied and is on the SAME ordered dispatch/render pipeline, so the pending
/// focus target is guaranteed to exist in fresh, post-render markup.
/// </summary>
public class OtpInputBackspaceFocusRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OtpInputBackspaceFocusRaceTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>Records, for every FocusElement call, a snapshot of every cell's
    /// rendered "value" attribute AT THE EXACT MOMENT the call was made.</summary>
    private sealed class RenderOrderCheckingInteropService : TrackingInteropService
    {
        public IRenderedComponent<L.OtpInput>? Cut { get; set; }
        public List<(string ElementId, string RenderedValueAtCallTime)> Calls { get; } = new();

        public override ValueTask FocusElement(string elementId)
        {
            var renderedValue = string.Concat(Cut!.FindAll("input").Select(i => i.GetAttribute("value") ?? ""));
            Calls.Add((elementId, renderedValue));
            return base.FocusElement(elementId);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Type five digits, then Backspace repeatedly from the last box. Each
    // Backspace's focus-move call must see markup that ALREADY reflects the
    // shrunken value — i.e. the render happened before the interop call, not
    // after (or concurrently with) it.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Repeated_Backspace_Focus_Calls_See_Already_Rendered_Value()
    {
        var interop = new RenderOrderCheckingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 5)
            .Add(c => c.Value, "12345"));
        interop.Cut = cut;

        // Values expected to already be rendered at the moment each Backspace's
        // focus-move call fires: box 4 cleared -> "1234", box 3 -> "123", etc.
        var expectedAfterEachStep = new[] { "1234", "123", "12", "1" };

        for (var step = 0; step < expectedAfterEachStep.Length; step++)
        {
            var index = 4 - step;
            cut.FindAll("input")[index].KeyDown(new KeyboardEventArgs { Key = "Backspace" });

            Assert.True(interop.Calls.Count > step, $"Expected a focus call after clearing box {index}.");
            var call = interop.Calls[step];
            Assert.Equal(expectedAfterEachStep[step], call.RenderedValueAtCallTime);
        }

        // The last Backspace (index 0) clears the first box but issues no
        // focus-backward call (there is no box before it).
        cut.FindAll("input")[0].KeyDown(new KeyboardEventArgs { Key = "Backspace" });
        Assert.Equal(expectedAfterEachStep.Length, interop.Calls.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Same race, different trigger: typing an accepted digit re-keys (and thus
    // recreates) the just-filled cell too. The focus-forward call must also
    // land after the render that created it.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sequential_Typing_Focus_Calls_See_Already_Rendered_Value()
    {
        var interop = new RenderOrderCheckingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, ""));
        interop.Cut = cut;

        cut.FindAll("input")[0].Input(new ChangeEventArgs { Value = "1" });

        Assert.Single(interop.Calls);
        Assert.Equal("1", interop.Calls[0].RenderedValueAtCallTime);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // The invalid-char rejection path also re-keys the cell (nonce bump) and
    // must defer its restore-focus call the same way.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Rejected_Char_Focus_Restore_Sees_Already_Reverted_Value()
    {
        var interop = new RenderOrderCheckingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.InputMode, "Numeric")
            .Add(c => c.Value, ""));
        interop.Cut = cut;

        cut.FindAll("input")[0].Input(new ChangeEventArgs { Value = "x" });

        Assert.Single(interop.Calls);
        // The rejected char must already be gone from the rendered markup by
        // the time the restore-focus call fires.
        Assert.Equal("", interop.Calls[0].RenderedValueAtCallTime);
    }
}
