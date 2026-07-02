using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Combobox;

/// <summary>
/// Coverage for W2 (CSS half): ComboboxContent default max-height + scroll.
/// ComboboxContent has no search input inside its listbox div (the search lives
/// in the ComboboxInput trigger), so the fix is a simple class swap:
/// overflow-hidden → max-h-96 overflow-y-auto on the listbox root.
/// </summary>
public class ComboboxContentMaxHeightTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ComboboxContentMaxHeightTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderOpenCombobox()
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.AddAttribute(1, "Placeholder", "Search...");
                b.CloseComponent();

                b.OpenComponent<L.ComboboxContent>(2);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.ComboboxItem>(0);
                    c.AddAttribute(1, "Value", "apple");
                    c.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Open_Listbox_Has_Max_Height_Class()
    {
        var cut = RenderOpenCombobox();

        var listbox = cut.Find("[role='listbox']");
        Assert.Contains("max-h-96", listbox.ClassList);
    }

    [Fact]
    public void Open_Listbox_Has_Overflow_Y_Auto()
    {
        var cut = RenderOpenCombobox();

        var listbox = cut.Find("[role='listbox']");
        Assert.Contains("overflow-y-auto", listbox.ClassList);
    }

    [Fact]
    public void Open_Listbox_Does_Not_Have_Overflow_Hidden()
    {
        var cut = RenderOpenCombobox();

        var listbox = cut.Find("[role='listbox']");
        Assert.DoesNotContain("overflow-hidden", listbox.ClassList);
    }

    [Fact]
    public void Consumer_Class_Can_Override_Via_Cx_Merge()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.AddAttribute(1, "Placeholder", "Search...");
                b.CloseComponent();

                b.OpenComponent<L.ComboboxContent>(2);
                b.AddAttribute(1, "Class", "custom-class");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var listbox = cut.Find("[role='listbox']");
        Assert.Contains("custom-class", listbox.ClassList);
        Assert.Contains("max-h-96", listbox.ClassList);
    }
}
