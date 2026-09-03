using Xunit;

namespace Lumeo.Tests.Components.Filters;

/// <summary>The query tree, the operator catalogue, the schema index and the date parser: the
/// half of Filters that has no DOM.</summary>
public class FilterModelTests
{
    private static FilterRule R(string id, string field, string op = "is", object? value = null) => new(id, new[] { field }, op, value);

    // ---------------------------------------------------------------- queries

    [Fact]
    public void Insert_Remove_And_Duplicate_Keep_The_Rest_Untouched()
    {
        var q = FilterQuery.Empty();
        q = FilterQueries.Insert(q, R("a", "name"));
        q = FilterQueries.Insert(q, R("b", "city"));
        Assert.Equal(new[] { "a", "b" }, q.Rules.Select(n => n.Id));

        var n = 0;
        q = FilterQueries.Duplicate(q, "a", () => "c" + ++n);
        Assert.Equal(new[] { "a", "c1", "b" }, q.Rules.Select(x => x.Id));

        var removed = FilterQueries.Remove(q, "c1");
        Assert.Equal(new[] { "a", "b" }, removed.Rules.Select(x => x.Id));
        Assert.Same(removed, FilterQueries.Remove(removed, "missing"));
    }

    [Fact]
    public void Removing_The_Last_Rule_Of_A_Group_Removes_The_Group()
    {
        var q = FilterQuery.Of(R("a", "name"), new FilterGroup("g", FilterCombinator.Or, new[] { R("b", "city") }));
        var next = FilterQueries.Remove(q, "b");
        Assert.Single(next.Rules);
        Assert.Equal("a", next.Rules[0].Id);
    }

    [Fact]
    public void Update_Returns_The_Same_Instance_When_Nothing_Changed()
    {
        var q = FilterQuery.Of(R("a", "name"));
        Assert.Same(q, FilterQueries.UpdateRule(q, "zzz", r => r with { Operator = "contains" }));
        var next = FilterQueries.UpdateRule(q, "a", r => r with { Operator = "contains" });
        Assert.NotSame(q, next);
        Assert.Equal("contains", FilterQueries.FindRule(next, "a")!.Operator);
    }

    [Fact]
    public void Move_MoveTo_Wrap_And_Unwrap()
    {
        var q = FilterQuery.Of(R("a", "name"), R("b", "city"), R("c", "age"));
        Assert.Equal(new[] { "b", "a", "c" }, FilterQueries.Move(q, "a", 1).Rules.Select(x => x.Id));
        Assert.Same(q, FilterQueries.Move(q, "a", -1));

        var wrapped = FilterQueries.WrapInGroup(q, "b", "g");
        var group = Assert.IsType<FilterGroup>(wrapped.Rules[1]);
        Assert.Equal("g", group.Id);
        Assert.Equal(FilterCombinator.Or, group.Combinator);

        var moved = FilterQueries.MoveTo(wrapped, "c", "g", 1);
        Assert.Equal(new[] { "b", "c" }, ((FilterGroup)moved.Rules[1]).Rules.Select(x => x.Id));
        Assert.Same(moved, FilterQueries.MoveTo(moved, "g", "g", 0));

        var unwrapped = FilterQueries.Unwrap(moved, "g");
        Assert.Equal(new[] { "a", "b", "c" }, unwrapped.Rules.Select(x => x.Id));
    }

    [Fact]
    public void Flatten_Skips_Incomplete_Rules_And_Shapes_Values()
    {
        var q = FilterQuery.Of(
            R("a", "name", "contains", "jo"),
            R("b", "city", ""),
            R("c", "tags", "has_any_of", new List<string> { "x", "y" }),
            R("d", "age", "between", new FilterRange(1.0, 9.0)));
        var terms = q.Flatten();
        Assert.Equal(3, terms.Count);
        Assert.Equal(new object?[] { "jo" }, terms[0].Values);
        Assert.Equal(new object?[] { "x", "y" }, terms[1].Values);
        Assert.Equal(new object?[] { 1.0, 9.0 }, terms[2].Values);
        Assert.Equal(4, q.Count);
    }

    [Fact]
    public void Issues_Cover_Operator_Value_Range_And_Empty_Group()
    {
        var q = FilterQuery.Of(
            R("a", "name", ""),
            R("b", "name", "contains", ""),
            R("c", "age", "between", new FilterRange(9.0, 1.0)),
            R("d", "age", "between", new FilterRange(1.0, null)),
            R("e", "name", "empty"),
            new FilterGroup("g", FilterCombinator.And, Array.Empty<FilterNode>()));
        var issues = FilterQueries.CollectIssues(q, r => r.Operator switch { "between" => FilterArity.Range, "empty" => FilterArity.None, _ => FilterArity.One });
        Assert.Equal(new[]
        {
            ("a", FilterIssueReason.MissingOperator), ("b", FilterIssueReason.MissingValue),
            ("c", FilterIssueReason.ReversedRange), ("d", FilterIssueReason.IncompleteRange), ("g", FilterIssueReason.EmptyGroup),
        }, issues.Select(i => (i.NodeId, i.Reason)));
    }

    [Fact]
    public void Prune_Drops_Empty_Groups_And_Dissolves_A_Group_Of_One_Group()
    {
        var inner = new FilterGroup("inner", FilterCombinator.Or, new[] { R("a", "name") });
        var q = FilterQuery.Of(new FilterGroup("outer", FilterCombinator.And, new FilterNode[] { inner }), new FilterGroup("empty", FilterCombinator.And, Array.Empty<FilterNode>()));
        var pruned = FilterQueries.Prune(q);
        Assert.Single(pruned.Rules);
        Assert.Equal("inner", pruned.Rules[0].Id);
    }

    // ---------------------------------------------------------------- operators

    [Fact]
    public void Catalogue_Follows_The_Field_Kind_And_Labels()
    {
        var catalogue = FilterOperators.Build(new FilterLabels().OperatorLabels);
        var text = FilterOperators.Resolve(new FilterField { Id = "t", Label = "T" }, catalogue);
        Assert.Equal("contains", text[0].Value);
        Assert.Equal("contains", text[0].Label);
        Assert.Equal(FilterArity.None, FilterOperators.Get(text, "empty")!.Arity);
        var select = FilterOperators.Resolve(new FilterField { Id = "s", Label = "S", Kind = FilterValueKind.Select }, catalogue);
        Assert.Equal(FilterArity.Many, FilterOperators.Get(select, "is_any_of")!.Arity);
        var own = new FilterField { Id = "o", Label = "O", Operators = new[] { new FilterOperatorDef("x", "X") } };
        Assert.Equal("x", FilterOperators.Resolve(own, catalogue)[0].Value);
    }

    [Fact]
    public void Default_Operator_Prefers_The_Named_One_Then_The_First_Visible()
    {
        var catalogue = FilterOperators.Build(new FilterLabels().OperatorLabels);
        var field = new FilterField { Id = "t", Label = "T", DefaultOperator = "is" };
        Assert.Equal("is", FilterOperators.Default(field, FilterOperators.Resolve(field, catalogue)));
        var ops = new[] { new FilterOperatorDef("h", "H", Hidden: true), new FilterOperatorDef("v", "V") };
        Assert.Equal("v", FilterOperators.Default(new FilterField { Id = "x", Label = "X" }, ops));
    }

    [Fact]
    public void Negate_Swaps_To_The_Inverse_Or_Flips_The_Flag()
    {
        var catalogue = FilterOperators.Build(new FilterLabels().OperatorLabels);
        var ops = catalogue[FilterValueKind.Text];
        Assert.Equal(("is_not", false), FilterOperators.Negate(FilterOperators.Get(ops, "is"), ops, false));
        Assert.Equal(("starts_with", true), FilterOperators.Negate(FilterOperators.Get(ops, "starts_with"), ops, false));
    }

    [Fact]
    public void Coerce_Reshapes_A_Value_For_The_New_Arity()
    {
        Assert.Null(FilterValues.Coerce("x", FilterArity.One, FilterArity.None));
        Assert.Equal(new[] { "x" }, Assert.IsAssignableFrom<IReadOnlyList<string>>(FilterValues.Coerce("x", FilterArity.One, FilterArity.Many)));
        Assert.Equal("a", FilterValues.Coerce(new List<string> { "a", "b" }, FilterArity.Many, FilterArity.One));
        var range = Assert.IsType<FilterRange>(FilterValues.Coerce(new List<string> { "1", "2" }, FilterArity.Many, FilterArity.Range));
        Assert.Equal(("1", "2"), (range.From, range.To));
    }

    // ---------------------------------------------------------------- index

    [Fact]
    public void Index_Resolves_Nested_Paths_And_Searches_Deep()
    {
        var fields = new[]
        {
            new FilterField { Id = "name", Label = "Name", Fields = new[] { new FilterField { Id = "first", Label = "First name" }, new FilterField { Id = "last", Label = "Last name", Keywords = new[] { "surname" } } } },
            new FilterField { Id = "name", Label = "Duplicate, ignored" },
            new FilterField { Id = "status", Label = "Status", Kind = FilterValueKind.Select },
        };
        var index = new FilterIndex(fields);
        Assert.Equal(2, index.Roots.Count);
        Assert.Equal("Last name", index.Get(new[] { "name", "last" })!.Label);
        Assert.Equal("Name > Last name", index.FormatPath(new[] { "name", "last" }, " > "));
        Assert.Equal(new[] { "first", "last" }, index.Children(new[] { "name" }).Select(f => f.Id));
        Assert.Equal(new[] { "last" }, index.SearchDeep("surname").Select(e => e.Field.Id));
        Assert.DoesNotContain(index.SearchDeep("name"), e => e.Field.IsBranch);
    }

    [Fact]
    public void Exclusive_Option_Clears_The_Others_And_Is_Cleared_By_Them()
    {
        static bool IsNone(string v) => v == "none";
        Assert.Equal(new[] { "none" }, FilterIndex.ApplyExclusive(new[] { "a", "b", "none" }, new[] { "a", "b" }, IsNone));
        Assert.Equal(new[] { "a" }, FilterIndex.ApplyExclusive(new[] { "none", "a" }, new[] { "none" }, IsNone));
        var same = new[] { "a", "b" };
        Assert.Same(same, FilterIndex.ApplyExclusive(same, new[] { "a", "b" }, IsNone));
    }

    // ---------------------------------------------------------------- dates

    [Fact]
    public void Dates_Parse_Relative_Phrases_And_Explicit_Days()
    {
        var today = new DateOnly(2026, 9, 3); // a Thursday
        Assert.Equal(FilterRelativeDate.Today, FilterDates.Parse("today")!.Relative);
        Assert.Equal(new FilterRelativeDate(FilterDateUnit.Week, 1), FilterDates.Parse("next week")!.Relative);
        Assert.Equal(new FilterRelativeDate(FilterDateUnit.Day, -3), FilterDates.Parse("3 days ago")!.Relative);
        Assert.Equal(new DateOnly(2026, 9, 7), FilterDates.Parse("monday", today, System.Globalization.CultureInfo.InvariantCulture)!.Resolve(today));
        Assert.Equal(new DateOnly(2026, 8, 31), FilterDates.Parse("last monday", today, System.Globalization.CultureInfo.InvariantCulture)!.Resolve(today));
        Assert.Equal(new DateOnly(2024, 5, 1), FilterDates.Parse("2024-05-01")!.Date);
        Assert.Null(FilterDates.Parse("no such thing"));
    }

    [Fact]
    public void Dates_Format_Relative_Values_Through_The_Labels()
    {
        var labels = new FilterLabels();
        Assert.Equal("today", FilterDates.Format(new FilterDateValue(Relative: FilterRelativeDate.Today), labels));
        Assert.Equal("in 2 weeks", FilterDates.Format(new FilterDateValue(Relative: new FilterRelativeDate(FilterDateUnit.Week, 2)), labels));
        Assert.Equal("3 days ago", FilterDates.Format(new FilterDateValue(Relative: new FilterRelativeDate(FilterDateUnit.Day, -3)), labels));
        Assert.Equal(new DateOnly(2024, 5, 1).ToString("d"), FilterDates.Format(FilterDateValue.Of(new DateOnly(2024, 5, 1)), labels));
    }

    [Fact]
    public void Labels_Come_From_The_Locale_Catalogue()
    {
        var labels = new FilterLabels();
        Assert.Equal("1 filter applied", labels.CountAnnouncement(1));
        Assert.Equal("3 filters applied", labels.CountAnnouncement(3));
        Assert.Equal("Choose a condition for Name", labels.StepAnnouncement(FilterDraftStep.Operator, "Name"));
        Assert.Equal(FilterOperators.AllValues.Count, labels.OperatorLabels.Count);
    }


    // ---------------------------------------------------------------- review follow-ups

    [Fact]
    public void UpdateRule_Returns_The_Same_Tree_When_Nothing_Changed()
    {
        var q = FilterQuery.Of(R("a", "name", "contains", "x"), new FilterGroup("g", FilterCombinator.Or, new FilterNode[] { R("b", "city", "is", "Bern") }));
        Assert.Same(q, FilterQueries.UpdateRule(q, "a", r => r));
        Assert.Same(q, FilterQueries.UpdateRule(q, "b", r => r with { Value = "Bern" }));
        Assert.NotSame(q, FilterQueries.UpdateRule(q, "b", r => r with { Value = "Basel" }));
    }

    [Fact]
    public void Query_Round_Trips_Through_System_Text_Json()
    {
        var q = FilterQuery.Of(
            R("r1", "title", "contains", "a"),
            new FilterGroup("g", FilterCombinator.Or, new FilterNode[]
            {
                R("r2", "tags", "has_any_of", new[] { "x", "y" }),
                R("r3", "amount", "between", new FilterRange(1.0, 5.0)),
                R("r4", "due", "is", new FilterDateValue(Relative: FilterRelativeDate.Tomorrow)),
                R("r5", "archived", "is", true),
            }));

        var json = System.Text.Json.JsonSerializer.Serialize(q);
        var back = System.Text.Json.JsonSerializer.Deserialize<FilterQuery>(json)!;

        Assert.Equal(
            q.Flatten().Select(t => (t.Field, t.Operator, string.Join(",", t.Values.Select(v => v?.ToString())))),
            back.Flatten().Select(t => (t.Field, t.Operator, string.Join(",", t.Values.Select(v => v?.ToString())))));
        var group = Assert.IsType<FilterGroup>(back.Rules[1]);
        Assert.Equal(FilterCombinator.Or, group.Combinator);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(((FilterRule)group.Rules[0]).Value);
        Assert.Equal(new FilterRange(1.0, 5.0), ((FilterRule)group.Rules[1]).Value);
        Assert.Equal(new FilterDateValue(Relative: FilterRelativeDate.Tomorrow), ((FilterRule)group.Rules[2]).Value);
        Assert.Equal(true, ((FilterRule)group.Rules[3]).Value);
        Assert.DoesNotContain("\"Values\"", json);
    }

    [Fact]
    public void Dates_Parse_The_Phrases_The_Labels_Show()
    {
        var de = new FilterLabels
        {
            DateToday = "heute", DateTomorrow = "morgen", DateYesterday = "gestern",
            DateInFormat = "in {0} {1}", DateAgoFormat = "vor {0} {1}", DateThisFormat = "diese(n/s) {0}",
            DateDay = "Tag", DateDays = "Tagen", DateWeek = "Woche", DateWeeks = "Wochen", DateMonth = "Monat", DateMonths = "Monaten", DateYear = "Jahr", DateYears = "Jahren",
        };
        Assert.Equal(FilterRelativeDate.Today, FilterDates.Parse("Heute", labels: de)!.Relative);
        Assert.Equal(new FilterRelativeDate(FilterDateUnit.Week, 1), FilterDates.Parse("in 1 Woche", labels: de)!.Relative);
        Assert.Equal(new FilterRelativeDate(FilterDateUnit.Day, -3), FilterDates.Parse("vor 3 Tagen", labels: de)!.Relative);
        Assert.Equal(new FilterRelativeDate(FilterDateUnit.Month, 0), FilterDates.Parse("diese(n/s) Monat", labels: de)!.Relative);
        // what the labels format is what they parse
        var value = new FilterDateValue(Relative: new FilterRelativeDate(FilterDateUnit.Year, 2));
        Assert.Equal(value, FilterDates.Parse(FilterDates.Format(value, de), labels: de));
        // English keeps working next to the labels
        Assert.Equal(new FilterRelativeDate(FilterDateUnit.Week, 1), FilterDates.Parse("next week", labels: de)!.Relative);
        // a format without spaces between count and unit parses too
        var zh = new FilterLabels { DateInFormat = "{0}{1}\u540e", DateWeek = "\u5468", DateWeeks = "\u5468" };
        Assert.Equal(new FilterRelativeDate(FilterDateUnit.Week, 2), FilterDates.Parse("2\u5468\u540e", labels: zh)!.Relative);
    }


    [Fact]
    public void Dates_Keep_Their_Type_Through_Json()
    {
        var q = FilterQuery.Of(R("a", "created", "is", new DateOnly(2024, 5, 1)), R("b", "seen", "is_after", new DateTime(2024, 5, 1, 8, 30, 0, DateTimeKind.Utc)));
        var back = System.Text.Json.JsonSerializer.Deserialize<FilterQuery>(System.Text.Json.JsonSerializer.Serialize(q))!;
        Assert.Equal(new DateOnly(2024, 5, 1), ((FilterRule)back.Rules[0]).Value);
        Assert.Equal(new DateTime(2024, 5, 1, 8, 30, 0, DateTimeKind.Utc), ((FilterRule)back.Rules[1]).Value);
    }

    [Fact]
    public void CollectIssues_Runs_The_Custom_Check_For_Every_Arity()
    {
        var q = FilterQuery.Of(R("range", "amount", "between", new FilterRange(1.0, 2.0)), R("none", "title", "empty"), R("one", "title", "is", "x"));
        var issues = FilterQueries.CollectIssues(q,
            r => r.Operator switch { "between" => FilterArity.Range, "empty" => FilterArity.None, _ => FilterArity.One },
            r => "no " + r.Id);
        Assert.Equal(new[] { "range", "none", "one" }, issues.Select(i => i.NodeId));
        Assert.All(issues, i => Assert.Equal(FilterIssueReason.Custom, i.Reason));
        // a built-in failure wins over the custom check
        var reversed = FilterQueries.CollectIssues(FilterQuery.Of(R("r", "amount", "between", new FilterRange(5.0, 1.0))), _ => FilterArity.Range, _ => "custom");
        Assert.Equal(FilterIssueReason.ReversedRange, Assert.Single(reversed).Reason);
    }
}
