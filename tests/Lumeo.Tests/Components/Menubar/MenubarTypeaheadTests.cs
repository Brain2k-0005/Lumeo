using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// #225 — Menubar gains typeahead parity with DropdownMenu. Printable keys on
/// an open menu accumulate a query and jump focus to the first matching item.
/// </summary>
public class MenubarTypeaheadTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public MenubarTypeaheadTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderMenubar()
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Menubar>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.MenubarMenu>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(menu =>
                {
                    menu.OpenComponent<L.MenubarTrigger>(0);
                    menu.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "File")));
                    menu.CloseComponent();

                    menu.OpenComponent<L.MenubarContent>(1);
                    menu.AddAttribute(2, "ChildContent", (RenderFragment)(content =>
                    {
                        content.OpenComponent<L.MenubarItem>(0);
                        content.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Open")));
                        content.CloseComponent();
                        content.OpenComponent<L.MenubarItem>(2);
                        content.AddAttribute(3, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Save")));
                        content.CloseComponent();
                    }));
                    menu.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Printable_Key_On_Open_Menu_Triggers_Typeahead()
    {
        var cut = RenderMenubar();
        cut.Find("button").Click(); // open the menu

        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "s" });

        var call = Assert.Single(_interop.TypeaheadCalls);
        Assert.Equal("s", call.Query);
        Assert.StartsWith("menubar-content-", call.ContainerId);
    }

    [Fact]
    public void Navigation_And_Modifier_Keys_Are_Not_Typeahead()
    {
        var cut = RenderMenubar();
        cut.Find("button").Click();

        var content = cut.Find("[role='menu']");
        content.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }); // menubar nav
        content.KeyDown(new KeyboardEventArgs { Key = "s", CtrlKey = true });
        Assert.Empty(_interop.TypeaheadCalls);
    }
}
