using System;
using System.Collections.Generic;

namespace Lumeo.Docs.Pages.Demos;

// ---------------------------------------------------------------------------
// Deterministic, in-memory seed data for the Northlight SaaS product demo.
// Everything here is computed from a fixed base (no Random, no DateTime.Now)
// so the page prerenders identically on every build and stays cheap in WASM.
// ---------------------------------------------------------------------------

/// <summary>A customer row rendered in the virtualized DataGrid CRM view.</summary>
public sealed record NorthlightCustomer(
    int Id,
    string Company,
    string Plan,
    string Status,
    int Seats,
    int Mrr,
    DateOnly Signup,
    string Owner,
    string Region,
    int MrrDelta,        // MoM % change in MRR (signed; -100 = fully churned)
    int Health,          // 0–100 account-health score
    int LastSeenDays,    // days since the account was last active
    string ContactName,
    string ContactEmail,
    string Notes);

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

    private static readonly string[] Regions = { "AMER", "EMEA", "APAC" };
    private static readonly string[] FirstNames =
    {
        "Ava", "Noah", "Mia", "Liam", "Zoe", "Ethan", "Lena", "Omar", "Ivy", "Rhys",
        "Nina", "Theo", "Cara", "Milo", "Sana", "Kai",
    };
    private static readonly string[] LastNames =
    {
        "Chen", "Okoye", "Rao", "Weber", "Bianchi", "Ramirez", "Nowak", "Haddad",
        "Lindqvist", "Osei", "Delacroix", "Tanaka",
    };

    // Status-appropriate CRM notes so the detail row reads like a real account log.
    private static readonly string[][] NotesByStatus =
    {
        new[] // Active
        {
            "Expansion call booked — evaluating an extra 20 seats for Q3.",
            "Champion promoted to VP; strong exec sponsorship in place.",
            "Rolled out to a second department; usage up week over week.",
        },
        new[] // Trial
        {
            "Day-7 of trial — activated SSO, exploring the analytics tab.",
            "POC scoped with the data team; decision expected next week.",
            "Trial extended once; needs a security review before signing.",
        },
        new[] // Paused
        {
            "Downgraded after a reorg; budget frozen until next quarter.",
            "Seats reduced — awaiting new admin to re-onboard the team.",
            "Renewal at risk; scheduled a save call with the sponsor.",
        },
        new[] // Churned
        {
            "Churned to an in-house build; open door for a win-back in 6mo.",
            "Lost the internal champion; no active users for 60+ days.",
            "Cancelled over pricing; flagged for the win-back campaign.",
        },
    };

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

                // Region — AMER-heavy spread (~50% AMER, 30% EMEA, 20% APAC).
                var rRoll = (i * 4) % 10;
                var region = rRoll < 5 ? Regions[0] : rRoll < 8 ? Regions[1] : Regions[2];

                // MoM MRR momentum, keyed to lifecycle: trials growing, paused slipping,
                // churned collapsed, active mixed-but-mostly-up.
                var mrrDelta = statusIdx switch
                {
                    3 => -100,
                    2 => -((i % 12) + 1),        // paused: -1..-12%
                    1 => (i % 9) + 1,            // trial:  +1..+9%
                    _ => ((i * 5) % 40) - 10,    // active: -10..+29%
                };

                // Health score biased by lifecycle stage, with deterministic jitter.
                var healthBase = statusIdx switch { 0 => 82, 1 => 63, 2 => 44, _ => 20 };
                var health = Math.Clamp(healthBase + ((i * 6) % 19) - 9, 4, 99);

                // Last-seen recency, again lifecycle-shaped.
                var lastSeen = statusIdx switch
                {
                    0 => i % 7,                  // active:  0–6d
                    1 => i % 11,                 // trial:   0–10d
                    2 => 12 + (i % 34),          // paused:  12–45d
                    _ => 34 + (i % 88),          // churned: 34–121d
                };

                var contactFirst = FirstNames[i % FirstNames.Length];
                var contactLast = LastNames[(i / 3) % LastNames.Length];
                var contactName = $"{contactFirst} {contactLast}";
                var contactEmail = $"{contactFirst.ToLowerInvariant()}.{contactLast.ToLowerInvariant()}@{Bases[b].ToLowerInvariant()}.com";

                var notes = NotesByStatus[statusIdx][i % NotesByStatus[statusIdx].Length];

                list.Add(new NorthlightCustomer(
                    id, company, Plans[planIdx], Statuses[statusIdx], seats, mrr, signup, owner,
                    region, mrrDelta, health, lastSeen, contactName, contactEmail, notes));
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
