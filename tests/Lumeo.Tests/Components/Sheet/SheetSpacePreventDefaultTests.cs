using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sheet;

/// <summary>
/// Battle-wave2 #90 (keyboard-a11y) — the non-AsChild <see cref="L.SheetTrigger"/> and
/// <see cref="L.SheetClose"/> wrappers are <c>div[role=button]</c>, which have no native
/// key synthesis: Space both activated the control AND scroll-jumped the page. The fix
/// suppresses Space's default action via the library's <c>RegisterPreventDefaultKeys</c>
/// interop (the same idiom as DialogTrigger / DialogClose / CollapsibleTrigger), which
/// requires the wrapper to carry a stable <c>id</c> so the JS handler can target it.
///
/// bUnit cannot observe a JS-level <c>preventDefault</c> nor real focus, so this test
/// asserts the OBSERVABLE precondition the fix introduces: the wrapper now renders a
/// non-empty <c>id</c> attribute (it had none before), which is what the key-suppression
/// registration binds to. It also pins the existing behaviour (Enter/Space still
/// activate; AsChild still renders no wrapper).
/// </summary>
public class SheetSpacePreventDefaultTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SheetSpacePreventDefaultTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderTrigger(EventCallback<bool>? openChanged = null)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Sheet>(0);
            builder.AddAttribute(1, "IsOpen", false);
            if (openChanged.HasValue) builder.AddAttribute(2, "IsOpenChanged", openChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetTrigger>(0);
                // default AsChild=false -> the role=button wrapper path
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Open")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    private IRenderedComponent<IComponent> RenderClose(EventCallback<bool>? openChanged = null)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Sheet>(0);
            builder.AddAttribute(1, "IsOpen", true);
            if (openChanged.HasValue) builder.AddAttribute(2, "IsOpenChanged", openChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(content =>
                {
                    content.OpenComponent<L.SheetClose>(0);
                    content.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Close")));
                    content.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    // ---- the fix: the role=button wrapper carries a stable id (key-suppression target) ----

    [Fact]
    public void Trigger_RoleButton_Wrapper_Has_Stable_Id()
    {
        var cut = RenderTrigger();
        var wrapper = cut.Find("div[role='button']");
        var id = wrapper.GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(id),
            "SheetTrigger's role=button wrapper must expose an id so Space preventDefault can be registered against it.");
    }

    [Fact]
    public void Close_RoleButton_Wrapper_Has_Stable_Id()
    {
        var cut = RenderClose();
        var wrapper = cut.FindAll("div[role='button']")
            .First(d => d.TextContent.Trim() == "Close");
        var id = wrapper.GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(id),
            "SheetClose's role=button wrapper must expose an id so Space preventDefault can be registered against it.");
    }

    [Fact]
    public void Trigger_Consumer_Supplied_Id_Wins()
    {
        // The key-suppression must bind to the EFFECTIVE id (a splatted id wins,
        // since @attributes renders after the explicit id=).
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Sheet>(0);
            builder.AddAttribute(1, "IsOpen", false);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SheetTrigger>(0);
                b.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object>
                {
                    ["id"] = "my-sheet-trigger"
                });
                b.AddAttribute(2, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Open")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var wrapper = cut.Find("div[role='button']");
        Assert.Equal("my-sheet-trigger", wrapper.GetAttribute("id"));
    }

    // ---- existing behaviour preserved: activation still works on Enter and Space ----

    [Fact]
    public void Trigger_Space_Still_Opens_The_Sheet()
    {
        bool? opened = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => opened = v);
        var cut = RenderTrigger(openChanged: cb);

        cut.Find("div[role='button']").KeyDown(new KeyboardEventArgs { Key = " " });
        Assert.True(opened);
    }

    [Fact]
    public void Trigger_Enter_Still_Opens_The_Sheet()
    {
        bool? opened = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => opened = v);
        var cut = RenderTrigger(openChanged: cb);

        cut.Find("div[role='button']").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.True(opened);
    }
}
