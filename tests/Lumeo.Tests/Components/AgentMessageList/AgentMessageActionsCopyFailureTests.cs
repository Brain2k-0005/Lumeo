using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Lumeo.Tests.Components.AgentMessageList;

/// <summary>
/// Round-6/7 (Codex) — AgentMessageActions false copy confirmation. The clipboard
/// interop exception was caught and SWALLOWED, but execution continued to
/// _copied = true + OnCopy — so a dead Server circuit (JSDisconnectedException)
/// still flipped the label to "Copied" and fired OnCopy for a copy that never
/// happened. The fix returns from the catch: no confirmation, no OnCopy, label
/// stays "Copy" so the user can retry.
/// </summary>
public class AgentMessageActionsCopyFailureTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public AgentMessageActionsCopyFailureTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Copy_Failure_Shows_No_Confirmation_And_Skips_OnCopy()
    {
        _interop.ThrowOnCopyToClipboard = true;
        var copied = 0;

        var cut = _ctx.Render<Lumeo.AgentMessageActions>(p => p
            .Add(x => x.CopyText, "hello world")
            .Add(x => x.OnCopy, _ => copied++));

        cut.Find("[aria-label='Copy']").Click();

        // The clipboard write was attempted...
        Assert.Contains("hello world", _interop.CopyToClipboardCalls);
        // ...but it failed, so NO "Copied" confirmation and OnCopy never fired.
        Assert.DoesNotContain("Copied", cut.Markup);
        Assert.NotEmpty(cut.FindAll("[aria-label='Copy']")); // label stayed "Copy"
        Assert.Equal(0, copied);
    }

    [Fact]
    public void Copy_Browser_Denial_JSException_Is_Silent_No_Op_Not_A_Throw()
    {
        // Round-8 (Codex): a browser clipboard REJECTION — an insecure (non-HTTPS)
        // origin has no navigator.clipboard, or the user denied the permission —
        // surfaces from the interop as a plain JSException, NOT a disconnect. The
        // pre-fix catches (JSDisconnectedException / ObjectDisposedException) missed
        // it, so clicking Copy THREW instead of taking the silent-no-op path.
        _interop.CopyToClipboardException = new JSException("permission denied");
        var copied = 0;

        var cut = _ctx.Render<Lumeo.AgentMessageActions>(p => p
            .Add(x => x.CopyText, "hello world")
            .Add(x => x.OnCopy, _ => copied++));

        // Must NOT throw...
        var ex = Record.Exception(() => cut.Find("[aria-label='Copy']").Click());
        Assert.Null(ex);

        // ...the write was attempted, but failed → no confirmation, OnCopy skipped.
        Assert.Contains("hello world", _interop.CopyToClipboardCalls);
        Assert.DoesNotContain("Copied", cut.Markup);
        Assert.NotEmpty(cut.FindAll("[aria-label='Copy']")); // label stayed "Copy"
        Assert.Equal(0, copied);
    }

    [Fact]
    public void Copy_Success_Shows_Confirmation_And_Fires_OnCopy()
    {
        _interop.ThrowOnCopyToClipboard = false;
        string? copiedText = null;

        var cut = _ctx.Render<Lumeo.AgentMessageActions>(p => p
            .Add(x => x.CopyText, "hello world")
            .Add(x => x.OnCopy, t => copiedText = t));

        cut.Find("[aria-label='Copy']").Click();

        Assert.Contains("hello world", _interop.CopyToClipboardCalls);
        Assert.Contains("Copied", cut.Markup);
        Assert.Equal("hello world", copiedText);
    }
}
