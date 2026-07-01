var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

// Verification fixture for the DataGrid ServerMode + grouping fix: 5,000 rows across 8
// departments / 4 statuses, deliberately large enough that PageSize=25 yields 200 pages —
// exactly the "lots of data across many pages" scenario the reported production bug
// (auto-expand / content-disappearing / laggy grouping) needs to reproduce realistically.
var departments = new[] { "Engineering", "Marketing", "Sales", "Support", "HR", "Finance", "Legal", "Operations" };
var statuses = new[] { "Active", "OnLeave", "Remote", "Terminated" };
var firstNames = new[] { "Alice", "Bob", "Carol", "Dan", "Eve", "Frank", "Grace", "Heidi", "Ivan", "Judy", "Karl", "Liam", "Mona", "Nate", "Olga", "Pete" };
var rng = new Random(42);
var employees = Enumerable.Range(1, 5000).Select(id => new Employee(
    id,
    $"{firstNames[rng.Next(firstNames.Length)]} #{id}",
    departments[rng.Next(departments.Length)],
    statuses[rng.Next(statuses.Length)],
    30000 + rng.Next(0, 120000)
)).ToList();

app.MapGet("/api/employees", (
    int page,
    int pageSize,
    string? groupBy,
    string? sortField,
    string? sortDir,
    string? search) =>
{
    IEnumerable<Employee> query = employees;

    if (!string.IsNullOrWhiteSpace(search))
    {
        var s = search.Trim();
        query = query.Where(e =>
            e.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
            e.Department.Contains(s, StringComparison.OrdinalIgnoreCase) ||
            e.Status.Contains(s, StringComparison.OrdinalIgnoreCase));
    }

    var materialized = query.ToList();
    var totalCount = materialized.Count;

    // Sort by the active grouping field first so consecutive server pages show
    // coherent, contiguous group runs (the realistic, documented per-page-grouping
    // contract this component supports) — then by the requested sort field/dir.
    IOrderedEnumerable<Employee>? ordered = null;
    if (!string.IsNullOrEmpty(groupBy))
        ordered = OrderBy(materialized, groupBy, "asc", null);

    if (!string.IsNullOrEmpty(sortField))
        ordered = ordered is null
            ? OrderBy(materialized, sortField, sortDir, null)
            : OrderBy(materialized, sortField, sortDir, ordered);

    var sorted = ordered?.ToList() ?? materialized;

    var pageItems = sorted
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToList();

    return Results.Ok(new { items = pageItems, totalCount });
});

app.Run("http://localhost:5280");

static IOrderedEnumerable<Employee> OrderBy(List<Employee> src, string field, string? dir, IOrderedEnumerable<Employee>? then)
{
    Func<Employee, IComparable> selector = field switch
    {
        "Name" => e => e.Name,
        "Department" => e => e.Department,
        "Status" => e => e.Status,
        "Salary" => e => e.Salary,
        _ => e => e.Id,
    };
    var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
    return then is null
        ? (desc ? src.OrderByDescending(selector) : src.OrderBy(selector))
        : (desc ? then.ThenByDescending(selector) : then.ThenBy(selector));
}

internal record Employee(int Id, string Name, string Department, string Status, int Salary);
