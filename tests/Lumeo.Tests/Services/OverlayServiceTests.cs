using Lumeo.Services;
using Xunit;

namespace Lumeo.Tests.Services;

public class OverlayServiceTests
{
    [Fact]
    public void Close_With_Result_Fires_OnClose_With_Cancelled_False()
    {
        var service = new OverlayService();
        string? receivedId = null;
        object? receivedResult = null;
        bool? receivedCancelled = null;

        service.OnClose += (id, result, cancelled) =>
        {
            receivedId = id;
            receivedResult = result;
            receivedCancelled = cancelled;
        };

        service.Close("overlay-1", "my-result");

        Assert.Equal("overlay-1", receivedId);
        Assert.Equal("my-result", receivedResult);
        Assert.False(receivedCancelled);
    }

    [Fact]
    public void Close_With_Null_Result_Fires_OnClose_With_Cancelled_False()
    {
        var service = new OverlayService();
        object? receivedResult = "sentinel";
        bool? receivedCancelled = null;

        service.OnClose += (id, result, cancelled) =>
        {
            receivedResult = result;
            receivedCancelled = cancelled;
        };

        service.Close("overlay-1", null);

        Assert.Null(receivedResult);
        Assert.False(receivedCancelled); // Close(null) is NOT a cancel
    }

    [Fact]
    public void Cancel_Fires_OnClose_With_Cancelled_True()
    {
        var service = new OverlayService();
        bool? receivedCancelled = null;
        object? receivedResult = "sentinel";

        service.OnClose += (id, result, cancelled) =>
        {
            receivedResult = result;
            receivedCancelled = cancelled;
        };

        service.Cancel("overlay-1");

        Assert.Null(receivedResult);
        Assert.True(receivedCancelled);
    }

    [Fact]
    public void OverlayResult_Ok_Has_Correct_Properties()
    {
        var result = OverlayResult.Ok("data");

        Assert.False(result.Cancelled);
        Assert.Equal("data", result.Data);
        Assert.Equal("data", result.GetData<string>());
    }

    [Fact]
    public void OverlayResult_Ok_With_Null_Is_Not_Cancelled()
    {
        var result = OverlayResult.Ok(null);

        Assert.False(result.Cancelled);
        Assert.Null(result.Data);
    }

    [Fact]
    public void OverlayResult_CancelResult_Has_Correct_Properties()
    {
        var result = OverlayResult.CancelResult();

        Assert.True(result.Cancelled);
        Assert.Null(result.Data);
    }

    [Fact]
    public void ShowDialogAsync_Fires_OnShow_With_Instance()
    {
        var service = new OverlayService();
        OverlayInstance? capturedInstance = null;

        service.OnShow += instance => capturedInstance = instance;

        // Start the task (don't await — Tcs is never resolved in this unit test)
        _ = service.ShowDialogAsync<Microsoft.AspNetCore.Components.ComponentBase>("Test");

        Assert.NotNull(capturedInstance);
        Assert.Equal(OverlayType.Dialog, capturedInstance!.Type);
        Assert.Equal("Test", capturedInstance.Title);
    }
}
