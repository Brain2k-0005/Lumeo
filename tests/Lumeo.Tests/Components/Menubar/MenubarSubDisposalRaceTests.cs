using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// Overlay-disposal-race regression (5.12.2) — see
/// <see cref="Lumeo.Tests.Components.Tooltip.TooltipDisposalRaceTests"/> for the full
/// writeup. MenubarSubContent goes through the same shared
/// <see cref="Lumeo.Services.OverlayExitAnimator"/> exit latch as Tooltip.
/// </summary>
public class MenubarSubDisposalRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MenubarSubDisposalRaceTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderMenubar()
        => _ctx.Render(builder =>
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
                        content.OpenComponent<L.MenubarSub>(0);
                        content.AddAttribute(1, "ChildContent", (RenderFragment)(sub =>
                        {
                            sub.OpenComponent<L.MenubarSubTrigger>(0);
                            sub.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "More")));
                            sub.CloseComponent();

                            sub.OpenComponent<L.MenubarSubContent>(2);
                            sub.AddAttribute(3, "ChildContent", (RenderFragment)(sc =>
                            {
                                sc.OpenComponent<L.MenubarItem>(0);
                                sc.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Sub Item")));
                                sc.CloseComponent();
                            }));
                            sub.CloseComponent();
                        }));
                        content.CloseComponent();
                    }));
                    menu.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    // The menubar trigger is the first button (rendered before the open content).
    private static AngleSharp.Dom.IElement MenubarTrigger(IRenderedComponent<IComponent> cut)
        => cut.FindAll("button")[0];

    [Fact]
    public void AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.MenubarSubContent]]"));

        var cut = RenderMenubar();
        MenubarTrigger(cut).Click();
        cut.Find("button[id*='sub-trigger']").Click();
        Assert.Equal("open", cut.Find("[id*='sub-content']").GetAttribute("data-state"));

        // Close the whole menu (re-click the menubar trigger) while the submenu is open.
        var ex = Record.Exception(() => MenubarTrigger(cut).Click());
        Assert.Null(ex);
    }
}
