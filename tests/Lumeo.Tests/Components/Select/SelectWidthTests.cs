using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// Regression: Select is a block-level form control that fills its container by
/// default (like Input). Before the fix the wrapper was only "relative", so under
/// the root's `items-start` it shrink-wrapped and SelectTrigger's own w-full
/// resolved against a collapsed box — the control never filled.
/// </summary>
public class SelectWidthTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SelectWidthTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderSelect(string? cls = null) =>
        _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            if (cls is not null) builder.AddAttribute(1, "Class", cls);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Wrapper_Is_Full_Width_By_Default()
    {
        var cut = RenderSelect();
        var wrapper = cut.Find("div.relative");
        Assert.Contains("w-full", wrapper.GetAttribute("class"));
    }

    [Fact]
    public void Consumer_Class_Width_Overrides_Default_W_Full()
    {
        // Audit guard: a consumer that intentionally constrains the width must still
        // win (tailwind-merge: w-full and w-64 are the same "w" group → last wins).
        var cut = RenderSelect("w-64");
        var cls = cut.Find("div.relative").GetAttribute("class") ?? "";
        Assert.Contains("w-64", cls);
        Assert.DoesNotContain("w-full", cls);
    }
}
