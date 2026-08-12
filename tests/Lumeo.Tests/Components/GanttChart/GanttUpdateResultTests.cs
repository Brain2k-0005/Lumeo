using Lumeo.GanttV3;
using Xunit;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Pure-type coverage for <see cref="GanttUpdateResult"/>/<see cref="GanttUpdateAdjustment"/> —
/// the Phase-4 reject/accept/accept-with-adjustment <c>OnTaskUpdate</c> commit gate. Mirrors
/// <c>SchedulerDropResultTests</c> (<c>tests/Lumeo.Tests/Components/Scheduler/</c>) by design —
/// see <see cref="GanttUpdateResult"/>'s own remarks for why the two types share one shape.
/// </summary>
public class GanttUpdateResultTests
{
    [Fact]
    public void Reject_Is_Not_Accepted_And_Has_No_Adjustment()
    {
        var result = GanttUpdateResult.Reject;

        Assert.False(result.Accepted);
        Assert.Null(result.Adjustment);
    }

    [Fact]
    public void Accept_Is_Accepted_With_No_Adjustment()
    {
        var result = GanttUpdateResult.Accept;

        Assert.True(result.Accepted);
        Assert.Null(result.Adjustment);
    }

    [Fact]
    public void AcceptWith_Carries_The_Adjustment()
    {
        var adjustment = new GanttUpdateAdjustment(Start: new DateTime(2026, 3, 20));
        var result = GanttUpdateResult.AcceptWith(adjustment);

        Assert.True(result.Accepted);
        Assert.Equal(adjustment, result.Adjustment);
    }

    [Fact]
    public void Implicit_Bool_True_Conversion_Keeps_The_Trivial_Accept_Case_Simple()
    {
        // Concrete proof of the "just let it through stays trivial" design goal —
        // OnTaskUpdate="update => true" compiles directly against the
        // Func<GanttTaskUpdate, GanttUpdateResult> parameter type via this conversion.
        GanttUpdateResult result = true;

        Assert.True(result.Accepted);
        Assert.Null(result.Adjustment);
    }

    [Fact]
    public void Implicit_Bool_False_Conversion_Rejects()
    {
        GanttUpdateResult result = false;

        Assert.False(result.Accepted);
    }

    [Fact]
    public void A_Rejected_Result_Discards_Any_Adjustment_Even_If_One_Were_Constructible()
    {
        // AcceptWith always sets Accepted=true by construction, so the only way to reach a
        // rejected-with-adjustment state would be a private-constructor bypass — this test
        // pins the invariant at the public surface: Reject's Adjustment is always null.
        Assert.Null(GanttUpdateResult.Reject.Adjustment);
    }

    [Fact]
    public void Equality_Compares_Accepted_And_Adjustment()
    {
        var a = GanttUpdateResult.AcceptWith(new GanttUpdateAdjustment(Progress: 50));
        var b = GanttUpdateResult.AcceptWith(new GanttUpdateAdjustment(Progress: 50));
        var c = GanttUpdateResult.AcceptWith(new GanttUpdateAdjustment(Progress: 51));

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(GanttUpdateResult.Accept, GanttUpdateResult.Reject);
    }

    [Fact]
    public void Adjustment_Null_Members_Mean_Keep_The_Proposed_Value()
    {
        // GanttUpdateAdjustment's own contract (see its remarks): a null Start/End/Progress
        // leaves the corresponding proposed value untouched. Pinned here as a pure-type fact
        // (leaving the Start/End application itself to GanttChartRenderTests's end-to-end
        // coverage) — every member defaults to null via the primary-constructor defaults.
        var adjustment = new GanttUpdateAdjustment();

        Assert.Null(adjustment.Start);
        Assert.Null(adjustment.End);
        Assert.Null(adjustment.Progress);
    }
}
