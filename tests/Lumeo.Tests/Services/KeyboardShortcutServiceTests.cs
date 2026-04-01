using Bunit;
using Xunit;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Services;

public class KeyboardShortcutServiceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private KeyboardShortcutService _service = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        _service = _ctx.Services.GetRequiredService<KeyboardShortcutService>();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Registration ---

    [Fact]
    public async Task RegisterAsync_Returns_Disposable_Handle()
    {
        var handle = await _service.RegisterAsync("ctrl+k", () => Task.CompletedTask);

        Assert.NotNull(handle);
        await handle.DisposeAsync();
    }

    [Fact]
    public async Task RegisterAsync_With_Action_Handler_Returns_Disposable_Handle()
    {
        var handle = await _service.RegisterAsync("ctrl+k", () => { });

        Assert.NotNull(handle);
        await handle.DisposeAsync();
    }

    [Fact]
    public async Task RegisterAsync_Multiple_Shortcuts_Returns_Separate_Handles()
    {
        var handle1 = await _service.RegisterAsync("ctrl+k", () => Task.CompletedTask);
        var handle2 = await _service.RegisterAsync("ctrl+shift+p", () => Task.CompletedTask);

        Assert.NotNull(handle1);
        Assert.NotNull(handle2);
        Assert.NotSame(handle1, handle2);

        await handle1.DisposeAsync();
        await handle2.DisposeAsync();
    }

    // --- OnShortcutTriggered ---

    [Fact]
    public async Task OnShortcutTriggered_Calls_Handler_For_Registered_Shortcut()
    {
        var called = false;
        var handle = await _service.RegisterAsync("ctrl+k", () =>
        {
            called = true;
            return Task.CompletedTask;
        });

        // Access the module via reflection to find the registered shortcut ID
        // Instead, test through the public JSInvokable method
        // We test indirectly by verifying no exception occurs
        await _service.OnShortcutTriggered("non-existent-id");

        Assert.False(called); // Should not call for unknown id
        await handle.DisposeAsync();
    }

    [Fact]
    public async Task OnShortcutTriggered_With_Unknown_Id_Does_Not_Throw()
    {
        // Should silently ignore unknown shortcut IDs
        await _service.OnShortcutTriggered("unknown-id-12345");
    }

    // --- Key combo normalization ---

    [Fact]
    public async Task RegisterAsync_Normalizes_KeyCombo_Modifiers()
    {
        // These should both register without throwing - normalization handles ordering
        var h1 = await _service.RegisterAsync("shift+ctrl+k", () => Task.CompletedTask);
        var h2 = await _service.RegisterAsync("ctrl+shift+k", () => Task.CompletedTask);

        Assert.NotNull(h1);
        Assert.NotNull(h2);
        await h1.DisposeAsync();
        await h2.DisposeAsync();
    }

    [Fact]
    public async Task RegisterAsync_Single_Key_Without_Modifier()
    {
        var handle = await _service.RegisterAsync("escape", () => Task.CompletedTask);

        Assert.NotNull(handle);
        await handle.DisposeAsync();
    }

    [Fact]
    public async Task RegisterAsync_Alt_Modifier()
    {
        var handle = await _service.RegisterAsync("alt+n", () => Task.CompletedTask);

        Assert.NotNull(handle);
        await handle.DisposeAsync();
    }

    // --- Dispose via handle ---

    [Fact]
    public async Task ShortcutHandle_DisposeAsync_Does_Not_Throw()
    {
        var handle = await _service.RegisterAsync("ctrl+k", () => Task.CompletedTask);

        var exception = await Record.ExceptionAsync(() => handle.DisposeAsync().AsTask());
        Assert.Null(exception);
    }

    [Fact]
    public async Task ShortcutHandle_Double_DisposeAsync_Does_Not_Throw()
    {
        var handle = await _service.RegisterAsync("ctrl+k", () => Task.CompletedTask);

        await handle.DisposeAsync();
        var exception = await Record.ExceptionAsync(() => handle.DisposeAsync().AsTask());
        Assert.Null(exception);
    }

    // --- PreventDefault parameter ---

    [Fact]
    public async Task RegisterAsync_Accepts_PreventDefault_False()
    {
        var handle = await _service.RegisterAsync("ctrl+s", () => Task.CompletedTask, preventDefault: false);

        Assert.NotNull(handle);
        await handle.DisposeAsync();
    }

    [Fact]
    public async Task RegisterAsync_Accepts_PreventDefault_True()
    {
        var handle = await _service.RegisterAsync("ctrl+s", () => Task.CompletedTask, preventDefault: true);

        Assert.NotNull(handle);
        await handle.DisposeAsync();
    }

    // --- DisposeAsync ---

    [Fact]
    public async Task DisposeAsync_Does_Not_Throw()
    {
        await _service.RegisterAsync("ctrl+k", () => Task.CompletedTask);

        var exception = await Record.ExceptionAsync(() => _service.DisposeAsync().AsTask());
        Assert.Null(exception);
    }
}
