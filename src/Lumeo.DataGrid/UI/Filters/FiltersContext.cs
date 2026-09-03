using Microsoft.AspNetCore.Components;

namespace Lumeo;

/// <summary>The segment of a chip that holds focus or should open.</summary>
public enum FilterChipSegment { Combinator, Field, Operator, Value, Add, Menu, Ungroup, Remove, Drag }

/// <summary>The store behind a <see cref="Filters"/>: the schema index, the query, the draft, focus,
/// and every action a chip, row or editor performs. Cascaded to the parts; a custom chip or
/// editor reaches it through <c>[CascadingParameter] FiltersContext</c>.</summary>
public sealed class FiltersContext
{
    private readonly Filters _owner;
    private readonly Dictionary<string, Dictionary<string, FilterChoice>> _resolved = new();
    private readonly Dictionary<string, HashSet<string>> _claimed = new();
    private int _idCounter;
    private readonly string _idSeed;

    internal FiltersContext(Filters owner, string idSeed)
    {
        _owner = owner;
        _idSeed = idSeed;
    }

    // ---- schema and configuration (set by the owner on every render) ----
    public FilterIndex Index { get; internal set; } = new(Array.Empty<FilterField>());
    public FilterLabels Labels { get; internal set; } = new();
    public IReadOnlyDictionary<FilterValueKind, IReadOnlyList<FilterOperatorDef>> Catalogue { get; internal set; } = FilterOperators.Build(new FilterLabels().OperatorLabels);
    public IReadOnlyDictionary<string, RenderFragment<FilterEditorContext>> Editors { get; internal set; } = new Dictionary<string, RenderFragment<FilterEditorContext>>();
    public Lumeo.Size Size { get; internal set; } = Lumeo.Size.Md;
    public bool Disabled { get; internal set; }
    public bool ReadOnly { get; internal set; }
    /// <summary>True when the bar refuses every edit: disabled or read only.</summary>
    public bool Locked => Disabled || ReadOnly;
    public FiltersVariant Variant { get; internal set; }
    public bool Reorderable { get; internal set; }
    public int MaxPathSegments { get; internal set; } = 3;
    public string? MenuClass { get; internal set; }
    public bool CanConvertToAdvanced { get; internal set; }
    public RenderFragment<FilterValueContext>? ValueTemplate { get; internal set; }
    public RenderFragment<FilterRule>? ChipTemplate { get; internal set; }

    // ---- state ----
    public FilterQuery Query { get; internal set; } = FilterQuery.Empty();
    public FilterDraft? Draft { get; internal set; }
    public int RuleCount => FilterQueries.Count(Query);
    public string? FocusId { get; private set; }
    public FilterChipSegment? FocusSegment { get; private set; }
    public bool AutoOpen { get; private set; }
    public string Announcement { get; private set; } = "";
    public int AnnouncementSeq { get; private set; }

    /// <summary>A fresh node id, unique within this bar.</summary>
    public string NextId() => $"{_idSeed}f{++_idCounter}";

    // ---- schema helpers ----
    public IReadOnlyList<FilterOperatorDef> ResolveOperators(FilterField field) => FilterOperators.Resolve(field, Catalogue);

    /// <summary>The editor for a field and operator: the field's own, a registered one by key, or
    /// the one the operator's arity and the field's kind imply. Null for an operator without a value.</summary>
    public RenderFragment<FilterEditorContext>? ResolveEditor(FilterField field, FilterOperatorDef? op)
    {
        if (field.EditorTemplate is not null) return field.EditorTemplate;
        if (field.Editor is not null && Editors.TryGetValue(field.Editor, out var named)) return named;
        var arity = FilterOperators.ArityOf(op);
        if (arity == FilterArity.None) return null;
        if (arity == FilterArity.Range)
            return Editors.GetValueOrDefault(field.Kind == FilterValueKind.Date ? "date-range" : "range") ?? Editors.GetValueOrDefault("range");
        if (arity == FilterArity.Many && field.HasOptions) return Editors.GetValueOrDefault("multiselect");
        var key = field.Kind switch
        {
            FilterValueKind.Number => "number",
            FilterValueKind.Range => "range",
            FilterValueKind.Date => "date",
            FilterValueKind.Select => "select",
            FilterValueKind.MultiSelect => "multiselect",
            FilterValueKind.Boolean => "boolean",
            _ => "text",
        };
        return Editors.GetValueOrDefault(key) ?? Editors.GetValueOrDefault("text");
    }

    // ---- option resolution shared across chips and editors ----
    private string KeyOf(FilterField field) => Index.All.FirstOrDefault(e => ReferenceEquals(e.Field, field)).Path is { } p && p.Count > 0 ? FilterIndex.Join(p) : "#" + field.Id;

    internal void RememberOptions(FilterField field, IReadOnlyList<FilterChoice> options)
    {
        var key = KeyOf(field);
        if (!_resolved.TryGetValue(key, out var map)) _resolved[key] = map = new Dictionary<string, FilterChoice>();
        var landed = false;
        foreach (var o in options) { if (!map.ContainsKey(o.Value)) landed = true; map[o.Value] = o; }
        if (landed) Render();
    }

    /// <summary>The option a value stands for, from the field's static options or anything loaded or resolved so far.</summary>
    public FilterChoice? ResolveOption(FilterField field, string value)
    {
        if (field.Options?.FirstOrDefault(o => o.Value == value) is { } declared) return declared;
        return _resolved.TryGetValue(KeyOf(field), out var map) ? map.GetValueOrDefault(value) : null;
    }

    /// <summary>Asks the field's <see cref="FilterField.ResolveValues"/> for the labels of values
    /// nobody has seen yet; a chip restored from a saved query calls this once per render.</summary>
    public void EnsureResolved(FilterField field, IReadOnlyList<string> values)
    {
        if (field.ResolveValues is null || values.Count == 0) return;
        var key = KeyOf(field);
        if (!_claimed.TryGetValue(key, out var claimed)) _claimed[key] = claimed = new HashSet<string>();
        var missing = values.Where(v => ResolveOption(field, v) is null && claimed.Add(v)).ToList();
        if (missing.Count == 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var options = await field.ResolveValues(missing);
                await _owner.Dispatch(() => RememberOptions(field, options));
            }
            catch
            {
                await _owner.Dispatch(() => { foreach (var v in missing) claimed.Remove(v); });
            }
        });
    }

    // ---- focus ----
    /// <summary>Records which chip and segment hold focus; <paramref name="autoOpen"/> asks that segment to open its popover on its next render.</summary>
    public void SetFocus(string? id, FilterChipSegment? segment, bool autoOpen = false)
    {
        if (FocusId == id && FocusSegment == segment && AutoOpen == autoOpen) return;
        FocusId = id; FocusSegment = segment; AutoOpen = autoOpen;
        Render();
    }

    /// <summary>A chip consumed its auto-open request.</summary>
    public void ConsumeAutoOpen(string id, FilterChipSegment segment)
    {
        if (FocusId == id && FocusSegment == segment && AutoOpen) { AutoOpen = false; }
    }

    /// <summary>Moves keyboard focus to an element by id.</summary>
    public Task FocusElement(string id) => _owner.FocusElementAsync(id);

    // ---- announcements ----
    public void Announce(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        Announcement = message;
        AnnouncementSeq++;
        Render();
    }

    // ---- query actions ----
    private bool Emit(FilterQuery next, FilterChangeReason reason, FilterRule? rule)
    {
        if (Locked) return false;
        var field = rule is null ? null : Index.Get(rule.Path);
        return _owner.Emit(next, new FilterChangeDetails(reason, rule, field));
    }

    public void AddRule(FilterRule rule, string? parentId = null)
    {
        if (Locked) return;
        var next = FilterQueries.Insert(Query, rule, parentId);
        if (!Emit(next, FilterChangeReason.Add, rule)) return;
        Announce(Labels.CountAnnouncement(FilterQueries.Count(next)));
    }

    public string AddGroup(string? parentId = null, FilterCombinator combinator = FilterCombinator.Or)
    {
        if (Locked) return "";
        var id = NextId();
        var next = FilterQueries.Insert(Query, new FilterGroup(id, combinator, Array.Empty<FilterNode>()), parentId);
        if (!Emit(next, FilterChangeReason.Add, null)) return "";
        Announce(Labels.GroupAdded);
        return id;
    }

    public void UpdateRule(string id, Func<FilterRule, FilterRule> update)
    {
        if (Locked) return;
        var next = FilterQueries.UpdateRule(Query, id, update);
        if (ReferenceEquals(next, Query)) return;
        Emit(next, FilterChangeReason.Update, FilterQueries.FindRule(next, id));
    }

    public void RemoveNode(string id)
    {
        if (Locked) return;
        var removed = FilterQueries.FindRule(Query, id);
        var next = FilterQueries.Remove(Query, id);
        if (ReferenceEquals(next, Query)) return;
        if (!Emit(next, FilterChangeReason.Remove, removed)) return;
        Announce(removed is not null ? Labels.CountAnnouncement(FilterQueries.Count(next)) : Labels.GroupRemoved);
    }

    public void DuplicateNode(string id)
    {
        if (Locked) return;
        var next = FilterQueries.Duplicate(Query, id, NextId);
        if (ReferenceEquals(next, Query)) return;
        if (!Emit(next, FilterChangeReason.Duplicate, FilterQueries.FindRule(Query, id))) return;
        Announce(Labels.CountAnnouncement(FilterQueries.Count(next)));
    }

    public void NegateRule(string id)
    {
        if (Locked) return;
        if (FilterQueries.FindRule(Query, id) is not { } rule || Index.Get(rule.Path) is not { } field) return;
        var operators = ResolveOperators(field);
        var (op, negated) = FilterOperators.Negate(FilterOperators.Get(operators, rule.Operator), operators, rule.Negated);
        var next = FilterQueries.UpdateRule(Query, id, r => r with { Operator = op ?? r.Operator, Negated = negated });
        Emit(next, FilterChangeReason.Negate, FilterQueries.FindRule(next, id));
    }

    private void AnnounceReorder(FilterQuery next, string id, string? fromParentId)
    {
        if (FilterQueries.Find(next, id) is not { Parent: { } parent } found) return;
        var label = found.Node is FilterRule r ? Index.FormatPath(r.Path, Labels.PathSeparator)
            : ((FilterGroup)found.Node).Combinator == FilterCombinator.And ? Labels.GroupAll : Labels.GroupAny;
        var position = found.Index + 1;
        var total = parent.Rules.Count;
        if (fromParentId is not null && fromParentId != parent.Id)
        {
            var destination = parent.Id == next.Id ? Labels.FiltersLabel : parent.Combinator == FilterCombinator.And ? Labels.GroupAll : Labels.GroupAny;
            Announce(string.Format(Labels.MoveAnnouncement, label, destination, position, total));
            return;
        }
        Announce(string.Format(Labels.ReorderAnnouncement, label, position, total));
    }

    public void MoveNode(string id, int delta)
    {
        if (Locked) return;
        var next = FilterQueries.Move(Query, id, delta);
        if (ReferenceEquals(next, Query)) return;
        if (!Emit(next, FilterChangeReason.Reorder, FilterQueries.FindRule(next, id))) return;
        AnnounceReorder(next, id, null);
    }

    public void MoveNodeTo(string id, string parentId, int index)
    {
        if (Locked) return;
        var from = FilterQueries.Find(Query, id)?.Parent?.Id;
        var next = FilterQueries.MoveTo(Query, id, parentId, index);
        if (ReferenceEquals(next, Query)) return;
        if (!Emit(next, FilterChangeReason.Reorder, FilterQueries.FindRule(next, id))) return;
        AnnounceReorder(next, id, from);
    }

    public void CopyNodeTo(string id, string parentId, int index)
    {
        if (Locked) return;
        var next = FilterQueries.CopyTo(Query, id, parentId, index, NextId);
        if (ReferenceEquals(next, Query)) return;
        if (!Emit(next, FilterChangeReason.Duplicate, FilterQueries.FindRule(Query, id))) return;
        Announce(Labels.CountAnnouncement(FilterQueries.Count(next)));
    }

    public void WrapInGroup(string id, FilterCombinator combinator = FilterCombinator.Or)
    {
        if (Locked) return;
        var next = FilterQueries.WrapInGroup(Query, id, NextId(), combinator);
        if (ReferenceEquals(next, Query)) return;
        if (!Emit(next, FilterChangeReason.Add, FilterQueries.FindRule(next, id))) return;
        Announce(Labels.GroupAdded);
    }

    public void UnwrapGroup(string groupId)
    {
        if (Locked) return;
        var next = FilterQueries.Unwrap(Query, groupId);
        if (ReferenceEquals(next, Query)) return;
        if (!Emit(next, FilterChangeReason.Remove, null)) return;
        Announce(Labels.GroupRemoved);
    }

    public void SetCombinator(string groupId, FilterCombinator combinator)
    {
        if (Locked) return;
        var next = FilterQueries.SetCombinator(Query, groupId, combinator);
        if (ReferenceEquals(next, Query)) return;
        if (!Emit(next, FilterChangeReason.Combinator, null)) return;
        Announce(combinator == FilterCombinator.And ? Labels.GroupAll : Labels.GroupAny);
    }

    public void ToggleCombinator(string groupId)
    {
        if (FilterQueries.Find(Query, groupId)?.Node is not FilterGroup g) return;
        SetCombinator(groupId, g.Combinator == FilterCombinator.And ? FilterCombinator.Or : FilterCombinator.And);
    }

    public void ClearQuery()
    {
        if (Locked) return;
        var next = FilterQueries.Clear(Query);
        if (ReferenceEquals(next, Query)) return;
        if (!Emit(next, FilterChangeReason.Clear, null)) return;
        Announce(Labels.CountAnnouncement(0));
    }

    // ---- draft (the add-filter flow) ----
    public void OpenCreate(IReadOnlyList<string>? pickerPath = null)
    {
        if (Locked) return;
        Draft = FilterDraft.Create(pickerPath);
        Render();
    }

    public void OpenAmend(string ruleId, FilterDraftStep step)
    {
        if (Locked) return;
        if (FilterQueries.FindRule(Query, ruleId) is not { } rule) return;
        Draft = FilterDraft.Amend(rule, step);
        Render();
    }

    public void CloseDraft()
    {
        if (Draft is null) return;
        Draft = null;
        Render();
    }

    public void SetDraftPickerPath(IReadOnlyList<string> path)
    {
        if (Draft is null || Locked) return;
        Draft = Draft with { PickerPath = path, Query = "" };
        Render();
    }

    public void SetDraftQuery(string query)
    {
        if (Draft is null || Locked) return;
        Draft = Draft with { Query = query };
        Render();
    }

    /// <summary>The field step: a new rule is added with no operator and its operator segment asked
    /// to open; an amended rule takes the new field and starts over at the operator.</summary>
    public void SelectDraftField(IReadOnlyList<string> path)
    {
        if (Draft is null || Locked) return;
        if (Index.Get(path) is not { } field) return;
        var defaultOperator = FilterOperators.Default(field, ResolveOperators(field));
        var draft = Draft.SelectField(path, defaultOperator);
        if (draft.RuleId is { } ruleId)
        {
            Draft = null;
            UpdateRule(ruleId, r => r with { Path = path, Operator = "", Value = null, Negated = false });
            SetFocus(ruleId, FilterChipSegment.Operator, autoOpen: true);
            return;
        }
        var id = NextId();
        Draft = null;
        AddRule(new FilterRule(id, path, ""));
        SetFocus(id, FilterChipSegment.Operator, autoOpen: true);
    }

    internal void Render() => _owner.RequestRender();

    /// <summary>The bar that owns this context.</summary>
    internal Filters Owner => _owner;

    /// <summary>The DOM id of a rule's chip.</summary>
    public string ChipElementId(string ruleId) => _owner.ChipElementId(ruleId);
}
