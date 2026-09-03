using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Command;

/// <summary>A host that filters its own items (<c>ShouldFilter="false"</c>) keeps its groups: the
/// group's own visibility check must not hide a result set the search text happens not to match.</summary>
public class CommandShouldFilterGroupTests
{
    private static IRenderedComponent<IComponent> RenderPalette(BunitContext ctx, bool shouldFilter)
        => ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ShouldFilter", shouldFilter);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                b.CloseComponent();
                b.OpenComponent<L.CommandList>(1);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.CommandGroup>(0);
                    list.AddAttribute(1, "Heading", "Results");
                    list.AddAttribute(2, "ChildContent", (RenderFragment)(grp =>
                    {
                        grp.OpenComponent<L.CommandItem>(0);
                        grp.AddAttribute(1, "FilterValue", "apple");
                        grp.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                        grp.CloseComponent();
                    }));
                    list.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Group_Stays_Visible_When_The_Host_Filters()
    {
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();
        var cut = RenderPalette(ctx, shouldFilter: false);

        cut.Find("input").Input("zzz");

        Assert.False(cut.Find("[data-slot='command-group']").HasAttribute("hidden"));
        Assert.Single(cut.FindAll("[data-slot='command-item']"));
    }

    [Fact]
    public void Group_Hides_When_The_Command_Filters_Everything_Out()
    {
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();
        var cut = RenderPalette(ctx, shouldFilter: true);

        cut.Find("input").Input("zzz");

        Assert.True(cut.Find("[data-slot='command-group']").HasAttribute("hidden"));
    }
}
