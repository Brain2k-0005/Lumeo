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
    public void DefaultScheme_Is_Zinc()
    {
        Assert.Equal("zinc", _service.CurrentScheme);
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
    public void AvailableSchemes_Has_Eight_Entries()
    {
        Assert.Equal(8, ThemeService.AvailableSchemes.Count);
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

    // --- ToggleModeAsync (LU-22: two-state toggle between RESOLVED appearances) ---

    // LU-22: ToggleModeAsync used to cycle System → Dark → Light → System off
    // CurrentMode alone (see CycleModeAsync below, which keeps that behaviour under
    // its new name). That meant a System→Dark step on a dark-OS machine was visually
    // a no-op, so a light/dark switch needed two clicks. ToggleModeAsync now flips
    // between the two RESOLVED appearances (IsDark), so it always lands on the
    // opposite of what's currently showing — including straight out of System mode,
    // on either a dark or a light OS preference.

    [Fact]
    public async Task ToggleModeAsync_Updates_IsDark()
    {
        _js.SetResult("themeManager.isDark", true);

        await _service.ToggleModeAsync();

        Assert.True(_service.IsDark);
    }

    [Fact]
    public async Task ToggleModeAsync_From_System_With_Dark_OS_Preference_Goes_To_Light()
    {
        // OS prefers dark: System resolves IsDark=true, so the single click must
        // switch straight to Light (previously landed on Dark — a visual no-op).
        _js.SetResult("themeManager.isDark", true);
        await _service.SetModeAsync(ThemeMode.System);

        await _service.ToggleModeAsync();

        Assert.Equal(ThemeMode.Light, _service.CurrentMode);
    }

    [Fact]
    public async Task ToggleModeAsync_From_System_With_Light_OS_Preference_Goes_To_Dark()
    {
        // OS prefers light: System resolves IsDark=false, so the single click
        // switches to Dark — a visible change either way.
        _js.SetResult("themeManager.isDark", false);
        await _service.SetModeAsync(ThemeMode.System);

        await _service.ToggleModeAsync();

        Assert.Equal(ThemeMode.Dark, _service.CurrentMode);
    }

    [Fact]
    public async Task ToggleModeAsync_From_Dark_Goes_To_Light()
    {
        _js.SetResult("themeManager.isDark", true);
        await _service.SetModeAsync(ThemeMode.Dark);

        await _service.ToggleModeAsync();

        Assert.Equal(ThemeMode.Light, _service.CurrentMode);
    }

    [Fact]
    public async Task ToggleModeAsync_From_Light_Goes_To_Dark()
    {
        _js.SetResult("themeManager.isDark", false);
        await _service.SetModeAsync(ThemeMode.Light);

        await _service.ToggleModeAsync();

        Assert.Equal(ThemeMode.Dark, _service.CurrentMode);
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

    // --- CycleModeAsync (the old ToggleModeAsync three-way cycle, kept under its own name) ---

    // rc.43: this is the three-way cycle System → Dark → Light → System that used to
    // live in ToggleModeAsync. It moved to CycleModeAsync under LU-22 so callers that
    // deliberately want to offer System as a stop (e.g. ThemeToggle with
    // IncludeSystem="true") keep that behaviour, while ToggleModeAsync itself becomes
    // a plain resolved-state binary flip (see above).

    [Fact]
    public async Task CycleModeAsync_From_System_Goes_To_Dark()
    {
        _js.SetResult("themeManager.isDark", false);
        await _service.SetModeAsync(ThemeMode.System);

        await _service.CycleModeAsync();

        Assert.Equal(ThemeMode.Dark, _service.CurrentMode);
    }

    [Fact]
    public async Task CycleModeAsync_From_Dark_Goes_To_Light()
    {
        _js.SetResult("themeManager.isDark", true);
        await _service.SetModeAsync(ThemeMode.Dark);

        await _service.CycleModeAsync();

        Assert.Equal(ThemeMode.Light, _service.CurrentMode);
    }

    [Fact]
    public async Task CycleModeAsync_From_Light_Goes_To_System()
    {
        _js.SetResult("themeManager.isDark", false);
        await _service.SetModeAsync(ThemeMode.Light);

        await _service.CycleModeAsync();

        Assert.Equal(ThemeMode.System, _service.CurrentMode);
    }

    [Fact]
    public async Task CycleModeAsync_Fires_OnThemeChanged()
    {
        _js.SetResult("themeManager.isDark", false);

        var changed = false;
        _service.OnThemeChanged += () => changed = true;

        await _service.CycleModeAsync();

        Assert.True(changed);
    }

    // --- OnExternalThemeChange (#312/#313 live OS + cross-tab sync) ---

    [Fact]
    public async Task OnExternalThemeChange_Rereads_State_From_JS()
    {
        _js.SetResult("themeManager.getMode", "system");
        _js.SetResult("themeManager.getScheme", "violet");
        _js.SetResult("themeManager.isDark", true);

        await _service.OnExternalThemeChange();

        Assert.Equal("violet", _service.CurrentScheme);
        Assert.True(_service.IsDark);
    }

    [Fact]
    public async Task OnExternalThemeChange_Fires_OnThemeChanged()
    {
        var changed = false;
        _service.OnThemeChanged += () => changed = true;

        await _service.OnExternalThemeChange();

        Assert.True(changed);
    }

    [Fact]
    public async Task InitializeAsync_Registers_The_Theme_Listener()
    {
        _js.SetResult("themeManager.getMode", "system");
        _js.SetResult("themeManager.getScheme", "zinc");
        _js.SetResult("themeManager.isDark", false);

        await _service.InitializeAsync();

        Assert.Contains("themeManager.registerThemeListener", _js.VoidCalls);
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
        private readonly List<string> _calls = new();

        /// <summary>Every identifier invoked (typed or void), in call order.</summary>
        public IReadOnlyList<string> VoidCalls => _calls;

        public void SetResult(string identifier, object? value) =>
            _results[identifier] = value;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            _calls.Add(identifier);
            return ValueTask.FromResult(_results.TryGetValue(identifier, out var val) ? (TValue)val! : default!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
