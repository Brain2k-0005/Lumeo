using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.UploadTrigger;

/// <summary>
/// Triage #70 (medium, state-on-data-change) — re-selecting the SAME file in the
/// native picker never re-fired <c>OnFilesSelected</c> because the underlying
/// <c>&lt;input type="file"&gt;</c> value was never reset. The browser only raises
/// <c>change</c> when the chosen path differs from the input's current value, so an
/// identical second pick was silently swallowed. Unlike FileUpload (whose
/// accumulating item list incidentally masks the stale-value bug), UploadTrigger is
/// a pure pick-trigger with no list, so it must reset the element after every pick.
///
/// bUnit can't drive a real native file picker or observe the DOM's
/// <c>el.value = ''</c>, so the testable seam is the .NET wiring: after raising
/// <c>OnFilesSelected</c>, <c>HandleChange</c> must call the interop reset. These
/// tests drive the real <see cref="InputFile.OnChange"/> callback the component
/// wires up and assert on the recorded <c>ResetFileInput</c> calls captured by
/// <see cref="TrackingInteropService"/>.
///
/// Without the fix (<c>HandleChange</c> only awaits <c>OnFilesSelected.InvokeAsync</c>)
/// <c>ResetFileInputCallCount</c> stays 0 and these tests fail; with the fix it is
/// incremented once per picker confirmation.
/// </summary>
public class UploadTriggerReselectSameFileTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public UploadTriggerReselectSameFileTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task Picking_A_File_Resets_The_Native_Input_So_The_Same_File_Can_Be_Re_Picked()
    {
        var cut = _ctx.Render<L.UploadTrigger>();

        // Nothing picked yet — no reset should have happened.
        Assert.Equal(0, _interop.ResetFileInputCallCount);

        var file = new FakeBrowserFile { Name = "report.pdf" };
        await RaiseInputFileChange(cut, file);

        // After the pick the component must clear the input's value so the
        // browser will fire `change` again for an identical subsequent selection.
        Assert.Equal(1, _interop.ResetFileInputCallCount);
    }

    [Fact]
    public async Task Re_Selecting_The_Same_File_Fires_OnFilesSelected_Again()
    {
        var picks = 0;
        var cut = _ctx.Render<L.UploadTrigger>(p => p
            .Add(t => t.OnFilesSelected, EventCallback.Factory.Create<InputFileChangeEventArgs>(
                this, _ => picks++)));

        var file = new FakeBrowserFile { Name = "same.pdf" };

        // First pick of the file.
        await RaiseInputFileChange(cut, file);
        Assert.Equal(1, picks);
        Assert.Equal(1, _interop.ResetFileInputCallCount);

        // The user re-picks the IDENTICAL file. Because the prior pick reset the
        // input's value, the change handler runs again and OnFilesSelected re-fires.
        await RaiseInputFileChange(cut, file);
        Assert.Equal(2, picks);
        Assert.Equal(2, _interop.ResetFileInputCallCount);
    }

    // ── Fake IBrowserFile ────────────────────────────────────────────────────
    private sealed class FakeBrowserFile : IBrowserFile
    {
        public string Name { get; init; } = "test.txt";
        public DateTimeOffset LastModified { get; init; } = DateTimeOffset.UtcNow;
        public long Size { get; init; } = 100;
        public string ContentType { get; init; } = "text/plain";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => new MemoryStream(new byte[Size]);
    }

    // ── Helpers (mirror UploadTriggerBehaviorTests) ──────────────────────────

    /// <summary>
    /// Drives the real <see cref="InputFile.OnChange"/> callback that the
    /// component binds to its private <c>HandleChange</c> handler, exercising the
    /// actual <c>OnChange="HandleChange"</c> wiring and the post-callback reset.
    /// </summary>
    private static async Task RaiseInputFileChange(
        IRenderedComponent<L.UploadTrigger> cut, params IBrowserFile[] files)
    {
        var onChange = cut.FindComponent<InputFile>().Instance.OnChange;
        var args = CreateInputFileChangeEventArgs(files);
        await cut.InvokeAsync(() => onChange.InvokeAsync(args));
    }

    private static InputFileChangeEventArgs CreateInputFileChangeEventArgs(params IBrowserFile[] files)
    {
        // InputFileChangeEventArgs has a non-public constructor taking the file list.
        var allFlags = System.Reflection.BindingFlags.NonPublic
                      | System.Reflection.BindingFlags.Public
                      | System.Reflection.BindingFlags.Instance;

        var ctors = typeof(InputFileChangeEventArgs).GetConstructors(allFlags);
        var filesList = (IReadOnlyList<IBrowserFile>)files.ToList().AsReadOnly();

        foreach (var ctor in ctors)
        {
            var ps = ctor.GetParameters();
            if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(filesList.GetType()))
                return (InputFileChangeEventArgs)ctor.Invoke(new object[] { filesList });
        }

        foreach (var ctor in ctors)
        {
            if (ctor.GetParameters().Length == 1)
            {
                try { return (InputFileChangeEventArgs)ctor.Invoke(new object[] { filesList }); }
                catch { /* try next */ }
            }
        }

        throw new InvalidOperationException(
            "Cannot construct InputFileChangeEventArgs. Available constructors: "
            + string.Join(", ", ctors.Select(c =>
                string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name)))));
    }
}
