using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.UploadTrigger;

public class UploadTriggerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public UploadTriggerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_A_Hidden_File_Input_Inside_A_Label()
    {
        var cut = _ctx.Render<L.UploadTrigger>();
        Assert.NotNull(cut.Find("label"));
        var input = cut.Find("input[type='file']");
        Assert.Contains("sr-only", input.GetAttribute("class"));
    }

    [Fact]
    public void Multiple_Is_Forwarded_To_The_Input()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p.Add(u => u.Multiple, true));
        Assert.True(cut.Find("input[type='file']").HasAttribute("multiple"));
    }

    [Fact]
    public void Accept_Filter_Is_Forwarded_To_The_Input()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p.Add(u => u.Accept, "image/*"));
        Assert.Equal("image/*", cut.Find("input[type='file']").GetAttribute("accept"));
    }

    [Fact]
    public void Disabled_Is_Forwarded_To_The_Input()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p.Add(u => u.Disabled, true));
        Assert.True(cut.Find("input[type='file']").HasAttribute("disabled"));
    }
}
