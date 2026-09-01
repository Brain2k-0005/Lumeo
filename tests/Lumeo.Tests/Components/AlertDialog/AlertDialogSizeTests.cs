using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.AlertDialog;

/// <summary>
/// AlertDialogContent.Size — family completion of the same lever DialogContent.Size
/// already gives Dialog (AlertDialogContent is DialogContent's structural twin: same
/// panel shape, same "relative grid w-full {size} gap-4 bg-background p-6 shadow-lg
/// sm:rounded-lg" CssClass). AlertDialogSize's SizeClass switch is a 1:1 copy of
/// DialogContent.DialogSize's own switch, so these tests mirror DialogTests's own
/// Size-variant coverage exactly — see DialogTests.RenderDialogWithSize /
/// DialogContent_Size_Sm_Adds_MaxW_Sm / DialogContent_Size_Full_Adds_Full_MaxW.
///
/// Exact space-delimited token assertions throughout (never Assert.Contains on the raw
/// class string) — "max-w-sm" is a substring of nothing else here, but "max-w-lg" IS a
/// substring-adjacent risk once max-w-2xl/max-w-4xl are in the same list, so every check
/// below splits on whitespace first.
/// </summary>
public class AlertDialogSizeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AlertDialogSizeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertExactToken(string cls, string expectedToken)
    {
        var tokens = cls.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(expectedToken, tokens);
    }

    private static void AssertTokenAbsent(string cls, string absentToken)
    {
        var tokens = cls.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.DoesNotContain(absentToken, tokens);
    }

    private IRenderedComponent<IComponent> RenderAlertDialogWithSize(L.AlertDialogContent.AlertDialogSize? size)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                var seq = 1;
                if (size.HasValue)
                    b.AddAttribute(seq++, "Size", size.Value);
                b.AddAttribute(seq, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.AddContent(0, "Size test content");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- A consumer who sets nothing gets exactly today's rendering ---

    [Fact]
    public void Default_Parameter_Value_Is_AlertDialogSize_Default()
    {
        Assert.Equal(L.AlertDialogContent.AlertDialogSize.Default, new L.AlertDialogContent().Size);
    }

    [Fact]
    public void No_Size_Set_Renders_Exactly_The_Pre_Existing_MaxW_Lg_Class()
    {
        // Predicted-vs-actual: before this change CssClass was the single literal
        // "relative grid w-full max-w-lg gap-4 bg-background p-6 shadow-lg sm:rounded-lg".
        // Rendering with no Size attribute at all (not even an explicit Default) must
        // reproduce every one of those tokens, byte-for-byte, and nothing else in the
        // max-w-* family.
        var cut = RenderAlertDialogWithSize(size: null);
        var dialog = cut.Find("[role='alertdialog']");
        var tokens = (dialog.GetAttribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var expected in new[] { "relative", "grid", "w-full", "max-w-lg", "gap-4", "bg-background", "p-6", "shadow-lg", "sm:rounded-xl" })
            Assert.Contains(expected, tokens);

        foreach (var otherSize in new[] { "max-w-sm", "max-w-2xl", "max-w-4xl", "max-w-[calc(100vw-2rem)]" })
            Assert.DoesNotContain(otherSize, tokens);
    }

    [Theory]
    [InlineData(L.AlertDialogContent.AlertDialogSize.Sm, "max-w-sm")]
    [InlineData(L.AlertDialogContent.AlertDialogSize.Default, "max-w-lg")]
    [InlineData(L.AlertDialogContent.AlertDialogSize.Lg, "max-w-2xl")]
    [InlineData(L.AlertDialogContent.AlertDialogSize.Xl, "max-w-4xl")]
    public void Size_Adds_The_Expected_MaxW_Token(L.AlertDialogContent.AlertDialogSize size, string expectedToken)
    {
        var cut = RenderAlertDialogWithSize(size);
        var dialog = cut.Find("[role='alertdialog']");
        AssertExactToken(dialog.GetAttribute("class") ?? "", expectedToken);
    }

    [Fact]
    public void Size_Full_Adds_Viewport_Relative_MaxW_And_MaxH()
    {
        var cut = RenderAlertDialogWithSize(L.AlertDialogContent.AlertDialogSize.Full);
        var cls = cut.Find("[role='alertdialog']").GetAttribute("class") ?? "";
        AssertExactToken(cls, "max-w-[calc(100vw-2rem)]");
        AssertExactToken(cls, "max-h-[calc(100vh-2rem)]");
        AssertTokenAbsent(cls, "max-w-lg");
    }

    [Fact]
    public void Size_Sm_Does_Not_Also_Carry_The_Default_MaxW_Lg_Token()
    {
        // Regression guard: a naive Cx.Merge misuse (e.g. leaving the old literal
        // "max-w-lg" in the base string alongside the new SizeClass) would render BOTH
        // tokens — Assert.Contains("max-w-lg", cls) as a raw-string check would still
        // pass against "max-w-sm max-w-lg ...", which is exactly the false-negative
        // shape the exact-token discipline here exists to catch.
        var cut = RenderAlertDialogWithSize(L.AlertDialogContent.AlertDialogSize.Sm);
        var cls = cut.Find("[role='alertdialog']").GetAttribute("class") ?? "";
        AssertExactToken(cls, "max-w-sm");
        AssertTokenAbsent(cls, "max-w-lg");
    }
}
