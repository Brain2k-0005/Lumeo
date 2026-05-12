using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.QueryBuilder;

public class QueryBuilderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public QueryBuilderTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<QueryField> Fields() =>
    [
        new() { Name = "name", Label = "Name", Type = QueryFieldType.Text },
        new() { Name = "age", Label = "Age", Type = QueryFieldType.Number },
        new() { Name = "active", Label = "Active", Type = QueryFieldType.Boolean },
        new()
        {
            Name = "status", Label = "Status", Type = QueryFieldType.Select,
            Options = new[] { ("open", "Open"), ("closed", "Closed") }
        }
    ];

    [Fact]
    public void Renders_With_Empty_Query_RootGroup_And_Combinator_No_Rules()
    {
        var cut = _ctx.Render<Lumeo.QueryBuilder>(p => p.Add(q => q.Fields, Fields()));

        // root group present
        var group = cut.Find("[role='group']");
        Assert.NotNull(group);

        // AND combinator radio is checked, no rule rows
        var radios = cut.FindAll("[role='radiogroup'] [role='radio']");
        Assert.Equal(2, radios.Count);
        Assert.Equal("true", radios.First(r => r.TextContent.Trim() == "AND").GetAttribute("aria-checked"));

        // empty-state hint shown
        Assert.Contains("No conditions yet", cut.Markup);
    }

    [Fact]
    public void AddRule_Adds_A_Rule_Row()
    {
        QueryGroup? captured = null;
        var cut = _ctx.Render<Lumeo.QueryBuilder>(p => p
            .Add(q => q.Fields, Fields())
            .Add(q => q.QueryChanged, EventCallback.Factory.Create<QueryGroup>(this, g => captured = g)));

        cut.FindAll("button").First(b => b.TextContent.Contains("Rule")).Click();

        Assert.NotNull(captured);
        Assert.Single(captured!.Rules);
        Assert.IsType<QueryRule>(captured.Rules[0]);
        // a field <select> is now rendered for the rule
        Assert.Contains(cut.FindAll("select"), s => s.GetAttribute("aria-label") == "Field");
    }

    [Fact]
    public void Selecting_Field_Populates_Operator_Dropdown()
    {
        var cut = _ctx.Render<Lumeo.QueryBuilder>(p => p.Add(q => q.Fields, Fields()));
        cut.FindAll("button").First(b => b.TextContent.Contains("Rule")).Click();

        // default field is the first ("name", Text) — operator select should have the text default ops
        var operatorSelect = cut.FindAll("select").First(s => s.GetAttribute("aria-label") == "Operator");
        var optionTexts = operatorSelect.QuerySelectorAll("option").Select(o => o.TextContent).ToList();
        Assert.Contains("equals", optionTexts);
        Assert.Contains("contains", optionTexts);
        Assert.Contains("starts with", optionTexts);

        // change to the Number field — operators become the number set
        var fieldSelect = cut.FindAll("select").First(s => s.GetAttribute("aria-label") == "Field");
        fieldSelect.Change("age");

        operatorSelect = cut.FindAll("select").First(s => s.GetAttribute("aria-label") == "Operator");
        optionTexts = operatorSelect.QuerySelectorAll("option").Select(o => o.TextContent).ToList();
        Assert.Contains("less than", optionTexts);
        Assert.Contains("between", optionTexts);
        Assert.DoesNotContain("contains", optionTexts);
    }

    [Fact]
    public void Changing_Combinator_Updates_Query_Via_QueryChanged()
    {
        QueryGroup? captured = null;
        var cut = _ctx.Render<Lumeo.QueryBuilder>(p => p
            .Add(q => q.Fields, Fields())
            .Add(q => q.QueryChanged, EventCallback.Factory.Create<QueryGroup>(this, g => captured = g)));

        cut.FindAll("[role='radio']").First(r => r.TextContent.Trim() == "OR").Click();

        Assert.NotNull(captured);
        Assert.Equal(QueryCombinator.Or, captured!.Combinator);
    }

    [Fact]
    public void AddGroup_Nests_A_Group()
    {
        QueryGroup? captured = null;
        var cut = _ctx.Render<Lumeo.QueryBuilder>(p => p
            .Add(q => q.Fields, Fields())
            .Add(q => q.QueryChanged, EventCallback.Factory.Create<QueryGroup>(this, g => captured = g)));

        cut.FindAll("button").First(b => b.TextContent.Contains("Group")).Click();

        Assert.NotNull(captured);
        Assert.Single(captured!.Rules);
        Assert.IsType<QueryGroup>(captured.Rules[0]);
        // two group elements now: root + nested
        Assert.Equal(2, cut.FindAll("[role='group']").Count);
    }

    [Fact]
    public void RemoveRule_Removes_It()
    {
        QueryGroup? captured = null;
        var cut = _ctx.Render<Lumeo.QueryBuilder>(p => p
            .Add(q => q.Fields, Fields())
            .Add(q => q.QueryChanged, EventCallback.Factory.Create<QueryGroup>(this, g => captured = g)));

        cut.FindAll("button").First(b => b.TextContent.Contains("Rule")).Click();
        Assert.Single(captured!.Rules);

        cut.FindAll("button[aria-label='Remove rule']").First().Click();
        Assert.Empty(captured!.Rules);
    }

    [Fact]
    public void ShowJsonPreview_Renders_Json_And_ToJson_RoundTrips()
    {
        var query = new QueryGroup
        {
            Combinator = QueryCombinator.Or,
            Rules =
            {
                new QueryRule { Field = "name", Operator = "contains", Value = "ab" },
                new QueryGroup
                {
                    Combinator = QueryCombinator.And,
                    Rules = { new QueryRule { Field = "age", Operator = ">", Value = 18 } }
                }
            }
        };

        var cut = _ctx.Render<Lumeo.QueryBuilder>(p => p
            .Add(q => q.Fields, Fields())
            .Add(q => q.Query, query)
            .Add(q => q.ShowJsonPreview, true));

        Assert.Contains("\"combinator\"", cut.Markup);

        // ToJson/FromJson round-trip
        var json = Lumeo.QueryBuilder.ToJson(query);
        var parsed = Lumeo.QueryBuilder.FromJson(json);
        Assert.NotNull(parsed);
        Assert.Equal(QueryCombinator.Or, parsed!.Combinator);
        Assert.Equal(2, parsed.Rules.Count);
        Assert.IsType<QueryRule>(parsed.Rules[0]);
        Assert.IsType<QueryGroup>(parsed.Rules[1]);
        Assert.Equal("contains", ((QueryRule)parsed.Rules[0]).Operator);
    }

    [Fact]
    public void ToExpression_Compiles_And_Filters()
    {
        var query = new QueryGroup
        {
            Combinator = QueryCombinator.And,
            Rules =
            {
                new QueryRule { Field = nameof(Person.Age), Operator = ">=", Value = 18 },
                new QueryRule { Field = nameof(Person.Name), Operator = "contains", Value = "an" }
            }
        };

        var predicate = Lumeo.QueryBuilder.ToExpression<Person>(query);
        Assert.NotNull(predicate);
        var fn = predicate!.Compile();

        Assert.True(fn(new Person { Name = "Daniel", Age = 30 }));
        Assert.False(fn(new Person { Name = "Daniel", Age = 12 }));   // age fails
        Assert.False(fn(new Person { Name = "Bob", Age = 40 }));      // name fails
    }

    private sealed class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }
}
