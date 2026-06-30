using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// Regression tests for two bug patterns fixed in DatePicker:
///
/// Pattern A — Controlled-component rollback: when a controlled parent rejects a calendar
/// pick (fires HandleDateSelected/HandlePresetSelected/HandleClear but then re-renders with
/// the original Value), the typeable input buffer must roll back to show the bound value.
/// Before the fix, HandleDateSelected did not update _lastPushed before calling ValueChanged,
/// so OnParametersSet saw Value == _lastSyncedValue and skipped the resync, leaving the
/// input buffer at stale in-flight text.
///
/// Pattern B — FormField splat-id override: inside a FormField the &lt;label for&gt; points
/// at the generated EffectiveTriggerId, but a consumer-splatted id in AdditionalAttributes
/// rendered AFTER the generated id and silently won, causing the label's `for` to target a
/// non-existent element. DatePicker now strips the splatted id via EffectiveAttributes when
/// InsideFormField, mirroring the fix already applied to Input.
/// </summary>
public class DatePickerBugPatternTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerBugPatternTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ────────────────────────────────────────────────────────────────────────
    // Pattern A — controlled-component rollback
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Core regression: the user types "draft" into the AllowKeyboardInput field without
    /// committing, then clicks a calendar day. HandleDateSelected fires and — with the fix —
    /// sets _lastPushed = pickedDate BEFORE calling ValueChanged. The controlled parent then
    /// re-renders with the original (rejected) Value. Because pickedDate != oldDate, the
    /// OnParametersSet controlled branch adopts the parent's value and the buffer rolls back
    /// to the properly-formatted old date.
    ///
    /// WITHOUT the fix HandleDateSelected never updated _lastSyncedValue (the old guard), so
    /// OnParametersSet saw Value == _lastSyncedValue == oldDate and silently SKIPPED the
    /// resync, leaving the buffer frozen at the stale "draft" text.
    /// </summary>
    [Fact]
    public void Controlled_Rejection_After_Calendar_Pick_Rolls_Back_Input_Buffer()
    {
        var oldDate = new DateOnly(2024, 3, 15);

        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.AllowKeyboardInput, true)
            .Add(c => c.Format, "yyyy-MM-dd")
            .Add(c => c.Culture, CultureInfo.InvariantCulture)
            .Add(c => c.Value, oldDate)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(
                this, _ => { /* controlled parent: reject by never updating Value */ })));

        // Verify initial state.
        Assert.Equal("2024-03-15", cut.Find("input[type='text']").GetAttribute("value"));

        // User types partial text without committing.
        // _inputBuffer is now "draft" but _lastPushed remains oldDate.
        cut.Find("input[type='text']").Input("draft");
        Assert.Equal("draft", cut.Find("input[type='text']").GetAttribute("value"));

        // Click the calendar icon button to open the popover.
        cut.Find("button[aria-label='Open calendar']").Click();

        // Click a calendar day — triggers HandleDateSelected which (with the fix)
        // sets _lastPushed = pickedDate BEFORE calling ValueChanged.
        // Layout: [0]=icon-btn [1]=prev-nav [2]=next-nav [3+]=day-buttons
        var allButtons = cut.FindAll("button[type='button']");
        if (allButtons.Count > 5)
        {
            try { allButtons[5].Click(); }   // a mid-page day, always enabled (no min/max)
            catch { allButtons[3].Click(); } // fallback: first available day
        }

        // Simulate the controlled parent re-rendering with the rejected (original) value.
        // In real Blazor this happens synchronously inside the ValueChanged.InvokeAsync call;
        // in bUnit we do it explicitly after the calendar-click event has been processed.
        cut.Render(p => p
            .Add(c => c.Value, oldDate)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(
                this, _ => { })));

        // With the fix: _lastPushed == pickedDate != oldDate → controlled branch adopts
        // → _inputBuffer = FormatBuffer(oldDate) = "2024-03-15".
        // Without the fix: _lastSyncedValue == oldDate == Value → branch skipped
        // → _inputBuffer remains "draft" (the bug).
        Assert.Equal("2024-03-15", cut.Find("input[type='text']").GetAttribute("value"));
    }

    /// <summary>
    /// Commit + rejection: user types a date and blurs to commit it. The commit path
    /// (CommitBufferAsync) sets _lastPushed = parsed BEFORE calling ValueChanged, so a
    /// parent rejection that comes back with the old Value is correctly detected
    /// (Value != _lastPushed) and rolls the buffer back.
    /// </summary>
    [Fact]
    public void Controlled_Rejection_After_Typed_Commit_Rolls_Back_Input_Buffer()
    {
        var oldDate = new DateOnly(2024, 3, 15);

        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.AllowKeyboardInput, true)
            .Add(c => c.Format, "yyyy-MM-dd")
            .Add(c => c.Culture, CultureInfo.InvariantCulture)
            .Add(c => c.Value, oldDate)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(
                this, _ => { /* reject */ })));

        // Type + blur → commits "2024-06-01", sets _lastPushed = 2024-06-01.
        var input = cut.Find("input[type='text']");
        input.Input("2024-06-01");
        input.Blur();

        // Parent rejects and re-renders with the original value.
        cut.Render(p => p
            .Add(c => c.Value, oldDate)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(
                this, _ => { })));

        // Buffer must roll back to the bound date.
        Assert.Equal("2024-03-15", cut.Find("input[type='text']").GetAttribute("value"));
    }

    /// <summary>
    /// Echo suppression: when the parent ACCEPTS the edit and echoes the same Value back
    /// (the normal @bind round-trip), the controlled branch recognises incoming == _lastPushed
    /// and does NOT clobber the committed text. This guards against accidental re-formatting
    /// that could disturb the caret in a live editor.
    /// </summary>
    [Fact]
    public void Controlled_Echo_Of_Own_Commit_Does_Not_Disturb_Input_Buffer()
    {
        DateOnly? parentBound = null;

        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.AllowKeyboardInput, true)
            .Add(c => c.Format, "yyyy-MM-dd")
            .Add(c => c.Culture, CultureInfo.InvariantCulture)
            .Add(c => c.Value, parentBound)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(
                this, v => { parentBound = v; })));

        // Type + blur to commit — fires ValueChanged → parentBound = 2024-06-01.
        var input = cut.Find("input[type='text']");
        input.Input("2024-06-01");
        input.Blur();

        // Simulate the parent echoing the accepted value back (@bind auto-sync).
        cut.Render(p => p
            .Add(c => c.Value, parentBound)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(
                this, v => { parentBound = v; })));

        // Echo must NOT clobber the buffer — it should show the committed date unchanged.
        Assert.Equal("2024-06-01", cut.Find("input[type='text']").GetAttribute("value"));
    }

    /// <summary>
    /// Programmatic parent reset: when the parent pushes a Value genuinely different from
    /// what was last pushed, the controlled branch must adopt it and resync the buffer.
    /// </summary>
    [Fact]
    public void Controlled_Parent_Programmatic_Reset_Resyncs_Buffer()
    {
        var initial = new DateOnly(2024, 1, 1);

        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.AllowKeyboardInput, true)
            .Add(c => c.Format, "yyyy-MM-dd")
            .Add(c => c.Culture, CultureInfo.InvariantCulture)
            .Add(c => c.Value, initial)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, _ => { })));

        Assert.Equal("2024-01-01", cut.Find("input[type='text']").GetAttribute("value"));

        // Parent programmatically resets to a different date.
        var reset = new DateOnly(2025, 12, 31);
        cut.Render(p => p
            .Add(c => c.Value, reset)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, _ => { })));

        Assert.Equal("2025-12-31", cut.Find("input[type='text']").GetAttribute("value"));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Pattern B — FormField splat-id override
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inside a FormField, the DatePicker trigger button adopts the generated ControlId as
    /// its id so the field's &lt;label for&gt; resolves to it. A consumer-splatted id in
    /// AdditionalAttributes must be stripped (via EffectiveAttributes) so the generated id
    /// wins, preventing a broken label-for association (Codex P2).
    /// </summary>
    [Fact]
    public void Consumer_Splatted_Id_Inside_FormField_Does_Not_Override_Trigger_Id()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Departure date");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DatePicker>(0);
                b.AddAttribute(1, "AllowKeyboardInput", false);
                b.AddAttribute(2, "AdditionalAttributes",
                    new Dictionary<string, object> { ["id"] = "consumer-trigger-id" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // The trigger button must carry the FormField-generated id, NOT "consumer-trigger-id".
        var trigger = cut.Find("button[type='button']");
        var triggerId = trigger.GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");

        Assert.False(string.IsNullOrEmpty(triggerId), "trigger button must have a non-empty id");
        Assert.Equal(labelFor, triggerId);
        Assert.NotEqual("consumer-trigger-id", triggerId);
    }

    /// <summary>
    /// Guard: outside a FormField the splatted id is NOT stripped — it reaches the trigger
    /// unchanged so standalone consumers can still set a custom id.
    /// </summary>
    [Fact]
    public void Consumer_Splatted_Id_Outside_FormField_Reaches_Trigger()
    {
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.AllowKeyboardInput, false)
            .Add(c => c.AdditionalAttributes,
                new Dictionary<string, object> { ["id"] = "standalone-trigger-id" }));

        var trigger = cut.Find("button[type='button']");
        Assert.Equal("standalone-trigger-id", trigger.GetAttribute("id"));
    }
}
