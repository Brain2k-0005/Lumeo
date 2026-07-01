using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.IO;

namespace Lumeo.Tests.Components.FileUpload;

// ── Fake IBrowserFile (file-local; the other FileUpload test files have their
//    own file-scoped copies) ────────────────────────────────────────────────
file sealed class FakeBrowserFile : IBrowserFile
{
    public string Name { get; init; } = "test.txt";
    public DateTimeOffset LastModified { get; init; } = DateTimeOffset.UtcNow;
    public long Size { get; init; } = 100;
    public string ContentType { get; init; } = "text/plain";

    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        => new MemoryStream(new byte[Size]);
}

/// <summary>
/// Regression coverage for the two EDGE-DATA FileUpload battle-test bugs:
///
/// (n=35) Thumbnails keyed by file.Name collide / cross-wipe on duplicate
///   filenames. Two selected images with the SAME name shared a single
///   _thumbnailUrls entry, so the second write clobbered the first's preview and
///   removing EITHER row deleted the shared thumbnail for BOTH. Fixed by keying
///   _thumbnailUrls on the per-item Id throughout (write/lookup/remove).
///
/// (n=150) Avatar variant accumulated hidden items and counted them against
///   MaxFiles instead of replacing. The avatar slot only ever renders a single
///   _thumbnailUrl, yet every re-pick APPENDED a fresh item; after enough picks
///   the next selection spuriously tripped the "Maximum N files" rejection.
///   Fixed by clearing the prior slot (items/thumbnails/cancellations) before
///   adding the new file when Variant==Avatar.
/// </summary>
public class FileUploadEdgeDataRegressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileUploadEdgeDataRegressionTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ── n=35: duplicate-filename thumbnails don't collide / cross-wipe ────────

    /// <summary>
    /// Two image files with the IDENTICAL name selected into a Dropzone with
    /// ShowThumbnails. Under the old file.Name keying the dictionary held a single
    /// shared entry, so only one preview could exist. With the fix each item keys
    /// its own thumbnail by Id, so BOTH rows render a thumbnail &lt;img&gt; and the
    /// internal dictionary holds two distinct entries.
    /// </summary>
    [Fact]
    public async Task Duplicate_Filename_Thumbnails_Do_Not_Collide()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Multiple, true)
            .Add(b => b.ShowThumbnails, true)
            .Add(b => b.Variant, Lumeo.FileUpload.FileUploadVariant.Dropzone));

        // Same Name, distinct file instances — the realistic duplicate-name case.
        var a = new FakeBrowserFile { Name = "photo.png", Size = 8, ContentType = "image/png" };
        var b = new FakeBrowserFile { Name = "photo.png", Size = 8, ContentType = "image/png" };
        var args = CreateInputFileChangeEventArgs(a, b);

        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        var items = GetUploadItems(cut);
        Assert.Equal(2, items.Count);

        // Each item has its OWN thumbnail entry, keyed by the distinct item.Id.
        var thumbs = GetThumbnailUrls(cut);
        Assert.Equal(2, thumbs.Count);
        Assert.All(items, i => Assert.True(thumbs.ContainsKey(i.Id)));

        // Both rows therefore render a thumbnail <img alt="photo.png"> — the old
        // shared-key behaviour could only ever produce one.
        Assert.Equal(2, cut.FindAll("img[alt='photo.png']").Count);
    }

    /// <summary>
    /// Removing ONE of two same-named items must not wipe the other's thumbnail.
    /// Under file.Name keying, RemoveItemAsync removed the shared dictionary entry,
    /// blanking the surviving row's preview. With Id keying the survivor keeps its
    /// thumbnail.
    /// </summary>
    [Fact]
    public async Task Removing_One_Duplicate_Named_Item_Keeps_The_Others_Thumbnail()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Multiple, true)
            .Add(b => b.ShowThumbnails, true)
            .Add(b => b.Variant, Lumeo.FileUpload.FileUploadVariant.Dropzone));

        var a = new FakeBrowserFile { Name = "dup.png", Size = 8, ContentType = "image/png" };
        var b = new FakeBrowserFile { Name = "dup.png", Size = 8, ContentType = "image/png" };
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, CreateInputFileChangeEventArgs(a, b)));
        cut.Render();

        var items = GetUploadItems(cut);
        Assert.Equal(2, items.Count);
        var survivor = items[1];
        var removed = items[0];

        // Remove the FIRST item. No throw, and the survivor's thumbnail stays.
        var ex = await Record.ExceptionAsync(() =>
            cut.InvokeAsync(async () => await RemoveItem(cut, removed)));
        Assert.Null(ex);
        cut.Render();

        var thumbs = GetThumbnailUrls(cut);
        Assert.False(thumbs.ContainsKey(removed.Id));   // removed entry gone
        Assert.True(thumbs.ContainsKey(survivor.Id));   // survivor's preview intact
        Assert.Single(GetUploadItems(cut));
        Assert.Single(cut.FindAll("img[alt='dup.png']"));
    }

    // ── n=150: Avatar re-pick replaces instead of accumulating ────────────────

    /// <summary>
    /// Re-picking the avatar more times than MaxFiles must NOT trip the MaxFiles
    /// rejection: the avatar slot only shows one preview, so prior items are
    /// cleared on each re-pick. Under the old append-only code the hidden items
    /// piled up and the (MaxFiles+1)-th pick spuriously failed with a global error.
    /// </summary>
    [Fact]
    public async Task Avatar_Repick_Replaces_And_Never_Trips_MaxFiles()
    {
        var rejected = new List<string>();

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Variant, Lumeo.FileUpload.FileUploadVariant.Avatar)
            .Add(b => b.ShowThumbnails, true)
            .Add(b => b.MaxFiles, 1)
            .Add(b => b.OnFileRejected, EventCallback.Factory.Create<(IBrowserFile File, string Reason)>(
                this, t => rejected.Add(t.Reason))));

        // Pick the avatar several times — far more than MaxFiles=1.
        Exception? ex = null;
        for (var i = 0; i < 4; i++)
        {
            var pick = new FakeBrowserFile { Name = $"avatar{i}.png", Size = 8, ContentType = "image/png" };
            var args = CreateInputFileChangeEventArgs(pick);
            ex ??= await Record.ExceptionAsync(() =>
                cut.InvokeAsync(async () => await TriggerFileSelected(cut, args)));
        }
        cut.Render();

        Assert.Null(ex);
        // Exactly one item is ever retained (the latest), so MaxFiles is never hit.
        var items = GetUploadItems(cut);
        Assert.Single(items);
        Assert.Equal("avatar3.png", items[0].Name);
        Assert.Equal(Lumeo.FileUpload.FileUploadStatus.Pending, items[0].Status);

        // No MaxFiles rejection fired and no global error is shown.
        Assert.Empty(rejected);
        Assert.Null(GetGlobalErrorMessage(cut));
    }

    // ── Helpers (mirror FileUploadHighBugRegressionTests) ─────────────────────

    private static InputFileChangeEventArgs CreateInputFileChangeEventArgs(params IBrowserFile[] files)
    {
        var allFlags = System.Reflection.BindingFlags.NonPublic
                      | System.Reflection.BindingFlags.Public
                      | System.Reflection.BindingFlags.Instance;

        var ctors = typeof(InputFileChangeEventArgs).GetConstructors(allFlags);
        var filesList = (IReadOnlyList<IBrowserFile>)files.ToList().AsReadOnly();

        foreach (var ctor in ctors)
        {
            var ps = ctor.GetParameters();
            if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(filesList.GetType()))
            {
                return (InputFileChangeEventArgs)ctor.Invoke(new object[] { filesList });
            }
        }

        foreach (var ctor in ctors)
        {
            var ps = ctor.GetParameters();
            if (ps.Length == 1)
            {
                try { return (InputFileChangeEventArgs)ctor.Invoke(new object[] { filesList }); }
                catch { /* try next */ }
            }
        }

        throw new InvalidOperationException(
            "Cannot construct InputFileChangeEventArgs. Available constructors: "
            + string.Join(", ", ctors.Select(c => string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name)))));
    }

    private static Task TriggerFileSelected(IRenderedComponent<Lumeo.FileUpload> cut, InputFileChangeEventArgs args)
    {
        var method = typeof(Lumeo.FileUpload)
            .GetMethod("HandleFileSelected",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        return (Task)method.Invoke(cut.Instance, new object[] { args })!;
    }

    private static Task RemoveItem(IRenderedComponent<Lumeo.FileUpload> cut, Lumeo.FileUpload.FileUploadItem item)
    {
        var method = typeof(Lumeo.FileUpload)
            .GetMethod("RemoveItemAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        return (Task)method.Invoke(cut.Instance, new object[] { item })!;
    }

    private static List<Lumeo.FileUpload.FileUploadItem> GetUploadItems(IRenderedComponent<Lumeo.FileUpload> cut)
    {
        var field = typeof(Lumeo.FileUpload)
            .GetField("_uploadItems",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (List<Lumeo.FileUpload.FileUploadItem>)field.GetValue(cut.Instance)!;
    }

    private static Dictionary<string, string> GetThumbnailUrls(IRenderedComponent<Lumeo.FileUpload> cut)
    {
        var field = typeof(Lumeo.FileUpload)
            .GetField("_thumbnailUrls",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (Dictionary<string, string>)field.GetValue(cut.Instance)!;
    }

    private static string? GetGlobalErrorMessage(IRenderedComponent<Lumeo.FileUpload> cut)
    {
        var field = typeof(Lumeo.FileUpload)
            .GetField("_globalErrorMessage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (string?)field.GetValue(cut.Instance);
    }
}
