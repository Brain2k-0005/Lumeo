using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toast;

public class ToastTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToastTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ─── Toast component rendering ───────────────────────────────────────────

    [Fact]
    public void Toast_Renders_Div_With_Role_Alert()
    {
        var cut = _ctx.Render<L.Toast>();

        var alert = cut.Find("[role='alert'],[role='status']");
        Assert.NotNull(alert);
    }

    [Fact]
    public void Toast_Default_Variant_Has_Border_And_BgCard()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Variant, ToastVariant.Default));

        var alert = cut.Find("[role='alert'],[role='status']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("bg-card", cls);
        Assert.Contains("text-foreground", cls);
    }

    [Fact]
    public void Toast_Destructive_Variant_Has_Destructive_Classes()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Variant, ToastVariant.Destructive));

        var alert = cut.Find("[role='alert'],[role='status']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("border-destructive", cls);
        Assert.Contains("text-destructive-text", cls);
    }

    [Fact]
    public void Toast_Success_Variant_Has_Success_Classes()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Variant, ToastVariant.Success));

        var alert = cut.Find("[role='alert'],[role='status']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("border-success", cls);
        Assert.Contains("text-success-text", cls);
    }

    [Fact]
    public void Toast_Warning_Variant_Has_Warning_Classes()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Variant, ToastVariant.Warning));

        var alert = cut.Find("[role='alert'],[role='status']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("border-warning", cls);
        Assert.Contains("text-warning-text", cls);
    }

    [Fact]
    public void Toast_Info_Variant_Has_Info_Classes()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Variant, ToastVariant.Info));

        var alert = cut.Find("[role='alert'],[role='status']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("border-info", cls);
        Assert.Contains("text-info-text", cls);
    }

    [Fact]
    public void Toast_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.ChildContent, (RenderFragment)(builder =>
                builder.AddContent(0, "Toast body"))));

        Assert.Contains("Toast body", cut.Markup);
    }

    [Fact]
    public void Toast_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Class, "my-toast"));

        var alert = cut.Find("[role='alert'],[role='status']");
        Assert.Contains("my-toast", alert.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Toast_Alert_Has_Base_Classes()
    {
        var cut = _ctx.Render<L.Toast>();

        var alert = cut.Find("[role='alert'],[role='status']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("rounded-md", cls);
        Assert.Contains("shadow-lg", cls);
        Assert.Contains("border", cls);
    }

    // ─── ToastTitle ─────────────────────────────────────────────────────────

    [Fact]
    public void ToastTitle_Renders_Paragraph_With_Content()
    {
        var cut = _ctx.Render<L.ToastTitle>(p => p
            .Add(b => b.ChildContent, (RenderFragment)(b =>
                b.AddContent(0, "My Title"))));

        var p = cut.Find("p");
        Assert.Contains("My Title", p.TextContent);
    }

    [Fact]
    public void ToastTitle_Has_Font_Semibold_Class()
    {
        var cut = _ctx.Render<L.ToastTitle>();

        var p = cut.Find("p");
        Assert.Contains("font-semibold", p.GetAttribute("class") ?? "");
    }

    // ─── ToastDescription ───────────────────────────────────────────────────

    [Fact]
    public void ToastDescription_Renders_Div_With_Content()
    {
        var cut = _ctx.Render<L.ToastDescription>(p => p
            .Add(b => b.ChildContent, (RenderFragment)(b =>
                b.AddContent(0, "Some description"))));

        Assert.Contains("Some description", cut.Markup);
    }

    [Fact]
    public void ToastDescription_Has_Text_Xs_Class()
    {
        var cut = _ctx.Render<L.ToastDescription>();

        var div = cut.Find("div");
        Assert.Contains("text-xs", div.GetAttribute("class") ?? "");
    }

    // ─── ToastClose ─────────────────────────────────────────────────────────

    [Fact]
    public void ToastClose_Renders_Button()
    {
        var cut = _ctx.Render<L.ToastClose>();

        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void ToastClose_Click_Invokes_OnClose_Callback()
    {
        var called = false;
        var cut = _ctx.Render<L.ToastClose>(p => p
            .Add(b => b.OnClose, EventCallback.Factory.Create(_ctx, () => called = true)));

        cut.Find("button").Click();

        Assert.True(called);
    }

    [Fact]
    public void ToastClose_Has_Absolute_Positioning()
    {
        var cut = _ctx.Render<L.ToastClose>();

        var btn = cut.Find("button");
        Assert.Contains("absolute", btn.GetAttribute("class") ?? "");
    }

    // ─── ToastViewport ──────────────────────────────────────────────────────

    [Fact]
    public void ToastViewport_Renders_Div()
    {
        var cut = _ctx.Render<L.ToastViewport>();

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void ToastViewport_Default_Position_BottomRight()
    {
        var cut = _ctx.Render<L.ToastViewport>();

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("bottom-4", cls);
        Assert.Contains("end-4", cls);
    }

    [Fact]
    public void ToastViewport_TopLeft_Position_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.ToastViewport>(p => p
            .Add(b => b.Position, L.ToastViewport.ToastPosition.TopLeft));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("top-4", cls);
        Assert.Contains("start-4", cls);
    }

    [Fact]
    public void ToastViewport_TopRight_Position_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.ToastViewport>(p => p
            .Add(b => b.Position, L.ToastViewport.ToastPosition.TopRight));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("top-4", cls);
        Assert.Contains("end-4", cls);
    }

    [Fact]
    public void ToastViewport_Has_Fixed_And_ZIndex_Class()
    {
        var cut = _ctx.Render<L.ToastViewport>();

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("fixed", cls);
        Assert.Contains("z-[100]", cls);
    }

    // ─── ToastProvider ──────────────────────────────────────────────────────

    [Fact]
    public void ToastProvider_Renders_Without_Toasts_Initially()
    {
        var cut = _ctx.Render<L.ToastProvider>();

        // No toast alerts visible until a message is shown
        var alerts = cut.FindAll("[role='alert'],[role='status']");
        Assert.Empty(alerts);
    }

    [Fact]
    public void ToastProvider_Shows_Toast_When_ToastService_Show_Is_Called()
    {
        var toastService = _ctx.Services.GetService(typeof(ToastService)) as ToastService;
        Assert.NotNull(toastService);

        var cut = _ctx.Render<L.ToastProvider>();

        toastService!.Show("Hello World");
        cut.WaitForState(() => cut.FindAll("[role='alert'],[role='status']").Count > 0, TimeSpan.FromSeconds(2));

        Assert.NotEmpty(cut.FindAll("[role='alert'],[role='status']"));
        Assert.Contains("Hello World", cut.Markup);
    }

    [Fact]
    public void ToastProvider_Shows_Description_When_Provided()
    {
        var toastService = _ctx.Services.GetService(typeof(ToastService)) as ToastService;
        Assert.NotNull(toastService);

        var cut = _ctx.Render<L.ToastProvider>();

        toastService!.Show("Title", "My description");
        cut.WaitForState(() => cut.FindAll("[role='alert'],[role='status']").Count > 0, TimeSpan.FromSeconds(2));

        Assert.Contains("My description", cut.Markup);
    }

    [Fact]
    public void ToastProvider_Shows_Variant_Class_For_Destructive()
    {
        var toastService = _ctx.Services.GetService(typeof(ToastService)) as ToastService;
        Assert.NotNull(toastService);

        var cut = _ctx.Render<L.ToastProvider>();

        toastService!.Show("Error!", variant: ToastVariant.Destructive);
        cut.WaitForState(() => cut.FindAll("[role='alert'],[role='status']").Count > 0, TimeSpan.FromSeconds(2));

        var alert = cut.Find("[role='alert'],[role='status']");
        Assert.Contains("border-destructive", alert.GetAttribute("class") ?? "");
    }

    [Fact]
    public void ToastProvider_Dismiss_Removes_Toast()
    {
        var toastService = _ctx.Services.GetService(typeof(ToastService)) as ToastService;
        Assert.NotNull(toastService);

        var cut = _ctx.Render<L.ToastProvider>();

        toastService!.Show("Dismiss me");
        cut.WaitForState(() => cut.FindAll("[role='alert'],[role='status']").Count > 0, TimeSpan.FromSeconds(2));

        // Click the close button
        cut.Find("button").Click();
        cut.WaitForState(() => cut.FindAll("[role='alert'],[role='status']").Count == 0, TimeSpan.FromSeconds(2));

        Assert.Empty(cut.FindAll("[role='alert'],[role='status']"));
    }

    [Fact]
    public void ToastProvider_Default_Position_Is_BottomRight()
    {
        var cut = _ctx.Render<L.ToastProvider>();

        // The viewport div should have bottom-right positioning classes
        var divs = cut.FindAll("div");
        Assert.Contains(divs, d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("bottom-4") && cls.Contains("end-4");
        });
    }

    [Fact]
    public void ToastProvider_Custom_Position_TopLeft()
    {
        var cut = _ctx.Render<L.ToastProvider>(p => p
            .Add(b => b.Position, L.ToastViewport.ToastPosition.TopLeft));

        var divs = cut.FindAll("div");
        Assert.Contains(divs, d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("top-4") && cls.Contains("start-4");
        });
    }

    // ─── Spam safety (regression: "die Seite crashed wenn man zu viele
    //     Toasts spamt") ─────────────────────────────────────────────────────
    //
    // Root cause: ToastProvider.HandleShowAsync's MaxToasts eviction loop
    // picked the oldest toast WITHOUT excluding ones already mid-exit
    // (Leaving). RemoveWithExitAsync's idempotency guard
    // (`if (toast.Leaving) return;`) completes synchronously — no `await` is
    // ever reached — so `await`ing it does not yield back to the renderer's
    // synchronization context (an already-completed Task's continuation runs
    // inline, per normal async/await semantics). When a burst of Show() calls
    // raced the SAME oldest toast, a losing coroutine kept re-selecting that
    // still-present (merely flagged) toast forever: a fully synchronous busy
    // loop with no yield point, which also starved the WINNING coroutine's own
    // Task.Delay(ExitAnimationMs) continuation (it needs the same single
    // thread to fire) — so the toast never actually finished evicting either.
    // Net effect: the render thread hung permanently. Fixed by excluding
    // already-Leaving toasts from eviction candidates.

    // PR #357 round-2 (Codex) — the eviction loop broke WITHOUT freeing a slot
    // whenever every mounted toast at/above the cap was already Leaving (a
    // burst arriving while a wave of 220ms exits is still in flight hits this
    // every time), so the new toast mounted anyway: `_toasts.Count` could blow
    // straight through MaxToasts mid-burst even though a WaitForState poll
    // taken only ONCE, after the fact, could still land on a moment where the
    // count had (temporarily, coincidentally) dipped back to <=5 — exactly
    // the flaky-on-CI failure mode this test hit. ToastProvider now queues
    // (rather than mounts) a toast whenever no non-Leaving candidate can be
    // evicted, so `_toasts.Count` can never exceed MaxToasts, period — proven
    // below by asserting the bound at EVERY render via OnAfterRender, not by
    // sampling the DOM once on a timer. No wall-clock race: the assertion
    // fires synchronously off the renderer's own render-committed event, so
    // it can't miss a transient overshoot regardless of CI scheduling noise.
    [Fact]
    public void ToastProvider_Spamming_Show_Does_Not_Hang_And_Bounds_Mounted_Toasts()
    {
        var toastService = _ctx.Services.GetService(typeof(ToastService)) as ToastService;
        Assert.NotNull(toastService);

        var cut = _ctx.Render<L.ToastProvider>(); // MaxToasts default = 5
        const int maxToasts = 5;

        var maxMountedObserved = 0;
        int? firstOverCapObserved = null;
        cut.OnAfterRender += (_, _) =>
        {
            var count = cut.FindAll("[role='alert'],[role='status']").Count;
            if (count > maxMountedObserved) maxMountedObserved = count;
            if (count > maxToasts) firstOverCapObserved ??= count;
        };

        // Fire 100 toasts back-to-back with no awaits in between — this is
        // exactly the "spam the button" burst the bug report described, and
        // is what raced concurrent HandleShowAsync evictions against each
        // other pre-fix.
        for (var i = 0; i < 100; i++)
        {
            toastService!.Show(new ToastOptions { Title = $"Spam #{i}" });
        }

        // Pre-fix this hung forever (the render thread livelocked and never
        // produced another frame — reproduced live against the docs site: the
        // page became permanently unresponsive and had to be force-killed).
        // A timeout here would itself be the regression signal (still-hanging
        // case); the invariant that the count never exceeded the cap is
        // proven by the render hook above, not by this end-state snapshot.
        cut.WaitForAssertion(
            () => Assert.InRange(cut.FindAll("[role='alert'],[role='status']").Count, 1, maxToasts),
            TimeSpan.FromSeconds(15));

        Assert.Null(firstOverCapObserved); // never exceeded MaxToasts, not even transiently mid-burst
        Assert.True(maxMountedObserved >= 1, "expected at least one render with toasts mounted");

        var mounted = cut.FindAll("[role='alert'],[role='status']").Count;
        Assert.InRange(mounted, 1, maxToasts);

        // The strongest proof the UI isn't wedged: it still responds to a
        // brand-new Show() call issued AFTER the burst has settled — even
        // though it has to wait its turn behind whatever the (bounded, at
        // most MaxToasts) pending queue from the burst left in front of it.
        toastService.Show(new ToastOptions { Title = "Post-burst toast" });
        cut.WaitForAssertion(() => Assert.Contains("Post-burst toast", cut.Markup), TimeSpan.FromSeconds(15));

        Assert.Null(firstOverCapObserved); // still holds after the post-burst toast mounts too
    }

    [Fact]
    public void ToastProvider_Dismiss_During_Entrance_Does_Not_Throw()
    {
        var toastService = _ctx.Services.GetService(typeof(ToastService)) as ToastService;
        Assert.NotNull(toastService);

        var cut = _ctx.Render<L.ToastProvider>();

        // bUnit's JSInterop is loose-mode (AddLumeoServices), so
        // attachToastEnterEnd never actually calls back OnEnterAnimationEnd —
        // the toast stays `_entering` until the 350 ms fallback timer or an
        // early Leaving flip. Dismissing immediately after Show forces exactly
        // that early-Leaving-while-entering path (Toast.OnParametersSet).
        var id = toastService!.Show(new ToastOptions { Title = "Gone before it entered" });
        toastService.Dismiss(id);

        // No ObjectDisposedException / unhandled exception should propagate
        // through the exit animation + component disposal — bUnit surfaces
        // any exception thrown during rendering by rethrowing it out of
        // WaitForState/WaitForAssertion, so simply reaching an empty,
        // stable DOM here is proof none occurred.
        cut.WaitForState(
            () => cut.FindAll("[role='alert'],[role='status']").Count == 0,
            TimeSpan.FromSeconds(5));

        Assert.Empty(cut.FindAll("[role='alert'],[role='status']"));
    }
}
