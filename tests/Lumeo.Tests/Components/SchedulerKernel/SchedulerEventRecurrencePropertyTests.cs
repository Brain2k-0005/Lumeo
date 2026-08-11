using Xunit;

namespace Lumeo.Tests.Components.SchedulerKernel;

/// <summary>
/// Proves — rather than only asserting in a code comment — the "purely additive" claim on
/// <see cref="SchedulerEvent.Recurrence"/> (spec §4.1, and the wave-1a task's own explicit
/// requirement: "it must be purely additive so no existing consumer breaks"). See
/// <c>SchedulerTypes.cs</c>'s remarks on <c>Recurrence</c> for why it's a record-body property
/// rather than a 14th primary-constructor parameter.
/// </summary>
public class SchedulerEventRecurrencePropertyTests
{
    private static readonly DateTime Start = new(2026, 8, 10, 9, 0, 0);
    private static readonly DateTime End = new(2026, 8, 10, 10, 0, 0);

    [Fact]
    public void Existing_13Arg_Positional_Construction_Still_Compiles_And_Runs_Unchanged()
    {
        // Exactly the shape every pre-existing call site (docs samples, consumer code) uses —
        // proves the primary constructor's arity/order is untouched.
        var ev = new SchedulerEvent(
            "id", "title", Start, End, AllDay: false, Color: "red", Url: null,
            ExtendedProps: null, DaysOfWeek: null, RecurrenceEnd: null,
            ExceptionDates: null, ResourceId: null, ClassNames: "cls");

        Assert.Null(ev.Recurrence);
    }

    [Fact]
    public void Recurrence_Defaults_To_Null()
    {
        var ev = new SchedulerEvent("id", "title", Start, End);
        Assert.Null(ev.Recurrence);
    }

    [Fact]
    public void Recurrence_Is_Settable_Via_ObjectInitializer_And_With_Expression()
    {
        var rule = new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily);

        var viaInitializer = new SchedulerEvent("id", "title", Start, End) { Recurrence = rule };
        Assert.Same(rule, viaInitializer.Recurrence);

        var viaWith = viaInitializer with { Recurrence = null };
        Assert.Null(viaWith.Recurrence);
        Assert.Same(rule, viaInitializer.Recurrence); // original untouched — `with` doesn't mutate.
    }

    [Fact]
    public void Deconstruct_Signature_Is_Unchanged_By_The_New_Property()
    {
        // The pre-existing 13-value Deconstruct call shape must still compile and populate every
        // field correctly — Recurrence being a body property (not positional) is exactly what
        // keeps this working without a deprecation cycle.
        var ev = new SchedulerEvent("id", "title", Start, End, ClassNames: "cls")
        {
            Recurrence = new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Weekly)
        };

        var (id, title, start, end, allDay, color, url, extendedProps, daysOfWeek, recurrenceEnd, exceptionDates, resourceId, classNames) = ev;

        Assert.Equal("id", id);
        Assert.Equal("title", title);
        Assert.Equal(Start, start);
        Assert.Equal(End, end);
        Assert.False(allDay);
        Assert.Null(color);
        Assert.Null(url);
        Assert.Null(extendedProps);
        Assert.Null(daysOfWeek);
        Assert.Null(recurrenceEnd);
        Assert.Null(exceptionDates);
        Assert.Null(resourceId);
        Assert.Equal("cls", classNames);
    }

    [Fact]
    public void Equality_And_HashCode_Automatically_Include_Recurrence_Even_Though_It_Is_Not_A_Positional_Member()
    {
        // Per the C# record spec, the compiler-synthesized Equals/GetHashCode/ToString compare
        // and print EVERY instance field a record declares — including an auto-property's
        // backing field, whether or not that property is one of the primary constructor's
        // positional parameters. Only Deconstruct (and the primary constructor's own signature)
        // is generated strictly from the positional parameter list; see SchedulerTypes.cs's
        // remarks on Recurrence and Deconstruct_Signature_Is_Unchanged_By_The_New_Property above.
        // So two events differing ONLY in Recurrence are correctly NOT equal and hash
        // differently — no extra hand-rolled comparison logic is needed for THIS (built-in
        // record) equality mechanism. (Scheduler.razor's own separate, hand-rolled
        // ComputeEventsHash is a different mechanism entirely and is out of this task's scope —
        // see the wave-1a task report.)
        var a = new SchedulerEvent("id", "title", Start, End) { Recurrence = new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily) };
        var b = new SchedulerEvent("id", "title", Start, End) { Recurrence = new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Weekly) };
        var c = new SchedulerEvent("id", "title", Start, End) { Recurrence = new SchedulerRecurrenceRule(SchedulerRecurrenceFrequency.Daily) };

        Assert.NotEqual(a, b);
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());

        // Same Recurrence value (by record equality, not reference) -> equal.
        Assert.Equal(a, c);
        Assert.Equal(a.GetHashCode(), c.GetHashCode());
    }
}
