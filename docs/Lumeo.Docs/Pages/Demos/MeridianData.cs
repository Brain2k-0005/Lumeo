namespace Lumeo.Docs.Pages.Demos;

// =====================================================================================
// Meridian Ops — deterministic, in-memory demo data.
//
// Everything here is generated ONCE from a fixed seed (no live Random, no DateTime.Now
// captured per-render) so the console renders identically on every load and never
// produces a prerender/hydration mismatch. `Anchor` is the single date the whole
// console is relative to; it uses DateTime.Today (date-only, stable within a day) so
// the schedule + ETAs read as "current" without pulling wall-clock time.
// =====================================================================================

/// <summary>A single freight movement — the row type for the dense DataGrid.</summary>
public sealed class Shipment
{
    public required string Reference { get; init; }
    public required string Origin { get; init; }
    public required string Destination { get; init; }
    public string Lane => $"{Origin} → {Destination}";
    public required string Carrier { get; init; }
    public required string Mode { get; init; }       // Road / Rail / Air / Sea
    public required string Priority { get; init; }    // Standard / Expedited / Critical
    public required string Status { get; init; }      // Booked / In Transit / At Hub / Customs / Delayed / Exception / Delivered
    public required string Region { get; init; }
    public int Weight { get; init; }                  // kg
    public decimal Value { get; init; }               // EUR
    public DateTime Eta { get; init; }
    public int ProgressPct { get; init; }
}

/// <summary>One lane/berth bar for the schedule (Gantt) view — kept free of any
/// Lumeo.Gantt type so this file has no dependency on the lazy satellite assembly.</summary>
public sealed record ScheduleBar(
    string Id,
    string Name,
    string Lane,
    int StartOffsetDays,
    int DurationDays,
    int Progress,
    string[]? Dependencies = null,
    bool IsMilestone = false);

/// <summary>A step in an approval's workflow (drives the Steps component).</summary>
public sealed record ApprovalStep(string Title, string Description);

/// <summary>A dated event in an approval's history (drives the Timeline).</summary>
public sealed record ApprovalEvent(string Title, string Description, string Time);

/// <summary>A pending approval / workflow item.</summary>
public sealed record ApprovalRequest(
    string Id,
    string Title,
    string Requester,
    string RequesterRole,
    string Category,
    decimal Amount,
    string Submitted,
    int CurrentStep,
    IReadOnlyList<ApprovalStep> Steps,
    IReadOnlyList<ApprovalEvent> History,
    string Summary);

/// <summary>An immutable audit-log record.</summary>
public sealed record AuditEntry(
    DateTime Timestamp,
    string Actor,
    string ActorRole,
    string Action,
    string Category,   // Auth / Data / Config / Shipment / Billing
    string Severity,   // Info / Notice / Warning / Critical
    string Target,
    string Detail);

public static class MeridianData
{
    /// <summary>The one date the entire console is relative to.</summary>
    public static readonly DateTime Anchor = DateTime.Today;

    private static readonly (string Origin, string Region)[] Origins =
    {
        ("Rotterdam RTM", "North"), ("Hamburg HAM", "North"), ("Antwerp ANR", "West"),
        ("Felixstowe FXT", "West"), ("Le Havre LEH", "West"), ("Gdansk GDN", "East"),
        ("Barcelona BCN", "South"), ("Genoa GOA", "South"), ("Piraeus PIR", "South"),
        ("Valencia VLC", "South"), ("Bremerhaven BRV", "North"), ("Gothenburg GOT", "North"),
    };

    private static readonly string[] Destinations =
    {
        "Munich", "Prague", "Vienna", "Milan", "Lyon", "Warsaw", "Berlin",
        "Zurich", "Frankfurt", "Madrid", "Krakow", "Stuttgart", "Budapest", "Copenhagen",
    };

    private static readonly string[] Carriers =
    {
        "NordFreight", "Vanguard Cargo", "Meridian Direct", "AlpineHaul", "IberLine",
        "BalticBridge", "CentroLog", "SkyReach Air", "BlueWave Sea", "RailoEuropa",
    };

    private static readonly string[] Priorities = { "Standard", "Standard", "Standard", "Expedited", "Expedited", "Critical" };
    private static readonly string[] Statuses =
    {
        "Booked", "In Transit", "In Transit", "At Hub", "Customs", "Delayed", "Exception", "Delivered", "Delivered",
    };

    private static string ModeFor(Random r) => r.Next(100) switch
    {
        < 55 => "Road",
        < 78 => "Rail",
        < 90 => "Sea",
        _ => "Air",
    };

    private static int ProgressFor(string status, Random r) => status switch
    {
        "Booked" => 0,
        "Delivered" => 100,
        "Exception" => r.Next(20, 70),
        "Delayed" => r.Next(30, 80),
        "Customs" => r.Next(70, 90),
        "At Hub" => r.Next(45, 75),
        _ => r.Next(15, 95),
    };

    /// <summary>640 deterministic shipments — enough to force DataGrid virtualization.</summary>
    public static IReadOnlyList<Shipment> Shipments { get; } = BuildShipments();

    private static Shipment[] BuildShipments()
    {
        var r = new Random(20240701);
        var list = new Shipment[640];
        for (var i = 0; i < list.Length; i++)
        {
            var (origin, region) = Origins[r.Next(Origins.Length)];
            var dest = Destinations[r.Next(Destinations.Length)];
            var mode = ModeFor(r);
            var status = Statuses[r.Next(Statuses.Length)];
            var priority = Priorities[r.Next(Priorities.Length)];
            var weight = mode switch
            {
                "Air" => r.Next(200, 6_000),
                "Road" => r.Next(1_500, 24_000),
                "Rail" => r.Next(8_000, 44_000),
                _ => r.Next(12_000, 48_000),
            };
            var etaOffset = status == "Delivered" ? -r.Next(1, 8) : r.Next(0, 22);
            list[i] = new Shipment
            {
                Reference = $"MRD-{10_000 + i}",
                Origin = origin,
                Destination = dest,
                Carrier = Carriers[r.Next(Carriers.Length)],
                Mode = mode,
                Priority = priority,
                Status = status,
                Region = region,
                Weight = weight,
                Value = r.Next(2, 260) * 1_000m + r.Next(0, 1_000),
                Eta = Anchor.AddDays(etaOffset),
                ProgressPct = ProgressFor(status, r),
            };
        }
        return list;
    }

    /// <summary>Terminal &amp; corridor programme for the Gantt view (day offsets from
    /// Anchor). Spans ~5 weeks of multi-day phases: wide enough that Day/Week/Month all
    /// render legible bars with their labels INSIDE the bar (not colliding with the
    /// today-line), yet compact enough that the chart width stays modest — the Gantt
    /// preserves raw scrollLeft across zoom switches, so a very wide chart would open
    /// Month/Year scrolled past the data. Anchor (today) sits ~30% in.</summary>
    public static IReadOnlyList<ScheduleBar> Schedule { get; } = new List<ScheduleBar>
    {
        new("berth-a",   "Berth A · MSC Aurelia",       "Quay 1 — Deep Sea",  -10, 6, 80),
        new("berth-b",   "Berth B · Nordic Trader",     "Quay 1 — Deep Sea",   -2, 6, 40, new[] { "berth-a" }),
        new("berth-c",   "Berth C · Adriatic Star",     "Quay 2 — Short Sea",  -7, 5, 62),
        new("berth-d",   "Berth D · Baltic Runner",     "Quay 2 — Short Sea",   2, 7, 15, new[] { "berth-c" }),
        new("crane-1",   "Crane gang — discharge cycle","Yard Operations",     -9, 7, 66),
        new("crane-2",   "Crane gang — load-out cycle", "Yard Operations",      1, 8, 20, new[] { "crane-1" }),
        new("rail-1",    "Rail corridor 7841 → Munich", "Rail Dispatch",       -5, 8, 55),
        new("rail-2",    "Rail corridor 7902 → Prague", "Rail Dispatch",        6, 8,  0, new[] { "rail-1" }),
        new("road-1",    "Road network — Antwerp hub",  "Road Dispatch",       -8, 10, 44),
        new("road-2",    "Road network — Lyon relay",   "Road Dispatch",        9, 9,  0, new[] { "road-1" }),
        new("cust-1",    "Customs window — Bloc EU",    "Customs & Docs",      -6, 12, 72),
        new("ms-launch", "Cut-off · manifest lock",     "Customs & Docs",      24, 0,  0, new[] { "cust-1" }, IsMilestone: true),
        new("maint-1",   "Reefer bay maintenance",      "Maintenance",          0, 8, 34),
    };

    public static IReadOnlyList<ApprovalRequest> Approvals { get; } = BuildApprovals();

    private static ApprovalRequest[] BuildApprovals() => new[]
    {
        new ApprovalRequest(
            "REQ-4471", "Carrier rate exception — BlueWave Sea", "Dana Whitfield", "Procurement Lead",
            "Rate Card", 148_500m, "2 days ago", 2,
            new List<ApprovalStep>
            {
                new("Submitted", "Requester filed exception"),
                new("Ops Review", "Operations validates lane impact"),
                new("Finance", "Budget owner signs off"),
                new("Approved", "Rate published to booking engine"),
            },
            new List<ApprovalEvent>
            {
                new("Filed by Dana Whitfield", "Q3 spot rate above contract ceiling.", "Mon 09:12"),
                new("Ops validated", "Lane volume confirmed at 42 TEU/week.", "Mon 14:40"),
                new("Awaiting Finance", "Routed to budget owner M. Cole.", "Tue 08:05"),
            },
            "Approve a temporary spot rate that exceeds the contracted ceiling on the North Sea short-sea lane."),
        new ApprovalRequest(
            "REQ-4468", "Overtime authorisation — Yard crane gang", "Marcus Feld", "Terminal Supervisor",
            "Labour", 12_300m, "3 days ago", 1,
            new List<ApprovalStep>
            {
                new("Submitted", "Shift lead requests overtime"),
                new("Ops Review", "Duty manager confirms coverage"),
                new("Approved", "Payroll notified"),
            },
            new List<ApprovalEvent>
            {
                new("Filed by Marcus Feld", "Weekend discharge backlog on Quay 1.", "Sat 18:20"),
                new("Pending Ops", "Duty manager reviewing roster.", "Sun 07:15"),
            },
            "Authorise a weekend overtime block to clear the deep-sea discharge backlog on Quay 1."),
        new ApprovalRequest(
            "REQ-4459", "Customs broker onboarding — CentroLog", "Priya Nadkarni", "Compliance Officer",
            "Vendor", 0m, "5 days ago", 3,
            new List<ApprovalStep>
            {
                new("Submitted", "Compliance opens vendor case"),
                new("Due Diligence", "KYC + sanctions screening"),
                new("Legal", "Master service agreement review"),
                new("Approved", "Vendor activated in system"),
            },
            new List<ApprovalEvent>
            {
                new("Case opened", "New broker for the Eastern corridor.", "Mon 10:00"),
                new("Screening clear", "No sanctions or adverse media hits.", "Wed 12:30"),
                new("Legal cleared MSA", "Standard terms accepted.", "Thu 16:45"),
            },
            "Activate CentroLog as a customs broker for the Eastern EU corridor after diligence and legal review."),
        new ApprovalRequest(
            "REQ-4450", "Reefer bay capital repair", "Tomasz Wójcik", "Maintenance Manager",
            "CapEx", 86_000m, "6 days ago", 1,
            new List<ApprovalStep>
            {
                new("Submitted", "Maintenance files CapEx request"),
                new("Engineering", "Scope + vendor quote review"),
                new("Finance", "CapEx committee sign-off"),
                new("Approved", "Purchase order raised"),
            },
            new List<ApprovalEvent>
            {
                new("Filed by Tomasz Wójcik", "Reefer bay 3 compressor end-of-life.", "Wed 09:40"),
                new("Engineering triage", "Two vendor quotes attached.", "Thu 11:10"),
            },
            "Fund a capital repair of the reefer bay 3 compressor unit before the peak perishables season."),
    };

    public static IReadOnlyList<AuditEntry> Audit { get; } = BuildAudit();

    private static AuditEntry[] BuildAudit()
    {
        // Deterministic: each entry's timestamp is Anchor minus a fixed minute offset.
        DateTime T(int minutesAgo) => Anchor.AddHours(9).AddMinutes(-minutesAgo);
        return new[]
        {
            new AuditEntry(T(3),    "j.harmon",       "Ops Controller",     "Rerouted shipment MRD-10231 via Antwerp hub", "Shipment", "Notice",   "MRD-10231",   "Weather diversion applied to 6 road legs."),
            new AuditEntry(T(11),   "system",         "Automation",         "Auto-closed 42 delivered shipments",          "Shipment", "Info",     "batch/eod",   "Nightly reconciliation job."),
            new AuditEntry(T(24),   "d.whitfield",    "Procurement Lead",   "Submitted rate exception REQ-4471",           "Billing",  "Notice",   "REQ-4471",    "Spot rate above contract ceiling."),
            new AuditEntry(T(37),   "m.cole",         "Finance Director",   "Approved invoice batch INV-2231",             "Billing",  "Info",     "INV-2231",    "€1.24M across 118 shipments."),
            new AuditEntry(T(52),   "unknown",        "—",                  "Failed login (3 attempts)",                   "Auth",     "Warning",  "svc-eu-3",    "Locked after threshold; IP 10.4.x."),
            new AuditEntry(T(74),   "p.nadkarni",     "Compliance Officer", "Updated sanctions screening ruleset",         "Config",   "Notice",   "policy/kyc",  "Added 2 jurisdictions to watch list."),
            new AuditEntry(T(96),   "a.okonkwo",      "Data Engineer",      "Exported customs manifest dataset",           "Data",     "Notice",   "export/9931", "14,204 rows to secure bucket."),
            new AuditEntry(T(120),  "root",           "Platform Admin",     "Rotated service credentials",                 "Config",   "Critical", "vault/prod",  "Scheduled 90-day key rotation."),
            new AuditEntry(T(151),  "m.feld",         "Terminal Supervisor","Overrode berth allocation for Quay 1",        "Shipment", "Warning",  "berth/Q1-A",  "Manual priority bump, Critical cargo."),
            new AuditEntry(T(188),  "system",         "Automation",         "Detected ETA slippage on 9 lanes",            "Shipment", "Warning",  "monitor/eta", "Threshold >6h; alerts dispatched."),
            new AuditEntry(T(232),  "j.harmon",       "Ops Controller",     "Acknowledged exception on MRD-10442",         "Shipment", "Notice",   "MRD-10442",   "Damaged seal reported at hub."),
            new AuditEntry(T(300),  "t.wojcik",       "Maintenance Manager","Opened CapEx request REQ-4450",               "Billing",  "Info",     "REQ-4450",    "Reefer bay compressor replacement."),
            new AuditEntry(T(360),  "l.marchetti",    "Security Analyst",   "Reviewed access grant for CentroLog",         "Auth",     "Notice",   "vendor/clog", "Scoped to customs module only."),
            new AuditEntry(T(455),  "system",         "Automation",         "Backup completed",                            "Data",     "Info",     "backup/nas",  "Snapshot 3.1 TB, verified."),
            new AuditEntry(T(540),  "d.whitfield",    "Procurement Lead",   "Edited carrier contract NordFreight",         "Config",   "Notice",   "ctr/NF-22",   "Extended fuel surcharge clause."),
            new AuditEntry(T(610),  "unknown",        "—",                  "Blocked API request (rate limit)",            "Auth",     "Warning",  "api/gw-2",    "429 storm from a stale integration."),
            new AuditEntry(T(720),  "a.okonkwo",      "Data Engineer",      "Reindexed shipment search cluster",           "Data",     "Info",     "search/idx",  "Latency back under 40ms."),
            new AuditEntry(T(915),  "m.cole",         "Finance Director",   "Voided duplicate invoice INV-2199",           "Billing",  "Critical", "INV-2199",    "Double-billed on lane BCN→Milan."),
            new AuditEntry(T(1100), "p.nadkarni",     "Compliance Officer", "Archived expired vendor documents",           "Config",   "Info",     "vendor/docs", "6 vendors, retention policy met."),
            new AuditEntry(T(1320), "root",           "Platform Admin",     "Applied security patch to gateway",           "Config",   "Critical", "gw/prod",     "CVE mitigation, zero downtime."),
        };
    }

    public static readonly string[] AuditCategories = { "Auth", "Data", "Config", "Shipment", "Billing" };
    public static readonly string[] AuditSeverities = { "Info", "Notice", "Warning", "Critical" };
}
