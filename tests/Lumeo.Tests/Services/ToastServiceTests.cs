using Xunit;
using Lumeo.Services;

namespace Lumeo.Tests.Services;

public class ToastServiceTests
{
    private readonly ToastService _service = new();

    // --- Show ---

    [Fact]
    public void Show_Fires_OnShow_With_Correct_Title()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Show("Hello");

        Assert.NotNull(received);
        Assert.Equal("Hello", received!.Title);
    }

    [Fact]
    public void Show_Fires_OnShow_With_Correct_Description()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Show("Title", "Some description");

        Assert.NotNull(received);
        Assert.Equal("Some description", received!.Description);
    }

    [Fact]
    public void Show_Fires_OnShow_With_Default_Variant()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Show("Title");

        Assert.NotNull(received);
        Assert.Equal(ToastVariant.Default, received!.Variant);
    }

    [Fact]
    public void Show_Fires_OnShow_With_Specified_Variant()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Show("Title", null, ToastVariant.Success);

        Assert.NotNull(received);
        Assert.Equal(ToastVariant.Success, received!.Variant);
    }

    // --- Null description ---

    [Fact]
    public void Show_With_Null_Description_Does_Not_Throw()
    {
        _service.OnShow += _ => { };
        _service.Show("Title", null);
    }

    [Fact]
    public void Show_With_Null_Description_Sets_Description_To_Null()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Show("Title");

        Assert.NotNull(received);
        Assert.Null(received!.Description);
    }

    // --- No subscribers ---

    [Fact]
    public void Show_Without_Subscribers_Does_Not_Throw()
    {
        // No subscribers added — should not throw
        _service.Show("Title", "Description", ToastVariant.Default);
    }

    [Fact]
    public void Convenience_Methods_Without_Subscribers_Do_Not_Throw()
    {
        _service.Success("Title");
        _service.Error("Title");
        _service.Warning("Title");
        _service.Info("Title");
    }

    // --- Success convenience method ---

    [Fact]
    public void Success_Fires_OnShow_With_Success_Variant()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Success("Done!");

        Assert.NotNull(received);
        Assert.Equal("Done!", received!.Title);
        Assert.Equal(ToastVariant.Success, received!.Variant);
    }

    [Fact]
    public void Success_Passes_Description()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Success("Done!", "All good");

        Assert.NotNull(received);
        Assert.Equal("All good", received!.Description);
    }

    // --- Error convenience method ---

    [Fact]
    public void Error_Fires_OnShow_With_Destructive_Variant()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Error("Failed!");

        Assert.NotNull(received);
        Assert.Equal("Failed!", received!.Title);
        Assert.Equal(ToastVariant.Destructive, received!.Variant);
    }

    [Fact]
    public void Error_Passes_Description()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Error("Failed!", "Something went wrong");

        Assert.NotNull(received);
        Assert.Equal("Something went wrong", received!.Description);
    }

    // --- Warning convenience method ---

    [Fact]
    public void Warning_Fires_OnShow_With_Warning_Variant()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Warning("Watch out!");

        Assert.NotNull(received);
        Assert.Equal("Watch out!", received!.Title);
        Assert.Equal(ToastVariant.Warning, received!.Variant);
    }

    [Fact]
    public void Warning_Passes_Description()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Warning("Watch out!", "Disk almost full");

        Assert.NotNull(received);
        Assert.Equal("Disk almost full", received!.Description);
    }

    // --- Info convenience method ---

    [Fact]
    public void Info_Fires_OnShow_With_Info_Variant()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Info("FYI");

        Assert.NotNull(received);
        Assert.Equal("FYI", received!.Title);
        Assert.Equal(ToastVariant.Info, received!.Variant);
    }

    [Fact]
    public void Info_Passes_Description()
    {
        ToastMessage? received = null;
        _service.OnShow += msg => received = msg;

        _service.Info("FYI", "Just so you know");

        Assert.NotNull(received);
        Assert.Equal("Just so you know", received!.Description);
    }

    // --- Multiple subscribers ---

    [Fact]
    public void Show_Notifies_All_Subscribers()
    {
        int callCount = 0;
        _service.OnShow += _ => callCount++;
        _service.OnShow += _ => callCount++;

        _service.Show("Multi");

        Assert.Equal(2, callCount);
    }

    // --- ToastMessage record ---

    [Fact]
    public void ToastMessage_Record_Properties_Match()
    {
        var msg = new ToastMessage("Title", "Desc", ToastVariant.Info);

        Assert.Equal("Title", msg.Title);
        Assert.Equal("Desc", msg.Description);
        Assert.Equal(ToastVariant.Info, msg.Variant);
    }

    [Fact]
    public void ToastVariant_Has_All_Expected_Values()
    {
        var values = Enum.GetValues<ToastVariant>();
        Assert.Contains(ToastVariant.Default, values);
        Assert.Contains(ToastVariant.Success, values);
        Assert.Contains(ToastVariant.Destructive, values);
        Assert.Contains(ToastVariant.Warning, values);
        Assert.Contains(ToastVariant.Info, values);
    }
}
