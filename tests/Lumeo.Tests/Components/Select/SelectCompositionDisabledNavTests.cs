using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// Composition-mode Select must exclude disabled options from keyboard navigation,
/// the same way data-bound mode already does. Previously disabled SelectItems
/// self-registered into the nav list, so ArrowDown/Up/Home/End could land on them
/// and Enter would select them (the click path guarded Disabled, the keyboard path
/// did not).
/// </summary>
public class SelectCompositionDisabledNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SelectCompositionDisabledNavTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<IComponent> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    // apple, banana (disabled), cherry
    private IRenderedComponent<IComponent> RenderWithDisabledMiddle(EventCallback<string?>? valueChanged = null)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            if (valueChanged.HasValue) builder.AddAttribute(2, "ValueChanged", valueChanged.Value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    void Item(int seq, string val, string label, bool disabled = false)
                    {
                        c.OpenComponent<L.SelectItem>(seq);
                        c.AddAttribute(seq + 1, "Value", val);
                        if (disabled) c.AddAttribute(seq + 2, "Disabled", true);
                        c.AddAttribute(seq + 3, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
                        c.CloseComponent();
                    }
                    Item(0, "apple", "Apple");
                    Item(4, "banana", "Banana", disabled: true);
                    Item(8, "cherry", "Cherry");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void ArrowDown_Skips_The_Disabled_Item()
    {
        var cut = RenderWithDisabledMiddle();
        var listbox = cut.Find("[role='listbox']");

        listbox.KeyDown("ArrowDown"); // -> Apple
        Assert.Contains("bg-accent", FindOption(cut, "Apple")!.ClassList);

        listbox.KeyDown("ArrowDown"); // skips disabled Banana -> Cherry
        Assert.Contains("bg-accent", FindOption(cut, "Cherry")!.ClassList);
        Assert.DoesNotContain("bg-accent", FindOption(cut, "Banana")!.ClassList);
    }

    [Fact]
    public void End_Lands_On_Last_Enabled_And_Enter_Never_Selects_Disabled()
    {
        // Reorder mentally: disabled is the LAST item to prove End clamps to enabled.
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ValueChanged", cb);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    void Item(int seq, string val, string label, bool disabled = false)
                    {
                        c.OpenComponent<L.SelectItem>(seq);
                        c.AddAttribute(seq + 1, "Value", val);
                        if (disabled) c.AddAttribute(seq + 2, "Disabled", true);
                        c.AddAttribute(seq + 3, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
                        c.CloseComponent();
                    }
                    Item(0, "apple", "Apple");
                    Item(4, "cherry", "Cherry", disabled: true); // last item disabled
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("End"); // clamps to Apple (Cherry disabled)
        Assert.Contains("bg-accent", FindOption(cut, "Apple")!.ClassList);

        try { cut.Find("[role='listbox']").KeyDown("Enter"); } catch (ArgumentException) { }
        Assert.Equal("apple", selected); // never the disabled "cherry"
    }

    [Fact]
    public void Typeahead_Skips_The_Disabled_Item()
    {
        var cut = RenderWithDisabledMiddle();

        cut.Find("[role='listbox']").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "b" });

        // "Banana" is the only "b" item but it's disabled -> not in the nav set, so
        // typeahead finds no match and doesn't highlight it.
        Assert.DoesNotContain("bg-accent", FindOption(cut, "Banana")!.ClassList);
    }

    [Fact]
    public void Disabling_An_Item_While_The_Menu_Is_Open_Removes_It_From_Keyboard_Nav()
    {
        // Regression for the RegisterItem-upsert fix: all three start enabled; disabling
        // the middle one AFTER the menu is open must refresh the keyboard-nav registry.
        // The old add-if-absent RegisterItem froze the first-seen disabled=false state, so
        // ArrowDown could still land on (and Enter select) an item that is now disabled.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    void Item(int seq, string val, string label)
                    {
                        c.OpenComponent<L.SelectItem>(seq);
                        c.AddAttribute(seq + 1, "Value", val);
                        c.AddAttribute(seq + 2, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
                        c.CloseComponent();
                    }
                    Item(0, "apple", "Apple");
                    Item(4, "banana", "Banana");
                    Item(8, "cherry", "Cherry");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Disable Banana while the menu is open (the upsert/re-register path).
        var banana = cut.FindComponents<L.SelectItem>().First(c => c.Instance.Value == "banana");
        banana.Render(p => p.Add(x => x.Disabled, true));

        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("ArrowDown"); // -> Apple
        Assert.Contains("bg-accent", FindOption(cut, "Apple")!.ClassList);

        listbox.KeyDown("ArrowDown"); // skips the now-disabled Banana -> Cherry
        Assert.Contains("bg-accent", FindOption(cut, "Cherry")!.ClassList);
        Assert.DoesNotContain("bg-accent", FindOption(cut, "Banana")!.ClassList);
    }
}
