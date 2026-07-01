using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// Battle-test regression (wave1 #76, lifecycle/low): the DatePicker's uncontrolled
/// fallback (<c>_internalOpen</c>) was only ever written while <c>Open</c> was null.
/// So once the parent took control (<c>Open</c> non-null) and closed the popover,
/// <c>_internalOpen</c> stayed stale at its last uncontrolled value. Reverting to
/// uncontrolled (<c>Open</c> -> null) then resurrected that stale state and reopened
/// the popover unexpectedly.
///
/// The fix mirrors a non-null <c>Open</c> into <c>_internalOpen</c> in
/// <c>OnParametersSet</c>, so the uncontrolled fallback is always current when control
/// is released. These tests use the default button trigger (no Inline) so the popover's
/// open/closed state is observable via the Calendar markup ("Mo"/"Tu" day headers).
/// </summary>
public class DatePickerControlledRevertTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerControlledRevertTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Revert_To_Uncontrolled_After_Controlled_Close_Does_Not_Reopen_Popover()
    {
        // Start uncontrolled (Open = null). AllowKeyboardInput=false keeps the legacy
        // button trigger so the popover toggle is driven the same way the other tests use.
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.AllowKeyboardInput, false));

        // 1. Uncontrolled open via the trigger -> _internalOpen = true.
        cut.Find("button[type='button']").Click();
        Assert.Contains("Mo", cut.Markup);

        // 2. Parent takes control and closes it (Open = false). The popover closes,
        //    but before the fix _internalOpen stayed stale at true.
        cut.Render(p => p.Add(c => c.Open, false));
        Assert.DoesNotContain("Mo", cut.Markup);

        // 3. Parent releases control (Open -> null). The uncontrolled fallback now owns
        //    the state again. Before the fix _internalOpen was still true, so the popover
        //    silently reopened. With the fix it was mirrored to false, so it stays closed.
        cut.Render(p => p.Add(c => c.Open, (bool?)null));
        Assert.DoesNotContain("Mo", cut.Markup);
    }

    [Fact]
    public void Revert_To_Uncontrolled_After_Controlled_Open_Keeps_Popover_Open()
    {
        // Symmetric case: the controlled value was open when control is released.
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.AllowKeyboardInput, false));

        // Parent opens it via the controlled parameter (no uncontrolled click first,
        // so _internalOpen starts at its false default).
        cut.Render(p => p.Add(c => c.Open, true));
        Assert.Contains("Mo", cut.Markup);

        // Release control. The mirrored _internalOpen is now true, so the popover the
        // user is looking at stays open instead of snapping shut on the next render.
        cut.Render(p => p.Add(c => c.Open, (bool?)null));
        Assert.Contains("Mo", cut.Markup);
    }
}
