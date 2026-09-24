using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// DocFlow O3: "Select: a value without matching item shows an empty trigger silently;
/// with items built in a loop the closed trigger does not show the selected item's label."
/// Repro attempt against current master. Evidence this does NOT reproduce: Select already
/// resolves the closed trigger's label through <c>ResolveTagLabel</c>/<c>_labelCache</c>,
/// populated as each composition-mode SelectItem (loop-built or not) self-registers —
/// including a one-time re-render nudge for the case where the trigger renders (closed)
/// before its children register (Select.razor's RegisterItem, "5.7.0"/"Codex P2" comments).
/// An unmatched value falls back to showing the raw value string rather than nothing.
/// </summary>
public class SelectLoopBuiltTriggerLabelTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectLoopBuiltTriggerLabelTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Option(string Id, string Name);

    private static readonly List<Option> Options = new()
    {
        new("1", "Alpha"),
        new("2", "Bravo"),
        new("3", "Charlie"),
    };

    private IRenderedComponent<IComponent> RenderClosedSelect(string value)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", false);
            builder.AddAttribute(2, "Value", value);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    var seq = 0;
                    // Loop-built items — the exact pattern DocFlow's report describes.
                    foreach (var opt in Options)
                    {
                        c.OpenComponent<L.SelectItem>(seq++);
                        c.AddAttribute(seq++, "Value", opt.Id);
                        c.AddAttribute(seq++, "ChildContent", (RenderFragment)(ic => ic.AddContent(0, opt.Name)));
                        c.CloseComponent();
                    }
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Closed_Trigger_Shows_The_Loop_Built_Items_Resolved_Label()
    {
        var cut = RenderClosedSelect(value: "2");

        var triggerText = cut.Find("[data-slot='select-trigger']").TextContent;
        Assert.Contains("Bravo", triggerText);
        Assert.DoesNotContain("2", triggerText.Replace("Bravo", ""));
    }

    [Fact]
    public void Unknown_Value_Shows_The_Raw_Value_Not_An_Empty_Trigger()
    {
        var cut = RenderClosedSelect(value: "no-such-id");

        var triggerText = cut.Find("[data-slot='select-trigger']").TextContent;
        Assert.Contains("no-such-id", triggerText);
    }
}
