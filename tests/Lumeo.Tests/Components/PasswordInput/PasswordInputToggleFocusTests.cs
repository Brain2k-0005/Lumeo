using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.PasswordInput;

/// <summary>
/// Regression: the visibility toggle had <c>tabindex="-1"</c>, making it
/// unreachable for keyboard users. It must be in the tab order and carry
/// visible focus styling.
/// </summary>
public class PasswordInputToggleFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PasswordInputToggleFocusTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Toggle_Button_Is_Keyboard_Reachable()
    {
        var cut = _ctx.Render<Lumeo.PasswordInput>();

        var toggle = cut.Find("button[type='button']");
        Assert.Null(toggle.GetAttribute("tabindex"));
    }

    [Fact]
    public void Toggle_Button_Has_Focus_Visible_Styling()
    {
        var cut = _ctx.Render<Lumeo.PasswordInput>();

        var cls = cut.Find("button[type='button']").GetAttribute("class") ?? "";
        Assert.Contains("focus-visible:ring", cls);
    }
}
