using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ThemeToggle;

/// <summary>
/// Reality check against the gap-scan assumption: ThemeToggleBehaviorTests already pins
/// the mode-cycle outcome of activation via .Click() ("First_Click_From_System_Cycles_
/// To_Dark" — the exact handler a synthesized Enter/Space keydown would run, since
/// ThemeToggle is a plain native &lt;button&gt;), and ThemeToggleBinaryModeTests covers the
/// IncludeSystem=false variant. Writing another click-cycles-the-mode test here would be
/// tautological. The one keyboard-specific angle neither file covers: the button carries
/// no tabindex override, so native Tab genuinely reaches it.
/// </summary>
public class ThemeToggleKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ThemeToggleKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.JSInterop.Setup<string>("themeManager.getMode").SetResult("system");
        _ctx.JSInterop.Setup<string>("themeManager.getScheme").SetResult("zinc");
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Toggle_Button_Carries_No_Tabindex_Override()
    {
        var cut = _ctx.Render<L.ThemeToggle>();

        var button = cut.Find("button");
        Assert.False(button.HasAttribute("tabindex"));
        Assert.False(button.HasAttribute("disabled"));
    }
}
