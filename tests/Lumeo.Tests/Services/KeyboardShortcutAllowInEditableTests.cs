using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Services;

/// <summary>
/// Round-2 P2. The JS shortcut listener now skips EVERY shortcut whose target is an
/// editable element unless the registration opted in via <c>allowInEditable</c> — so a
/// modifier combo like Ctrl/Cmd+B (bound to e.g. the sidebar) yields to the browser's
/// native bold while typing, while a Ctrl/Cmd+K palette can opt in and still fire. The
/// actual key gating lives in components.js (not reachable from bUnit's headless DOM);
/// the testable seam is the .NET → JS contract: RegisterAsync must forward the
/// <c>allowInEditable</c> flag as the 4th argument of the <c>addShortcut</c> interop call.
/// </summary>
public class KeyboardShortcutAllowInEditableTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public KeyboardShortcutAllowInEditableTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        // KeyboardShortcutService imports the module with a ?v=<assembly-version>
        // cache-buster; set up the same versioned path so its invocations are captured.
        var v = typeof(KeyboardShortcutService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(KeyboardShortcutService).Assembly.GetName().Version?.ToString()
            ?? "0";
        _module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        _module.Mode = JSRuntimeMode.Loose;
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private KeyboardShortcutService Service => _ctx.Services.GetRequiredService<KeyboardShortcutService>();

    [Fact]
    public async Task RegisterAsync_Forwards_AllowInEditable_Flag_To_addShortcut()
    {
        var svc = Service;

        // Default: not allowed in editable (ctrl+b must yield to native bold).
        await svc.RegisterAsync("ctrl+b", () => Task.CompletedTask);
        // Opt-in: a palette-style shortcut that must fire even while typing.
        await svc.RegisterAsync("ctrl+k", () => Task.CompletedTask, allowInEditable: true);

        var calls = _module.Invocations["addShortcut"];
        Assert.Equal(2, calls.Count);

        // Args: (id, normalizedCombo, preventDefault, allowInEditable)
        Assert.True(calls[0].Arguments.Count >= 4);
        Assert.False((bool)calls[0].Arguments[3]!); // ctrl+b → not allowed in editable
        Assert.True((bool)calls[1].Arguments[3]!);  // ctrl+k → opted in
    }
}
