using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Real-browser regression test for lost keystrokes in a controlled text control whose
/// <c>ValueChanged</c> handler stores the value only AFTER an <c>await</c>
/// (<c>await Task.Delay(30)</c>), typed at 15 ms/key into a real Blazor Server circuit.
///
/// Measured with this page (20 runs per control, 54-char sentence):
/// <list type="bullet">
/// <item>origin/master Input: 0/20 exact, 34-39 characters kept. Textarea before the fix:
/// 0/20, 31-36 kept.</item>
/// <item>First version of the fix (push history cleared on catch-up): Input 12-15/20 exact
/// (51-54 kept); Input with a Prefix 0/20 (29-37 kept).</item>
/// <item>Current fix: 20/20 for Input, Input with a Prefix, Textarea and InputMask.</item>
/// </list>
/// The residual loss of the first version was in Lumeo, not a Blazor ceiling: two handlers
/// whose delays expire in the same timer tick run their continuations newest-first, so the
/// newest value's echo arrived, the history was cleared as "caught up", and the older echo
/// that followed was adopted as an external change, overwriting the DOM. Server-side
/// instrumentation showed every lost character at exactly such a pair.
///
/// What is asserted, and what deliberately is not: the DOM must hold the whole sentence and
/// the last value the handler received must be the whole sentence. The page's own stored
/// value is NOT asserted, because the page's handler races itself: when the last two
/// handlers finish in the same tick in reverse order, it keeps the second-to-last value
/// (seen in about 1 of 30 runs). No component can change what the consumer's handler
/// stores; the component's job is to keep the user's text and report every keystroke.
///
/// Drives <c>tests/Lumeo.Tests.ServerHost</c>'s <c>/e2e/input-async-echo</c> page via the
/// same <c>LUMEO_GANTT_E2E_BASE_URL</c> / port-5299 host the InputSizing/Gantt suites use.
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

    [Theory]
    [InlineData("input-under-test")]
    [InlineData("input-prefix-under-test")]
    [InlineData("textarea-under-test")]
    [InlineData("mask-under-test")]
    public async Task Typing_A_Sentence_Keeps_Every_Character(string testId)
    {
        var control = _page.Locator($"[data-testid='{testId}']");
        await control.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });

        await control.ClickAsync();
        await control.PressSequentiallyAsync(Sentence, new LocatorPressSequentiallyOptions { Delay = 15 });

        // The handler saw the final keystroke...
        await Assertions.Expect(_page.Locator($"[data-testid='{testId}-received']"))
            .ToHaveTextAsync(Sentence, new() { Timeout = 5000 });
        // ...and every handler has finished, so every late (possibly out-of-order)
        // re-render has already reached the component.
        await Assertions.Expect(_page.Locator($"[data-testid='{testId}-inflight']"))
            .ToHaveTextAsync("0", new() { Timeout = 5000 });

        Assert.Equal(Sentence, await control.InputValueAsync());
    }
}
