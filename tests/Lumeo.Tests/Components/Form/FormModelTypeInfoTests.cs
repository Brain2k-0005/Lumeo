using System.Text.Json.Serialization;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Form;

/// <summary>
/// PR #364 review: without <see cref="L.Form{TModel}.ModelTypeInfo"/>, the init/ResetValues
/// snapshot round-trip is reflection-based. <c>TModel</c> itself carries
/// <c>[DynamicallyAccessedMembers(All)]</c>, but that does NOT cascade into the types of
/// <c>TModel</c>'s own nested reference-type properties — those can still be trimmed away in
/// a published-trimmed app, silently dropping nested values on <c>ResetValues()</c>.
/// <see cref="L.Form{TModel}.ModelTypeInfo"/> lets a consumer supply their own source-generated
/// <see cref="JsonTypeInfo{T}"/> (covering the whole object graph) to close that gap. These
/// tests cover the new parameter end-to-end (wired through Serialize/Deserialize, not just
/// accepted and ignored) — nested member fidelity under actual trimming still requires a
/// published-trimmed reference-app check, not something a unit test can observe.
/// </summary>
public partial class FormModelTypeInfoTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FormModelTypeInfoTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class Address
    {
        public string? Street { get; set; }
    }

    private sealed class ProfileModel
    {
        public string? Name { get; set; }
        public Address? Address { get; set; }
    }

    [JsonSerializable(typeof(ProfileModel))]
    private partial class ProfileModelContext : JsonSerializerContext
    {
    }

    [Fact]
    public async Task ResetValues_With_ModelTypeInfo_Restores_Nested_Property()
    {
        var model = new ProfileModel { Name = "Ann", Address = new Address { Street = "Main St" } };

        var cut = _ctx.Render<L.Form<ProfileModel>>(p => p
            .Add(f => f.Model, model)
            .Add(f => f.ModelTypeInfo, ProfileModelContext.Default.ProfileModel)
            .AddChildContent("<button type=\"submit\">go</button>"));

        model.Name = "changed";
        model.Address!.Street = "changed";

        await cut.InvokeAsync(() => cut.Instance.ResetValues());

        Assert.Equal("Ann", model.Name);
        Assert.Equal("Main St", model.Address!.Street);
    }

    [Fact]
    public async Task ResetValues_Without_ModelTypeInfo_Still_Restores_Flat_And_Nested_Property()
    {
        // Reflection fallback (ModelTypeInfo unset) is not trimmed in this test process,
        // so it still works here — this guards the fallback path stays functionally
        // unchanged, not that it's trim-safe (see class doc).
        var model = new ProfileModel { Name = "Ann", Address = new Address { Street = "Main St" } };

        var cut = _ctx.Render<L.Form<ProfileModel>>(p => p
            .Add(f => f.Model, model)
            .AddChildContent("<button type=\"submit\">go</button>"));

        model.Name = "changed";
        model.Address!.Street = "changed";

        await cut.InvokeAsync(() => cut.Instance.ResetValues());

        Assert.Equal("Ann", model.Name);
        Assert.Equal("Main St", model.Address!.Street);
    }

    [Fact]
    public async Task ResetValues_Recaptures_Snapshot_When_ModelTypeInfo_Arrives_On_The_Same_Model_Instance()
    {
        // PR #366 review: OnParametersSet used to compare only the Model REFERENCE, so a parent
        // that supplies ModelTypeInfo after the first render (keeping the same Model instance)
        // never triggered a re-capture — ResetValues() kept restoring the stale reflection-based
        // baseline from init instead of a fresh snapshot taken once the trim-safe context arrived.
        var model = new ProfileModel { Name = "Ann", Address = new Address { Street = "Main St" } };

        var cut = _ctx.Render<L.Form<ProfileModel>>(p => p
            .Add(f => f.Model, model)
            .AddChildContent("<button type=\"submit\">go</button>"));

        // Same instance, mutated before ModelTypeInfo shows up.
        model.Name = "Bob";

        cut.Render(p => p
            .Add(f => f.Model, model)
            .Add(f => f.ModelTypeInfo, ProfileModelContext.Default.ProfileModel)
            .AddChildContent("<button type=\"submit\">go</button>"));

        // Further mutation after the (expected) re-capture point.
        model.Name = "Carol";

        await cut.InvokeAsync(() => cut.Instance.ResetValues());

        // Must restore to "Bob" (the state at ModelTypeInfo-arrival recapture), not "Ann"
        // (the stale init snapshot) and not "Carol" (never captured).
        Assert.Equal("Bob", model.Name);
        Assert.Equal("Main St", model.Address!.Street);
    }
}
