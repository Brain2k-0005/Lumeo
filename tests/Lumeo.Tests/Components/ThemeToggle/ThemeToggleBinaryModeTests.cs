using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.ThemeToggle;

/// <summary>
/// Regression tests for <c>ThemeToggle.IncludeSystem</c>: with the binary mode a
/// single click must always land on the OPPOSITE resolved appearance instead of
/// walking the three-way System cycle (where a System->Dark step is invisible
/// when the OS already prefers dark — reported as "I have to click twice").
/// </summary>
public class ThemeToggleBinaryModeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ThemeToggleBinaryModeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task Binary_Mode_Single_Click_From_Dark_Requests_Light()
    {
        // ThemeService.IsDark is resolved via JS ("themeManager.isDark") — make the
        // mock report a dark appearance so the toggle's internal state really IS
        // dark before the click (bUnit's loose default would return false).
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(true);
        var theme = _ctx.Services.GetRequiredService<ThemeService>();
        await theme.SetModeAsync(ThemeMode.Dark);

        var cut = _ctx.Render<L.ThemeToggle>(p => p.Add(x => x.IncludeSystem, false));
        // The toggle syncs its dark flag asynchronously in OnAfterRenderAsync — wait
        // for the rendered state instead of racing it (deflake rule: no fixed waits).
        cut.WaitForState(() => cut.Find("button").GetAttribute("aria-pressed") == "true", TimeSpan.FromSeconds(5));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Equal(ThemeMode.Light, theme.CurrentMode), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Default_Mode_Still_Walks_The_Three_Way_Cycle()
    {
        var theme = _ctx.Services.GetRequiredService<ThemeService>();
        await theme.SetModeAsync(ThemeMode.System);

        var cut = _ctx.Render<L.ThemeToggle>();
        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // System -> Dark is the documented first step of the cycle.
        cut.WaitForAssertion(() => Assert.Equal(ThemeMode.Dark, theme.CurrentMode), TimeSpan.FromSeconds(5));
    }
}
