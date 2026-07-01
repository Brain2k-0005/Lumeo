using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Input;

/// <summary>
/// Regression coverage for the clear-button focus-loss bug (battle-wave2 n=154,
/// keyboard-a11y): clicking the clearable "X" moved focus onto that button, and
/// clearing the value removes the button from the DOM (it renders only while the
/// value is non-empty). Focus therefore fell to &lt;body&gt;, stranding keyboard
/// users (WCAG 2.4.3). <c>HandleClear</c> must restore focus to the &lt;input&gt;
/// after clearing.
///
/// bUnit cannot move real DOM focus, so this asserts the OBSERVABLE MECHANISM:
/// the component calls <c>ElementReference.FocusAsync()</c>, which bUnit records
/// and exposes via <see cref="FocusAsyncAssertJSInteropExtensions.VerifyFocusAsyncInvoke"/>.
/// Without the fix, FocusAsync is never invoked from the clear path and the
/// verification (expecting exactly one invocation) fails.
/// </summary>
public class InputClearFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InputClearFocusTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Clicking_Clear_Button_Restores_Focus_To_Input()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Clearable, true)
            .Add(b => b.Value, "hello"));

        // The clear button is only present while the value is non-empty.
        var clearBtn = cut.Find("button[aria-label='Clear']");
        clearBtn.Click();

        // Focus must be handed back to the input (not left on <body>).
        // AutoFocus is false, so the clear path is the ONLY trigger of FocusAsync.
        _ctx.JSInterop.VerifyFocusAsyncInvoke();
    }
}
