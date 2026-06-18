using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Combobox;

/// <summary>
/// Regression tests for issue #198 (c): composition-mode ComboboxItem gained a
/// Disabled parameter that suppresses selection, announces aria-disabled and renders
/// the disabled styling — mirroring SelectItem.Disabled.
/// </summary>
public class ComboboxItemDisabledTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ComboboxItemDisabledTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderComposition(EventCallback<string>? valueChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            if (valueChanged.HasValue)
                builder.AddAttribute(2, "ValueChanged", valueChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();

                b.OpenComponent<L.ComboboxContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    AddItem(c, 0, "apple", "Apple", disabled: false);
                    AddItem(c, 10, "banana", "Banana", disabled: true);
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        static void AddItem(RenderTreeBuilder rb, int seq, string value, string label, bool disabled)
        {
            rb.OpenComponent<L.ComboboxItem>(seq);
            rb.AddAttribute(seq + 1, "Value", value);
            rb.AddAttribute(seq + 2, "Disabled", disabled);
            rb.AddAttribute(seq + 3, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
            rb.CloseComponent();
        }
    }

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<IComponent> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    [Fact]
    public void Disabled_Item_Has_AriaDisabled_And_Disabled_Styling()
    {
        var cut = RenderComposition();

        var banana = FindOption(cut, "Banana")!;
        Assert.Equal("true", banana.GetAttribute("aria-disabled"));
        Assert.Contains("cursor-not-allowed", banana.ClassList);
        Assert.Contains("opacity-50", banana.ClassList);

        var apple = FindOption(cut, "Apple")!;
        Assert.Equal("false", apple.GetAttribute("aria-disabled"));
        Assert.DoesNotContain("cursor-not-allowed", apple.ClassList);
    }

    [Fact]
    public void Clicking_Disabled_Item_Does_Not_Select()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string>(_ctx, (string v) => selected = v);
        var cut = RenderComposition(valueChanged: cb);

        var banana = FindOption(cut, "Banana")!;
        try { banana.Click(); } catch (ArgumentException) { }

        Assert.Null(selected);
    }

    [Fact]
    public void Clicking_Enabled_Item_Still_Selects()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string>(_ctx, (string v) => selected = v);
        var cut = RenderComposition(valueChanged: cb);

        var apple = FindOption(cut, "Apple")!;
        try { apple.Click(); } catch (ArgumentException) { }

        Assert.Equal("apple", selected);
    }
}
