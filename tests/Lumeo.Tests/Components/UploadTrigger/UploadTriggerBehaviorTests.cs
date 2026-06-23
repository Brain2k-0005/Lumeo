using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using L = Lumeo;

namespace Lumeo.Tests.Components.UploadTrigger;

/// <summary>
/// Behavior/contract tests for <see cref="L.UploadTrigger"/>. The component's
/// whole reason to exist is to wrap an <see cref="InputFile"/> in a button-styled
/// label and re-raise the native picker's change as <c>OnFilesSelected</c>. These
/// tests drive the real <see cref="InputFile.OnChange"/> callback the component
/// wires up (<c>OnChange="HandleChange"</c>) and assert the consumer callback
/// fires with the selected files, plus that the picker-shaping attributes
/// (Multiple / Accept / Disabled) reach the underlying input element.
/// </summary>
public class UploadTriggerBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public UploadTriggerBehaviorTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

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

    // ── Core: the callback fires when files are provided ─────────────────────

    [Fact]
    public async Task Selecting_Files_Fires_OnFilesSelected_With_The_Picked_Files()
    {
        InputFileChangeEventArgs? received = null;

        var cut = _ctx.Render<L.UploadTrigger>(p => p
            .Add(t => t.OnFilesSelected, EventCallback.Factory.Create<InputFileChangeEventArgs>(
                this, e => received = e)));

        var file = new FakeBrowserFile { Name = "report.pdf", Size = 2048, ContentType = "application/pdf" };
        await RaiseInputFileChange(cut, file);

        Assert.NotNull(received);
        Assert.Equal(1, received!.FileCount);
        Assert.Equal("report.pdf", received.File.Name);
    }

    [Fact]
    public async Task OnFilesSelected_Forwards_All_Files_When_Multiple_Selected()
    {
        IReadOnlyList<IBrowserFile>? files = null;

        var cut = _ctx.Render<L.UploadTrigger>(p => p
            .Add(t => t.Multiple, true)
            .Add(t => t.OnFilesSelected, EventCallback.Factory.Create<InputFileChangeEventArgs>(
                this, e => files = e.GetMultipleFiles(10))));

        var a = new FakeBrowserFile { Name = "a.png", ContentType = "image/png" };
        var b = new FakeBrowserFile { Name = "b.png", ContentType = "image/png" };
        await RaiseInputFileChange(cut, a, b);

        Assert.NotNull(files);
        Assert.Equal(2, files!.Count);
        Assert.Equal(new[] { "a.png", "b.png" }, files.Select(f => f.Name));
    }

    [Fact]
    public async Task Selecting_Files_Does_Not_Throw_When_No_Callback_Wired()
    {
        // OnFilesSelected defaults to an unset EventCallback. A consumer that only
        // wants the pick affordance (no handler yet) must not crash when the user
        // confirms the picker — InvokeAsync on an unset callback is a no-op.
        var cut = _ctx.Render<L.UploadTrigger>();

        var file = new FakeBrowserFile { Name = "noop.txt" };
        await RaiseInputFileChange(cut, file); // should complete without throwing

        Assert.NotNull(cut.Find("input[type='file']"));
    }

    // ── Picker-shaping params reach the real input element ───────────────────

    [Fact]
    public void Multiple_And_Accept_Are_Forwarded_Together_To_The_Input()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p
            .Add(t => t.Multiple, true)
            .Add(t => t.Accept, ".pdf,.docx"));

        var input = cut.Find("input[type='file']");
        Assert.True(input.HasAttribute("multiple"));
        Assert.Equal(".pdf,.docx", input.GetAttribute("accept"));
    }

    [Fact]
    public void Disabled_Marks_The_Input_And_Disables_Pointer_On_The_Label()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p.Add(t => t.Disabled, true));

        Assert.True(cut.Find("input[type='file']").HasAttribute("disabled"));

        // The label is the click target; when disabled it must not be clickable.
        var labelClass = cut.Find("label").GetAttribute("class") ?? "";
        Assert.Contains("pointer-events-none", labelClass);
        Assert.Contains("cursor-not-allowed", labelClass);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded_To_The_Input()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p
            .Add(t => t.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "replace-doc"
            }));

        var input = cut.Find("input[type='file']");
        Assert.Equal("replace-doc", input.GetAttribute("data-testid"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Drives the real <see cref="InputFile.OnChange"/> callback that the
    /// component binds to its private <c>HandleChange</c> handler. Invoking the
    /// component-supplied callback (rather than reaching into the private method)
    /// exercises the actual <c>OnChange="HandleChange"</c> wiring and the
    /// <c>OnFilesSelected</c> re-raise.
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
