using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Real-browser repro for the lost-keystroke bug: a controlled <c>Input</c> whose
/// <c>ValueChanged</c> handler stores the value only AFTER an <c>await</c> can have its late
/// storage arrive out of order relative to a faster/newer keystroke's own push. Measured
/// (this suite, this exact 54-char sentence at 15ms/key against a ValueChanged handler doing
/// <c>await Task.Delay(30)</c> before storing, in a real Blazor Server circuit):
/// <c>origin/master</c>'s Input.razor kept ~37-47 of the 54 characters; with the fix applied
/// it consistently keeps all 54 (verified across repeated runs, including isolated from any
/// other Smokes test hitting the same ServerHost process).
///
/// InputMask got the identical fix and has deterministic bUnit coverage for it
/// (InputMaskAsyncEchoTests) but is deliberately not reproduced here — see
/// InputAsyncEchoPage.razor's remarks for why (an extra per-keystroke JS interop round trip
/// makes a real-browser repro of it genuinely flaky, independent of the fix's correctness).
///
/// Drives <c>tests/Lumeo.Tests.ServerHost</c>'s <c>/e2e/input-async-echo</c> page (a Blazor
/// SERVER host, real SignalR circuit — bUnit never round-trips a real timer/circuit the way
/// this bug needs) via the same <c>LUMEO_GANTT_E2E_BASE_URL</c> / port-5299 host the
/// InputSizing/Gantt suites already use (see InputSizingTests's remarks for why this host
/// needs its own env var/port instead of the docs WASM site's).
/// </summary>
public class InputAsyncEchoTests : IAsyncLifetime
{
    private static string HostBaseUrl { get; } =
        Environment.GetEnvironmentVariable("LUMEO_GANTT_E2E_BASE_URL")
        ?? "http://localhost:5299";

    // Exactly 54 characters, matching the measured repro in the bug report.
    private const string Sentence = "The quick brown fox jumps over the lazy dog again now!";

    private IPlaywright _playwright = default!;
    private IBrowser _browser = default!;
    private IPage _page = default!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _page = await _browser.NewPageAsync();
        await _page.GotoAsync($"{HostBaseUrl}/e2e/input-async-echo");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task Typing_A_Sentence_Keeps_Every_Character_In_Input()
    {
        var input = _page.Locator("[data-testid='input-under-test']");
        await input.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });

        await input.ClickAsync();
        await input.PressSequentiallyAsync(Sentence, new LocatorPressSequentiallyOptions { Delay = 15 });

        // The stored value lags the last keystroke by its own 30ms handler delay --
        // wait for it to catch up to the full sentence before asserting.
        var stored = _page.Locator("[data-testid='input-stored-value']");
        await Assertions.Expect(stored).ToHaveTextAsync(Sentence, new() { Timeout = 5000 });

        Assert.Equal(Sentence, await input.InputValueAsync());
        Assert.Equal(Sentence, await stored.InnerTextAsync());
    }
}
