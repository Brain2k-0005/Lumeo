using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InputMask;

/// <summary>
/// #177: Backspace truncated from the end (ignored the caret), ValueChanged fired
/// twice (keydown + input), and the caret was never restored after re-masking.
/// Deletion now flows through the single native input handler (caret-correct),
/// fires ValueChanged once, and the caret is repositioned via SetInputCaret.
/// </summary>
public class InputMaskCaretTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public InputMaskCaretTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Typing_Fires_ValueChanged_Once()
    {
        var count = 0;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###")
            .Add(c => c.ValueChanged, _ => count++));

        cut.Find("input").Input("123");

        Assert.Equal(1, count);
    }

    [Fact]
    public void Deleting_Fires_ValueChanged_Once()
    {
        var count = 0;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###")
            .Add(c => c.Value, "123456")
            .Add(c => c.ValueChanged, _ => count++));

        // A native input event (what Backspace produces) with one char removed.
        cut.Find("input").Input("12345");

        // Exactly one — previously keydown + input double-fired.
        Assert.Equal(1, count);
    }

    [Fact]
    public void Deleting_Middle_Char_Is_Caret_Aware_Not_End_Truncation()
    {
        string? raw = null;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###")
            .Add(c => c.Value, "123456")
            .Add(c => c.ValueChanged, v => raw = v));

        // Caret was after "2"; deleting yields the browser string "13-456".
        // The raw value must become "13456" (caret-aware), NOT "12345"
        // (the old _rawValue[..^1] end-truncation).
        cut.Find("input").Input("13-456");

        Assert.Equal("13456", raw);
    }

    [Fact]
    public void Input_Restores_The_Caret()
    {
        _interop.InputCaret = 3; // browser caret after typing "123"
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###"));

        cut.Find("input").Input("123");

        // The masked display is "123" (no dangling separator until the next field
        // starts), so the caret rests at the end of the three digits, index 3.
        Assert.NotEmpty(_interop.SetInputCaretCalls);
        Assert.Equal(3, _interop.SetInputCaretCalls[^1].Position);
    }

    [Fact]
    public void Caret_Maps_To_End_Of_Filled_Significant_Slots()
    {
        // Two significant chars typed; the display is "12" (the separator only
        // appears once the third slot is entered), so the caret lands at index 2.
        _interop.InputCaret = 2;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "##/##"));

        cut.Find("input").Input("12");

        Assert.Equal(2, _interop.SetInputCaretCalls[^1].Position);
    }

    [Fact]
    public void Caret_Maps_Past_Interior_Literal_When_More_Follows()
    {
        // Caret is between the digits "12|3456" of a re-masked "12-3456"-style
        // value. Two significant chars precede it, and a literal sits right after
        // the second slot, so the caret skips past the separator to index 3.
        _interop.InputCaret = 2;
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "##-###")
            .Add(c => c.Value, "12345"));

        // Browser string with the caret implied after "12"; full value present so
        // ApplyMask renders the trailing separator + remaining digits.
        cut.Find("input").Input("12-345");

        // Display "12-345": after 2 filled slots the '-' literal is skipped → 3.
        Assert.Equal(3, _interop.SetInputCaretCalls[^1].Position);
    }
}
