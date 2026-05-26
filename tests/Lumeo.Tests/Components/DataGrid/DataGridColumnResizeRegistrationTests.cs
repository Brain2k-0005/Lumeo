using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression for #40: column resize did nothing on the first drag attempt
/// because the JS pointerdown listener was lazy-registered inside Blazor's
/// own pointerdown handler — by the time JS attached its listener, the
/// originating event had already been dispatched, so the user had to release
/// and press again before resize actually started. Registration now happens
/// on the first OnAfterRenderAsync (before any pointer interaction) which
/// makes the very first drag work.
///
/// This test asserts the structural invariant: a DataGrid with N resizable
/// columns calls RegisterColumnResize N times before any user interaction
/// occurs. We don't try to simulate the drag itself — bUnit can't drive
/// setPointerCapture and we already cover the JS handler via the e2e suite.
/// </summary>
public class DataGridColumnResizeRegistrationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridColumnResizeRegistrationTests()
    {
        _ctx.AddLumeoServices();
        // AddLumeoServices binds IComponentInteropService → ComponentInteropService.
        // Last-registration-wins for the interface so DataGridHeaderCell (which
        // now injects the interface) resolves to the tracking impl. The
        // concrete ComponentInteropService binding is left intact for any
        // sibling component that still injects the concrete type.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<Row> Data() => new()
    {
        new(1, "Alice"),
        new(2, "Bob"),
    };

    [Fact]
    public void Resizable_Columns_Register_Resize_Handler_On_First_Render()
    {
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id",   Title = "ID",   Resizable = true  },
            new() { Field = "Name", Title = "Name", Resizable = true  },
        };

        _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, cols));

        // No pointerdown simulated — registration MUST land at first render,
        // not deferred until the user interacts.
        Assert.Equal(2, _interop.RegisterColumnResizeCallCount);
        // Each handle id is unique per column instance.
        Assert.Equal(_interop.RegisterColumnResizeHandleIds.Distinct().Count(),
                     _interop.RegisterColumnResizeCallCount);
    }

    [Fact]
    public void Non_Resizable_Columns_Do_Not_Register()
    {
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id",   Title = "ID",   Resizable = false },
            new() { Field = "Name", Title = "Name", Resizable = false },
        };

        _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, cols));

        Assert.Equal(0, _interop.RegisterColumnResizeCallCount);
    }

    [Fact]
    public void Mixed_Resizable_Flags_Only_Register_The_Resizable_Columns()
    {
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id",   Title = "ID",   Resizable = false },
            new() { Field = "Name", Title = "Name", Resizable = true  },
        };

        _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, cols));

        Assert.Equal(1, _interop.RegisterColumnResizeCallCount);
    }
}
