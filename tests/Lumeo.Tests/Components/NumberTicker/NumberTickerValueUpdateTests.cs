using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.NumberTicker;

/// <summary>
/// Regression coverage for a reported bug: "NumberTicker ignores Value updates
/// when the element id is reused — a consumer had to force @key remounts to
/// get updates to take effect." Investigated at the library level: the
/// suspected mechanism (the JS tick registration keying on the element id and
/// skipping re-init for a "known" id) does NOT reproduce.
/// motion.js's tickNumber always cancels the prior rAF and re-registers
/// unconditionally on every call — there is no id-based "already known, skip"
/// guard anywhere in the JS or the C# OnAfterRenderAsync path. This test
/// asserts a plain Value change on the SAME (never re-keyed) component
/// instance re-ticks correctly with the right from/to pair, guarding against
/// a regression of the behavior that was reported broken but is not.
/// </summary>
public class NumberTickerValueUpdateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _motionModule;

    public NumberTickerValueUpdateTests()
    {
        _ctx.AddLumeoServices();
        _motionModule = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        _motionModule.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Value_Change_On_The_Same_Un_Keyed_Instance_Re_Ticks_With_Correct_From_To()
    {
        var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
            .Add(n => n.Value, 10)
            .Add(n => n.StartValue, 0));

        var tickCallsBefore = _motionModule.Invocations.Count(i => i.Identifier == "motion.tickNumber");
        Assert.Equal(1, tickCallsBefore); // initial tick, StartValue -> Value

        cut.Render(p => p.Add(n => n.Value, 50));

        var tickInvocations = _motionModule.Invocations.Where(i => i.Identifier == "motion.tickNumber").ToList();
        Assert.Equal(2, tickInvocations.Count); // a SECOND tick call, no remount needed
        var second = tickInvocations[1];
        Assert.Equal(10d, (double)second.Arguments[1]!); // from = last displayed value
        Assert.Equal(50d, (double)second.Arguments[2]!); // to = new Value
    }

    [Fact]
    public void Repeated_Value_Changes_Each_Re_Tick_From_The_Previous_Target()
    {
        var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
            .Add(n => n.Value, 10)
            .Add(n => n.StartValue, 0));

        cut.Render(p => p.Add(n => n.Value, 50));
        cut.Render(p => p.Add(n => n.Value, 75));

        var tickInvocations = _motionModule.Invocations.Where(i => i.Identifier == "motion.tickNumber").ToList();
        Assert.Equal(3, tickInvocations.Count);
        Assert.Equal(50d, (double)tickInvocations[2].Arguments[1]!); // from = the previous target
        Assert.Equal(75d, (double)tickInvocations[2].Arguments[2]!); // to = the new Value
    }
}
