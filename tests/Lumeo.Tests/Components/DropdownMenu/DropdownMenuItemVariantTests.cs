using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// Wave 4 — DropdownMenuItem gains shadcn's <c>inset</c> and
/// <c>variant="destructive"</c>, plus the DropdownMenuShortcut subcomponent.
/// Asserts both the applied Tailwind classes and the data-* styling hooks.
/// </summary>
public class DropdownMenuItemVariantTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DropdownMenuItemVariantTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DropdownMenuItem> RenderItem(Action<ComponentParameterCollectionBuilder<L.DropdownMenuItem>> configure)
        => _ctx.Render<L.DropdownMenuItem>(p =>
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
        Assert.DoesNotContain("ps-8", btn.GetAttribute("class") ?? "");
        Assert.DoesNotContain("text-destructive", btn.GetAttribute("class") ?? "");
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
        var cls = btn.GetAttribute("class") ?? "";
        Assert.Contains("text-destructive", cls);
        Assert.Contains("focus:bg-destructive/10", cls);
    }

    [Fact]
    public void Shortcut_Renders_Right_Aligned_Muted()
    {
        var cut = _ctx.Render<L.DropdownMenuShortcut>(p =>
            p.Add(s => s.ChildContent, (RenderFragment)(c => c.AddContent(0, "⌘K"))));
        var span = cut.Find("span");
        var cls = span.GetAttribute("class") ?? "";
        Assert.Contains("ms-auto", cls);
        Assert.Contains("text-muted-foreground", cls);
        Assert.Contains("⌘K", span.TextContent);
    }
}
