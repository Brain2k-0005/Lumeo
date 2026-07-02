using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// shadcn-sidebar motion-parity contract (user request: the sidebar must feel 1:1 like
/// https://ui.shadcn.com/docs/components/radix/sidebar). History: 4.0.3 fixed a
/// user-reported asymmetric label-fade (duration-150 + per-direction delays vs the
/// container's duration-300) by syncing the fade to the container — but shadcn doesn't
/// fade menu labels AT ALL, and the residual cross-fade still felt different. The current
/// contract, matching shadcn exactly:
///
/// - Container (aside): geometry-only transition at 200ms LINEAR
///   (transition-[width,translate] duration-200 ease-linear) — shadcn's sidebar-gap/
///   -container animate width/left/right at exactly duration-200 ease-linear.
/// - Menu-button label: NO transition, NO opacity classes — the span stays mounted and is
///   hard-CLIPPED by the collapsing width (button overflow-hidden), shadcn's
///   [&gt;span:last-child]:truncate mechanism. Visibility comes from geometry alone.
/// - Group label (SidebarGroupLabel): animates out via -mt-8 + opacity-0 over
///   transition-[margin,opacity] duration-200 ease-linear (shadcn's exact classes) —
///   the -mt-8 equals the label's fixed h-8 so following rows slide up seamlessly.
/// </summary>
public class SidebarLabelFadeTimingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SidebarLabelFadeTimingTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderSidebar(bool isCollapsed, bool withGroupLabel = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarProvider>(0);
            builder.AddAttribute(1, "IsCollapsed", isCollapsed);
            builder.AddAttribute(2, "Variant", L.SidebarProvider.SidebarVariant.Icon);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarComponent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(sc =>
                {
                    if (withGroupLabel)
                    {
                        sc.OpenComponent<L.SidebarGroupLabel>(0);
                        sc.AddAttribute(1, "ChildContent", (RenderFragment)(g => g.AddContent(0, "Platform")));
                        sc.CloseComponent();
                    }
                    sc.OpenComponent<L.SidebarMenu>(2);
                    sc.AddAttribute(3, "ChildContent", (RenderFragment)(menu =>
                    {
                        menu.OpenComponent<L.SidebarMenuItem>(0);
                        menu.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                        {
                            item.OpenComponent<L.SidebarMenuButton>(0);
                            // The label span only renders when IconContent is ALSO set.
                            item.AddAttribute(1, "IconContent", (RenderFragment)(i => i.AddContent(0, "★")));
                            item.AddAttribute(2, "LabelContent", (RenderFragment)(l => l.AddContent(0, "Dashboard")));
                            item.CloseComponent();
                        }));
                        menu.CloseComponent();
                    }));
                    sc.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static AngleSharp.Dom.IElement FindLabelSpan(IRenderedComponent<IComponent> cut) =>
        cut.FindAll("span").Single(s => s.TextContent.Trim() == "Dashboard");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Container_Animates_Geometry_At_200ms_Linear(bool isCollapsed)
    {
        var cut = RenderSidebar(isCollapsed);
        var containerClass = cut.Find("aside").ClassList;

        Assert.Contains("duration-200", containerClass);
        Assert.Contains("ease-linear", containerClass);
        Assert.Contains("transition-[width,translate]", containerClass);
        Assert.DoesNotContain("transition-all", containerClass);
        Assert.DoesNotContain(containerClass, c => c.StartsWith("delay-"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MenuButton_Label_Is_Clipped_Not_Faded(bool isCollapsed)
    {
        var cut = RenderSidebar(isCollapsed);
        var labelClass = FindLabelSpan(cut).ClassList;

        // shadcn parity: no fade — the span carries no transition/opacity/delay classes
        // in EITHER state; visibility comes entirely from the width clip.
        Assert.DoesNotContain(labelClass, c => c.StartsWith("transition"));
        Assert.DoesNotContain(labelClass, c => c.StartsWith("opacity-"));
        Assert.DoesNotContain(labelClass, c => c.StartsWith("delay-"));
        Assert.DoesNotContain(labelClass, c => c.StartsWith("duration-"));
        Assert.Contains("whitespace-nowrap", labelClass);
        Assert.Contains("truncate", labelClass);
    }

    [Fact]
    public void MenuButton_Label_Stays_Mounted_When_Collapsed()
    {
        // Clip-not-fade only works if the span is still in the DOM on the collapsed rail.
        Assert.NotNull(FindLabelSpan(RenderSidebar(isCollapsed: true)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GroupLabel_Slides_Out_Via_Margin_And_Opacity_At_200ms_Linear(bool isCollapsed)
    {
        var cut = RenderSidebar(isCollapsed, withGroupLabel: true);
        var groupLabel = cut.FindAll("div").Single(d => d.TextContent.Trim() == "Platform");
        var cls = groupLabel.ClassList;

        // shadcn's exact mechanism: -mt-8 pulls following content up by the label's own
        // fixed h-8 while it fades, both over transition-[margin,opacity] 200ms linear.
        Assert.Contains("transition-[margin,opacity]", cls);
        Assert.Contains("duration-200", cls);
        Assert.Contains("ease-linear", cls);
        Assert.Contains("h-8", cls);
        Assert.DoesNotContain("sr-only", cls);
        if (isCollapsed)
        {
            Assert.Contains("-mt-8", cls);
            Assert.Contains("opacity-0", cls);
        }
        else
        {
            Assert.DoesNotContain("-mt-8", cls);
            Assert.DoesNotContain("opacity-0", cls);
        }
    }

    [Fact]
    public void Collapsed_Icon_Rail_Is_ShadcnWidth_With_IconSquare_Button_Padding()
    {
        var cut = RenderSidebar(isCollapsed: true);

        // shadcn geometry: SIDEBAR_WIDTH_ICON = 3rem (w-12) with p-2 icon-square buttons.
        Assert.Contains("w-12", cut.Find("aside").ClassList);
        Assert.Contains("px-2", cut.Find("a").ClassList);
    }
}
