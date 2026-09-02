using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Overlays;

/// <summary>Field report 4.4: SideOffset and AlignOffset reach the positioner (Radix's sideOffset
/// and alignOffset), with the defaults that placed the contents before they existed.</summary>
public class FloatingOffsetTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public FloatingOffsetTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private TrackingInteropService Tracking => _interop;

    [Fact]
    public void Popover_Passes_Its_Offsets_To_The_Positioner()
    {
        _ctx.Render<L.Popover>(p => p.Add(x => x.Open, true).AddChildContent<L.PopoverContent>(c => c.Add(x => x.SideOffset, 10).Add(x => x.AlignOffset, -6).AddChildContent("body")));
        var call = Assert.Single(Tracking.PositionFixedCalls);
        Assert.Equal((10, -6), (call.Offset, call.AlignOffset));
    }

    [Fact]
    public void Popover_Defaults_Keep_The_Previous_Placement()
    {
        _ctx.Render<L.Popover>(p => p.Add(x => x.Open, true).AddChildContent<L.PopoverContent>(c => c.AddChildContent("body")));
        var call = Assert.Single(Tracking.PositionFixedCalls);
        Assert.Equal((4, 0), (call.Offset, call.AlignOffset));
    }

    [Fact]
    public void DropdownMenu_Passes_Its_Offsets_To_The_Positioner()
    {
        _ctx.Render<L.DropdownMenu>(p => p.Add(x => x.Open, true).AddChildContent<L.DropdownMenuContent>(c => c.Add(x => x.SideOffset, 2).Add(x => x.AlignOffset, 8).AddChildContent<L.DropdownMenuItem>(i => i.AddChildContent("Item"))));
        var call = Assert.Single(Tracking.PositionFixedCalls);
        Assert.Equal((2, 8), (call.Offset, call.AlignOffset));
    }
}
