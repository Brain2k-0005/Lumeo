using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components;

/// <summary>
/// DocFlow T2 density follow-up (#523): <c>--lumeo-menu-item-h</c> shipped in 5.11.1 but
/// no component read it. This wires a `min-height` floor onto every plain/checkbox/radio
/// item across DropdownMenu, ContextMenu, and Menubar at their default/Comfortable rung —
/// the fallback (32px) equals each item's own natural content height there (padding + the
/// text-sm line), so wiring it in is a no-op unless a consumer redefines the token.
/// DropdownMenu is the only density-aware family (see DensityGuide); Compact (28px) and
/// Spacious (40px) are NOT driven by this token — only Comfortable/default reads it — so
/// this suite also pins that those two rungs render their own fixed class, unaffected by
/// the token.
/// </summary>
public class MenuItemHeightTokenTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MenuItemHeightTokenTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string TokenClass = "min-h-[var(--lumeo-menu-item-h,calc(var(--spacing,0.25rem)*8))]";
    private const string TokenClassSm = "sm:" + TokenClass;

    private static string ClassOf<T>(IRenderedComponent<T> cut, string selector) where T : IComponent =>
        cut.Find(selector).GetAttribute("class") ?? "";

    // --- DropdownMenuItem: density-aware, sm:-prefixed (layers over the mobile min-h-11 floor) ---

    private IRenderedComponent<IComponent> RenderDropdownItem(L.Density? density) =>
        _ctx.Render(builder =>
        {
            void RenderItem(RenderTreeBuilder scope)
            {
                scope.OpenComponent<L.DropdownMenuItem>(0);
                scope.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Item")));
                scope.CloseComponent();
            }

            if (density.HasValue)
            {
                builder.OpenComponent<L.DensityScope>(0);
                builder.AddAttribute(1, "Value", density.Value);
                builder.AddAttribute(2, "ChildContent", (RenderFragment)(scope => RenderItem(scope)));
                builder.CloseComponent();
            }
            else
            {
                RenderItem(builder);
            }
        });

    [Fact]
    public void DropdownMenuItem_No_DensityScope_Carries_The_Token_Class()
    {
        var cut = RenderDropdownItem(null);
        Assert.Contains(TokenClassSm, ClassOf(cut, "button"));
    }

    [Fact]
    public void DropdownMenuItem_Comfortable_Carries_The_Token_Class()
    {
        var cut = RenderDropdownItem(L.Density.Comfortable);
        Assert.Contains(TokenClassSm, ClassOf(cut, "button"));
    }

    [Fact]
    public void DropdownMenuItem_Compact_Does_Not_Carry_The_Token_Class()
    {
        var cut = RenderDropdownItem(L.Density.Compact);
        var cls = ClassOf(cut, "button");
        Assert.DoesNotContain(TokenClassSm, cls);
        Assert.Contains("sm:min-h-7", cls);
    }

    [Fact]
    public void DropdownMenuItem_Spacious_Does_Not_Carry_The_Token_Class()
    {
        var cut = RenderDropdownItem(L.Density.Spacious);
        var cls = ClassOf(cut, "button");
        Assert.DoesNotContain(TokenClassSm, cls);
        Assert.Contains("sm:min-h-10", cls);
    }

    // --- DropdownMenuCheckboxItem / DropdownMenuRadioItem: density-aware, unprefixed ---

    [Fact]
    public void DropdownMenuCheckboxItem_Comfortable_Carries_The_Token_Class()
    {
        var cut = _ctx.Render<L.DropdownMenuCheckboxItem>(p =>
            p.Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Show grid"))));
        Assert.Contains(TokenClass, ClassOf(cut, "button"));
    }

    [Fact]
    public void DropdownMenuRadioItem_Comfortable_Carries_The_Token_Class()
    {
        var cut = _ctx.Render<L.DropdownMenuRadioGroup>(p =>
        {
            p.Add(g => g.Value, "a");
            p.Add(g => g.ChildContent, (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.DropdownMenuRadioItem>(0);
                inner.AddAttribute(1, "Value", "a");
                inner.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                inner.CloseComponent();
            }));
        });
        Assert.Contains(TokenClass, ClassOf(cut, "button"));
    }

    // --- ContextMenuItem / CheckboxItem / RadioItem: not density-aware, sm:/unprefixed ---

    [Fact]
    public void ContextMenuItem_Carries_The_Token_Class()
    {
        var cut = _ctx.Render<L.ContextMenuItem>(p =>
            p.Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Item"))));
        Assert.Contains(TokenClassSm, ClassOf(cut, "button"));
    }

    [Fact]
    public void ContextMenuCheckboxItem_Carries_The_Token_Class()
    {
        var cut = _ctx.Render<L.ContextMenuCheckboxItem>(p =>
            p.Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Show grid"))));
        Assert.Contains(TokenClass, ClassOf(cut, "button"));
    }

    [Fact]
    public void ContextMenuRadioItem_Carries_The_Token_Class()
    {
        var cut = _ctx.Render<L.ContextMenuRadioGroup>(p =>
        {
            p.Add(g => g.Value, "a");
            p.Add(g => g.ChildContent, (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.ContextMenuRadioItem>(0);
                inner.AddAttribute(1, "Value", "a");
                inner.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                inner.CloseComponent();
            }));
        });
        Assert.Contains(TokenClass, ClassOf(cut, "button"));
    }

    // --- MenubarItem / CheckboxItem / RadioItem: not density-aware, unprefixed ---

    [Fact]
    public void MenubarItem_Carries_The_Token_Class()
    {
        var cut = _ctx.Render<L.MenubarItem>(p =>
            p.Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Item"))));
        Assert.Contains(TokenClass, ClassOf(cut, "button"));
    }

    [Fact]
    public void MenubarCheckboxItem_Carries_The_Token_Class()
    {
        var cut = _ctx.Render<L.MenubarCheckboxItem>(p =>
            p.Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Show grid"))));
        Assert.Contains(TokenClass, ClassOf(cut, "button"));
    }

    [Fact]
    public void MenubarRadioItem_Carries_The_Token_Class()
    {
        var cut = _ctx.Render<L.MenubarRadioGroup>(p =>
        {
            p.Add(g => g.Value, "a");
            p.Add(g => g.ChildContent, (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.MenubarRadioItem>(0);
                inner.AddAttribute(1, "Value", "a");
                inner.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                inner.CloseComponent();
            }));
        });
        Assert.Contains(TokenClass, ClassOf(cut, "button"));
    }

    // --- SubTrigger variants: wrapping a Sub context is heavyweight to render for a pure
    // CSS-class check, so this is a source-level guard instead (same technique as
    // GeometryTokenGuardTests/MenuColorTokenGuardTests) — proves each of the three
    // SubTrigger files actually references the token, complementing the rendered
    // assertions above for the plain/checkbox/radio items. ---

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lumeo.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Lumeo.slnx not found above " + AppContext.BaseDirectory);
    }

    [Theory]
    [InlineData("src/Lumeo/UI/DropdownMenu/DropdownMenuSubTrigger.razor")]
    [InlineData("src/Lumeo/UI/ContextMenu/ContextMenuSubTrigger.razor")]
    [InlineData("src/Lumeo/UI/Menubar/MenubarSubTrigger.razor")]
    public void SubTrigger_Source_References_The_Menu_Item_Height_Token(string relativeFile)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), relativeFile.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("--lumeo-menu-item-h", text);
    }
}
