namespace Lumeo;

/// <summary>Every operation on a <see cref="FilterQuery"/>. All of them return a new tree and leave
/// the input untouched; an operation that changes nothing returns the very same instance, so a
/// caller can compare by reference.</summary>
public static class FilterQueries
{
    /// <summary>Every rule in the tree, depth first.</summary>
    public static IReadOnlyList<FilterRule> Flatten(FilterGroup query)
    {
        var list = new List<FilterRule>();
        void Walk(FilterNode node)
        {
            if (node is FilterRule r) { list.Add(r); return; }
            foreach (var child in ((FilterGroup)node).Rules) Walk(child);
        }
        Walk(query);
        return list;
    }

    /// <summary>True once the rule has an operator; a rule fresh from the field picker has none.</summary>
    public static bool IsComplete(FilterRule rule) => rule.Operator != "";

    /// <summary>The complete rules as flat terms for an API call.</summary>
    public static IReadOnlyList<FilterTerm> FlattenTerms(FilterGroup query)
        => Flatten(query).Where(IsComplete).Select(r => new FilterTerm(r.Path, r.Field, r.Operator, r.Values, r.Negated)).ToList();

    public static int Count(FilterGroup query) => Flatten(query).Count;

    public static bool IsEmpty(FilterGroup query) => Count(query) == 0;

    /// <summary>Finds a node with its parent and index; the root reports a null parent and index -1.</summary>
    public static (FilterNode Node, FilterGroup? Parent, int Index)? Find(FilterQuery query, string id)
    {
        if (query.Id == id) return (query, null, -1);
        (FilterNode, FilterGroup?, int)? Walk(FilterGroup group)
        {
            for (var i = 0; i < group.Rules.Count; i++)
            {
                var child = group.Rules[i];
                if (child.Id == id) return (child, group, i);
                if (child is FilterGroup g && Walk(g) is { } found) return found;
            }
            return null;
        }
        return Walk(query);
    }

    public static FilterRule? FindRule(FilterQuery query, string id) => Find(query, id)?.Node as FilterRule;

    private static FilterGroup Rewrite(FilterGroup group, Func<FilterGroup, bool> shouldRewrite, Func<FilterGroup, FilterGroup> transform)
    {
        if (shouldRewrite(group)) return transform(group);
        var changed = false;
        var rules = new List<FilterNode>(group.Rules.Count);
        foreach (var child in group.Rules)
        {
            if (child is not FilterGroup g) { rules.Add(child); continue; }
            var next = Rewrite(g, shouldRewrite, transform);
            if (!ReferenceEquals(next, child)) changed = true;
            rules.Add(next);
        }
        return changed ? group with { Rules = rules } : group;
    }

    private static FilterQuery AsQuery(FilterGroup group) => (FilterQuery)group;

    /// <summary>Replaces the rule with the given id by a changed copy; the same tree comes back when
    /// the copy equals the rule.</summary>
    public static FilterQuery UpdateRule(FilterQuery query, string id, Func<FilterRule, FilterRule> update)
        => AsQuery(Rewrite(query,
            g => g.Rules.Any(c => c.Id == id && c is FilterRule),
            g =>
            {
                var index = g.Rules.ToList().FindIndex(c => c.Id == id && c is FilterRule);
                var before = (FilterRule)g.Rules[index];
                var after = update(before);
                if (ReferenceEquals(after, before) || after.Equals(before)) return g;
                var rules = g.Rules.ToList();
                rules[index] = after;
                return g with { Rules = rules };
            }));

    /// <summary>Removes the node; a group left empty by that is removed too.</summary>
    public static FilterQuery Remove(FilterQuery query, string id)
    {
        FilterGroup Prune(FilterGroup group)
        {
            var changed = false;
            var rules = new List<FilterNode>();
            foreach (var child in group.Rules)
            {
                if (child.Id == id) { changed = true; continue; }
                if (child is FilterGroup g)
                {
                    var next = Prune(g);
                    if (!ReferenceEquals(next, child)) changed = true;
                    if (next.Rules.Count == 0) continue;
                    rules.Add(next);
                    continue;
                }
                rules.Add(child);
            }
            return changed ? group with { Rules = rules } : group;
        }
        return AsQuery(Prune(query));
    }

    /// <summary>Inserts a node into a group (the root by default) at an index (the end by default).</summary>
    public static FilterQuery Insert(FilterQuery query, FilterNode node, string? parentId = null, int? index = null)
    {
        var target = parentId ?? query.Id;
        return AsQuery(Rewrite(query, g => g.Id == target, g =>
        {
            var rules = g.Rules.ToList();
            var at = index is null ? rules.Count : Math.Clamp(index.Value, 0, rules.Count);
            rules.Insert(at, node);
            return g with { Rules = rules };
        }));
    }

    private static FilterNode Clone(FilterNode node, Func<string> nextId) => node switch
    {
        FilterRule r => r with { Id = nextId() },
        FilterGroup g => g with { Id = nextId(), Rules = g.Rules.Select(c => Clone(c, nextId)).ToList() },
        _ => node,
    };

    /// <summary>Inserts a copy of the node right after it.</summary>
    public static FilterQuery Duplicate(FilterQuery query, string id, Func<string> nextId)
    {
        if (Find(query, id) is not { Parent: { } parent } found) return query;
        return Insert(query, Clone(found.Node, nextId), parent.Id, found.Index + 1);
    }

    public static FilterQuery SetCombinator(FilterQuery query, string groupId, FilterCombinator combinator)
        => AsQuery(Rewrite(query, g => g.Id == groupId, g => g.Combinator == combinator ? g : g with { Combinator = combinator }));

    public static FilterQuery ToggleCombinator(FilterQuery query, string groupId)
    {
        if (Find(query, groupId)?.Node is not FilterGroup g) return query;
        return SetCombinator(query, groupId, g.Combinator == FilterCombinator.And ? FilterCombinator.Or : FilterCombinator.And);
    }

    /// <summary>Moves a node by a number of positions among its siblings.</summary>
    public static FilterQuery Move(FilterQuery query, string id, int delta)
    {
        if (Find(query, id) is not { Parent: { } parent } found) return query;
        var from = found.Index;
        var to = from + delta;
        if (delta == 0 || to < 0 || to >= parent.Rules.Count) return query;
        return AsQuery(Rewrite(query, g => g.Id == parent.Id, g =>
        {
            var rules = g.Rules.ToList();
            var moved = rules[from];
            rules.RemoveAt(from);
            rules.Insert(to, moved);
            return g with { Rules = rules };
        }));
    }

    private static bool Contains(FilterNode node, string id)
    {
        if (node.Id == id) return true;
        return node is FilterGroup g && g.Rules.Any(c => Contains(c, id));
    }

    private static FilterGroup Detach(FilterGroup group, string id)
    {
        var changed = false;
        var rules = new List<FilterNode>();
        foreach (var child in group.Rules)
        {
            if (child.Id == id) { changed = true; continue; }
            if (child is FilterGroup g)
            {
                var next = Detach(g, id);
                if (!ReferenceEquals(next, child)) changed = true;
                rules.Add(next);
                continue;
            }
            rules.Add(child);
        }
        return changed ? group with { Rules = rules } : group;
    }

    /// <summary>Moves a node into another group at an index; a group cannot move into itself.</summary>
    public static FilterQuery MoveTo(FilterQuery query, string id, string parentId, int index)
    {
        if (Find(query, id) is not { Parent: { } parent } found) return query;
        if (Contains(found.Node, parentId)) return query;
        if (Find(query, parentId)?.Node is not FilterGroup) return query;
        var sameParent = parent.Id == parentId;
        var target = sameParent && found.Index < index ? index - 1 : index;
        if (sameParent && target == found.Index) return query;
        return Insert(AsQuery(Detach(query, id)), found.Node, parentId, target);
    }

    /// <summary>Inserts a copy of a node into another group at an index.</summary>
    public static FilterQuery CopyTo(FilterQuery query, string id, string parentId, int index, Func<string> nextId)
    {
        if (Find(query, id) is not { Parent: not null } found) return query;
        if (Find(query, parentId)?.Node is not FilterGroup) return query;
        return Insert(query, Clone(found.Node, nextId), parentId, index);
    }

    /// <summary>Wraps a node in a new group in its place.</summary>
    public static FilterQuery WrapInGroup(FilterQuery query, string id, string groupId, FilterCombinator combinator = FilterCombinator.Or)
    {
        if (Find(query, id) is not { Parent: { } parent } found) return query;
        return AsQuery(Rewrite(query, g => g.Id == parent.Id, g => g with
        {
            Rules = g.Rules.Select(c => c.Id == id ? new FilterGroup(groupId, combinator, new[] { c }) : c).ToList(),
        }));
    }

    /// <summary>Dissolves a group into its parent, keeping its rules in place.</summary>
    public static FilterQuery Unwrap(FilterQuery query, string groupId)
    {
        if (query.Id == groupId) return query;
        if (Find(query, groupId) is not { Parent: { } parent, Node: FilterGroup dissolved } found) return query;
        return AsQuery(Rewrite(query, g => g.Id == parent.Id, g =>
        {
            var rules = g.Rules.ToList();
            rules.RemoveAt(found.Index);
            rules.InsertRange(found.Index, dissolved.Rules);
            return g with { Rules = rules };
        }));
    }

    public static FilterQuery Clear(FilterQuery query) => query.Rules.Count == 0 ? query : query with { Rules = Array.Empty<FilterNode>() };

    /// <summary>Drops empty groups and dissolves a group that holds only one group.</summary>
    public static FilterQuery Prune(FilterQuery query)
    {
        FilterNode? PruneNode(FilterNode node)
        {
            if (node is FilterRule) return node;
            var group = (FilterGroup)node;
            var rules = group.Rules.Select(PruneNode).Where(n => n is not null).Select(n => n!).ToList();
            if (rules.Count == 0) return null;
            if (rules.Count == 1 && rules[0] is FilterGroup only) return only;
            return group with { Rules = rules };
        }
        var kept = query.Rules.Select(PruneNode).Where(n => n is not null).Select(n => n!).ToList();
        return query with { Rules = kept };
    }

    /// <summary>The rules and groups that are not ready to filter: no operator, no value, half a
    /// range, a reversed range, an empty group, or a field's own validation message.</summary>
    public static IReadOnlyList<FilterIssue> CollectIssues(FilterQuery query, Func<FilterRule, FilterArity?> arityOf, Func<FilterRule, string?>? validate = null)
    {
        var issues = new List<FilterIssue>();
        void Visit(FilterGroup group, bool isRoot)
        {
            if (!isRoot && group.Rules.Count == 0) issues.Add(new FilterIssue(group.Id, FilterIssueColumn.Group, FilterIssueReason.EmptyGroup));
            foreach (var child in group.Rules)
            {
                if (child is FilterGroup g) { Visit(g, false); continue; }
                var rule = (FilterRule)child;
                var arity = arityOf(rule);
                if (arity is null) continue;
                if (!IsComplete(rule)) { issues.Add(new FilterIssue(rule.Id, FilterIssueColumn.Operator, FilterIssueReason.MissingOperator)); continue; }
                if (arity == FilterArity.None) continue;
                var values = rule.Values;
                if (arity == FilterArity.Range)
                {
                    if (values.Count < 2 || FilterValues.IsBlank(values[0]) || FilterValues.IsBlank(values[1]))
                    {
                        issues.Add(new FilterIssue(rule.Id, FilterIssueColumn.Value, FilterIssueReason.IncompleteRange));
                        continue;
                    }
                    if (FilterValues.CompareBounds(values[0], values[1]) > 0)
                        issues.Add(new FilterIssue(rule.Id, FilterIssueColumn.Value, FilterIssueReason.ReversedRange));
                    continue;
                }
                if (values.Count == 0 || values.All(FilterValues.IsBlank))
                {
                    issues.Add(new FilterIssue(rule.Id, FilterIssueColumn.Value, FilterIssueReason.MissingValue));
                    continue;
                }
                if (validate?.Invoke(rule) is { Length: > 0 } message)
                    issues.Add(new FilterIssue(rule.Id, FilterIssueColumn.Value, FilterIssueReason.Custom, message));
            }
        }
        Visit(query, true);
        return issues;
    }
}
