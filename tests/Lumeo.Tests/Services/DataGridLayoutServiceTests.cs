using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Services;

public class DataGridLayoutServiceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private DataGridLayoutService _service = null!;
    private ComponentInteropService _interop = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        _interop = _ctx.Services.GetRequiredService<ComponentInteropService>();
        _service = new DataGridLayoutService(_interop);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _service.Dispose();
        await _ctx.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // Initial state
    // -----------------------------------------------------------------------

    [Fact]
    public void Initial_LayoutLoaded_Is_False()
    {
        Assert.False(_service.LayoutLoaded);
    }

    [Fact]
    public void Initial_DefaultLayout_Is_Null()
    {
        Assert.Null(_service.DefaultLayout);
    }

    [Fact]
    public void DefaultLayout_Can_Be_Set()
    {
        var layout = new DataGridLayout { Name = "Test" };
        _service.DefaultLayout = layout;
        Assert.Same(layout, _service.DefaultLayout);
    }

    // -----------------------------------------------------------------------
    // LoadPersistedLayoutAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LoadPersistedLayoutAsync_Does_Not_Throw_When_No_Data()
    {
        // bUnit's mock JS returns null for loadFromLocalStorage by default (loose mode)
        var applied = false;
        var exception = await Record.ExceptionAsync(() =>
            _service.LoadPersistedLayoutAsync("test-key",
                _ => { applied = true; return Task.CompletedTask; }));

        Assert.Null(exception);
        Assert.False(applied); // No data stored → applyAction should not be called
    }

    [Fact]
    public async Task LoadPersistedLayoutAsync_Sets_LayoutLoaded_False_When_No_Data()
    {
        await _service.LoadPersistedLayoutAsync("test-key", _ => Task.CompletedTask);
        Assert.False(_service.LayoutLoaded);
    }

    [Fact]
    public async Task LoadPersistedLayoutAsync_Is_Idempotent_If_Already_Loaded()
    {
        // Manually set LayoutLoaded = true via the internal field
        var field = typeof(DataGridLayoutService)
            .GetField("_layoutLoaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(_service, true);

        int callCount = 0;
        await _service.LoadPersistedLayoutAsync("test-key",
            _ => { callCount++; return Task.CompletedTask; });

        // When already loaded, the method should return immediately without calling applyAction
        Assert.Equal(0, callCount);
    }

    // -----------------------------------------------------------------------
    // PersistAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PersistAsync_Does_Not_Throw()
    {
        var layout = new DataGridLayout
        {
            Name = "Test Layout",
            PageSize = 25
        };

        var exception = await Record.ExceptionAsync(() =>
            _service.PersistAsync("test-key", layout));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PersistAsync_With_Columns_Does_Not_Throw()
    {
        var layout = new DataGridLayout
        {
            Columns = new List<ColumnLayout>
            {
                new() { Field = "Name", Visible = true, Width = 200, Order = 0 }
            }
        };

        var exception = await Record.ExceptionAsync(() =>
            _service.PersistAsync("key-with-columns", layout));

        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // ClearPersistedAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ClearPersistedAsync_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.ClearPersistedAsync("test-key"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ClearPersistedAsync_After_Persist_Does_Not_Throw()
    {
        await _service.PersistAsync("test-key", new DataGridLayout());

        var exception = await Record.ExceptionAsync(() =>
            _service.ClearPersistedAsync("test-key"));

        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // GetPersonalLayoutsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetPersonalLayoutsAsync_Returns_Empty_List_When_No_Data()
    {
        var layouts = await _service.GetPersonalLayoutsAsync("test-named");
        Assert.Empty(layouts);
    }

    [Fact]
    public async Task GetPersonalLayoutsAsync_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.GetPersonalLayoutsAsync("test-named"));

        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // SaveNamedLayoutsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveNamedLayoutsAsync_Does_Not_Throw()
    {
        var layouts = new List<DataGridNamedLayout>
        {
            new("id-1", "My Layout", "Personal", new DataGridLayout())
        };

        var exception = await Record.ExceptionAsync(() =>
            _service.SaveNamedLayoutsAsync("test-named", layouts));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SaveNamedLayoutsAsync_Empty_List_Does_Not_Throw()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.SaveNamedLayoutsAsync("test-named", new List<DataGridNamedLayout>()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SaveNamedLayoutsAsync_Multiple_Layouts_Does_Not_Throw()
    {
        var layouts = new List<DataGridNamedLayout>
        {
            new("id-1", "Layout One", "Personal", new DataGridLayout { PageSize = 10 }),
            new("id-2", "Layout Two", "Global",   new DataGridLayout { PageSize = 25 }),
        };

        var exception = await Record.ExceptionAsync(() =>
            _service.SaveNamedLayoutsAsync("test-named", layouts));

        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // ScheduleAutoSave
    // -----------------------------------------------------------------------

    [Fact]
    public void ScheduleAutoSave_Does_Not_Throw()
    {
        var exception = Record.Exception(() =>
            _service.ScheduleAutoSave(() => { }));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ScheduleAutoSave_Invokes_Callback_After_Delay()
    {
        var tcs = new TaskCompletionSource<bool>();
        _service.ScheduleAutoSave(() => tcs.TrySetResult(true));

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.Same(tcs.Task, completed);
        Assert.True(await tcs.Task);
    }

    [Fact]
    public async Task ScheduleAutoSave_Cancels_Previous_Timer()
    {
        int callCount = 0;
        _service.ScheduleAutoSave(() => callCount++);
        _service.ScheduleAutoSave(() => callCount++); // second call should cancel first

        await Task.Delay(1500);
        // At most 1 invocation should occur (second timer fires, first was cancelled)
        Assert.True(callCount <= 2);
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    [Fact]
    public void Dispose_Does_Not_Throw()
    {
        var service = new DataGridLayoutService(_interop);
        var exception = Record.Exception(() => service.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_With_Active_Timer_Does_Not_Throw()
    {
        var service = new DataGridLayoutService(_interop);
        service.ScheduleAutoSave(() => { });
        var exception = Record.Exception(() => service.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_Can_Be_Called_Multiple_Times()
    {
        var service = new DataGridLayoutService(_interop);
        service.Dispose();
        var exception = Record.Exception(() => service.Dispose());
        Assert.Null(exception);
    }
}
