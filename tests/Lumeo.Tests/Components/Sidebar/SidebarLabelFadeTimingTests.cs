using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// Regression tests for a user-reported asymmetric-timing bug (confirmed via decompiling
/// the shipped NuGet package): SidebarMenuButton's label faded with duration-150 and an
/// ASYMMETRIC delay per direction (delay-0 collapsing, delay-150 expanding), while
/// SidebarComponent's own width transition always used duration-300 with no delay in
/// either direction. Collapsing: the label finished fading at t=150ms while the container
/// kept shrinking until t=300ms — two visibly sequential steps ("items shrink first, then
/// the sidebar catches up"). Expanding: the 150ms delay + 150ms duration happened to SUM to
/// 300ms, exactly matching the container's finish time by coincidence — so expanding looked
/// synced only at the very end, not throughout. The label's own <c>ease-out</c> class was
/// also a different curve than the container's implicit Tailwind default
/// (cubic-bezier(0.4,0,0.2,1) from transition-all, unless overridden).
///
/// Fix: duration-300, no delay, no explicit easing override (falls back to the same
/// Tailwind default the container uses) — identical in both directions.
/// </summary>
public class SidebarLabelFadeTimingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SidebarLabelFadeTimingTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderMenuButton(bool isCollapsed)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarProvider>(0);
            builder.AddAttribute(1, "IsCollapsed", isCollapsed);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarComponent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(sc =>
                {
                    sc.OpenComponent<L.SidebarMenu>(0);
                    sc.AddAttribute(1, "ChildContent", (RenderFragment)(menu =>
                    {
                        menu.OpenComponent<L.SidebarMenuItem>(0);
                        menu.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                        {
                            item.OpenComponent<L.SidebarMenuButton>(0);
                            // The label span only renders when IconContent is ALSO set
                            // (SidebarMenuButton.razor's Anchor: @if (IconContent is not
                            // null) { icon; @if (LabelContent is not null) { label } }).
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
    public void Label_Fade_Duration_And_Delay_Match_The_Container_Transition_In_Both_Directions(bool isCollapsed)
    {
        var cut = RenderMenuButton(isCollapsed);

        var labelClass = FindLabelSpan(cut).ClassList;
        var containerClass = cut.Find("aside").ClassList;

        // Must match the container's actual duration (duration-300) and have NO delay in
        // either direction — the exact bug: the label used to differ from this per-direction.
        Assert.Contains("duration-300", labelClass);
        Assert.Contains("duration-300", containerClass);
        Assert.DoesNotContain(labelClass, c => c.StartsWith("delay-"));

        // Must NOT carry its own easing override — falls back to the same Tailwind default
        // (cubic-bezier(0.4,0,0.2,1) from transition-all/transition-opacity) the container
        // implicitly uses, instead of a different explicit curve like ease-out.
        Assert.DoesNotContain(labelClass, c => c.StartsWith("ease-"));
    }

    [Fact]
    public void Label_Is_Invisible_When_Collapsed_And_Visible_When_Expanded()
    {
        // Guard: the opacity toggle itself (the actual visual effect) must survive the
        // timing-symmetry fix untouched.
        Assert.Contains("opacity-0", FindLabelSpan(RenderMenuButton(isCollapsed: true)).ClassList);
        Assert.Contains("opacity-100", FindLabelSpan(RenderMenuButton(isCollapsed: false)).ClassList);
    }
}
