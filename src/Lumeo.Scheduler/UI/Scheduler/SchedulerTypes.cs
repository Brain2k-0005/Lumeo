namespace Lumeo;

/// <summary>
/// A calendar/scheduler event. Used as the data contract between Blazor and the
/// FullCalendar JS wrapper.
/// </summary>
/// <param name="Id">Stable identifier used to reconcile edits back into the caller's collection.</param>
/// <param name="Title">The label rendered on the event chip.</param>
/// <param name="Start">Start timestamp (inclusive).</param>
/// <param name="End">End timestamp (exclusive, per FullCalendar convention).</param>
/// <param name="AllDay">When true the event is rendered in the all-day lane.</param>
/// <param name="Color">CSS color or variable reference, e.g. "var(--color-primary)".</param>
/// <param name="Url">Optional link opened on click instead of firing OnEventClick.</param>
/// <param name="ExtendedProps">Arbitrary app-level metadata round-tripped through the JS layer.</param>
/// <param name="DaysOfWeek">
/// For recurring events: the days of the week on which the event repeats.
/// Uses FullCalendar's free simple recurrence model (no premium rrule plugin required).
/// e.g. [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday].
/// When set, <see cref="Start"/> and <see cref="End"/> provide the start/end times
/// (time-of-day part only) and <see cref="RecurrenceEnd"/> bounds the recurrence.
/// </param>
/// <param name="RecurrenceEnd">
/// Optional end date for a recurring event. The event will not appear after this date.
/// Only meaningful when <see cref="DaysOfWeek"/> is set.
/// </param>
/// <param name="ExceptionDates">
/// Dates on which a recurring event should be skipped (one-off exclusions).
/// Only meaningful when <see cref="DaysOfWeek"/> is set.
/// </param>
/// <param name="ResourceId">
/// Optional resource identifier. When a <c>Resources</c> list is provided on the
/// <see cref="Scheduler"/> component, events are color-coded by their resource and
/// a resource legend is rendered above the calendar.
/// </param>
/// <param name="ClassNames">
/// Extra CSS class names to apply to the event chip, as produced by the
/// <c>EventClassNames</c> callback on the Scheduler component (or set directly here).
/// </param>
public record SchedulerEvent(
    string Id,
    string Title,
    DateTime Start,
    DateTime End,
    bool AllDay = false,
    string? Color = null,
    string? Url = null,
    Dictionary<string, object>? ExtendedProps = null,
    IReadOnlyList<DayOfWeek>? DaysOfWeek = null,
    DateTime? RecurrenceEnd = null,
    IReadOnlyList<DateTime>? ExceptionDates = null,
    string? ResourceId = null,
    string? ClassNames = null
)
{
    // Trim safety: this record is deserialized from JS (JsOnEventClick/JsOnEventChange
    // [JSInvokable] parameters). JSRuntime's reflection-based serializer must never bind
    // the positional ctor — the trimmer strips its parameter names
    // ("ConstructorContainsNullParameterNames", crashes the component under a trimmed
    // publish). With this parameterless ctor STJ uses property-based (de)serialization
    // instead. Do not remove.
    public SchedulerEvent() : this("", "", default, default) { }

    /// <summary>
    /// Optional structured recurrence rule (wave-1 RRULE subset: <c>FREQ=DAILY|WEEKLY|MONTHLY</c>,
    /// <c>INTERVAL</c>, <c>COUNT</c>, <c>UNTIL</c>, <c>BYDAY</c>). When set, this takes precedence
    /// over the legacy <see cref="DaysOfWeek"/>/<see cref="RecurrenceEnd"/> pair — both express the
    /// same underlying recurrence engine (<c>SchedulerRecurrenceExpander</c> translates
    /// <see cref="DaysOfWeek"/>+<see cref="RecurrenceEnd"/> into the equivalent
    /// <see cref="SchedulerRecurrenceRule"/> at its own boundary when <see cref="Recurrence"/> is
    /// unset), so setting both is redundant rather than contradictory — but is still flagged via a
    /// debug-build assertion (not a thrown exception, matching this repo's "warn, don't crash the
    /// render" posture) since it usually indicates the caller meant to pick one.
    /// <see cref="ExceptionDates"/> continues to double as this rule's <c>EXDATE</c> list.
    /// </summary>
    /// <remarks>
    /// Deliberately declared as a body property (<c>{ get; init; }</c>), NOT appended to the
    /// primary constructor's positional parameter list, even though the parameter-list shape is
    /// how earlier design notes for this feature sketched it. Appending it positionally would
    /// change the compiler-synthesized <c>Deconstruct</c>/constructor signature that
    /// <c>PublicAPI.Shipped.txt</c> already pins for this shipped record — CONTRIBUTING.md's
    /// "Never delete or change the signature of a shipped public member directly" rule — forcing
    /// an unnecessary <c>[Obsolete]</c> deprecation cycle for a purely-additive feature. A body
    /// property adds a new getter/init member without touching anything already shipped: every
    /// existing call site (positional or named) keeps compiling and behaving identically, and
    /// object-initializer / <c>with</c>-expression syntax (<c>ev with { Recurrence = rule }</c>)
    /// already reads/writes it like any other property. The ONE synthesized record member this
    /// does NOT flow into is <c>Deconstruct</c> — that method (and the primary constructor's own
    /// signature) is generated strictly from the primary constructor's parameter list, which is
    /// exactly what stays byte-for-byte unchanged here (verified: adding this property produced
    /// zero new/changed <c>PublicAPI.*.txt</c> entries for either). <c>Equals</c>/
    /// <c>GetHashCode</c>/<c>ToString</c>, by contrast, DO already pick it up automatically —
    /// per the C# record spec, the compiler-synthesized versions of those three compare/print
    /// every instance field the record declares, which includes this auto-property's backing
    /// field, not only the primary constructor's positional ones. So two events differing only
    /// in <see cref="Recurrence"/> are correctly NOT equal and hash differently, with no extra
    /// work needed here — see <c>SchedulerEventRecurrencePropertyTests</c> for the test that
    /// pins this down rather than assuming it. (This built-in record equality is a different
    /// mechanism from <c>Scheduler.razor</c>'s own hand-rolled <c>ComputeEventsHash</c>, which
    /// remains out of this task's scope — see the wave-1a task report.)
    /// </remarks>
    public SchedulerRecurrenceRule? Recurrence { get; init; }
}

/// <summary>
/// A named resource (person, room, equipment) used by the Scheduler for
/// color-coding events. Does not require the FullCalendar Premium resource plugin.
/// </summary>
/// <param name="Id">Identifier matched against <see cref="SchedulerEvent.ResourceId"/>.</param>
/// <param name="Title">Display name shown in the resource legend.</param>
/// <param name="Color">
/// CSS color applied to events belonging to this resource when the event itself
/// does not supply its own <see cref="SchedulerEvent.Color"/>.
/// </param>
public record SchedulerResource(string Id, string Title, string? Color = null);

/// <summary>
/// A date range produced when the user drag-selects in the calendar.
/// </summary>
public record SchedulerDateRange(DateTime Start, DateTime End, bool AllDay)
{
    // Trim safety: this record is deserialized from JS (JsOnDateSelect [JSInvokable]
    // parameter). See SchedulerEvent's parameterless ctor above. Do not remove.
    public SchedulerDateRange() : this(default, default, false) { }
}

/// <summary>
/// Which rendering engine <see cref="Scheduler"/> uses.
/// </summary>
public enum SchedulerEngine
{
    /// <summary>
    /// The FullCalendar-backed wrapper. The default, and unchanged from every previous release —
    /// an existing consumer that never sets <c>Engine</c> renders byte-identically.
    /// </summary>
    FullCalendar,

    /// <summary>
    /// Lumeo's own Blazor views (<c>SchedulerMonthView</c>, <c>SchedulerTimeGridView</c>,
    /// <c>SchedulerAgendaView</c>) — no third-party calendar JS at all.
    /// <para>
    /// Opt-in on purpose. The two engines are not pixel-identical and do not support exactly the
    /// same parameter set, so switching is a decision a consumer makes deliberately rather than
    /// inherits from an upgrade. What the first-party engine adds: it honours
    /// <see cref="SchedulerEvent.Recurrence"/> (the wrapper ignores it entirely), and it is the
    /// only path to the week-number, weekend-hiding, live-announcement and resource features.
    /// </para>
    /// <para>
    /// Not carried over yet: <c>SlotDuration</c>, and the JS-side imperative navigation the
    /// wrapper exposes. Those are listed on <see cref="Scheduler.Engine"/> so the gap is visible
    /// before the switch, not after.
    /// </para>
    /// </summary>
    FirstParty,
}

/// <summary>
/// Built-in views exposed by the Lumeo scheduler. Maps onto FullCalendar's
/// dayGridMonth / timeGridWeek / timeGridDay / listWeek view names.
/// </summary>
public enum SchedulerView
{
    Month,
    Week,
    Day,
    List
}
