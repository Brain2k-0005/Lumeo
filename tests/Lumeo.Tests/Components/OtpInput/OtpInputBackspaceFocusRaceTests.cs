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
/// the rendered markup already reflect the edit that triggered it, AND does the
/// call target the right cell id? Pre-fix: no (the handler calls FocusElement
/// before any render happens, so the DOM snapshot is still stale). Post-fix
/// (focus deferred to OnAfterRenderAsync): yes, every time — OnAfterRenderAsync
/// only runs after a render has been applied and is on the SAME ordered
/// dispatch/render pipeline, so the pending focus target is guaranteed to exist
/// in fresh, post-render markup.
/// </summary>
public class OtpInputBackspaceFocusRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OtpInputBackspaceFocusRaceTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>Records, for every FocusElement call, the target id AND a
    /// snapshot of every cell's rendered "value" attribute AT THE EXACT MOMENT
    /// the call was made.</summary>
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
    // Type five digits, then Backspace repeatedly from the LAST FILLED box
    // (index equals the char being cleared; the target is the DIFFERENT,
    // stable previous cell). Each focus-move call must land on the correct id
    // AND see markup that already reflects the shrunken value.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Repeated_Backspace_From_Filled_Box_Focus_Calls_Target_Correct_Id_After_Render()
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
            var expectedId = cut.FindAll("input")[index - 1].GetAttribute("id");

            cut.FindAll("input")[index].KeyDown(new KeyboardEventArgs { Key = "Backspace" });

            Assert.True(interop.Calls.Count > step, $"Expected a focus call after clearing box {index}.");
            var call = interop.Calls[step];
            Assert.Equal(expectedId, call.ElementId);
            Assert.Equal(expectedAfterEachStep[step], call.RenderedValueAtCallTime);
        }

        // The last Backspace (index 0) clears the first box but issues no
        // focus-backward call (there is no box before it).
        cut.FindAll("input")[0].KeyDown(new KeyboardEventArgs { Key = "Backspace" });
        Assert.Equal(expectedAfterEachStep.Length, interop.Calls.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // The field report's ACTUAL repro: caret behind the last digit (the empty
    // box past the end of Value), then Backspace repeatedly. That branch
    // (OtpInput.razor's "Backspace on an empty cell" case) clears the PREVIOUS
    // cell and refocuses THAT SAME index — so the just-recreated element IS
    // the focus target, not a stable neighbour. This is the exact race
    // described in the report ("the call lands on an element that is replaced
    // right after").
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Repeated_Backspace_From_Empty_Box_Behind_Last_Digit_Focus_Calls_Target_Correct_Id_After_Render()
    {
        var interop = new RenderOrderCheckingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        // Length 6, Value "12345": box 5 is the empty box behind the last digit.
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 6)
            .Add(c => c.Value, "12345"));
        interop.Cut = cut;

        var expectedValues = new[] { "1234", "123", "12", "1" };
        var expectedFocusIndex = new[] { 4, 3, 2, 1 };
        var triggerIndex = 5;

        for (var step = 0; step < expectedValues.Length; step++)
        {
            // The recreated cell IS the next trigger/target, so its id must be
            // captured before the KeyDown that (re)creates it.
            var expectedId = cut.FindAll("input")[expectedFocusIndex[step]].GetAttribute("id");

            cut.FindAll("input")[triggerIndex].KeyDown(new KeyboardEventArgs { Key = "Backspace" });

            Assert.True(interop.Calls.Count > step, $"Expected a focus call after backspacing from box {triggerIndex}.");
            var call = interop.Calls[step];
            Assert.Equal(expectedId, call.ElementId);
            Assert.Equal(expectedValues[step], call.RenderedValueAtCallTime);

            // The just-focused (now-empty) cell is where the next Backspace fires.
            triggerIndex = expectedFocusIndex[step];
        }

        // Final Backspace clears box 0 but issues no further focus-backward call.
        cut.FindAll("input")[0].KeyDown(new KeyboardEventArgs { Key = "Backspace" });
        Assert.Equal(expectedValues.Length, interop.Calls.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Same race, different trigger: typing an accepted digit re-keys (and thus
    // recreates) the just-filled cell too. The focus-forward call must also
    // land after the render that created it, on the correct id.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sequential_Typing_Focus_Calls_Target_Correct_Id_After_Render()
    {
        var interop = new RenderOrderCheckingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, ""));
        interop.Cut = cut;

        var expectedId = cut.FindAll("input")[1].GetAttribute("id");
        cut.FindAll("input")[0].Input(new ChangeEventArgs { Value = "1" });

        Assert.Single(interop.Calls);
        Assert.Equal(expectedId, interop.Calls[0].ElementId);
        Assert.Equal("1", interop.Calls[0].RenderedValueAtCallTime);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // The invalid-char rejection path also re-keys the cell (nonce bump) and
    // must defer its restore-focus call the same way, back to the SAME id.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Rejected_Char_Focus_Restore_Targets_Same_Id_After_Render()
    {
        var interop = new RenderOrderCheckingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.InputMode, "Numeric")
            .Add(c => c.Value, ""));
        interop.Cut = cut;

        var expectedId = cut.FindAll("input")[0].GetAttribute("id");
        cut.FindAll("input")[0].Input(new ChangeEventArgs { Value = "x" });

        Assert.Single(interop.Calls);
        Assert.Equal(expectedId, interop.Calls[0].ElementId);
        // The rejected char must already be gone from the rendered markup by
        // the time the restore-focus call fires.
        Assert.Equal("", interop.Calls[0].RenderedValueAtCallTime);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HandlePaste is reached from a JS callback, NOT a markup-bound event on
    // THIS component — so, unlike @oninput/@onkeydown, Blazor does not
    // automatically re-render OtpInput afterward. A caller with a BARE
    // ValueChanged (no @bind-Value round trip feeding Value back in as a
    // parameter) must still see the pasted digits paint AND the pending focus
    // move fire, which requires HandlePaste to explicitly request its own
    // render.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Paste_With_Bare_ValueChanged_Focuses_Next_Empty_Box_After_Render()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        string? changed = null;
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 6)
            .Add(c => c.ValueChanged, v => changed = v));

        var expectedId = cut.FindAll("input")[3].GetAttribute("id");

        var otp = cut.Instance;
        await cut.InvokeAsync(() =>
            (Task)typeof(L.OtpInput)
                .GetMethod("HandlePaste", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(otp, ["123"])!);

        Assert.Equal("123", changed);

        // The pasted digits must have actually painted (proves OtpInput
        // re-rendered itself, not just notified the caller).
        var inputs = cut.FindAll("input");
        Assert.Equal("1", inputs[0].GetAttribute("value"));
        Assert.Equal("2", inputs[1].GetAttribute("value"));
        Assert.Equal("3", inputs[2].GetAttribute("value"));

        Assert.Contains(expectedId!, interop.FocusElementCalls);
    }
}
