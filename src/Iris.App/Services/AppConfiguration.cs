using System.Text.Json;

namespace Iris.App.Services;

public sealed class AppConfiguration
{
	public IrisApiOptions IrisApi { get; init; } = new();

	public EntraIdOptions EntraId { get; init; } = new();

	public static AppConfiguration Load()
	{
		var path = Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json");
		if (!File.Exists(path))
		{
			return new AppConfiguration();
		}

		using var stream = File.OpenRead(path);
		return JsonSerializer.Deserialize<AppConfiguration>(
			stream,
			new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new AppConfiguration();
	}
}
