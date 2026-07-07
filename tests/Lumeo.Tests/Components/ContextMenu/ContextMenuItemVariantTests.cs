using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// Wave 4 — ContextMenuItem gains shadcn's <c>inset</c> and
/// <c>variant="destructive"</c>, plus the ContextMenuShortcut subcomponent.
/// </summary>
public class ContextMenuItemVariantTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ContextMenuItemVariantTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.ContextMenuItem> RenderItem(Action<ComponentParameterCollectionBuilder<L.ContextMenuItem>> configure)
        => _ctx.Render<L.ContextMenuItem>(p =>
        {
            configure(p);
            p.Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Item")));
        });

    [Fact]
    public void Default_Item_Has_No_Inset_Or_Variant_Hooks()
    {
        var cut = RenderItem(_ => { });
        var btn = cut.Find("button");
        Assert.False(btn.HasAttribute("data-inset"));
        Assert.False(btn.HasAttribute("data-variant"));
    }

    [Fact]
    public void Inset_Adds_Ps8_And_DataInset()
    {
        var cut = RenderItem(p => p.Add(i => i.Inset, true));
        var btn = cut.Find("button");
        Assert.Equal("true", btn.GetAttribute("data-inset"));
        Assert.Contains("ps-8", btn.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Destructive_Variant_Adds_TextDestructive_And_DataVariant()
    {
        var cut = RenderItem(p => p.Add(i => i.Variant, L.MenuItemVariant.Destructive));
        var btn = cut.Find("button");
        Assert.Equal("destructive", btn.GetAttribute("data-variant"));
        Assert.Contains("text-destructive", btn.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Shortcut_Renders_Right_Aligned_Muted()
    {
        var cut = _ctx.Render<L.ContextMenuShortcut>(p =>
            p.Add(s => s.ChildContent, (RenderFragment)(c => c.AddContent(0, "Del"))));
        var span = cut.Find("span");
        var cls = span.GetAttribute("class") ?? "";
        Assert.Contains("ms-auto", cls);
        Assert.Contains("text-muted-foreground", cls);
    }
}
