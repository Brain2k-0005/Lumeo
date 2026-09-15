using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

/// <summary>
/// Field report #464, finding 1: a direct <c>&gt; form</c> child of
/// <see cref="L.DrawerContent"/> (e.g. an <c>EditForm</c> wrapping the whole
/// body) collapsed to 0 height on the default Bottom side.
///
/// Root cause (verified against current source per CONTRACT.md's "verify
/// before changing" rule): lumeo.css carried a shared rule —
/// <c>[role="dialog"][id^="sheet-"/"dialog-"/"drawer-"] > form { flex: 1 1 0;
/// min-height: 0; ... }</c> — that force-applied flex-basis:0 to any direct
/// &lt;form&gt; child. That's safe for Sheet's Left/Right panel and Dialog's
/// panel: both have a DEFINITE height (h-full / a centered max-height box),
/// so the form has real free space to flex-grow into. A Drawer's default
/// Top/Bottom panel is h-auto (content-hugging, capped by max-height) — an
/// INDEFINITE height. Per the CSS flex algorithm, a flex-column container
/// with an auto main size derives its own size from its children's
/// hypothetical main sizes; for a flex-basis:0 item that's exactly 0. So the
/// panel — and everything in it — collapsed to zero height the instant its
/// child was a &lt;form&gt; instead of a plain &lt;div&gt; (a div gets no
/// such override and was never affected).
///
/// bUnit doesn't execute CSS layout, so the reproduction that actually
/// exercises the bug is the CSS-file assertion below (mirrors
/// GeometryTokenGuardTests' established "parse lumeo.css directly" pattern):
/// it fails against the ORIGINAL rule (which lists id^="drawer-") and passes
/// once the drawer-scoped override is removed. The bUnit test underneath it
/// is the brief's literal "render &lt;DrawerContent&gt;&lt;form&gt;...&lt;/form&gt;&lt;/DrawerContent&gt;"
/// case, confirming the fix's OTHER half — the panel's own wrapper classes
/// (flex/flex-col/overflow-y-auto/h-auto) are untouched and apply identically
/// regardless of the child's tag, i.e. a form now gets the exact same layout
/// as a div: none, beyond what the wrapper itself already provides.
/// </summary>
public class DrawerFormChildLayoutTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DrawerFormChildLayoutTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lumeo.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Lumeo.slnx not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void No_Automatic_Flex_Basis_Zero_Override_Targets_A_Direct_Form_Child_Of_A_Drawer_Panel()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "src/Lumeo/wwwroot/css/lumeo.css"));

        // Would match the pre-fix selector list
        // `[role="dialog"][id^="sheet-"] > form, [role="dialog"][id^="dialog-"] > form,
        //  [role="dialog"][id^="drawer-"] > form { ... }` — fails against the
        // original source, passes once the drawer prefix is removed.
        Assert.DoesNotMatch(@"\[id\^=[""']drawer-[""']\]\s*>\s*form", css);

        // Sheet/Dialog keep the override — only Drawer's content-hugging
        // panel needed the fix; this guards against a future edit
        // accidentally deleting the whole rule instead of just Drawer's part.
        Assert.Matches(@"\[id\^=[""']sheet-[""']\]\s*>\s*form", css);
        Assert.Matches(@"\[id\^=[""']dialog-[""']\]\s*>\s*form", css);
    }

    private IRenderedComponent<IComponent> RenderDrawerWithForm(L.Side side = L.Side.Bottom)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "Side", side);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenElement(0, "form");
                    inner.AddContent(1, "Form body");
                    inner.CloseElement();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Renders_A_Direct_Form_Child_Without_Crashing()
    {
        var cut = RenderDrawerWithForm();

        var form = cut.Find("form");
        Assert.Equal("Form body", form.TextContent);
    }

    [Theory]
    [InlineData(L.Side.Bottom)]
    [InlineData(L.Side.Top)]
    [InlineData(L.Side.Left)]
    [InlineData(L.Side.Right)]
    public void Panel_Wrapper_Keeps_Its_Flex_And_Scroll_Classes_Regardless_Of_Child_Tag(L.Side side)
    {
        // The panel (the "content's scroll/flex wrapper") owns flex-col +
        // overflow-y-auto unconditionally — it never special-cased its own
        // classes on the child's tag, so a <form> child gets exactly the
        // layout a <div> child would: whatever the wrapper itself provides,
        // nothing more.
        var cut = RenderDrawerWithForm(side);
        var panelClass = cut.Find("[role='dialog']").GetAttribute("class") ?? "";

        Assert.Contains("flex", panelClass.Split(' '));
        Assert.Contains("flex-col", panelClass.Split(' '));
        Assert.Contains("overflow-y-auto", panelClass.Split(' '));
    }
}
