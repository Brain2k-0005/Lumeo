using Bunit;
using Xunit;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Services;

public class ThemeServiceTests : IDisposable
{
    private readonly BunitContext _ctx = new();
    private readonly ThemeService _service;

    public ThemeServiceTests()
    {
        _ctx.AddLumeoServices();
        // Default JS return values for theme operations
        _ctx.JSInterop.Setup<string>("themeManager.getMode").SetResult("system");
        _ctx.JSInterop.Setup<string>("themeManager.getScheme").SetResult("orange");
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);

        _service = _ctx.Services.GetRequiredService<ThemeService>();
    }

    public void Dispose() => _ctx.Dispose();

    // --- Default state ---

    [Fact]
    public void DefaultMode_Is_System()
    {
        Assert.Equal(ThemeMode.System, _service.CurrentMode);
    }

    [Fact]
    public void DefaultScheme_Is_Orange()
    {
        Assert.Equal("orange", _service.CurrentScheme);
    }

    [Fact]
    public void DefaultIsDark_Is_False()
    {
        Assert.False(_service.IsDark);
    }

    // --- AvailableSchemes ---

    [Fact]
    public void AvailableSchemes_Is_Not_Empty()
    {
        Assert.NotEmpty(ThemeService.AvailableSchemes);
    }

    [Fact]
    public void AvailableSchemes_Contains_Orange()
    {
        Assert.Contains(ThemeService.AvailableSchemes, s => s.Id == "orange");
    }

    [Fact]
    public void AvailableSchemes_Contains_Blue()
    {
        Assert.Contains(ThemeService.AvailableSchemes, s => s.Id == "blue");
    }

    [Fact]
    public void AvailableSchemes_Contains_Zinc()
    {
        Assert.Contains(ThemeService.AvailableSchemes, s => s.Id == "zinc");
    }

    [Fact]
    public void AvailableSchemes_Contains_Green()
    {
        Assert.Contains(ThemeService.AvailableSchemes, s => s.Id == "green");
    }

    [Fact]
    public void AvailableSchemes_Contains_Rose()
    {
        Assert.Contains(ThemeService.AvailableSchemes, s => s.Id == "rose");
    }

    [Fact]
    public void AvailableSchemes_Has_Five_Entries()
    {
        Assert.Equal(5, ThemeService.AvailableSchemes.Count);
    }

    [Fact]
    public void ThemeSchemeInfo_Properties_Are_Correct()
    {
        var orange = ThemeService.AvailableSchemes.First(s => s.Id == "orange");

        Assert.Equal("Orange", orange.DisplayName);
        Assert.NotNull(orange.PreviewColor);
        Assert.NotEmpty(orange.PreviewColor);
    }

    // --- SetSchemeAsync ---

    [Fact]
    public async Task SetSchemeAsync_Updates_CurrentScheme()
    {
        _ctx.JSInterop.SetupVoid("themeManager.setScheme", _ => true);

        await _service.SetSchemeAsync("blue");

        Assert.Equal("blue", _service.CurrentScheme);
    }

    [Fact]
    public async Task SetSchemeAsync_Fires_OnThemeChanged()
    {
        _ctx.JSInterop.SetupVoid("themeManager.setScheme", _ => true);

        var changed = false;
        _service.OnThemeChanged += () => changed = true;

        await _service.SetSchemeAsync("blue");

        Assert.True(changed);
    }

    [Fact]
    public async Task SetSchemeAsync_Can_Set_Any_Scheme()
    {
        _ctx.JSInterop.SetupVoid("themeManager.setScheme", _ => true);

        foreach (var scheme in ThemeService.AvailableSchemes)
        {
            await _service.SetSchemeAsync(scheme.Id);
            Assert.Equal(scheme.Id, _service.CurrentScheme);
        }
    }

    // --- SetModeAsync ---

    [Fact]
    public async Task SetModeAsync_Updates_CurrentMode()
    {
        _ctx.JSInterop.SetupVoid("themeManager.setMode", _ => true);
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(true);

        await _service.SetModeAsync(ThemeMode.Dark);

        Assert.Equal(ThemeMode.Dark, _service.CurrentMode);
    }

    [Fact]
    public async Task SetModeAsync_Light_Mode_Updates_IsDark_False()
    {
        _ctx.JSInterop.SetupVoid("themeManager.setMode", _ => true);
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);

        await _service.SetModeAsync(ThemeMode.Light);

        Assert.False(_service.IsDark);
    }

    [Fact]
    public async Task SetModeAsync_Dark_Mode_Updates_IsDark_True()
    {
        _ctx.JSInterop.SetupVoid("themeManager.setMode", _ => true);
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(true);

        await _service.SetModeAsync(ThemeMode.Dark);

        Assert.True(_service.IsDark);
    }

    [Fact]
    public async Task SetModeAsync_Fires_OnThemeChanged()
    {
        _ctx.JSInterop.SetupVoid("themeManager.setMode", _ => true);
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);

        var changed = false;
        _service.OnThemeChanged += () => changed = true;

        await _service.SetModeAsync(ThemeMode.Light);

        Assert.True(changed);
    }

    // --- ToggleModeAsync ---

    [Fact]
    public async Task ToggleModeAsync_Updates_IsDark()
    {
        _ctx.JSInterop.SetupVoid("themeManager.toggle", _ => true);
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(true);

        await _service.ToggleModeAsync();

        Assert.True(_service.IsDark);
    }

    [Fact]
    public async Task ToggleModeAsync_When_Dark_Sets_Mode_To_Dark()
    {
        _ctx.JSInterop.SetupVoid("themeManager.toggle", _ => true);
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(true);

        await _service.ToggleModeAsync();

        Assert.Equal(ThemeMode.Dark, _service.CurrentMode);
    }

    [Fact]
    public async Task ToggleModeAsync_When_Light_Sets_Mode_To_Light()
    {
        _ctx.JSInterop.SetupVoid("themeManager.toggle", _ => true);
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);

        await _service.ToggleModeAsync();

        Assert.Equal(ThemeMode.Light, _service.CurrentMode);
    }

    [Fact]
    public async Task ToggleModeAsync_Fires_OnThemeChanged()
    {
        _ctx.JSInterop.SetupVoid("themeManager.toggle", _ => true);
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);

        var changed = false;
        _service.OnThemeChanged += () => changed = true;

        await _service.ToggleModeAsync();

        Assert.True(changed);
    }

    // --- ThemeMode enum ---

    [Fact]
    public void ThemeMode_Enum_Has_All_Values()
    {
        var values = Enum.GetValues<ThemeMode>();
        Assert.Contains(ThemeMode.System, values);
        Assert.Contains(ThemeMode.Light, values);
        Assert.Contains(ThemeMode.Dark, values);
    }

    // --- Multiple subscribers ---

    [Fact]
    public async Task OnThemeChanged_Notifies_Multiple_Subscribers()
    {
        _ctx.JSInterop.SetupVoid("themeManager.setScheme", _ => true);

        int count = 0;
        _service.OnThemeChanged += () => count++;
        _service.OnThemeChanged += () => count++;

        await _service.SetSchemeAsync("blue");

        Assert.Equal(2, count);
    }
}
