using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InputMask;

/// <summary>
/// State-on-data-change regressions for InputMask.
///
/// #41 (medium): when a typed character is mask-rejected, the re-masked display
/// can equal the PREVIOUS render's value, so Blazor's diff emits no patch for the
/// uncontrolled <c>value="@_displayValue"</c> binding and the rejected char stays
/// visible in the real DOM. The component now force-writes the masked display to
/// the DOM via <c>SetInputValue</c> whenever the display diverges from the raw
/// browser string. bUnit always re-renders the value attribute from the field, so
/// the bug is asserted via the recorded interop call (the mechanism), not markup.
///
/// #155 (low): changing <c>Mask</c> (or <c>PromptChar</c>) while <c>Value</c> stays
/// the same left a stale masked display because OnParametersSet only re-derived on
/// a <c>Value</c> change. The re-derivation guard now also fires on a mask-definition
/// change, which IS reflected in the rendered value attribute.
/// </summary>
public class InputMaskStateOnDataChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public InputMaskStateOnDataChangeTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ---- #41: rejected char must be force-synced back to the DOM ----

    [Fact]
    public void Rejected_Char_Forces_A_Value_Resync_To_The_Dom()
    {
        // Start with two digits already masked in ("12"). The browser then reports
        // "12a" (the user typed a letter into the third digit slot).
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###")
            .Add(c => c.Value, "12"));

        cut.Find("input").Input("12a");

        // 'a' is rejected, so the re-masked display is "12" — identical to the
        // previous render. Without the fix Blazor patches nothing and the browser
        // keeps "12a"; with the fix the corrected display is pushed to the DOM.
        var write = Assert.Single(_interop.SetInputValueCalls);
        Assert.Equal("12", write.Value);
    }

    [Fact]
    public void Clean_Input_Does_Not_Force_A_Value_Resync()
    {
        // Negative control: "123" passes the mask unchanged, the display equals the
        // browser string, so there is nothing to force-write.
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###"));

        cut.Find("input").Input("123");

        Assert.Empty(_interop.SetInputValueCalls);
    }

    // ---- #155: a runtime Mask change re-derives the display for the same Value ----

    [Fact]
    public void Changing_Mask_Re_Derives_Display_When_Value_Unchanged()
    {
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(c => c.Mask, "###-###")
            .Add(c => c.Value, "123456"));

        Assert.Equal("123-456", cut.Find("input").GetAttribute("value"));

        // Swap the literal separator from '-' to ' ' while Value stays "123456".
        cut.Render(p => p
            .Add(c => c.Mask, "### ###")
            .Add(c => c.Value, "123456"));

        // Without the fix the guard (Value != _rawValue) is false, so the display
        // stays the stale "123-456". With the fix the mask-change re-derives it.
        Assert.Equal("123 456", cut.Find("input").GetAttribute("value"));
    }
}
