using Lumeo.Services.Localization;

namespace Lumeo;

/// <summary>Every string <see cref="Filters"/> shows or announces. <see cref="FromLocalizer"/> fills
/// them from the <c>Filters.*</c> keys of the locale catalogue; set a property afterwards to
/// override one, and hand the result to <c>Filters.Labels</c>.</summary>
public sealed class FilterLabels
{
    public string AddFilter { get; set; } = "Add filter";
    public string AdvancedFilter { get; set; } = "Advanced filter";
    public string ShowRecords { get; set; } = "In this view, show records";
    public string BuilderEmpty { get; set; } = "No filters yet";
    public string BuilderEmptyHint { get; set; } = "Add a filter to narrow down what you see.";
    public string AddCondition { get; set; } = "Add filter";
    public string AddGroup { get; set; } = "Add group";
    public string AddToGroup { get; set; } = "Add filter to this group";
    public string RemoveGroup { get; set; } = "Remove group";
    public string WrapInGroup { get; set; } = "Wrap in group";
    public string Ungroup { get; set; } = "Ungroup";
    public string MoveToTop { get; set; } = "Move to top";
    /// <summary>Format with the 1-based group position.</summary>
    public string MoveToGroup { get; set; } = "Move to group {0}";
    public string Reorder { get; set; } = "Reorder";
    public string ReorderHint { get; set; } = "Press Alt with Arrow Up or Arrow Down to reorder";
    public string GroupAll { get; set; } = "All of the following are true...";
    public string GroupAny { get; set; } = "Any of the following are true...";
    public string GroupPlaceholder { get; set; } = "Drag filters here";
    public string GroupAdded { get; set; } = "Group added";
    public string GroupRemoved { get; set; } = "Group removed";
    /// <summary>Format with the label, the 1-based position and the total.</summary>
    public string ReorderAnnouncement { get; set; } = "{0} moved to position {1} of {2}";
    /// <summary>Format with the label, the destination, the 1-based position and the total.</summary>
    public string MoveAnnouncement { get; set; } = "{0} moved into {1}, position {2} of {3}";
    public string ClearAll { get; set; } = "Clear all";
    public string GroupMenu { get; set; } = "Group options";
    public string SearchFields { get; set; } = "Search attributes...";
    public string SearchOperators { get; set; } = "Search operators...";
    public string SearchOptions { get; set; } = "Search...";
    public string Back { get; set; } = "Back";
    public string Clear { get; set; } = "Clear";
    public string Apply { get; set; } = "Apply";
    public string Discard { get; set; } = "Discard changes";
    public string Empty { get; set; } = "No results";
    public string Loading { get; set; } = "Loading...";
    public string LoadingMore { get; set; } = "Loading more...";
    public string LoadMore { get; set; } = "Load more";
    public string Error { get; set; } = "Could not load";
    public string Retry { get; set; } = "Retry";
    public string Where { get; set; } = "Where";
    public string And { get; set; } = "And";
    public string Or { get; set; } = "Or";
    public string Combinator { get; set; } = "Change combinator";
    /// <summary>Format with the combinator word.</summary>
    public string CombinatorLabel { get; set; } = "{0}, change combinator";
    public string Duplicate { get; set; } = "Duplicate";
    public string Negate { get; set; } = "Negate";
    public string ConvertToAdvanced { get; set; } = "Advanced editor";
    public string Remove { get; set; } = "Remove";
    /// <summary>Format with the field label.</summary>
    public string ChipMenu { get; set; } = "{0} filter options";
    public string FiltersLabel { get; set; } = "Filters";
    public string ReadOnly { get; set; } = "Read only. These filters cannot be changed.";
    public string PathSeparator { get; set; } = " > ";
    public string ValuePlaceholder { get; set; } = "enter text...";
    public string SelectPlaceholder { get; set; } = "Select...";
    public string NoValue { get; set; } = "no value";
    public string SelectCondition { get; set; } = "Select condition";
    public string Incomplete { get; set; } = "incomplete filter";
    public string BranchAffordance { get; set; } = "opens a list";
    public string ExclusiveHint { get; set; } = "cannot be combined with the other options";
    /// <summary>Format with the label and the number of cleared picks.</summary>
    public string ExclusiveAnnouncement { get; set; } = "{0} selected. {1} other selections cleared.";
    /// <summary>Format with the count.</summary>
    public string ItemCount { get; set; } = "{0} items";
    public string FieldsLabel { get; set; } = "Attributes";
    /// <summary>Format with the count.</summary>
    public string ResultsAnnouncement { get; set; } = "{0} results";
    public string ActionsLabel { get; set; } = "Actions";
    /// <summary>Format with the field label.</summary>
    public string StepField { get; set; } = "Choose an attribute. {0}";
    public string StepOperator { get; set; } = "Choose a condition for {0}";
    public string StepValue { get; set; } = "Enter a value for {0}";
    public string CountOne { get; set; } = "1 filter applied";
    /// <summary>Format with the count.</summary>
    public string CountOther { get; set; } = "{0} filters applied";
    /// <summary>Format with the count.</summary>
    public string ValueCount { get; set; } = "{0} selected";
    /// <summary>Format with the summary and the comma-joined values.</summary>
    public string ValueDetail { get; set; } = "{0}: {1}";
    /// <summary>Format with both bounds.</summary>
    public string ValueRange { get; set; } = "{0} to {1}";
    /// <summary>Format with the field label.</summary>
    public string RangeFrom { get; set; } = "{0} from";
    public string RangeTo { get; set; } = "{0} to";
    public string RangeSeparator { get; set; } = "to";
    /// <summary>Format with the operator label.</summary>
    public string Negated { get; set; } = "not {0}";
    public string IssueOperator { get; set; } = "Choose a condition";
    public string IssueValue { get; set; } = "Enter a value";
    public string IssueRange { get; set; } = "Enter both ends of the range";
    public string IssueRangeOrder { get; set; } = "The end of the range comes before its start";
    public string IssueEmptyGroup { get; set; } = "This group has no conditions yet";
    /// <summary>Format with the count.</summary>
    public string IssueSummary { get; set; } = "{0} rows need attention";
    public string True { get; set; } = "True";
    public string False { get; set; } = "False";
    public string DateToday { get; set; } = "today";
    public string DateTomorrow { get; set; } = "tomorrow";
    public string DateYesterday { get; set; } = "yesterday";
    /// <summary>Format with the count and the unit.</summary>
    public string DateInFormat { get; set; } = "in {0} {1}";
    public string DateAgoFormat { get; set; } = "{0} {1} ago";
    /// <summary>Format with the unit.</summary>
    public string DateThisFormat { get; set; } = "this {0}";
    public string DateDay { get; set; } = "day";
    public string DateDays { get; set; } = "days";
    public string DateWeek { get; set; } = "week";
    public string DateWeeks { get; set; } = "weeks";
    public string DateMonth { get; set; } = "month";
    public string DateMonths { get; set; } = "months";
    public string DateYear { get; set; } = "year";
    public string DateYears { get; set; } = "years";
    public string DateFrom { get; set; } = "From";
    public string DateTo { get; set; } = "To";
    public string DatePlaceholder { get; set; } = "today, next week, 2024-05-01...";

    /// <summary>Operator labels by operator value (<c>contains</c>, <c>is_any_of</c>, ...).</summary>
    public Dictionary<string, string> OperatorLabels { get; set; } = new()
    {
        ["contains"] = "contains", ["not_contains"] = "does not contain", ["starts_with"] = "starts with", ["ends_with"] = "ends with",
        ["is"] = "is", ["is_not"] = "is not", ["is_any_of"] = "is any of", ["is_none_of"] = "is none of",
        ["has_any_of"] = "has any of", ["has_all_of"] = "has all of", ["has_none_of"] = "has none of",
        ["eq"] = "equals", ["neq"] = "does not equal", ["gt"] = "is greater than", ["gte"] = "is greater than or equal to",
        ["lt"] = "is less than", ["lte"] = "is less than or equal to", ["between"] = "is between", ["not_between"] = "is not between",
        ["is_before"] = "is before", ["is_after"] = "is after", ["is_on_or_before"] = "is on or before", ["is_on_or_after"] = "is on or after",
        ["empty"] = "is empty", ["not_empty"] = "is not empty",
    };

    public string CountAnnouncement(int count) => count == 1 ? CountOne : string.Format(CountOther, count);
    public string DateUnit(FilterDateUnit unit, int count) => (unit, count == 1) switch
    {
        (FilterDateUnit.Day, true) => DateDay, (FilterDateUnit.Day, false) => DateDays,
        (FilterDateUnit.Week, true) => DateWeek, (FilterDateUnit.Week, false) => DateWeeks,
        (FilterDateUnit.Month, true) => DateMonth, (FilterDateUnit.Month, false) => DateMonths,
        (_, true) => DateYear, _ => DateYears,
    };
    public string DateIn(int count, FilterDateUnit unit) => string.Format(DateInFormat, count, DateUnit(unit, count));
    public string DateAgo(int count, FilterDateUnit unit) => string.Format(DateAgoFormat, count, DateUnit(unit, count));
    public string DateThis(FilterDateUnit unit) => string.Format(DateThisFormat, DateUnit(unit, 1));
    public string StepAnnouncement(FilterDraftStep step, string label) => step switch
    {
        FilterDraftStep.Field => string.Format(StepField, label),
        FilterDraftStep.Operator => string.Format(StepOperator, label),
        _ => string.Format(StepValue, label),
    };
    public string IssueLabel(FilterIssue issue) => issue.Message ?? issue.Reason switch
    {
        FilterIssueReason.MissingOperator => IssueOperator,
        FilterIssueReason.IncompleteRange => IssueRange,
        FilterIssueReason.ReversedRange => IssueRangeOrder,
        FilterIssueReason.EmptyGroup => IssueEmptyGroup,
        _ => IssueValue,
    };

    /// <summary>The labels of the current UI culture, from the <c>Filters.*</c> keys.</summary>
    public static FilterLabels FromLocalizer(ILumeoLocalizer l)
    {
        var labels = new FilterLabels();
        foreach (var p in typeof(FilterLabels).GetProperties())
        {
            if (p.PropertyType != typeof(string) || !p.CanWrite) continue;
            var key = "Filters." + p.Name;
            var text = l[key];
            if (!string.IsNullOrEmpty(text) && text != key) p.SetValue(labels, text);
        }
        foreach (var op in FilterOperators.AllValues)
        {
            var key = "Filters.Op." + op;
            var text = l[key];
            if (!string.IsNullOrEmpty(text) && text != key) labels.OperatorLabels[op] = text;
        }
        return labels;
    }
}
