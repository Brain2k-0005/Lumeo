using System.Collections.Generic;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Features;

/// <summary>
/// #319 — FilterBar/FilterPill gain a data-driven operator/value model
/// (FilterDescriptor: Field / Operator / Value) with auto-rendered, dismissable pills.
/// </summary>
public class FilterModelTests
{
    private static BunitContext NewCtx()
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();
        return ctx;
    }

    [Fact]
    public void FilterBar_renders_descriptors_and_raises_remove()
    {
        using var ctx = NewCtx();
        Lumeo.FilterBar.FilterDescriptor? removed = null;
        var filters = new List<Lumeo.FilterBar.FilterDescriptor>
        {
            new("Status", "=", "Active"),
            new("Type", "=", "User"),
        };

        var cut = ctx.Render<Lumeo.FilterBar>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.OnRemoveFilter,
                EventCallback.Factory.Create<Lumeo.FilterBar.FilterDescriptor>(this, d => removed = d)));

        Assert.Contains("Status = Active", cut.Markup);
        Assert.Contains("Type = User", cut.Markup);

        // Each pill has a single dismiss button; removing the first raises the descriptor.
        cut.FindAll("button")[0].Click();
        Assert.Equal("Status", removed?.Field);
    }

    [Fact]
    public void FilterPill_without_operator_uses_label_colon_value()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.FilterPill>(p => p
            .Add(x => x.Label, "Tag")
            .Add(x => x.Value, "urgent"));

        Assert.Contains("Tag: urgent", cut.Markup);
    }
}
