using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// Coverage for W2 (CSS half): SelectContent default max-height + scroll.
/// Verifies that the open listbox renders with max-h-96 and that the scrollable
/// region is a child wrapper — NOT the outer listbox div itself — so the search
/// input stays pinned outside the scroll region when Select.Searchable is true.
///
/// Structural contract:
///   outer [role=listbox]  → max-h-96 flex flex-col overflow-hidden
///   └─ (optional) search input wrapper  ← pinned, NOT scrollable
///   └─ inner scroll div  → overflow-y-auto min-h-0 flex-1  ← options scroll here
/// </summary>
public class SelectContentMaxHeightTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectContentMaxHeightTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderOpenSelect(bool searchable = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            if (searchable)
                builder.AddAttribute(2, "Searchable", true);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectItem>(0);
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
        var cut = RenderOpenSelect();

        var listbox = cut.Find("[role='listbox']");
        Assert.Contains("max-h-96", listbox.ClassList);
    }

    [Fact]
    public void Open_Listbox_Has_Flex_Column_Layout()
    {
        var cut = RenderOpenSelect();

        var listbox = cut.Find("[role='listbox']");
        Assert.Contains("flex", listbox.ClassList);
        Assert.Contains("flex-col", listbox.ClassList);
    }

    [Fact]
    public void Option_Region_Has_Overflow_Y_Auto_Scroll()
    {
        var cut = RenderOpenSelect();

        // The inner scroll wrapper must exist inside the listbox.
        var scrollDiv = cut.Find("[role='listbox'] .overflow-y-auto");
        Assert.NotNull(scrollDiv);
        Assert.Contains("min-h-0", scrollDiv.ClassList);
        Assert.Contains("flex-1", scrollDiv.ClassList);
    }

    [Fact]
    public void Searchable_Search_Input_Is_Outside_Scroll_Region()
    {
        var cut = RenderOpenSelect(searchable: true);

        // Find the search input and the scroll wrapper.
        var searchInput = cut.Find("input[type='text']");
        var scrollDiv = cut.Find("[role='listbox'] .overflow-y-auto");

        // The search input must NOT be a descendant of the scrollable div.
        Assert.Null(scrollDiv.QuerySelector("input[type='text']"));
        Assert.NotNull(searchInput); // it still renders
    }

    [Fact]
    public void Searchable_Search_Input_Is_Direct_Child_Of_Listbox()
    {
        var cut = RenderOpenSelect(searchable: true);

        var listbox = cut.Find("[role='listbox']");
        var searchWrapper = listbox.Children.FirstOrDefault(c =>
            c.QuerySelector("input[type='text']") is not null);

        // The wrapper containing the input must be a DIRECT child of the listbox,
        // not nested inside the overflow-y-auto scroll div.
        Assert.NotNull(searchWrapper);
        Assert.DoesNotContain("overflow-y-auto", searchWrapper.ClassList);
    }

    [Fact]
    public void Consumer_Class_Can_Override_Via_Cx_Merge()
    {
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
                b.AddAttribute(1, "Class", "custom-class");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var listbox = cut.Find("[role='listbox']");
        Assert.Contains("custom-class", listbox.ClassList);
        // Default max-height still applies (Cx.Merge adds consumer class alongside defaults).
        Assert.Contains("max-h-96", listbox.ClassList);
    }
}
