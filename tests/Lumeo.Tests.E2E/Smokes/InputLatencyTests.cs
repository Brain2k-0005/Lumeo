using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Real-browser regression test for keystrokes lost under network latency with a plain,
/// synchronous <c>@bind-Value</c> on Lumeo's text controls.
///
/// The inner &lt;input&gt;/&lt;textarea&gt; used to render <c>value="@x"</c> next to
/// <c>@oninput</c>. Only Razor's <c>@bind</c> makes the compiler emit
/// <c>SetUpdatesAttributeName("value")</c>, which tells Blazor to update its last-rendered
/// value to the text each input event carried. Without it, the render batch answering
/// keystroke n set the field back to keystroke n's text while the user had already typed
/// further, so under latency characters vanished.
///
/// Latency is injected by delaying every frame the page sends over the circuit's WebSocket
/// (an init script wrapping <c>WebSocket.prototype.send</c>). CDP's
/// <c>Network.emulateNetworkConditions</c> does NOT reproduce this bug: it does not delay
/// the frames of an already-open WebSocket the way a slow network does.
///
/// Measured with this page (150 ms send delay, 80 ms per key): on the 5.12.1 components
/// Input, Textarea, PasswordInput and PromptInput ended as "Relsi yigsedcek" and InputMask
/// as "Rekcegyial" / "Realkcegyi", in every run; the native <c>@bind</c> baseline was always
/// correct. With the fix every control keeps the whole sentence, 10 of 10 runs.
///
/// Drives <c>tests/Lumeo.Tests.ServerHost</c>'s <c>/e2e/input-latency</c> page via the same
/// <c>LUMEO_GANTT_E2E_BASE_URL</c> / port-5299 host the InputSizing/Gantt suites use.
/// </summary>
public class InputLatencyTests : IAsyncLifetime
{
    private static string HostBaseUrl { get; } =
        Environment.GetEnvironmentVariable("LUMEO_GANTT_E2E_BASE_URL")
        ?? "http://localhost:5299";

    private const string Sentence = "Realistic typing speed check";

    // One-way delay added to every frame the browser sends to the server.
    private const int SendDelayMs = 150;

    // Realistic typing speed.
    private const int KeyDelayMs = 80;

    private IPlaywright _playwright = default!;
    private IBrowser _browser = default!;
    private IPage _page = default!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _page = await _browser.NewPageAsync();
        await _page.AddInitScriptAsync(
            "(() => { const send = WebSocket.prototype.send; " +
            "WebSocket.prototype.send = function (d) { setTimeout(() => send.call(this, d), " + SendDelayMs + "); }; })();");
        await _page.GotoAsync($"{HostBaseUrl}/e2e/input-latency");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Theory]
    [InlineData("native-bind")]
    [InlineData("lumeo-input")]
    [InlineData("lumeo-textarea")]
    [InlineData("lumeo-mask")]
    [InlineData("lumeo-password")]
    [InlineData("lumeo-prompt")]
    public async Task Typing_Under_Latency_Keeps_Every_Character(string testId)
    {
        var field = _page.Locator($"[data-testid='{testId}']").Locator("input, textarea").First;
        await field.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await field.ClickAsync();
        await _page.Keyboard.TypeAsync(Sentence, new KeyboardTypeOptions { Delay = KeyDelayMs });

        // The server has processed the final keystroke once the bound value stops changing;
        // its render batch (and every older one) has then reached the browser too.
        var bound = _page.Locator($"[data-testid='{testId}-bound']");
        var settled = await WaitForStableTextAsync(bound);

        var dom = await field.InputValueAsync();
        Assert.Equal(Sentence, dom);
        Assert.Equal(Sentence, settled);
    }

    // A mask that inserts literals rewrites the field on the server, so under latency it
    // races the typist by nature. Without latency the reformatted text must still reach the
    // DOM (the bound value attribute now holds the raw typed text, so the formatted render
    // differs and is written) and a caret moved into the middle must stay there.
    [Fact]
    public async Task Phone_Mask_Formats_And_Keeps_The_Caret_Without_Latency()
    {
        var page = await _browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{HostBaseUrl}/e2e/input-latency");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var field = page.Locator("[data-testid='lumeo-phone'] input");
            var bound = page.Locator("[data-testid='lumeo-phone-bound']");
            await field.ClickAsync();
            await page.Keyboard.TypeAsync("5551234567", new KeyboardTypeOptions { Delay = KeyDelayMs });

            await Assertions.Expect(bound).ToHaveTextAsync("5551234567", new() { Timeout = 5000 });
            await Assertions.Expect(field).ToHaveValueAsync("(555) 123-4567", new() { Timeout = 5000 });

            // Put the caret after "(555" and delete the digit before it: the re-masked text
            // shifts left and the caret stays after the remaining "55".
            await field.EvaluateAsync("el => el.setSelectionRange(4, 4)");
            await page.Keyboard.PressAsync("Backspace");

            await Assertions.Expect(bound).ToHaveTextAsync("551234567", new() { Timeout = 5000 });
            await Assertions.Expect(field).ToHaveValueAsync("(551) 234-567", new() { Timeout = 5000 });
            await Assertions.Expect(field).ToHaveJSPropertyAsync("selectionStart", 3, new() { Timeout = 5000 });
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    // Polls until the readout has not changed for a full second (or 15 s pass). A plain
    // "wait until it equals the sentence" would time out on the broken build without
    // showing what the field ended up as.
    private static async Task<string> WaitForStableTextAsync(ILocator locator)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        var last = await locator.TextContentAsync() ?? "";
        var stableSince = DateTime.UtcNow;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            var now = await locator.TextContentAsync() ?? "";
            if (now != last)
            {
                last = now;
                stableSince = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - stableSince >= TimeSpan.FromSeconds(1))
            {
                break;
            }
        }
        return last;
    }
}
