using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Verifies that <see cref="OnLayoutSave"/> is gated by the layout-service generation
/// counter so stale debounced timers cannot deliver outdated layout snapshots to the
/// consumer's backend even if the LocalStorage write was already correctly suppressed.
///
/// Defends against a subtle bug where the in-service guard inside PersistAsync only
/// covered the LocalStorage path while the OnLayoutSave callback continued to fire
/// regardless of whether the captured generation was still current.
/// </summary>
public class DataGridAutoSaveStaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridAutoSaveStaleTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<Row> Data() => new()
    {
        new(1, "Alice"),
        new(2, "Bob"),
        new(3, "Charlie"),
    };

    private static List<DataGridColumn<Row>> Cols() => new()
    {
        new() { Id = "col-id",   Field = "Id",   Title = "ID",   Sortable = true },
        new() { Id = "col-name", Field = "Name", Title = "Name", Sortable = true },
    };

    // -----------------------------------------------------------------------
    // OnLayoutSave is gated by the same generation counter the in-service
    // PersistAsync uses — proven by directly simulating a stale timer firing.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnLayoutSave_is_not_invoked_for_stale_generation()
    {
        var callCount = 0;
        DataGridLayout? lastLayout = null;

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.OnLayoutSave, EventCallback.Factory.Create<DataGridLayout>(this, l =>
            {
                callCount++;
                lastLayout = l;
            })));

        // Pull the private layout service so we can inspect its generation counter
        // and bump it in a way that simulates a stale debounced timer.
        var serviceField = typeof(DataGrid<Row>).GetField(
            "_layoutService",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var service = (DataGridLayoutService?)serviceField.GetValue(cut.Instance);
        Assert.NotNull(service);

        // Trigger 3 rapid layout-bumping operations. Each call to
        // ScheduleAutoSave bumps _saveGeneration. Only the last timer should
        // fire after debounce (older timers are disposed), but even if a
        // stale callback slipped through, the generation guard inside
        // PersistLayoutAsync must prevent OnLayoutSave from being invoked.
        await cut.InvokeAsync(() =>
        {
            cut.Instance.UpdateColumnWidth("col-id", 150);
            cut.Instance.UpdateColumnWidth("col-id", 160);
            cut.Instance.UpdateColumnWidth("col-id", 170);
        });

        // Advance past the 500 ms debounce window with margin for the timer
        // callback to actually run on the thread pool.
        await Task.Delay(1500);

        // Exactly one callback — only the latest generation reached the
        // consumer; older debounced timers were filtered out.
        Assert.Equal(1, callCount);
        Assert.NotNull(lastLayout);

        // -----------------------------------------------------------------
        // Direct race simulation: invoke the persist callback for an
        // explicitly stale generation and confirm OnLayoutSave does not fire.
        // We do this by calling ScheduleAutoSave again (bumps generation)
        // then hand-firing a no-op save with the now-stale generation.
        // -----------------------------------------------------------------
        var savedBefore = callCount;

        // Bump the generation via ScheduleAutoSave so anything captured below
        // becomes stale.
        await cut.InvokeAsync(() =>
        {
            cut.Instance.UpdateColumnWidth("col-id", 200);
        });

        // Stable handle on the generation observed BEFORE the next bump.
        var staleGen = service!.CurrentSaveGeneration - 1;

        // Bump again so staleGen is no longer current.
        await cut.InvokeAsync(() =>
        {
            cut.Instance.UpdateColumnWidth("col-id", 210);
        });
        Assert.NotEqual(staleGen, service.CurrentSaveGeneration);

        // Reach into PersistLayoutAsync directly with the stale generation.
        var persistMethod = typeof(DataGrid<Row>).GetMethod(
            "PersistLayoutAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        await cut.InvokeAsync(async () =>
        {
            var task = (Task)persistMethod.Invoke(cut.Instance, new object?[] { staleGen })!;
            await task;
        });

        // OnLayoutSave must NOT have been invoked for the stale generation.
        Assert.Equal(savedBefore, callCount);

        // Wait for the latest debounced timer to settle and verify the
        // callback only advances by one (the latest live generation).
        await Task.Delay(1500);
        Assert.Equal(savedBefore + 1, callCount);
    }
}
