using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Pure-type coverage for <see cref="L.SchedulerDropResult"/>/<see cref="L.SchedulerDropAdjustment"/>
/// — the widened reject/accept/accept-with-adjustment <c>CanDrop</c> contract (this task's
/// priority-1 item: widening it now, while it is still <c>PublicAPI.Unshipped</c>, before it
/// ships and becomes a permanent breaking-change window like Gantt v3's <c>OnTaskUpdate</c>).
/// </summary>
public class SchedulerDropResultTests
{
    [Fact]
    public void Reject_Is_Not_Accepted_And_Has_No_Adjustment()
    {
        var result = L.SchedulerDropResult.Reject;

        Assert.False(result.Accepted);
        Assert.Null(result.Adjustment);
    }

    [Fact]
    public void Accept_Is_Accepted_With_No_Adjustment()
    {
        var result = L.SchedulerDropResult.Accept;

        Assert.True(result.Accepted);
        Assert.Null(result.Adjustment);
    }

    [Fact]
    public void AcceptWith_Carries_The_Adjustment()
    {
        var adjustment = new L.SchedulerDropAdjustment(Start: new DateTime(2026, 3, 20, 9, 0, 0));
        var result = L.SchedulerDropResult.AcceptWith(adjustment);

        Assert.True(result.Accepted);
        Assert.Equal(adjustment, result.Adjustment);
    }

    [Fact]
    public void Implicit_Bool_True_Conversion_Keeps_The_Trivial_Allow_Case_Simple()
    {
        // This is the concrete proof of the "just allow it stays trivial" design goal —
        // a bare `true`/`false` still converts, so CanDrop="(ev, ctx) => true" (the common
        // case) needed ZERO changes when CanDrop's return type widened from bool.
        L.SchedulerDropResult result = true;

        Assert.True(result.Accepted);
        Assert.Null(result.Adjustment);
    }

    [Fact]
    public void Implicit_Bool_False_Conversion_Rejects()
    {
        L.SchedulerDropResult result = false;

        Assert.False(result.Accepted);
    }

    [Fact]
    public void A_Rejected_Result_Discards_Any_Adjustment_Even_If_One_Were_Constructible()
    {
        // AcceptWith always sets Accepted=true by construction, so the only way to reach a
        // rejected-with-adjustment state would be a private-constructor bypass — this test
        // pins the invariant at the public surface: Reject's Adjustment is always null.
        Assert.Null(L.SchedulerDropResult.Reject.Adjustment);
    }

    [Fact]
    public void Equality_Compares_Accepted_And_Adjustment()
    {
        var a = L.SchedulerDropResult.AcceptWith(new L.SchedulerDropAdjustment(AllDay: true));
        var b = L.SchedulerDropResult.AcceptWith(new L.SchedulerDropAdjustment(AllDay: true));
        var c = L.SchedulerDropResult.AcceptWith(new L.SchedulerDropAdjustment(AllDay: false));

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(L.SchedulerDropResult.Accept, L.SchedulerDropResult.Reject);
    }
}
