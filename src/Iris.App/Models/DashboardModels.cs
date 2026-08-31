namespace Iris.App.Models;

/// <summary>A single KPI tile shown at the top of the dashboard.</summary>
public sealed class StatCard
{
	public required string Title { get; init; }
	public required string Value { get; init; }
	public required string Delta { get; init; }
	public bool IsPositive { get; init; } = true;
	public required string Glyph { get; init; }
}

/// <summary>An entry in the "Recent activity" feed.</summary>
public sealed class ActivityItem
{
	public required string Title { get; init; }
	public required string Description { get; init; }
	public required string Timestamp { get; init; }
	public required string Glyph { get; init; }
	public string Category { get; init; } = "General";
}

/// <summary>A row in the team / projects table.</summary>
public sealed class ProjectRow
{
	public required string Name { get; init; }
	public required string Owner { get; init; }
	public required string Status { get; init; }
	public double Progress { get; init; }
}

/// <summary>A value used by the lightweight bar chart on the dashboard.</summary>
public sealed class ChartBar
{
	public required string Label { get; init; }
	public double Value { get; init; }
	public double Height { get; init; }
}
