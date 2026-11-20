using Bunit;
using Lumeo.Services;
using Lumeo.Services.Localization;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sortable;

/// <summary>
/// #551: SortableLists that share a <c>Group</c> exchange rows (drag + keyboard), the source
/// fires ItemsChanged without the row, the target with it at the drop index, OnTransfer
/// describes the move, different/empty groups reject, and an empty target accepts a drop.
/// </summary>
public class SortableListCrossListTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _tracking = new();

    private List<string> _a = new() { "a1", "a2", "a3" };
    private List<string> _b = new() { "b1", "b2" };
    private readonly List<string> _log = new();
    private L.SortableList<string>.SortableTransfer? _transferOnA;
    private L.SortableList<string>.SortableTransfer? _transferOnB;
    private List<string>? _changedA;
    private List<string>? _changedB;

    public SortableListCrossListTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo/js/components.js");
        module.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IComponentInteropService>(_tracking);
        _ctx.Services.AddSingleton<IOptions<LumeoLocalizationOptions>>(_ =>
        {
            var options = new LumeoLocalizationOptions();
            LumeoDefaultStrings.ApplyDefaults(options);
            return Options.Create(options);
        });
        _ctx.Services.AddScoped<ILumeoLocalizer, LumeoLocalizer>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment<string> TextTemplate => item => b => b.AddContent(0, item);

    private IRenderedComponent<IComponent> RenderPair(string? groupA, string? groupB, List<string>? a = null, List<string>? b = null, bool controlled = true)
    {
        _a = a ?? _a;
        _b = b ?? _b;
        return _ctx.Render((RenderFragment)(builder =>
        {
            void List(int seq, string? group, Func<List<string>> get, Action<List<string>> set, bool isA)
            {
                builder.OpenComponent<L.SortableList<string>>(seq);
                builder.AddAttribute(seq + 1, nameof(L.SortableList<string>.Items), get());
                builder.AddAttribute(seq + 2, nameof(L.SortableList<string>.Group), group);
                builder.AddAttribute(seq + 3, nameof(L.SortableList<string>.ItemTemplate), TextTemplate);
                builder.AddAttribute(seq + 4, nameof(L.SortableList<string>.ItemsChanged),
                    EventCallback.Factory.Create<List<string>>(this, v => { set(v); _log.Add(isA ? "A" : "B"); }));
                builder.AddAttribute(seq + 5, nameof(L.SortableList<string>.OnTransfer),
                    EventCallback.Factory.Create<L.SortableList<string>.SortableTransfer>(this, t => { if (isA) _transferOnA = t; else _transferOnB = t; }));
                builder.CloseComponent();
            }
            List(0, groupA, () => _a, v => { _a = v; _changedA = v; }, true);
            List(10, groupB, () => _b, v => { _b = v; _changedB = v; }, false);
        }));
    }

    private static List<string> Rows(IRenderedComponent<IComponent> cut, int list)
        => cut.FindAll("[data-slot='sortable-list']")[list]
            .QuerySelectorAll("[data-sortable-item]")
            .Select(e => e.TextContent.Trim()).ToList();

    [Fact]
    public async Task Drop_On_Row_Of_Sibling_List_Moves_Item_And_Fires_Both_ItemsChanged_In_Order()
    {
        var cut = RenderPair("g", "g");
        var a1 = cut.FindAll("[data-slot='sortable-list']")[0].QuerySelectorAll("[data-sortable-item]")[0];
        await cut.InvokeAsync(() => a1.DragStart());
        var b2 = cut.FindAll("[data-slot='sortable-list']")[1].QuerySelectorAll("[data-sortable-item]")[1];
        await cut.InvokeAsync(() => b2.Drop());

        Assert.Equal(new List<string> { "a2", "a3" }, _changedA);
        Assert.Equal(new List<string> { "b1", "a1", "b2" }, _changedB);
        Assert.Equal(new[] { "A", "B" }, _log); // source first, then target
        Assert.Equal(new List<string> { "a2", "a3" }, Rows(cut, 0));
        Assert.Equal(new List<string> { "b1", "a1", "b2" }, Rows(cut, 1));
    }

    [Fact]
    public async Task OnTransfer_Fires_On_Source_And_Target_With_Same_Payload()
    {
        var cut = RenderPair("g", "g");
        var lists = cut.FindAll("[data-slot='sortable-list']");
        var srcId = lists[0].Id!;
        var dstId = lists[1].Id!;
        await cut.InvokeAsync(() => lists[0].QuerySelectorAll("[data-sortable-item]")[1].DragStart());
        await cut.InvokeAsync(() => lists[1].QuerySelectorAll("[data-sortable-item]")[0].Drop());

        Assert.NotNull(_transferOnA);
        Assert.Equal(_transferOnA, _transferOnB);
        Assert.Equal(new L.SortableList<string>.SortableTransfer("a2", "g", srcId, 1, dstId, 0), _transferOnB);
    }

    [Fact]
    public async Task Drop_On_Empty_Target_List_Container_Appends()
    {
        var cut = RenderPair("g", "g", b: new List<string>());
        var lists = cut.FindAll("[data-slot='sortable-list']");
        Assert.Empty(Rows(cut, 1));
        await cut.InvokeAsync(() => lists[0].QuerySelectorAll("[data-sortable-item]")[2].DragStart());
        // Empty list: the only drop surface is the container / its drop zone.
        await cut.InvokeAsync(() => cut.FindAll("[data-slot='sortable-list']")[1].Drop());

        Assert.Equal(new List<string> { "a3" }, Rows(cut, 1));
        Assert.Equal(new List<string> { "a1", "a2" }, Rows(cut, 0));
        Assert.Contains("min-h-16", cut.FindAll("[data-slot='sortable-list']")[1].ClassName);
    }

    [Fact]
    public async Task Different_Groups_Reject_The_Drop()
    {
        var cut = RenderPair("one", "two");
        var lists = cut.FindAll("[data-slot='sortable-list']");
        await cut.InvokeAsync(() => lists[0].QuerySelectorAll("[data-sortable-item]")[0].DragStart());
        await cut.InvokeAsync(() => lists[1].QuerySelectorAll("[data-sortable-item]")[0].Drop());

        Assert.Empty(_log);
        Assert.Equal(new List<string> { "a1", "a2", "a3" }, Rows(cut, 0));
        Assert.Equal(new List<string> { "b1", "b2" }, Rows(cut, 1));
    }

    [Fact]
    public async Task Null_Group_Rejects_The_Drop()
    {
        var cut = RenderPair(null, null);
        var lists = cut.FindAll("[data-slot='sortable-list']");
        await cut.InvokeAsync(() => lists[0].QuerySelectorAll("[data-sortable-item]")[0].DragStart());
        await cut.InvokeAsync(() => lists[1].QuerySelectorAll("[data-sortable-item]")[0].Drop());

        Assert.Empty(_log);
        Assert.Equal(2, Rows(cut, 1).Count);
    }

    [Fact]
    public async Task Ungrouped_List_Has_No_Drop_Zone_Or_Min_Height()
    {
        var cut = RenderPair(null, null);
        Assert.Empty(cut.FindAll("[data-slot='sortable-drop-zone']"));
        Assert.DoesNotContain("min-h-16", cut.FindAll("[data-slot='sortable-list']")[0].ClassName);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ArrowRight_On_Handle_Sends_Item_To_Next_List_And_ArrowLeft_Back()
    {
        var cut = RenderPair("g", "g");
        var handle = cut.FindAll("[data-slot='sortable-list']")[0].QuerySelectorAll("[role='button']")[1];
        await cut.InvokeAsync(() => ((AngleSharp.Dom.IElement)handle).KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }));

        Assert.Equal(new List<string> { "a1", "a3" }, Rows(cut, 0));
        Assert.Equal(new List<string> { "b1", "a2", "b2" }, Rows(cut, 1));

        var back = cut.FindAll("[data-slot='sortable-list']")[1].QuerySelectorAll("[role='button']")[1];
        await cut.InvokeAsync(() => ((AngleSharp.Dom.IElement)back).KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" }));
        Assert.Equal(new List<string> { "a1", "a2", "a3" }, Rows(cut, 0));
        Assert.Equal(new List<string> { "b1", "b2" }, Rows(cut, 1));
    }

    [Fact]
    public async Task ArrowLeft_On_First_List_Is_A_NoOp()
    {
        var cut = RenderPair("g", "g");
        var handle = cut.FindAll("[data-slot='sortable-list']")[0].QuerySelectorAll("[role='button']")[0];
        await cut.InvokeAsync(() => ((AngleSharp.Dom.IElement)handle).KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" }));
        Assert.Empty(_log);
    }

    [Fact]
    public async Task Touch_Transfer_Reported_By_Js_Moves_The_Item()
    {
        var cut = RenderPair("g", "g");
        var lists = cut.FindAll("[data-slot='sortable-list']");
        var registry = SortableGroupRegistry.For(_tracking);
        await registry.TouchTransferAsync(lists[0].Id!, 0, lists[1].Id!, 2);

        Assert.Equal(new List<string> { "a2", "a3" }, Rows(cut, 0));
        Assert.Equal(new List<string> { "b1", "b2", "a1" }, Rows(cut, 1));
    }

    [Fact]
    public async Task Disposed_List_Is_Unregistered_From_The_Group()
    {
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "x" })
            .Add(l => l.Group, "g"));
        var registry = SortableGroupRegistry.For(_tracking);
        var id = cut.Find("[data-slot='sortable-list']").Id!;
        Assert.NotNull(registry.Find(id));

        await cut.Instance.DisposeAsync();
        Assert.Null(registry.Find(id));
    }
}
