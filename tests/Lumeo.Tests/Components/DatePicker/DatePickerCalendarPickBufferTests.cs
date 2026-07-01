using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// Codex P2 — a calendar pick must refresh the typeable input buffer.
///
/// With AllowKeyboardInput on and uncommitted draft text in the textbox, clicking a
/// calendar day set Value + _lastPushed and raised ValueChanged but never updated
/// _inputBuffer. For a controlled parent that echoes the value back, OnParametersSet
/// then recognises its own push (_lastPushed) and skips the buffer sync, so the
/// textbox kept showing the stale draft even though the bound date had changed.
/// HandleDateSelected (and the preset / clear paths) now refresh _inputBuffer on pick.
/// </summary>
public class DatePickerCalendarPickBufferTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerCalendarPickBufferTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string Format = "yyyy-MM-dd";

    [Fact]
    public void Calendar_Pick_Refreshes_Typed_Buffer_When_Controlled()
    {
        DateOnly? pushed = null;
        // A delegate makes this controlled (ValueChanged.HasDelegate == true) — the path
        // whose _lastPushed echo-skip used to leave the buffer stale.
        var cb = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? v) => pushed = v);

        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.Format, Format)
            .Add(c => c.Culture, CultureInfo.InvariantCulture)
            .Add(c => c.AllowKeyboardInput, true)
            .Add(c => c.Value, new DateOnly(2026, 6, 10))
            .Add(c => c.ValueChanged, cb));

        // 1. User types draft text into the textbox but never commits it (no blur).
        cut.Find("input").Input("draft-not-committed");

        // 2. Open the calendar and click the 20th (unique day number in the June grid).
        cut.Find("button[aria-label='Open calendar']").Click();
        cut.FindAll("button").First(b => b.TextContent.Trim() == "20").Click();

        // The picked date is pushed up...
        Assert.Equal(new DateOnly(2026, 6, 20), pushed);
        // ...and the textbox now shows the formatted picked date, NOT the stale draft.
        Assert.Equal("2026-06-20", cut.Find("input").GetAttribute("value"));
    }
}
