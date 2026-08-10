using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tabs;

/// <summary>
/// PadClass's Comfortable-density row is variant-scoped: Default needs `py-1` to fit
/// inside TabsList's `h-9 p-1` track (28px content box — the wave-0 C2 overflow fix),
/// but Card/Pill/Underline render inside a `h-10` track with NO `p-1` padding at all
/// (see TabsList.razor CssClass), so they were never at overflow risk and must keep the
/// original `py-1.5`. Codex #386 P2 finding 2: the py-1 fix originally applied to every
/// variant via one shared switch, silently shrinking Card/Pill/Underline's Comfortable
/// trigger from 32px to 28px with no matching justification.
/// </summary>
public class TabsTriggerPaddingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public TabsTriggerPaddingTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderTabs(L.Tabs.TabsVariant variant)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tabs>(0);
            builder.AddAttribute(1, "ActiveValue", "one");
            builder.AddAttribute(2, "Variant", variant);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TabsList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.TabsTrigger>(0);
                    inner.AddAttribute(1, "Value", "one");
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(t => t.AddContent(0, "First")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Default_Variant_Comfortable_Renders_Py1_Not_Py1Point5()
    {
        // Predicted-vs-actual (disable check): reverting PadClass to the pre-fix
        // unscoped switch (`_ => "px-3 py-1"` for every variant, no CurrentVariant
        // branch) still renders py-1 here — Default is unaffected either way, this
        // just pins the value stays correct after the variant-scoping fix.
        var cut = RenderTabs(L.Tabs.TabsVariant.Default);

        var cls = cut.Find("button[role='tab']").GetAttribute("class") ?? "";
        Assert.Contains("py-1", cls);
        Assert.DoesNotContain("py-1.5", cls);
    }

    [Theory]
    [InlineData(L.Tabs.TabsVariant.Card)]
    [InlineData(L.Tabs.TabsVariant.Pill)]
    [InlineData(L.Tabs.TabsVariant.Underline)]
    public void Non_Default_Variants_Keep_Py1Point5_At_Comfortable(L.Tabs.TabsVariant variant)
    {
        var cut = RenderTabs(variant);

        var cls = cut.Find("button[role='tab']").GetAttribute("class") ?? "";
        Assert.Contains("py-1.5", cls);
    }
}
