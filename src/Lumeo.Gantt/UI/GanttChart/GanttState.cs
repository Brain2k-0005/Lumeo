namespace Lumeo.GanttV3;

/// <summary>
/// A half-open date window describing which portion of the timeline is currently
/// materialized/rendered (design spec "Virtualization" — horizontal windowed time
/// range that extends on scroll). Pure data: no timezone conversion is ever applied
/// to <see cref="Start"/>/<see cref="End"/> anywhere in GanttV3 (see the TZ/DST-safety
/// note on <see cref="GanttScale"/>), so they keep whatever <see cref="DateTimeKind"/>
/// the caller gave them.
///
/// <c>public</c> (design spec Phase 3, T4): forced public the moment
/// <see cref="GanttState.VisibleRange"/> itself goes public — a public member cannot
/// expose a less-accessible type (CS0053), the same forcing rule this campaign has
/// already hit for <c>GanttRowKind</c>/<c>GanttVisibleRow</c> (Razor-compiler-forced
/// public components) and <c>GanttTaskUpdateSource</c>/<c>GanttScheduleDropContext</c>
/// (public `EventCallback`/`Func` parameters) — see their own remarks for the same
/// precedent applied to a different forcing mechanism. Recorded in
/// PublicAPI.Shipped.txt as of the Phase-4 promotion.
/// </summary>
public readonly record struct GanttDateRange(DateTime Start, DateTime End);

/// <summary>
/// The attach contract's host side (design spec Phase 3, T4 — hoistable
/// <see cref="GanttState"/> + imperative API). A GanttV3 component that accepts a
/// <c>State</c> parameter implements this EXPLICITLY (never through a public method of
/// its own — see <c>GanttChart</c>'s own remarks) and calls <see cref="GanttState.Attach"/>
/// with itself, so the state's imperative scroll/view-mode API has somewhere to route
/// to without reaching back into Blazor/interop internals itself (see
/// <see cref="GanttState"/>'s own class remarks for why it stays a plain, Blazor-free
/// C# object).
///
/// <c>internal</c>: never appears on <see cref="GanttState"/>'s own public surface —
/// callers only ever see the awaitable <c>Task</c>-returning public methods
/// (<see cref="GanttState.ScrollToDateAsync"/>/<see cref="GanttState.ScrollToTaskAsync"/>/
/// <see cref="GanttState.SetViewMode"/>) that route through whichever host is currently
/// attached; nobody outside this assembly ever implements or consumes this interface
/// directly. Both methods return the underlying component's own
/// <c>InvokeAsync(...)</c> Task unchanged, so a caller of the PUBLIC GanttState methods
/// that awaits them observes genuine completion (or a fault) rather than a
/// fire-and-forget dispatch — see <see cref="GanttState.ScrollToDateAsync"/>'s own
/// remarks for the one exception (<see cref="GanttState.SetViewMode"/> is deliberately
/// NOT awaitable, by design, unlike its "Async"-suffixed siblings here).
/// </summary>
internal interface IGanttStateHost
{
    /// <summary>
    /// Scrolls the host's timeline to center on <paramref name="date"/>, through the
    /// host's OWN existing scroll-intent interop (no second implementation) —
    /// dispatched via the host's <c>InvokeAsync</c>, so it is safe to call from outside
    /// a render cycle (design spec Phase 3, T4, constraint 5 — thread/render safety).
    /// </summary>
    Task ScrollToDateAsync(DateTime date);

    /// <summary>
    /// Requests a view-mode change, routed through the host's CLAIM-based view-mode
    /// ownership model (PR #382) rather than a direct <see cref="GanttState"/>
    /// mutation — see <c>GanttChart.ApplyViewModeIntentAsync</c>'s own remarks for why this
    /// is what keeps an imperative <see cref="GanttState.SetViewMode"/> call from
    /// reopening that model's bug class.
    /// </summary>
    Task SetViewModeAsync(GanttViewMode mode);
}

/// <summary>
/// Hoistable Gantt store — the REUI <c>useGanttState</c> analog called out in the
/// design spec ("Public API" &gt; Additive &gt; <c>GanttState</c>: "nav and view can be
/// rendered separately against one shared state instance"). Deliberately a plain C#
/// class with zero Blazor/DI/CascadingValue dependency, so it can be constructed and
/// unit-tested in isolation and so a single instance can later be shared between a
/// separately-rendered <c>GanttNav</c> and <c>GanttTimeline</c> (Phase 2/3) without
/// either owning the other's lifetime.
///
/// <c>public</c> (design spec Phase 3, T4): promoted the moment <c>GanttChart</c> exposes a
/// <c>State</c> parameter wired to it, per the phase-1 sequencing note this type's OWN
/// remarks originally flagged. Recorded in PublicAPI.Shipped.txt as of the Phase-4
/// promotion (<c>[EditorBrowsable(Never)]</c> lifted).
///
/// <b>Public surface is READ-everything, WRITE-selectively</b> (a deliberate split, not
/// an oversight): <see cref="Tasks"/>/<see cref="ViewMode"/>/<see cref="VisibleRange"/>/
/// <see cref="Collapsed"/>/<see cref="SelectedIds"/>/<see cref="Changed"/> and their
/// query helpers (<see cref="IsCollapsed"/>/<see cref="IsSelected"/>) are public — a
/// hoisted consumer needs to freely READ the shared state to build custom nav/view UI
/// against it (REUI's own <c>useGanttState</c> shape). The raw mutators
/// (<c>SetTasks</c>/<c>SetVisibleRange</c>/<c>SetCollapsed</c>/<c>ToggleCollapsed</c>/
/// <c>CommitViewMode</c>/<c>WouldChangeTasks</c>/the <c>SelectedIds</c> writers) stay
/// <c>internal</c> — <c>GanttChart</c>'s own commit machinery is the only legitimate direct
/// writer of raw state; a consumer that could call them directly would bypass the
/// viewport-reconcile/view-mode-ownership pipeline entirely (silently reverted on the
/// next parameter pass, or worse, exactly the "mutating state behind the model's back"
/// hazard this task's own dispatch calls out for <c>SetViewMode</c> specifically). The
/// three NEW imperative members (<see cref="ScrollToTaskAsync"/>/
/// <see cref="ScrollToDateAsync"/>/<see cref="SetViewMode"/>) are the deliberately
/// engineered EXCEPTION: public, but each routes through the attached host's own
/// pipeline (<see cref="IGanttStateHost"/>) rather than writing state directly.
///
/// Every mutator is idempotent with respect to <see cref="Changed"/>: setting a value
/// that is already current is a silent no-op and does not raise the event. This mirrors
/// the Gantt (v2) component's own change-detection discipline (hash-gated re-pushes —
/// see <c>Gantt.razor</c>'s <c>ComputeTasksHash</c>/<c>_lastOptionsHash</c> pattern) so a
/// caller that re-applies the same value on every render (a common Blazor pattern)
/// doesn't spuriously notify subscribers.
/// </summary>
public sealed class GanttState
{
    private List<GanttTask> _tasks = new();
    private GanttViewMode _viewMode = GanttViewMode.Day;
    private GanttDateRange _visibleRange;
    private readonly HashSet<string> _collapsed = new();
    private readonly HashSet<string> _selectedIds = new(StringComparer.Ordinal);

    // Design spec Phase 3, T4 — the attach contract. WEAK deliberately: a hoisted
    // GanttState is designed to potentially OUTLIVE any one component rendering
    // against it (a consumer may hold it in a field/service with a longer lifetime
    // than whichever GanttChart currently renders against it — the entire point of
    // hoisting). A STRONG reference here would pin a disposed component in memory
    // for as long as the state itself lives, a component-shaped leak this class has
    // no way to know about or bound. See Attach/Detach for the full contract.
    private WeakReference<IGanttStateHost>? _host;

    /// <summary>Raised after any mutator below actually changes state. Not raised for no-op sets (same value re-applied).</summary>
    public event Action? Changed;

    /// <summary>The current task set. Replace via <c>SetTasks</c> (internal — see the class remarks) — this list is never mutated in place.</summary>
    public IReadOnlyList<GanttTask> Tasks => _tasks;

    /// <summary>The active view mode. Change via <see cref="SetViewMode"/> (imperative, routed through the attached host's ownership model) or, internally, <c>CommitViewMode</c>.</summary>
    public GanttViewMode ViewMode => _viewMode;

    /// <summary>The currently materialized date window. Change via <c>SetVisibleRange</c> (internal — see the class remarks).</summary>
    public GanttDateRange VisibleRange => _visibleRange;

    /// <summary>Ids of collapsed (children-hidden) rows. Mutate via <c>SetCollapsed</c>/<c>ToggleCollapsed</c> (internal — see the class remarks).</summary>
    public IReadOnlySet<string> Collapsed => _collapsed;

    /// <summary>
    /// Ids of selected leaf rows (design spec Phase 3, T4 — the backing store for
    /// Phase 3 T6's checkbox selection UI, defined now so T6 can build on it without a
    /// rewrite; T4 itself ships NO selection UI). <c>ISet</c>-shaped (an
    /// <see cref="IReadOnlySet{T}"/> view over an ordinal <see cref="HashSet{T}"/>) to
    /// match T6's own plan wording ("two-way <c>SelectedIds</c> (<c>ISet&lt;string&gt;</c> +
    /// changed callback)"). Mutate via the internal <c>SetSelectedIds</c>/<c>SetSelected</c>/
    /// <c>ToggleSelected</c> trio — kept internal for the SAME reason
    /// <c>Collapsed</c>'s own writers are (see the class remarks): T6's tri-state
    /// parent/descendant selection semantics belong to <c>GanttChart</c>'s own row-click
    /// wiring, not to arbitrary consumer code mutating the set directly and bypassing
    /// them.
    /// </summary>
    public IReadOnlySet<string> SelectedIds => _selectedIds;

    /// <summary>
    /// Replaces the task set. No-ops (and does not raise <see cref="Changed"/>) when
    /// the new sequence is value-equal, element-for-element, to the current one —
    /// <see cref="GanttTask"/> is a record, so this is a cheap structural comparison
    /// that also picks up a <see cref="GanttTask.ParentId"/>-only change.
    ///
    /// <c>internal</c> (design spec Phase 3, T4 — see the class remarks): the raw
    /// write side of state stays <c>GanttChart</c>-only.
    /// </summary>
    internal void SetTasks(IEnumerable<GanttTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var next = tasks as IReadOnlyList<GanttTask> ?? tasks.ToList();
        if (TasksEqual(_tasks, next)) return;
        _tasks = next as List<GanttTask> ?? next.ToList();
        RaiseChanged();
    }

    /// <summary>
    /// Whether <c>SetTasks</c> with <paramref name="candidate"/> would actually change
    /// the task set (same structural comparison <c>SetTasks</c> uses). Lets a caller
    /// detect a task-set change WITHOUT committing it yet — <c>GanttChart</c>'s viewport
    /// reconcile needs the answer before it commits, so it can capture the live scroll
    /// center under the OLD tasks/range first (Codex round 14, finding #4).
    ///
    /// <c>internal</c> (design spec Phase 3, T4 — see the class remarks).
    /// </summary>
    internal bool WouldChangeTasks(IReadOnlyList<GanttTask> candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return !TasksEqual(_tasks, candidate);
    }

    /// <summary>
    /// Commits the active view mode directly. No-op when unchanged. This is the RAW
    /// write <c>GanttChart</c>'s own reconcile-commit phase uses once its CLAIM/reconcile
    /// pipeline has already decided a mode change is happening — never a public entry
    /// point (design spec Phase 3, T4): a caller that wants to CHANGE the mode calls
    /// <see cref="SetViewMode"/> instead, which routes through that same pipeline
    /// rather than writing here directly. Named distinctly from the public
    /// <see cref="SetViewMode"/> (not an overload — same one-argument shape would
    /// collide) specifically so the two can never be confused for one another at a
    /// call site.
    /// </summary>
    internal void CommitViewMode(GanttViewMode mode)
    {
        if (_viewMode == mode) return;
        _viewMode = mode;
        RaiseChanged();
    }

    /// <summary>Sets the materialized date window. No-op when unchanged. <c>internal</c> (design spec Phase 3, T4 — see the class remarks).</summary>
    internal void SetVisibleRange(DateTime start, DateTime end) => SetVisibleRange(new GanttDateRange(start, end));

    /// <summary>Sets the materialized date window. No-op when unchanged. <c>internal</c> (design spec Phase 3, T4 — see the class remarks).</summary>
    internal void SetVisibleRange(GanttDateRange range)
    {
        if (_visibleRange == range) return;
        _visibleRange = range;
        RaiseChanged();
    }

    /// <summary>True when the given task/row id is currently collapsed.</summary>
    public bool IsCollapsed(string taskId) => _collapsed.Contains(taskId);

    /// <summary>Sets or clears the collapsed state for a task/row id. No-op when unchanged. <c>internal</c> (design spec Phase 3, T4 — see the class remarks).</summary>
    internal void SetCollapsed(string taskId, bool collapsed)
    {
        var changed = collapsed ? _collapsed.Add(taskId) : _collapsed.Remove(taskId);
        if (changed) RaiseChanged();
    }

    /// <summary>Flips the collapsed state for a task/row id. Always raises <see cref="Changed"/> (a toggle is never a no-op). <c>internal</c> (design spec Phase 3, T4 — see the class remarks).</summary>
    internal void ToggleCollapsed(string taskId) => SetCollapsed(taskId, !IsCollapsed(taskId));

    /// <summary>True when the given task/row id is currently selected.</summary>
    public bool IsSelected(string taskId) => _selectedIds.Contains(taskId);

    /// <summary>
    /// Replaces the selected-id set wholesale. No-ops (and does not raise
    /// <see cref="Changed"/>) when value-equal (same set membership, order-independent)
    /// to the current one — the same discipline <see cref="SetTasks"/> applies, so a
    /// caller that re-applies its own current selection every render (T6's own two-way
    /// binding, once it exists) doesn't spuriously notify. <c>internal</c> — see the
    /// class remarks (T6 owns the write side).
    /// </summary>
    internal void SetSelectedIds(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var next = new HashSet<string>(ids, StringComparer.Ordinal);
        if (next.SetEquals(_selectedIds)) return;
        _selectedIds.Clear();
        foreach (var id in next) _selectedIds.Add(id);
        RaiseChanged();
    }

    /// <summary>Sets or clears the selected state for one task/row id. No-op when unchanged. <c>internal</c> (design spec Phase 3, T4 — see the class remarks).</summary>
    internal void SetSelected(string taskId, bool selected)
    {
        var changed = selected ? _selectedIds.Add(taskId) : _selectedIds.Remove(taskId);
        if (changed) RaiseChanged();
    }

    /// <summary>Flips the selected state for one task/row id. Always raises <see cref="Changed"/>. <c>internal</c> (design spec Phase 3, T4 — see the class remarks).</summary>
    internal void ToggleSelected(string taskId) => SetSelected(taskId, !IsSelected(taskId));

    // ── Attach/detach contract (design spec Phase 3, T4) ────────────────────────
    //
    // WHO attaches, WHEN: the GanttV3 component that was handed this instance via
    // its `State` parameter, once, from its own OnInitialized (a component reads
    // `State` at mount only — see GanttChart's own remarks for why re-binding `State`
    // to a DIFFERENT instance mid-lifetime is out of T4's scope). A component that
    // owns its OWN internal GanttState (State left null) never attaches at all.
    //
    // SECOND ATTACH: replaces the weak reference outright — last attacher wins.
    // Neither attacher errors. The superseded host is not disposed or notified; it
    // simply stops being the target for ScrollToDateAsync/ScrollToTaskAsync/
    // SetViewMode. It keeps participating in `Changed` exactly like every other
    // subscriber (Attach and event subscription are two independent mechanisms —
    // see Detach's own remarks) — so a chart that lost the "active" attachment
    // still visually reflects every state change, it just cannot be the one an
    // imperative scroll/view-mode call physically targets. This mirrors the ONE
    // scroll target REUI's own hoisting model implies: there is exactly one DOM
    // viewport to scroll at a time, so "who currently owns imperative routing" is
    // necessarily singular even when `Changed` fan-out is not.
    //
    // AFTER DETACH: TryGetHost reports nothing attached; every imperative method
    // below is a documented NO-OP (not a throw, not a queue — see
    // ScrollToDateAsync's own remarks for why).
    //
    // NO ATTACHMENT AT ALL (a GanttState constructed and used before anything ever
    // renders against it, or after the sole attached component tore down with
    // nothing replacing it): identical no-op contract.

    /// <summary>
    /// Attaches <paramref name="host"/> as the target for this state's imperative
    /// scroll/view-mode API. Held via a WEAK reference deliberately (see the private
    /// field's own remarks): GanttState is designed to potentially outlive any one
    /// component rendering against it, and a strong reference here would pin a
    /// disposed component in memory for as long as the state itself lives. A later
    /// call REPLACES whatever was attached before — see the class-level "Second
    /// attach" remarks above. <c>internal</c>: called by a GanttV3 component's own
    /// mount logic, never by consumer code.
    /// </summary>
    internal void Attach(IGanttStateHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = new WeakReference<IGanttStateHost>(host);
    }

    /// <summary>
    /// Detaches <paramref name="host"/> if — and only if — it is STILL the currently
    /// attached one. A no-op otherwise: a component superseded by a later
    /// <see cref="Attach"/> must not clear the NEWER host's registration on its own
    /// disposal (the "component disposed and replaced" case — see the class-level
    /// remarks). Called unconditionally from a hoisted component's own
    /// <c>IDisposable.Dispose</c>, so teardown is IMMEDIATE and does not wait on GC to
    /// notice the weak reference has gone stale — the weak reference is a safety net
    /// against a host that is garbage-collected without <c>Dispose</c> ever running
    /// (should not happen in Blazor's ordinary component lifecycle, but costs nothing
    /// to guard against), not the primary teardown mechanism; explicit Detach is.
    /// Together the two guarantee a disposed component can neither be resurrected
    /// (nothing re-attaches it — Attach is the only way back in, and only a NEW call
    /// performs that) nor leak (Detach drops the strong local var each caller held;
    /// the weak field itself was never a strong reference to begin with).
    /// </summary>
    internal void Detach(IGanttStateHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (_host is not null && _host.TryGetTarget(out var current) && ReferenceEquals(current, host))
            _host = null;
    }

    private bool TryGetHost(out IGanttStateHost host)
    {
        if (_host is not null && _host.TryGetTarget(out var h))
        {
            host = h;
            return true;
        }
        host = null!;
        return false;
    }

    // ── Imperative API (design spec Phase 3, T4 — the REUI apiRef analog) ───────

    /// <summary>
    /// Scrolls the attached component's timeline to center on <paramref name="date"/>,
    /// by routing through its EXISTING scroll-intent interop (<c>GanttChart</c>'s own
    /// <c>EmitScrollIntent</c> + <c>GanttTimeline</c>'s
    /// <c>ScrollToTodayRequestId</c>/<c>ScrollTargetDate</c> pipeline — no second
    /// implementation; see <see cref="IGanttStateHost.ScrollToDateAsync"/>'s own
    /// remarks).
    ///
    /// No-attachment contract: a NO-OP (a completed <see cref="Task"/>), not a throw
    /// and not a queued request. Chosen to match this class's own established
    /// idempotent-no-op convention (every mutator above is a silent no-op when there
    /// is nothing to do) rather than surprising a caller with an exception for what is
    /// very often a benign timing gap — a page that holds a hoisted <see
    /// cref="GanttState"/> in a field can legitimately call this before its chart has
    /// mounted, or after the chart it was pointed at has been removed from the tree
    /// with nothing replacing it (see the attach/detach remarks). A THROWING or
    /// QUEUEING contract was considered and rejected: throwing would make a purely
    /// timing-dependent condition a hard error for callers that have no reliable way
    /// to know "is anything currently attached" ahead of time without this class
    /// growing Blazor-shaped lifecycle awareness it deliberately does not have (see
    /// the class remarks); queueing would require this otherwise-lifetime-agnostic
    /// store to buffer and replay requests against a FUTURE, not-yet-known attachment,
    /// a stateful complexity nothing in the T4 scope asks for.
    ///
    /// Awaitable: returns the attached host's own dispatch <see cref="Task"/>
    /// unchanged (see <see cref="IGanttStateHost"/>'s own remarks), so a caller that
    /// awaits this observes genuine completion, not fire-and-forget.
    /// </summary>
    public Task ScrollToDateAsync(DateTime date) =>
        TryGetHost(out var host) ? host.ScrollToDateAsync(date) : Task.CompletedTask;

    /// <summary>
    /// Scrolls the attached component's timeline to center on <paramref name="taskId"/>'s
    /// temporal MIDPOINT (<c>Start + (End - Start) / 2</c> — the same "center of a
    /// span" convention <c>GanttChart.VisibleCenterDate</c> already applies to the whole
    /// visible range, applied here to one task's own span instead; for a milestone,
    /// where <c>End == Start</c>, this reduces to exactly <c>Start</c>), by resolving
    /// the task from the CURRENT <see cref="Tasks"/> and delegating to
    /// <see cref="ScrollToDateAsync"/> — see its own remarks for the
    /// no-attachment/thread-safety contract, both inherited unchanged.
    ///
    /// A no-op (a completed <see cref="Task"/>) when no task with <paramref
    /// name="taskId"/> exists in <see cref="Tasks"/> — the SAME silent-no-op
    /// convention as a missing attachment (see <see cref="ScrollToDateAsync"/>'s own
    /// remarks): a caller that raced a task removal against its own scroll request
    /// (e.g. a "jump to task" button whose target was just deleted by someone else)
    /// gets a no-op, not an exception for a condition it cannot reliably prevent.
    /// </summary>
    public Task ScrollToTaskAsync(string taskId)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        GanttTask? task = null;
        foreach (var t in _tasks)
        {
            if (t.Id != taskId) continue;
            task = t;
            break;
        }
        if (task is null) return Task.CompletedTask;
        var span = task.End - task.Start;
        return ScrollToDateAsync(task.Start + new TimeSpan(span.Ticks / 2));
    }

    /// <summary>
    /// Requests a view-mode change on the attached component (design spec Phase 3, T4
    /// — the REUI apiRef <c>setViewMode</c> analog), routed through its CLAIM-based
    /// view-mode ownership model (PR #382) rather than mutating <see cref="ViewMode"/>
    /// directly — see <see cref="IGanttStateHost.SetViewModeAsync"/> and
    /// <c>GanttChart.ApplyViewModeIntentAsync</c>'s own remarks for why a direct write
    /// here would reopen the exact class of bug PR #382 spent 8 review rounds
    /// closing, and for which <c>GanttViewModeSource</c> this claims with (a new
    /// <c>Imperative</c> source, treated like <c>Toolbar</c> for the
    /// controlled-consumer echo).
    ///
    /// Deliberately FIRE-AND-FORGET (no "Async" suffix, unlike
    /// <see cref="ScrollToDateAsync"/>/<see cref="ScrollToTaskAsync"/> above, and no
    /// <see cref="Task"/> return) — matches the plan's own naming
    /// (<c>ScrollToTaskAsync</c>/<c>ScrollToDateAsync</c>/<c>SetViewMode</c>, only the
    /// first two "Async"-suffixed) and this class's OWN OnThemeChanged-style
    /// dispatch-and-forget precedent elsewhere in this codebase (<c>GanttChart.OnThemeChanged</c>):
    /// a mode request is a "please switch to X" command, not an operation whose
    /// individual completion a caller is expected to gate further work on — the
    /// resulting mode is instead observable through <see cref="ViewMode"/> itself
    /// (poll it, or subscribe to <see cref="Changed"/>) exactly like every other
    /// externally-driven mutation of this shared store. Internally still dispatches
    /// through the attached host's own <c>InvokeAsync</c> (thread/render-safe per the
    /// same contract every method here has); the Task it returns is simply not
    /// surfaced to this method's own caller.
    ///
    /// No-attachment contract: identical no-op (see <see cref="ScrollToDateAsync"/>'s
    /// own remarks) — silently does nothing.
    /// </summary>
    public void SetViewMode(GanttViewMode mode)
    {
        if (TryGetHost(out var host)) _ = host.SetViewModeAsync(mode);
    }

    private void RaiseChanged() => Changed?.Invoke();

    private static bool TasksEqual(IReadOnlyList<GanttTask> a, IReadOnlyList<GanttTask> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            // Bug fix (CodeRabbit review): GanttTask is a record, so plain `!=`
            // uses its compiler-generated Equals — value-based for every
            // property EXCEPT Dependencies, which is string[]? (arrays compare
            // by REFERENCE, not content, even inside a record's own Equals).
            // Two structurally-identical but freshly-allocated Dependencies
            // arrays (a common shape for a caller re-materializing its Tasks
            // list every render) would make otherwise-identical tasks compare
            // unequal here, spuriously raising Changed on every such render;
            // conversely, an array the caller mutates IN PLACE (same
            // reference, different contents) would compare equal and silently
            // skip a real update. Compare Dependencies by sequence content
            // explicitly, then diff everything ELSE via a `with`-neutralized
            // copy — deliberately NOT a hand-listed field comparison, so this
            // stays automatically correct if GanttTask (shipped API; not
            // touched here) ever gains another property.
            if (!DependenciesEqual(a[i].Dependencies, b[i].Dependencies)) return false;
            if (a[i] with { Dependencies = null } != b[i] with { Dependencies = null }) return false;
        }
        return true;
    }

    private static bool DependenciesEqual(string[]? a, string[]? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
        return true;
    }
}
