using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// DocFlow follow-up O3 repro, reproduced with the CONSUMER's exact shape (not the earlier,
/// more generic loop-built-items repro in <see cref="SelectLoopBuiltTriggerLabelTests"/>):
/// <c>&lt;Select @bind-Value="_process" Label="Prozess"&gt;&lt;SelectTrigger Class="w-36" /&gt;
/// &lt;SelectContent&gt;@foreach (var p in Processes) { &lt;SelectItem Value="@p.ToString()"&gt;
/// @Label(p)&lt;/SelectItem&gt; }&lt;/SelectContent&gt;&lt;/Select&gt;</c> — a <c>Label</c> set on
/// <c>Select</c> itself (renders its own top &lt;label&gt;, unrelated to the trigger content),
/// a completely empty self-closing <c>SelectTrigger</c> (only <c>Class</c>, no
/// <c>ChildContent</c>/<c>Placeholder</c>), items whose <c>ChildContent</c> is a method-call
/// expression rather than a literal, and values produced by <c>enum.ToString()</c>.
///
/// Does NOT reproduce: the closed trigger already shows the resolved label in every
/// combination below. Same mechanism as <see cref="SelectSingleValueLabelTests"/> /
/// <see cref="SelectLoopBuiltTriggerLabelTests"/> — SelectContent's closed "registration-only"
/// pass (Context.Items is null && ChildContent is not null) renders every composed SelectItem
/// hidden so it self-registers into Select's persistent _labelCache (Select.razor
/// RegisterItem/ResolveTagLabel), and RegisterItem's one-shot InvokeAsync(StateHasChanged) nudge
/// covers the single render where the Trigger's own render pass ran before that registration.
/// </summary>
public class SelectEnumLabelEmptyTriggerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectEnumLabelEmptyTriggerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private enum ProcessType { Incoming, Outgoing, Return }

    private static readonly ProcessType[] Processes = { ProcessType.Incoming, ProcessType.Outgoing, ProcessType.Return };

    private static string Label(ProcessType p) => p switch
    {
        ProcessType.Incoming => "Eingehend",
        ProcessType.Outgoing => "Ausgehend",
        ProcessType.Return => "Retoure",
        _ => p.ToString(),
    };

    // Mirrors the consumer's markup 1:1: Label on Select, an empty self-closing
    // SelectTrigger with only Class set, SelectItems built in a @foreach with an
    // expression (method-call) ChildContent, values from enum.ToString().
    private IRenderedComponent<IComponent> RenderConsumerShape(string? value)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Value", value);
            builder.AddAttribute(2, "Label", "Prozess");
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "Class", "w-36");
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    var seq = 0;
                    foreach (var p in Processes)
                    {
                        c.OpenComponent<L.SelectItem>(seq++);
                        c.AddAttribute(seq++, "Value", p.ToString());
                        c.AddAttribute(seq++, "ChildContent", (RenderFragment)(ic => ic.AddContent(0, Label(p))));
                        c.CloseComponent();
                    }
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    private static string TriggerText<T>(IRenderedComponent<T> cut) where T : IComponent
        => cut.Find("[data-slot='select-trigger']").TextContent.Trim();

    [Fact]
    public void Closed_Trigger_Shows_The_Resolved_Label_For_An_Enum_Value_Set_Before_First_Render()
    {
        var cut = RenderConsumerShape(ProcessType.Outgoing.ToString());

        Assert.Contains("Ausgehend", TriggerText(cut));
        // The Select-level Label renders its own <label>, separate from the trigger text.
        Assert.Contains("Prozess", cut.Markup);
        Assert.Empty(cut.FindAll("[role='option']"));
    }

    [Fact]
    public void Closed_Trigger_Resolves_The_Label_When_The_Value_Is_Set_After_First_Render()
    {
        var child = (RenderFragment)(b =>
        {
            b.OpenComponent<L.SelectTrigger>(0);
            b.AddAttribute(1, "Class", "w-36");
            b.CloseComponent();

            b.OpenComponent<L.SelectContent>(2);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
            {
                var seq = 0;
                foreach (var p in Processes)
                {
                    c.OpenComponent<L.SelectItem>(seq++);
                    c.AddAttribute(seq++, "Value", p.ToString());
                    c.AddAttribute(seq++, "ChildContent", (RenderFragment)(ic => ic.AddContent(0, Label(p))));
                    c.CloseComponent();
                }
            }));
            b.CloseComponent();
        });

        var cut = _ctx.Render<L.Select>(p =>
        {
            p.Add(s => s.Value, (string?)null);
            p.Add(s => s.Label, "Prozess");
            p.Add(s => s.ChildContent, child);
        });
        Assert.DoesNotContain("Ausgehend", TriggerText(cut));

        // The value is set AFTER the trigger already rendered closed — every item's label
        // is already in Select's persistent _labelCache from the registration-only pass on
        // first render, so this needs no extra self-heal: the ordinary parameter-driven
        // re-render resolves it immediately.
        cut.Render(p => p.Add(s => s.Value, ProcessType.Outgoing.ToString()));

        Assert.Contains("Ausgehend", TriggerText(cut));
    }

    [Fact]
    public void Closed_Trigger_Resolves_Every_Enum_Value_Without_Ever_Opening()
    {
        foreach (var p in Processes)
        {
            var cut = RenderConsumerShape(p.ToString());
            Assert.Contains(Label(p), TriggerText(cut));
        }
    }
}
