using System;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// P2 (Codex/CodeRabbit) — submenu teardown on parent exit, Menubar variant. When this
/// menu stops being the menubar's open menu (closed or switched) while a submenu is
/// open, the submenu used to stay mounted with data-state="open" under a data-state=
/// "closed" parent. The fix makes MenubarSub compare the menubar's OpenMenuId to its
/// owning menu's id and reset its own state so the submenu exits/unmounts WITH the tree.
/// </summary>
public class MenubarSubTeardownTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MenubarSubTeardownTests() => _ctx.AddLumeoServices();
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
    public void Closing_Menu_While_Submenu_Open_Dismisses_The_Submenu()
    {
        var cut = RenderMenubar();

        // Open the menu, then open the submenu.
        MenubarTrigger(cut).Click();
        cut.Find("button[id*='sub-trigger']").Click();
        Assert.Equal("open", cut.Find("[id*='sub-content']").GetAttribute("data-state"));

        // Close the whole menu (re-click the menubar trigger) while the submenu is open.
        MenubarTrigger(cut).Click();

        // The submenu must exit WITH the tree: data-state flips to closed and the fading
        // surface is inert — not stranded open under a closed menu.
        var sub = cut.Find("[id*='sub-content']");
        Assert.Equal("closed", sub.GetAttribute("data-state"));
        Assert.Contains("pointer-events-none", sub.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Closing_Menu_Eventually_Unmounts_Both_Content_And_Submenu()
    {
        var cut = RenderMenubar();
        MenubarTrigger(cut).Click();
        cut.Find("button[id*='sub-trigger']").Click();
        Assert.NotEmpty(cut.FindAll("[id*='sub-content']"));

        MenubarTrigger(cut).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[id*='sub-content']"));
            Assert.Empty(cut.FindAll("[role='menu']"));
        }, timeout: TimeSpan.FromSeconds(5));
    }
}
