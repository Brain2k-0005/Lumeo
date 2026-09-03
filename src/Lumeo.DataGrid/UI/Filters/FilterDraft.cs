namespace Lumeo;

/// <summary>A rule being built (no <see cref="RuleId"/>) or amended, and the step it is at.</summary>
public sealed record FilterDraft(
    FilterDraftStep Step,
    bool Ready,
    string? RuleId,
    IReadOnlyList<string> Path,
    IReadOnlyList<string> PickerPath,
    string? Operator,
    object? Value,
    string Query)
{
    /// <summary>A fresh draft at the field step.</summary>
    public static FilterDraft Create(IReadOnlyList<string>? pickerPath = null)
        => new(FilterDraftStep.Field, false, null, Array.Empty<string>(), pickerPath ?? Array.Empty<string>(), null, null, "");

    /// <summary>A draft amending an existing rule at a step.</summary>
    public static FilterDraft Amend(FilterRule rule, FilterDraftStep step)
        => new(step, false, rule.Id, rule.Path, rule.Path.Take(Math.Max(0, rule.Path.Count - 1)).ToList(), rule.Operator, rule.Value, "");

    /// <summary>True once a field was chosen.</summary>
    public bool IsCommittable => Path.Count > 0;

    public FilterDraft SelectField(IReadOnlyList<string> path, string? defaultOperator)
        => this with { Path = path, Operator = defaultOperator, Ready = true, Value = null, Step = FilterDraftStep.Operator, Query = "" };

    public FilterDraft SelectOperator(string op, FilterArity arity, object? value)
        => arity == FilterArity.None
            ? this with { Operator = op, Value = null, Ready = true, Query = "" }
            : this with { Operator = op, Value = value, Step = FilterDraftStep.Value, Ready = false, Query = "" };

    public FilterDraft SetValue(object? value) => this with { Value = value };

    public FilterDraft Commit(object? value = null) => this with { Value = value ?? Value, Ready = true };

    public FilterDraft? Back() => Step switch
    {
        FilterDraftStep.Value => this with { Step = FilterDraftStep.Operator, Ready = false, Query = "" },
        FilterDraftStep.Operator => this with { Step = FilterDraftStep.Field, Ready = false, Query = "" },
        _ => null,
    };
}
