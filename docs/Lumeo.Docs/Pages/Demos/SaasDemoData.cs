using System;
using System.Collections.Generic;

namespace Lumeo.Docs.Pages.Demos;

// ---------------------------------------------------------------------------
// Deterministic, in-memory seed data for the Northlight SaaS product demo.
// Everything here is computed from a fixed base (no Random, no DateTime.Now)
// so the page prerenders identically on every build and stays cheap in WASM.
// ---------------------------------------------------------------------------

/// <summary>A customer row rendered in the virtualized DataGrid.</summary>
public sealed record NorthlightCustomer(
    int Id,
    string Company,
    string Plan,
    string Status,
    int Seats,
    int Mrr,
    DateOnly Signup,
    string Owner);

/// <summary>An entry in the dashboard "Recent activity" feed.</summary>
public sealed record NorthlightActivity(string Icon, string Actor, string Action, string Target, string When);

/// <summary>A draggable Kanban card. Mutable membership lives in the page.</summary>
public sealed class BoardCard
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string Tag { get; init; } = "";
    public string Assignee { get; init; } = "";
}

/// <summary>A left-rail navigation entry.</summary>
public sealed record NavEntry(string Key, string Icon, string Label);

public static class NorthlightData
{
    // Base building blocks for 40 x 16 = 640 unique-ish company names.
    private static readonly string[] Bases =
    {
        "Acme", "Globex", "Initech", "Umbra", "Vertex", "Nimbus", "Orbit", "Lumen",
        "Kestrel", "Aster", "Cobalt", "Fable", "Halcyon", "Zephyr", "Quill", "Meridian",
        "Onyx", "Pinnacle", "Solace", "Tempo", "Vantage", "Willow", "Axiom", "Beacon",
        "Cirrus", "Dovetail", "Ember", "Foundry", "Granite", "Harbor", "Ivory", "Junction",
        "Kindred", "Lattice", "Monarch", "Northwind", "Obsidian", "Prism", "Quartz", "Riverstone",
    };

    private static readonly string[] Suffixes =
    {
        "Labs", "Inc", "Group", "Systems", "Digital", "Cloud", "Works", "Studio",
        "Partners", "Global", "Ventures", "Networks", "Solutions", "Analytics", "Robotics", "Dynamics",
    };

    private static readonly string[] Plans = { "Free", "Starter", "Pro", "Enterprise" };
    private static readonly string[] Statuses = { "Active", "Trial", "Paused", "Churned" };
    private static readonly string[] Owners =
    {
        "Ava Chen", "Marcus Okoye", "Priya Rao", "Jonas Weber", "Sofia Bianchi", "Leo Ramirez",
    };

    private static readonly int[] PlanSeatBase = { 2, 6, 24, 120 };   // Free, Starter, Pro, Enterprise
    private static readonly int[] PlanSeatPrice = { 0, 12, 18, 26 };  // $/seat/mo

    public static IReadOnlyList<NorthlightCustomer> Customers { get; } = BuildCustomers();

    private static IReadOnlyList<NorthlightCustomer> BuildCustomers()
    {
        var list = new List<NorthlightCustomer>(Bases.Length * Suffixes.Length);
        var baseDate = new DateOnly(2023, 1, 3);
        var id = 1;
        for (var s = 0; s < Suffixes.Length; s++)
        {
            for (var b = 0; b < Bases.Length; b++)
            {
                var i = id - 1;
                var company = $"{Bases[b]} {Suffixes[s]}";

                // Weighted-but-deterministic plan spread (~15% Free, 30% Starter, 35% Pro, 20% Ent).
                var pRoll = (i * 7) % 20;
                var planIdx = pRoll < 3 ? 0 : pRoll < 9 ? 1 : pRoll < 16 ? 2 : 3;

                // Status spread (~62% Active, 18% Trial, 12% Paused, 8% Churned).
                var sRoll = (i * 3) % 50;
                var statusIdx = sRoll < 31 ? 0 : sRoll < 40 ? 1 : sRoll < 46 ? 2 : 3;

                var seats = PlanSeatBase[planIdx] + (i % 11) * (planIdx + 1);
                var mrr = statusIdx == 3 ? 0 : seats * PlanSeatPrice[planIdx];
                var signup = baseDate.AddDays((i * 13) % 900);
                var owner = Owners[i % Owners.Length];

                list.Add(new NorthlightCustomer(id, company, Plans[planIdx], Statuses[statusIdx], seats, mrr, signup, owner));
                id++;
            }
        }
        return list;
    }

    public static IReadOnlyList<NorthlightActivity> Activity { get; } = new List<NorthlightActivity>
    {
        new("UserPlus",   "Ava Chen",       "invited",   "3 teammates to the workspace", "2m ago"),
        new("CreditCard", "Meridian Cloud",  "upgraded",  "to the Enterprise plan",       "18m ago"),
        new("CheckCircle","Marcus Okoye",   "closed",    "“Onboarding flow” milestone", "1h ago"),
        new("TriangleAlert","Billing",      "flagged",   "2 failed charges to review",    "3h ago"),
        new("Rocket",     "Priya Rao",       "shipped",   "the v4 analytics dashboard",   "Yesterday"),
        new("MessageCircle","Jonas Weber",  "commented", "on “Retention cohort” report", "Yesterday"),
    };

    public static IReadOnlyList<NavEntry> PrimaryNav { get; } = new List<NavEntry>
    {
        new("dashboard", "LayoutDashboard", "Dashboard"),
        new("board",     "Columns3",        "Board"),
        new("customers", "Users",           "Customers"),
    };

    public static IReadOnlyList<NavEntry> SecondaryNav { get; } = new List<NavEntry>
    {
        new("settings", "Settings", "Settings"),
    };

    // Fresh board seed each call so the demo can be reset without shared mutation.
    public static (List<BoardCard> Todo, List<BoardCard> Doing, List<BoardCard> Review, List<BoardCard> Done) Board() => (
        new List<BoardCard>
        {
            new() { Id = "N-142", Title = "Cohort retention chart", Description = "Weekly + monthly cohorts on the analytics tab.", Tag = "Feature", Assignee = "AC" },
            new() { Id = "N-149", Title = "SAML SSO for Enterprise", Description = "Okta + Azure AD metadata upload.", Tag = "Security", Assignee = "JW" },
            new() { Id = "N-151", Title = "Usage-based billing preview", Tag = "Billing", Assignee = "SB" },
        },
        new List<BoardCard>
        {
            new() { Id = "N-133", Title = "Board drag-and-drop polish", Description = "Snappier drop zones + keyboard reorder.", Tag = "UX", Assignee = "MO" },
            new() { Id = "N-138", Title = "Webhook retry queue", Tag = "Platform", Assignee = "LR" },
        },
        new List<BoardCard>
        {
            new() { Id = "N-127", Title = "Dark-mode contrast audit", Description = "WCAG AA sweep across the app shell.", Tag = "A11y", Assignee = "PR" },
        },
        new List<BoardCard>
        {
            new() { Id = "N-118", Title = "CSV + Excel export", Tag = "Feature", Assignee = "AC" },
            new() { Id = "N-120", Title = "Command palette (⌘K)", Tag = "UX", Assignee = "MO" },
            new() { Id = "N-124", Title = "Empty & loading states", Tag = "UX", Assignee = "SB" },
        });
}
