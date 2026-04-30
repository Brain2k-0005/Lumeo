using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Kanban;

public class KanbanTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public KanbanTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.Kanban>();
        // Kanban root has overflow-x-auto class
        Assert.Contains("overflow-x-auto", cut.Markup);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.Kanban>(p => p.Add(c => c.Class, "board-cls"));
        Assert.Contains("board-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.Kanban>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "kanban-board" }));
        Assert.Contains("data-testid=\"kanban-board\"", cut.Markup);
    }

    [Fact]
    public void Renders_child_content()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Kanban>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "kanban-child");
                b.AddContent(2, "Column");
                b.CloseElement();
            }));
            builder.CloseComponent();
        });
        Assert.Contains("kanban-child", cut.Markup);
        Assert.Contains("Column", cut.Markup);
    }
}
