using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PasswordInput;

/// <summary>
/// Regression (battle-wave2 n47, state-on-data-change): a PasswordInput that was
/// revealed (<c>_showPassword == true</c>) must NOT keep leaking its secret once
/// the <c>Disabled</c> parameter flips to <c>true</c>. The visibility toggle is
/// uncontrolled internal UI state; a parent re-render that disables the field has
/// to re-mask the input and the (now disabled) toggle must not flip it back.
/// </summary>
public class PasswordInputDisabledRevealTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PasswordInputDisabledRevealTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Disabling_a_revealed_field_re_masks_the_input()
    {
        // Reveal the password first (enabled, toggle visible).
        var cut = _ctx.Render<L.PasswordInput>(p => p
            .Add(c => c.ShowToggle, true)
            .Add(c => c.Value, "MyPassword123"));

        cut.Find("button[type='button']").Click();
        Assert.Equal("text", cut.Find("input").GetAttribute("type"));

        // Parent re-render disables the field. The secret must be re-hidden:
        // without the fix the input type stays "text" (still revealed) because the
        // internal _showPassword flag survives the data change.
        cut.Render(p => p.Add(c => c.Disabled, true));

        Assert.Equal("password", cut.Find("input").GetAttribute("type"));
    }

    [Fact]
    public void Toggle_button_is_disabled_when_field_is_disabled()
    {
        var cut = _ctx.Render<L.PasswordInput>(p => p
            .Add(c => c.ShowToggle, true)
            .Add(c => c.Disabled, true)
            .Add(c => c.Value, "MyPassword123"));

        var toggle = cut.Find("button[type='button']");
        Assert.True(toggle.HasAttribute("disabled"));
    }

    [Fact]
    public void Clicking_toggle_on_a_disabled_field_does_not_reveal_the_secret()
    {
        var cut = _ctx.Render<L.PasswordInput>(p => p
            .Add(c => c.ShowToggle, true)
            .Add(c => c.Disabled, true)
            .Add(c => c.Value, "MyPassword123"));

        // ToggleVisibility is guarded against Disabled, so a click must be a no-op.
        cut.Find("button[type='button']").Click();

        Assert.Equal("password", cut.Find("input").GetAttribute("type"));
    }
}
