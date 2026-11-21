using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.NavigationMenu;

/// <summary>
/// #580: the root must not create a stacking context. With "z-10" on the root, the open
/// content (fixed z-50) was trapped at z-10 and a later NavigationMenu on the page painted
/// over it.
/// </summary>
public class NavigationMenuStackingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NavigationMenuStackingTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Root_DoesNotCreateStackingContext()
    {
        var cut = _ctx.Render(b =>
        {
            b.OpenComponent<L.NavigationMenu>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "x")));
            b.CloseComponent();
        });

        Assert.DoesNotContain("z-10", cut.Find("nav").ClassList);
    }
}
