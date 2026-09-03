using Microsoft.AspNetCore.Components;

namespace Lumeo;

/// <summary>What a value editor receives: the rule's field and operator, the draft value, and the
/// actions to commit or leave. Built-in editors and a field's <see cref="FilterField.EditorTemplate"/>
/// render from it.</summary>
public sealed class FilterEditorContext
{
    private readonly Action<object?> _setValue;
    private readonly Func<object?, bool, Task> _commit;
    private readonly Action _cancel;
    private readonly Action _back;

    internal FilterEditorContext(FilterField field, FilterOperatorDef op, object? value, FilterEditorHost host, FilterOptionsState options, FilterLabels labels, Lumeo.Size size, string autoFocusId,
        Action<object?> setValue, Func<object?, bool, Task> commit, Action cancel, Action back)
    {
        Field = field; Operator = op; Value = value; Host = host; Options = options; Labels = labels; Size = size; AutoFocusId = autoFocusId;
        _setValue = setValue; _commit = commit; _cancel = cancel; _back = back;
    }

    public FilterField Field { get; }
    public FilterOperatorDef Operator { get; }
    /// <summary>The draft value; call <see cref="SetValue"/> to change it and <see cref="Commit"/> to store it.</summary>
    public object? Value { get; internal set; }
    public FilterEditorHost Host { get; }
    /// <summary>The option list of a select or multiselect field: items, loading and paging.</summary>
    public FilterOptionsState Options { get; }
    public FilterLabels Labels { get; }
    public Lumeo.Size Size { get; }
    /// <summary>The id to give the editor's first control, so the popover focuses it on open.</summary>
    public string AutoFocusId { get; }

    /// <summary>Changes the draft value without storing it.</summary>
    public void SetValue(object? value) { Value = value; _setValue(value); }
    /// <summary>Stores the value (the draft when null) on the rule and closes the editor unless <paramref name="close"/> is false.</summary>
    public Task Commit(object? value = null, bool close = true) => _commit(value ?? Value, close);
    /// <summary>Leaves without storing.</summary>
    public void Cancel() => _cancel();
    /// <summary>Steps back to the operator.</summary>
    public void Back() => _back();
}

/// <summary>The option list behind a select or multiselect editor: static options filtered by the
/// search text, or pages loaded through <see cref="FilterField.LoadOptions"/> with debounce,
/// cancellation, paging and retry.</summary>
public sealed class FilterOptionsState : IDisposable
{
    private readonly FilterField _field;
    private readonly FiltersContext? _shared;
    private readonly Action _changed;
    private readonly bool _enabled;
    private readonly Dictionary<string, FilterChoice> _cache = new();
    private List<FilterChoice> _loaded = new();
    private CancellationTokenSource? _cts;
    private System.Threading.Timer? _debounce;
    private int _request;
    private string? _cursor;

    internal FilterOptionsState(FilterField field, FiltersContext? shared, bool enabled, Action changed)
    {
        _field = field; _shared = shared; _enabled = enabled; _changed = changed;
        if (field.Options is not null)
        {
            foreach (var o in field.Options) _cache[o.Value] = o;
            _shared?.RememberOptions(field, field.Options);
        }
        if (enabled && IsAsync) Schedule(0);
    }

    public bool IsAsync => _field.LoadOptions is not null;
    public bool Loading { get; private set; }
    public bool Error { get; private set; }
    public bool HasMore { get; private set; }
    public string Query { get; private set; } = "";

    /// <summary>The options to show: static ones matching the search, or the loaded pages.</summary>
    public IReadOnlyList<FilterChoice> Items
    {
        get
        {
            if (IsAsync) return _loaded;
            var q = Query.Trim();
            var all = _field.Options ?? Array.Empty<FilterChoice>();
            return q.Length == 0 ? all : all.Where(o => FilterIndex.Matches(o, q)).ToList();
        }
    }

    public void SetQuery(string query)
    {
        if (Query == query) return;
        Query = query;
        if (_enabled && IsAsync)
        {
            // A page that arrives for the old text after this must not land: cancel it now and
            // mark the list as loading while the new request waits for the debounce.
            _cts?.Cancel();
            _request++;
            Loading = true; Error = false; HasMore = false;
            Schedule(250);
        }
        _changed();
    }

    public void LoadMore()
    {
        if (!HasMore || Loading) return;
        _ = Run(Query, _cursor, append: true);
    }

    public void Retry() => _ = Run(Query, null, append: false);

    /// <summary>The option for a value: declared, loaded here, or resolved anywhere in the same <see cref="Filters"/>.</summary>
    public FilterChoice? Resolve(string value)
    {
        if (_field.Options?.FirstOrDefault(o => o.Value == value) is { } declared) return declared;
        if (_shared?.ResolveOption(_field, value) is { } shared) return shared;
        return _cache.GetValueOrDefault(value);
    }

    private void Schedule(int delay)
    {
        _debounce?.Dispose();
        var query = Query;
        _debounce = new System.Threading.Timer(_ => _ = Run(query, null, append: false), null, delay, Timeout.Infinite);
    }

    private async Task Run(string query, string? cursor, bool append)
    {
        if (_field.LoadOptions is null) return;
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        var id = ++_request;
        Loading = true; Error = false;
        if (!append) _loaded = new List<FilterChoice>();
        _changed();
        try
        {
            var result = await _field.LoadOptions(new FilterLoadRequest(query, cursor, cts.Token));
            if (id != _request) return;
            foreach (var o in result.Items) _cache[o.Value] = o;
            _shared?.RememberOptions(_field, result.Items);
            _loaded = append ? _loaded.Concat(result.Items).ToList() : result.Items.ToList();
            _cursor = result.NextCursor;
            HasMore = result.HasMore ?? result.NextCursor is not null;
            Loading = false;
        }
        catch (OperationCanceledException) { return; }
        catch (Exception)
        {
            if (id != _request) return;
            Loading = false; Error = true; HasMore = false;
        }
        _changed();
    }

    public void Dispose()
    {
        _debounce?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
