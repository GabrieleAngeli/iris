namespace Iris.App.Services;

public interface IDashboardDataService
{
	IReadOnlyList<StatCard> GetStats();
	IReadOnlyList<ActivityItem> GetRecentActivity();
	IReadOnlyList<ProjectRow> GetProjects();
	IReadOnlyList<ChartBar> GetWeeklyTraffic();
}

/// <summary>
/// Static sample data so the dashboard has something to show.
/// Glyph values are Segoe Fluent Icons code points.
/// </summary>
public sealed class DashboardDataService : IDashboardDataService
{
	public IReadOnlyList<StatCard> GetStats() =>
	[
		new StatCard { Title = "Active users",  Value = "8,241",    Delta = "+12.5%", IsPositive = true,  Glyph = "" },
		new StatCard { Title = "Revenue (MTD)", Value = "€ 63,900", Delta = "+4.2%", IsPositive = true, Glyph = "" },
		new StatCard { Title = "Open tickets",  Value = "37",       Delta = "-8.1%",  IsPositive = true,  Glyph = "" },
		new StatCard { Title = "Error rate",    Value = "0.42%",    Delta = "+0.06%", IsPositive = false, Glyph = "" },
	];

	public IReadOnlyList<ActivityItem> GetRecentActivity() =>
	[
		new ActivityItem { Title = "Deployment succeeded", Description = "Release 2026.8.14 is live in production.",   Timestamp = "2 min ago",  Glyph = "", Category = "CI/CD" },
		new ActivityItem { Title = "New sign-up",          Description = "m.rossi@contoso.com created an account.",     Timestamp = "18 min ago", Glyph = "", Category = "Users" },
		new ActivityItem { Title = "Ticket escalated",     Description = "#4821 login loop on Safari set to high.",     Timestamp = "1 hr ago",   Glyph = "", Category = "Support" },
		new ActivityItem { Title = "Invoice paid",         Description = "Acme Srl settled invoice INV-2231.",          Timestamp = "3 hr ago",   Glyph = "", Category = "Billing" },
		new ActivityItem { Title = "Backup completed",     Description = "Nightly database snapshot stored (12.4 GB).", Timestamp = "Yesterday",  Glyph = "", Category = "Ops" },
	];

	public IReadOnlyList<ProjectRow> GetProjects() =>
	[
		new ProjectRow { Name = "Aurora mobile app", Owner = "Giulia Ferri",  Status = "On track", Progress = 0.72 },
		new ProjectRow { Name = "Billing revamp",    Owner = "Luca Bianchi",  Status = "At risk",  Progress = 0.38 },
		new ProjectRow { Name = "Data platform v3",  Owner = "Sara Conti",    Status = "On track", Progress = 0.91 },
		new ProjectRow { Name = "Design system",     Owner = "Marco De Luca", Status = "Planning", Progress = 0.12 },
	];

	public IReadOnlyList<ChartBar> GetWeeklyTraffic()
	{
		double[] values = [420, 510, 480, 640, 720, 610, 560];
		string[] labels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
		double max = values.Max();

		return [.. values.Select((v, i) => new ChartBar
		{
			Label = labels[i],
			Value = v,
			Height = 20 + (v / max * 120)
		})];
	}
}
