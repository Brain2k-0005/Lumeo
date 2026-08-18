namespace Lumeo;

/// <summary>
/// A calendar/scheduler event — the data contract every Scheduler view renders from.
/// </summary>
/// <param name="Id">Stable identifier used to reconcile edits back into the caller's collection.</param>
/// <param name="Title">The label rendered on the event chip.</param>
/// <param name="Start">Start timestamp (inclusive).</param>
/// <param name="End">End timestamp (exclusive: an event ending 10:00 does not occupy the 10:00 slot).</param>
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
    /// The <see cref="SchedulerCalendar.Id"/> this event belongs to, or null when it belongs to
    /// none. Null and the empty string are DIFFERENT: a blank id matches a calendar declared with
    /// one, while null matches no calendar at all and is therefore never hidden by a visibility
    /// toggle — the same distinction <see cref="ResourceId"/> draws.
    /// </summary>
    /// <remarks>
    /// A body property rather than a positional parameter, for the reason spelled out on
    /// <see cref="Recurrence"/> below: appending to the primary constructor changes the
    /// compiler-synthesized Deconstruct/constructor signature that callers may already depend on.
    /// </remarks>
    public string? CalendarId { get; init; }

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
/// One named calendar an event can belong to — "Team", "Personal", a room's booking feed.
///
/// <para>
/// Distinct from <see cref="SchedulerResource"/> on purpose, because they answer different
/// questions. A RESOURCE is who or what an event consumes, and the resource views lay one out
/// per lane; a CALENDAR is which feed an event came from, and it decides whether the event is
/// shown at all. An event can have both: Alice's booking (resource) from the Team calendar.
/// </para>
/// </summary>
/// <param name="Id">Matches <see cref="SchedulerEvent.CalendarId"/>. A blank id is legitimate
/// and means the same as any other id — it is NOT "no calendar".</param>
/// <param name="Name">Shown on the visibility chip.</param>
/// <param name="Color">Colour for this calendar's events, used when neither the event nor its
/// resource supplies one.</param>
/// <param name="Visible">Initial visibility. The user's own toggling is component state from
/// then on, so re-rendering with the same list does not undo it.</param>
public record SchedulerCalendar(string Id, string Name, string? Color = null, bool Visible = true);

/// <summary>How several visible calendars share the space.</summary>
public enum SchedulerPaneMode
{
    /// <summary>All visible calendars in ONE view, drawn over each other. The default, and what
    /// the component did before calendars existed.</summary>
    Overlay,

    /// <summary>One view per visible calendar, side by side. Answers "what does each look like on
    /// its own" — the question overlaying cannot.</summary>
    SideBySide,
}

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
/// Built-in views exposed by the Lumeo scheduler, each rendered by a first-party
/// Blazor view component — no third-party calendar library is involved.
/// </summary>
public enum SchedulerView
{
    Month,
    Week,
    Day,
    List,

    /// <summary>
    /// One column per entry in <c>Scheduler.Resources</c>, for a single day. Requires
    /// <c>Resources</c> to be non-empty; without it the view falls back to <see cref="Day"/>.
    /// </summary>
    Resource,

    /// <summary>
    /// A rolling window of <c>Scheduler.VisibleDays</c> days starting at the current date —
    /// ReUI's "N-day" view. Unlike <see cref="Week"/> it does not align to a week start, so
    /// "the next three days" stays the next three days as you page through it. Set
    /// <c>Scheduler.VisibleDays</c> to choose the width.
    /// </summary>
    MultiDay,

    /// <summary>
    /// One ROW per entry in <c>Scheduler.Resources</c> with the time axis running
    /// horizontally across days, weeks or months — a resource timeline.
    /// <para>
    /// Not a variant of <see cref="Resource"/>: that lays out a single day with a column per
    /// resource and a vertical clock ("who is where today"), while this answers "how is this
    /// resource booked over the coming stretch". Requires <c>Resources</c>; without it the
    /// view falls back to <see cref="Day"/>. Added last so the existing values keep their
    /// numeric identity.
    /// </para>
    /// </summary>
    Timeline,
}
