using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

/// <summary>
/// DrawerContent.Size — family completion of the same lever SheetContent.Size already
/// gives Sheet (Drawer is Sheet's structural twin for the edge-panel / bottom-sheet
/// shape). Left/Right sizes only move the max-w cap (the w-3/4 base stays fixed, mirroring
/// SheetContent.SheetSize.Full's own documented Left/Right precedent); Top/Bottom sizes
/// move the max-h cap, and DrawerSize.Full additionally drops the permanent mt-24 "peek"
/// offset so it actually reaches both viewport edges (the mobile-fullscreen bottom-sheet
/// pattern) — every other Side/Size combination keeps mt-24 unchanged.
///
/// Exact space-delimited token assertions throughout — never Assert.Contains on the raw
/// class string (this file's own "mt-24"/"mt-2" family is exactly the kind of substring
/// collision that discipline exists to prevent — Split-then-Contains is used everywhere).
/// </summary>
public class DrawerSizeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DrawerSizeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static string[] Tokens(string? cls) => (cls ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private IRenderedComponent<IComponent> RenderDrawer(L.Side side, L.DrawerContent.DrawerSize? size)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                var seq = 1;
                b.AddAttribute(seq++, "Side", side);
                if (size.HasValue)
                    b.AddAttribute(seq++, "Size", size.Value);
                b.AddAttribute(seq, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Drawer content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Default_Parameter_Value_Is_DrawerSize_Default()
    {
        Assert.Equal(L.DrawerContent.DrawerSize.Default, new L.DrawerContent().Size);
    }

    // --- A consumer who sets nothing gets exactly today's rendering, every Side ---

    [Theory]
    [InlineData(L.Side.Bottom)]
    [InlineData(L.Side.Top)]
    public void No_Size_Set_TopBottom_Renders_Exactly_The_Pre_Existing_MaxH_96vh_And_Mt24(L.Side side)
    {
        var cut = RenderDrawer(side, size: null);
        var tokens = Tokens(cut.Find("[role='dialog']").GetAttribute("class"));

        Assert.Contains("max-h-[96vh]", tokens);
        Assert.Contains("mt-24", tokens);
        foreach (var other in new[] { "max-h-[40vh]", "max-h-screen", "max-h-full", "inset-y-0" })
            Assert.DoesNotContain(other, tokens);
    }

    [Theory]
    [InlineData(L.Side.Right)]
    [InlineData(L.Side.Left)]
    public void No_Size_Set_LeftRight_Renders_Exactly_The_Pre_Existing_MaxW_Sm_And_Mt24(L.Side side)
    {
        var cut = RenderDrawer(side, size: null);
        var tokens = Tokens(cut.Find("[role='dialog']").GetAttribute("class"));

        Assert.Contains("max-w-sm", tokens);
        Assert.Contains("w-3/4", tokens);
        Assert.Contains("mt-24", tokens);
        foreach (var other in new[] { "max-w-xs", "max-w-lg", "max-w-xl", "max-w-full" })
            Assert.DoesNotContain(other, tokens);
    }

    // --- Left/Right: Size controls the max-w cap ---

    [Theory]
    [InlineData(L.DrawerContent.DrawerSize.Sm, "max-w-xs")]
    [InlineData(L.DrawerContent.DrawerSize.Default, "max-w-sm")]
    [InlineData(L.DrawerContent.DrawerSize.Lg, "max-w-lg")]
    [InlineData(L.DrawerContent.DrawerSize.Xl, "max-w-xl")]
    [InlineData(L.DrawerContent.DrawerSize.Full, "max-w-full")]
    public void Right_Side_Size_Adds_The_Expected_MaxW_Token(L.DrawerContent.DrawerSize size, string expectedToken)
    {
        var cut = RenderDrawer(L.Side.Right, size);
        var tokens = Tokens(cut.Find("[role='dialog']").GetAttribute("class"));
        Assert.Contains(expectedToken, tokens);
        // Left/Right w-3/4 base is unaffected by Size, at every tier including Full
        // (mirrors SheetContent.SheetSize.Full's own documented Left/Right precedent).
        Assert.Contains("w-3/4", tokens);
        Assert.Contains("mt-24", tokens);
    }

    // --- Top/Bottom: Size controls the max-h cap ---

    [Theory]
    [InlineData(L.DrawerContent.DrawerSize.Sm, "max-h-[40vh]")]
    [InlineData(L.DrawerContent.DrawerSize.Default, "max-h-[96vh]")]
    [InlineData(L.DrawerContent.DrawerSize.Lg, "max-h-screen")]
    [InlineData(L.DrawerContent.DrawerSize.Xl, "max-h-full")]
    public void Bottom_Side_Non_Full_Size_Adds_The_Expected_MaxH_Token_And_Keeps_Mt24(L.DrawerContent.DrawerSize size, string expectedToken)
    {
        var cut = RenderDrawer(L.Side.Bottom, size);
        var tokens = Tokens(cut.Find("[role='dialog']").GetAttribute("class"));
        Assert.Contains(expectedToken, tokens);
        Assert.Contains("mt-24", tokens);
    }

    // --- Full on Top/Bottom is a true viewport takeover: mt-24 drops, inset-y-0/h-full/max-h-full apply ---

    [Theory]
    [InlineData(L.Side.Bottom)]
    [InlineData(L.Side.Top)]
    public void Full_Size_TopBottom_Drops_Mt24_And_Adds_Full_Viewport_Tokens(L.Side side)
    {
        var cut = RenderDrawer(side, L.DrawerContent.DrawerSize.Full);
        var tokens = Tokens(cut.Find("[role='dialog']").GetAttribute("class"));

        // Predicted-vs-actual (disable check): reverting MarginClass to the old
        // unconditional "mt-24" literal renders "mt-24" here — the predicted WRONG
        // value under the pre-fix code. The fix's correct value has NO mt-24 token.
        Assert.DoesNotContain("mt-24", tokens);
        Assert.Contains("inset-y-0", tokens);
        Assert.Contains("h-full", tokens);
        Assert.Contains("max-h-full", tokens);
    }

    [Theory]
    [InlineData(L.Side.Right)]
    [InlineData(L.Side.Left)]
    public void Full_Size_LeftRight_Keeps_Mt24_Unaffected(L.Side side)
    {
        // Full's mt-24 removal is scoped to Top/Bottom only — Left/Right's mt-24 has
        // nothing to do with the height axis Full changes there (width only).
        var cut = RenderDrawer(side, L.DrawerContent.DrawerSize.Full);
        var tokens = Tokens(cut.Find("[role='dialog']").GetAttribute("class"));
        Assert.Contains("mt-24", tokens);
        Assert.Contains("max-w-full", tokens);
    }
}
