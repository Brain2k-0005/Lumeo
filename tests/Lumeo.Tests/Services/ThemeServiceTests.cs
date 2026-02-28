using Xunit;
using Lumeo.Services;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Services;

public class ThemeServiceTests
{
    private readonly FakeJSRuntime _js = new();
    private readonly ThemeService _service;

    public ThemeServiceTests()
    {
        _service = new ThemeService(_js);
    }

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

    // --- InitializeAsync ---

    [Fact]
    public async Task InitializeAsync_Reads_Mode_From_JS()
    {
        _js.SetResult("themeManager.getMode", "dark");
        _js.SetResult("themeManager.getScheme", "orange");
        _js.SetResult("themeManager.isDark", true);

        await _service.InitializeAsync();

        Assert.Equal(ThemeMode.Dark, _service.CurrentMode);
    }

    [Fact]
    public async Task InitializeAsync_Reads_Scheme_From_JS()
    {
        _js.SetResult("themeManager.getMode", "system");
        _js.SetResult("themeManager.getScheme", "blue");
        _js.SetResult("themeManager.isDark", false);

        await _service.InitializeAsync();

        Assert.Equal("blue", _service.CurrentScheme);
    }

    [Fact]
    public async Task InitializeAsync_Reads_IsDark_From_JS()
    {
        _js.SetResult("themeManager.getMode", "system");
        _js.SetResult("themeManager.getScheme", "orange");
        _js.SetResult("themeManager.isDark", true);

        await _service.InitializeAsync();

        Assert.True(_service.IsDark);
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
        await _service.SetSchemeAsync("blue");

        Assert.Equal("blue", _service.CurrentScheme);
    }

    [Fact]
    public async Task SetSchemeAsync_Fires_OnThemeChanged()
    {
        var changed = false;
        _service.OnThemeChanged += () => changed = true;

        await _service.SetSchemeAsync("blue");

        Assert.True(changed);
    }

    [Fact]
    public async Task SetSchemeAsync_Can_Set_Any_Scheme()
    {
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
        _js.SetResult("themeManager.isDark", true);

        await _service.SetModeAsync(ThemeMode.Dark);

        Assert.Equal(ThemeMode.Dark, _service.CurrentMode);
    }

    [Fact]
    public async Task SetModeAsync_Light_Mode_Updates_IsDark_False()
    {
        _js.SetResult("themeManager.isDark", false);

        await _service.SetModeAsync(ThemeMode.Light);

        Assert.False(_service.IsDark);
    }

    [Fact]
    public async Task SetModeAsync_Dark_Mode_Updates_IsDark_True()
    {
        _js.SetResult("themeManager.isDark", true);

        await _service.SetModeAsync(ThemeMode.Dark);

        Assert.True(_service.IsDark);
    }

    [Fact]
    public async Task SetModeAsync_Fires_OnThemeChanged()
    {
        _js.SetResult("themeManager.isDark", false);

        var changed = false;
        _service.OnThemeChanged += () => changed = true;

        await _service.SetModeAsync(ThemeMode.Light);

        Assert.True(changed);
    }

    // --- ToggleModeAsync ---

    [Fact]
    public async Task ToggleModeAsync_Updates_IsDark()
    {
        _js.SetResult("themeManager.isDark", true);

        await _service.ToggleModeAsync();

        Assert.True(_service.IsDark);
    }

    [Fact]
    public async Task ToggleModeAsync_When_Dark_Sets_Mode_To_Dark()
    {
        _js.SetResult("themeManager.isDark", true);

        await _service.ToggleModeAsync();

        Assert.Equal(ThemeMode.Dark, _service.CurrentMode);
    }

    [Fact]
    public async Task ToggleModeAsync_When_Light_Sets_Mode_To_Light()
    {
        _js.SetResult("themeManager.isDark", false);

        await _service.ToggleModeAsync();

        Assert.Equal(ThemeMode.Light, _service.CurrentMode);
    }

    [Fact]
    public async Task ToggleModeAsync_Fires_OnThemeChanged()
    {
        _js.SetResult("themeManager.isDark", false);

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
        int count = 0;
        _service.OnThemeChanged += () => count++;
        _service.OnThemeChanged += () => count++;

        await _service.SetSchemeAsync("blue");

        Assert.Equal(2, count);
    }

    // --- Fake IJSRuntime ---

    private sealed class FakeJSRuntime : IJSRuntime
    {
        private readonly Dictionary<string, object?> _results = new();

        public void SetResult(string identifier, object? value) =>
            _results[identifier] = value;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(_results.TryGetValue(identifier, out var val) ? (TValue)val! : default!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
