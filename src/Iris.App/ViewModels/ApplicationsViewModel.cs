using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Iris.Contracts.Applications;

namespace Iris.App.ViewModels;

/// <summary>Workspace > Applications: catalog inventory with create + guarded edit.</summary>
public partial class ApplicationsViewModel : ObservableObject
{
	private const string ReadPermission = "applications.read";
	private const string WritePermission = "applications.write";

	private readonly IIrisApiClient _api;
	private readonly IAuthService _auth;

	public ApplicationsViewModel(IIrisApiClient api, IAuthService auth)
	{
		_api = api;
		_auth = auth;
	}

	public ObservableCollection<ApplicationRowViewModel> Applications { get; } = [];

	public IReadOnlyList<string> RuntimeTypes { get; } = ["CSharp", "JavaScript", "Java", "Node", "Docker"];

	[ObservableProperty] private bool _isLoading;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrEmpty(Error);

	public bool CanSeeApplications => _auth.Me?.EffectivePermissions.Contains(ReadPermission) == true;

	public bool CanManageApplications => _auth.Me?.EffectivePermissions.Contains(WritePermission) == true;

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	private bool _loaded;

	[RelayCommand]
	private async Task LoadAsync()
	{
		if (_loaded)
		{
			return;
		}

		await RefreshAsync();
		_loaded = true;
	}

	[RelayCommand]
	private async Task RefreshAsync()
	{
		IsLoading = true;
		Error = null;

		try
		{
			var applications = await _api.GetApplicationsAsync();
			Applications.Clear();
			foreach (var application in applications)
			{
				Applications.Add(new ApplicationRowViewModel(application, _api, this));
			}
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
		finally
		{
			IsLoading = false;
		}
	}

	public event EventHandler? NewApplicationRequested;

	public event EventHandler? NewApplicationCompleted;

	public event EventHandler<ApplicationRowViewModel>? EditApplicationRequested;

	public void RaiseEditRequested(ApplicationRowViewModel row) => EditApplicationRequested?.Invoke(this, row);

	[ObservableProperty] private string _newApplicationName = string.Empty;
	[ObservableProperty] private string _newApplicationSlug = string.Empty;
	[ObservableProperty] private string _newApplicationRuntimeType = "CSharp";
	[ObservableProperty] private string _newApplicationRepositoryUrl = string.Empty;
	[ObservableProperty] private string _newApplicationDefaultBranch = "main";
	[ObservableProperty] private string _newApplicationDescription = string.Empty;
	[ObservableProperty] private string _newApplicationArtifactProvider = string.Empty;
	[ObservableProperty] private string _newApplicationArtifactFeed = string.Empty;
	[ObservableProperty] private string _newApplicationArtifactName = string.Empty;
	[ObservableProperty] private string _newApplicationArtifactPath = string.Empty;
	[ObservableProperty] private string _newApplicationBuildPipelineUrl = string.Empty;
	[ObservableProperty] private bool _isCreatingApplication;
	[ObservableProperty] private string? _createApplicationError;

	public bool HasCreateApplicationError => !string.IsNullOrEmpty(CreateApplicationError);

	partial void OnCreateApplicationErrorChanged(string? value) => OnPropertyChanged(nameof(HasCreateApplicationError));

	[RelayCommand]
	private void RequestNewApplication()
	{
		NewApplicationName = string.Empty;
		NewApplicationSlug = string.Empty;
		NewApplicationRuntimeType = RuntimeTypes[0];
		NewApplicationRepositoryUrl = string.Empty;
		NewApplicationDefaultBranch = "main";
		NewApplicationDescription = string.Empty;
		NewApplicationArtifactProvider = string.Empty;
		NewApplicationArtifactFeed = string.Empty;
		NewApplicationArtifactName = string.Empty;
		NewApplicationArtifactPath = string.Empty;
		NewApplicationBuildPipelineUrl = string.Empty;
		CreateApplicationError = null;
		NewApplicationRequested?.Invoke(this, EventArgs.Empty);
	}

	[RelayCommand]
	private async Task CreateApplicationAsync()
	{
		var name = NewApplicationName.Trim();
		var repositoryUrl = NewApplicationRepositoryUrl.Trim();
		var defaultBranch = NewApplicationDefaultBranch.Trim();

		if (name.Length == 0 || repositoryUrl.Length == 0 || defaultBranch.Length == 0)
		{
			CreateApplicationError = "Name, repository URL and default branch are required.";
			return;
		}

		IsCreatingApplication = true;
		CreateApplicationError = null;

		try
		{
			var created = await _api.CreateApplicationAsync(new CreateApplicationRequest(
				name,
				string.IsNullOrWhiteSpace(NewApplicationSlug) ? null : NewApplicationSlug.Trim(),
				NewApplicationRuntimeType,
				repositoryUrl,
				defaultBranch,
				NewApplicationDescription,
				Clean(NewApplicationArtifactProvider),
				Clean(NewApplicationArtifactFeed),
				Clean(NewApplicationArtifactName),
				Clean(NewApplicationArtifactPath),
				Clean(NewApplicationBuildPipelineUrl)));

			Applications.Insert(0, new ApplicationRowViewModel(created, _api, this));
			NewApplicationCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			CreateApplicationError = ex.Message;
		}
		finally
		{
			IsCreatingApplication = false;
		}
	}

	internal static string? Clean(string value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ManifestValidationViewModel
{
	public ManifestValidationViewModel(
		string fileName,
		string targetApplicationName,
		string targetApplicationSlug,
		string schemaVersion,
		int configurationKeyCount,
		int dependencyCount,
		int placeholderCount,
		int warningCount,
		int typedDefaultValueCount,
		int applicationDependencyCount,
		IEnumerable<ManifestValidationIssueViewModel> issues)
	{
		FileName = fileName;
		TargetApplicationName = targetApplicationName;
		TargetApplicationSlug = targetApplicationSlug;
		SchemaVersion = schemaVersion;
		ConfigurationKeyCount = configurationKeyCount;
		DependencyCount = dependencyCount;
		PlaceholderCount = placeholderCount;
		WarningCount = warningCount;
		TypedDefaultValueCount = typedDefaultValueCount;
		ApplicationDependencyCount = applicationDependencyCount;
		Issues = new ObservableCollection<ManifestValidationIssueViewModel>(issues);
		ErrorCount = Issues.Count(i => i.Severity == ManifestIssueSeverity.Error);
		ManifestWarningCount = Issues.Count(i => i.Severity == ManifestIssueSeverity.Warning);
	}

	public string FileName { get; }

	public string TargetApplicationName { get; }

	public string TargetApplicationSlug { get; }

	public string TargetText => string.IsNullOrWhiteSpace(TargetApplicationSlug)
		? "No target application selected"
		: $"Target application: {TargetApplicationName} ({TargetApplicationSlug})";

	public string SchemaVersion { get; }

	public int ConfigurationKeyCount { get; }

	public int DependencyCount { get; }

	public int PlaceholderCount { get; }

	public int WarningCount { get; }

	public int TypedDefaultValueCount { get; }

	public int ApplicationDependencyCount { get; }

	public int ErrorCount { get; }

	public int ManifestWarningCount { get; }

	public ObservableCollection<ManifestValidationIssueViewModel> Issues { get; }

	public bool HasIssues => Issues.Count > 0;

	public bool IsValid => ErrorCount == 0;

	public bool IsInvalid => !IsValid;

	public bool HasWarnings => ManifestWarningCount > 0;

	public string StatusText => IsValid ? "Valid manifest" : "Manifest needs fixes";

	public string Summary =>
		$"{ConfigurationKeyCount} keys | {DependencyCount} dependencies | {PlaceholderCount} placeholders | {WarningCount} import warnings";

	public string TypeSummary => TypedDefaultValueCount == 0
		? "No typed default values detected yet"
		: $"{TypedDefaultValueCount} typed default values detected";

	public string LinkSummary => ApplicationDependencyCount == 0
		? "No application-to-application links declared"
		: $"{ApplicationDependencyCount} application-to-application links declared";

	public static ManifestValidationViewModel FromFailure(string fileName, string message, string targetApplicationName = "", string targetApplicationSlug = "") =>
		new(
			fileName,
			targetApplicationName,
			targetApplicationSlug,
			"unknown",
			0,
			0,
			0,
			0,
			0,
			0,
			[new ManifestValidationIssueViewModel(ManifestIssueSeverity.Error, message)]);
}

public sealed class ManifestValidationIssueViewModel(ManifestIssueSeverity severity, string message)
{
	public ManifestIssueSeverity Severity { get; } = severity;

	public string Message { get; } = message;

	public string SeverityText => Severity.ToString();

	public bool IsError => Severity == ManifestIssueSeverity.Error;

	public bool IsWarning => Severity == ManifestIssueSeverity.Warning;

	public bool IsInfo => Severity == ManifestIssueSeverity.Info;
}

public enum ManifestIssueSeverity
{
	Info,
	Warning,
	Error
}

internal static class ManifestValidator
{
	private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

	public static ManifestValidationViewModel Validate(
		string fileName,
		string json,
		IEnumerable<ApplicationRowViewModel> applications,
		string targetApplicationName,
		string targetApplicationSlug)
	{
		try
		{
			using var document = JsonDocument.Parse(json, new JsonDocumentOptions
			{
				AllowTrailingCommas = true,
				CommentHandling = JsonCommentHandling.Skip
			});

			if (document.RootElement.ValueKind != JsonValueKind.Object)
			{
				return ManifestValidationViewModel.FromFailure(fileName, "The manifest root must be a JSON object.", targetApplicationName, targetApplicationSlug);
			}

			var root = document.RootElement;
			var issues = new List<ManifestValidationIssueViewModel>();
			var schemaVersion = ReadString(root, "schemaVersion");
			if (string.IsNullOrWhiteSpace(schemaVersion))
			{
				issues.Add(Error("schemaVersion is required."));
				schemaVersion = "unknown";
			}
			else if (schemaVersion != "1.0" && schemaVersion != "1.1")
			{
				issues.Add(Warning($"schemaVersion '{schemaVersion}' is not explicitly supported yet; validating with the known manifest shape."));
			}

			var configurationKeys = ReadArray(root, "configurationKeys", issues);
			var dependencies = ReadArray(root, "dependencies", issues);
			var placeholders = ReadArray(root, "placeholders", issues);
			var warnings = ReadArray(root, "warnings", issues, required: false);
			issues.Add(Info($"Manifest will be associated with Iris application '{targetApplicationSlug}'."));

			var typedDefaultValueCount = ValidateConfigurationKeys(configurationKeys, schemaVersion, issues);
			var applicationDependencyCount = ValidateDependencies(dependencies, applications, issues);
			ValidatePlaceholders(placeholders, issues);

			if (configurationKeys.Count == 0)
			{
				issues.Add(Warning("No configurationKeys were found; Iris will have nothing to compile for this application version."));
			}

			return new ManifestValidationViewModel(
				fileName,
				targetApplicationName,
				targetApplicationSlug,
				schemaVersion,
				configurationKeys.Count,
				dependencies.Count,
				placeholders.Count,
				warnings.Count,
				typedDefaultValueCount,
				applicationDependencyCount,
				issues);
		}
		catch (JsonException ex)
		{
			var message = string.IsNullOrWhiteSpace(ex.Message)
				? "The selected file is not valid JSON."
				: ex.Message;
			return ManifestValidationViewModel.FromFailure(fileName, message, targetApplicationName, targetApplicationSlug);
		}
	}

	private static int ValidateConfigurationKeys(
		IReadOnlyList<JsonElement> configurationKeys,
		string schemaVersion,
		List<ManifestValidationIssueViewModel> issues)
	{
		var seen = new HashSet<string>(KeyComparer);
		var typedDefaultValueCount = 0;

		for (var index = 0; index < configurationKeys.Count; index++)
		{
			var item = configurationKeys[index];
			if (item.ValueKind != JsonValueKind.Object)
			{
				issues.Add(Error($"configurationKeys[{index}] must be an object."));
				continue;
			}

			var key = ReadString(item, "key");
			var targetKind = ReadString(item, "targetKind");
			var path = string.IsNullOrWhiteSpace(key) ? $"configurationKeys[{index}]" : $"configurationKeys['{key}']";

			if (string.IsNullOrWhiteSpace(key))
			{
				issues.Add(Error($"{path}.key is required."));
			}
			else if (!seen.Add($"{targetKind}|{key}"))
			{
				issues.Add(Warning($"{path} is duplicated for target '{targetKind}'."));
			}

			if (string.IsNullOrWhiteSpace(targetKind))
			{
				issues.Add(Error($"{path}.targetKind is required."));
			}

			RequireBoolean(item, "required", path, issues);
			var secret = RequireBoolean(item, "secret", path, issues);
			var valueType = ReadString(item, "valueType");
			var hasDefault = item.TryGetProperty("defaultValue", out var defaultValue) &&
				defaultValue.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

			if (hasDefault)
			{
				var detectedType = DetectValueType(defaultValue);
				if (detectedType != "string")
				{
					typedDefaultValueCount++;
				}

				if (secret == true)
				{
					issues.Add(Warning($"{path} is secret but carries a defaultValue; secrets should be resolved by the secret store, not stored in the manifest."));
				}

				if (!string.IsNullOrWhiteSpace(valueType) && !DefaultMatchesValueType(defaultValue, valueType))
				{
					issues.Add(Warning($"{path}.defaultValue looks like {detectedType}, but valueType is '{valueType}'."));
				}
			}

			if (schemaVersion == "1.1" && string.IsNullOrWhiteSpace(valueType))
			{
				issues.Add(Warning($"{path}.valueType is recommended for manifest 1.1."));
			}

			var scope = ReadString(item, "scope");
			if (!string.IsNullOrWhiteSpace(scope) &&
				scope is not "applicationVersion" and not "deployment" and not "installationInstance" and not "topology" and not "serviceReference" and not "secretStore" and not "manual")
			{
				issues.Add(Warning($"{path}.scope '{scope}' is not a known resolution scope."));
			}

			if (item.TryGetProperty("serialization", out var serialization) && serialization.ValueKind != JsonValueKind.Object)
			{
				issues.Add(Warning($"{path}.serialization should be an object."));
			}

			if (item.TryGetProperty("resolution", out var resolution) && resolution.ValueKind != JsonValueKind.Object)
			{
				issues.Add(Warning($"{path}.resolution should be an object."));
			}
		}

		return typedDefaultValueCount;
	}

	private static int ValidateDependencies(
		IReadOnlyList<JsonElement> dependencies,
		IEnumerable<ApplicationRowViewModel> applications,
		List<ManifestValidationIssueViewModel> issues)
	{
		var applicationSlugs = applications.Select(a => a.Slug).ToHashSet(KeyComparer);
		var applicationDependencyCount = 0;

		for (var index = 0; index < dependencies.Count; index++)
		{
			var item = dependencies[index];
			if (item.ValueKind != JsonValueKind.Object)
			{
				issues.Add(Error($"dependencies[{index}] must be an object."));
				continue;
			}

			var name = ReadString(item, "name");
			var path = string.IsNullOrWhiteSpace(name) ? $"dependencies[{index}]" : $"dependencies['{name}']";
			if (string.IsNullOrWhiteSpace(name))
			{
				issues.Add(Error($"{path}.name is required."));
			}

			if (string.IsNullOrWhiteSpace(ReadString(item, "category")))
			{
				issues.Add(Error($"{path}.category is required."));
			}

			RequireBoolean(item, "required", path, issues);

			var providerApplicationSlug = ReadString(item, "providerApplicationSlug");
			var providerPlaceholderKey = ReadString(item, "providerPlaceholderKey");
			if (!string.IsNullOrWhiteSpace(providerApplicationSlug))
			{
				applicationDependencyCount++;
				if (applicationSlugs.Contains(providerApplicationSlug))
				{
					issues.Add(Info($"{path} can be linked to Iris application '{providerApplicationSlug}'. Provider placeholder verification comes in the next step."));
				}
				else
				{
					issues.Add(Warning($"{path} references provider application '{providerApplicationSlug}', but it is not in the current Iris catalog."));
				}

				if (string.IsNullOrWhiteSpace(providerPlaceholderKey))
				{
					issues.Add(Warning($"{path} references a provider application but no providerPlaceholderKey."));
				}
			}
		}

		return applicationDependencyCount;
	}

	private static void ValidatePlaceholders(
		IReadOnlyList<JsonElement> placeholders,
		List<ManifestValidationIssueViewModel> issues)
	{
		var seen = new HashSet<string>(KeyComparer);
		for (var index = 0; index < placeholders.Count; index++)
		{
			var item = placeholders[index];
			if (item.ValueKind != JsonValueKind.Object)
			{
				issues.Add(Error($"placeholders[{index}] must be an object."));
				continue;
			}

			var key = ReadString(item, "key");
			var path = string.IsNullOrWhiteSpace(key) ? $"placeholders[{index}]" : $"placeholders['{key}']";
			if (string.IsNullOrWhiteSpace(key))
			{
				issues.Add(Error($"{path}.key is required."));
			}
			else if (!seen.Add(key))
			{
				issues.Add(Warning($"{path} is duplicated."));
			}

			RequireBoolean(item, "required", path, issues);
		}
	}

	private static IReadOnlyList<JsonElement> ReadArray(
		JsonElement root,
		string name,
		List<ManifestValidationIssueViewModel> issues,
		bool required = true)
	{
		if (!root.TryGetProperty(name, out var value))
		{
			if (required)
			{
				issues.Add(Error($"{name} is required."));
			}

			return [];
		}

		if (value.ValueKind != JsonValueKind.Array)
		{
			issues.Add(Error($"{name} must be an array."));
			return [];
		}

		return value.EnumerateArray().ToArray();
	}

	private static string? ReadString(JsonElement item, string name)
	{
		if (!item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
		{
			return null;
		}

		return value.ValueKind == JsonValueKind.String
			? value.GetString()
			: value.ToString();
	}

	private static bool? RequireBoolean(
		JsonElement item,
		string name,
		string path,
		List<ManifestValidationIssueViewModel> issues)
	{
		if (!item.TryGetProperty(name, out var value))
		{
			issues.Add(Error($"{path}.{name} is required."));
			return null;
		}

		if (value.ValueKind == JsonValueKind.True)
		{
			return true;
		}

		if (value.ValueKind == JsonValueKind.False)
		{
			return false;
		}

		issues.Add(Error($"{path}.{name} must be boolean."));
		return null;
	}

	private static bool DefaultMatchesValueType(JsonElement value, string valueType)
	{
		var normalized = valueType.Trim().ToLowerInvariant();
		return normalized switch
		{
			"string" or "uri" or "connectionstring" => value.ValueKind == JsonValueKind.String,
			"integer" or "int" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
			"decimal" or "number" => value.ValueKind == JsonValueKind.Number,
			"boolean" or "bool" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
			"json" => value.ValueKind is JsonValueKind.Object or JsonValueKind.Array,
			"array" or "list" or "stringlist" or "integerlist" or "booleanlist" => value.ValueKind == JsonValueKind.Array,
			_ => true
		};
	}

	private static string DetectValueType(JsonElement value) =>
		value.ValueKind switch
		{
			JsonValueKind.String => "string",
			JsonValueKind.Number => value.TryGetInt64(out _) ? "integer" : "decimal",
			JsonValueKind.True or JsonValueKind.False => "boolean",
			JsonValueKind.Array => "array",
			JsonValueKind.Object => "json",
			JsonValueKind.Null => "null",
			_ => value.ValueKind.ToString().ToLower(CultureInfo.InvariantCulture)
		};

	private static ManifestValidationIssueViewModel Error(string message) => new(ManifestIssueSeverity.Error, message);

	private static ManifestValidationIssueViewModel Warning(string message) => new(ManifestIssueSeverity.Warning, message);

	private static ManifestValidationIssueViewModel Info(string message) => new(ManifestIssueSeverity.Info, message);
}

public sealed partial class ApplicationRowViewModel : ObservableObject
{
	private const string LockResource = "application";
	private const int HeartbeatSeconds = 45;

	private readonly Guid _applicationId;
	private readonly IIrisApiClient _api;
	private readonly ApplicationsViewModel _parent;
	private CancellationTokenSource? _heartbeatCts;

	public ApplicationRowViewModel(ApplicationResponse application, IIrisApiClient api, ApplicationsViewModel parent)
	{
		_applicationId = application.Id;
		_api = api;
		_parent = parent;
		ApplyFrom(application);
	}

	public Guid Id => _applicationId;

	public IReadOnlyList<string> RuntimeTypes => _parent.RuntimeTypes;

	public bool CanManageApplications => _parent.CanManageApplications;

	[ObservableProperty] private string _name = string.Empty;
	[ObservableProperty] private string _slug = string.Empty;
	[ObservableProperty] private string _runtimeType = string.Empty;
	[ObservableProperty] private string _repositoryUrl = string.Empty;
	[ObservableProperty] private string _defaultBranch = string.Empty;
	[ObservableProperty] private string? _description;
	[ObservableProperty] private string? _artifactProvider;
	[ObservableProperty] private string? _artifactFeed;
	[ObservableProperty] private string? _artifactName;
	[ObservableProperty] private string? _artifactPath;
	[ObservableProperty] private string? _buildPipelineUrl;
	[ObservableProperty] private bool _isActive;
	[ObservableProperty] private int _versionCount;
	[ObservableProperty] private int _configurationKeyCount;
	[ObservableProperty] private int _dependencyCount;
	[ObservableProperty] private int _placeholderCount;
	[ObservableProperty] private DateTimeOffset? _lastImportedAtUtc;
	[ObservableProperty] private bool _isManifestUploadBusy;
	[ObservableProperty] private ManifestValidationViewModel? _manifestValidation;

	public string VersionCountText => VersionCount == 1 ? "1 version" : $"{VersionCount} versions";

	public string KnowledgeSummary => $"{ConfigurationKeyCount} keys | {DependencyCount} dependencies | {PlaceholderCount} placeholders";

	public string LastImportText => LastImportedAtUtc is { } value
		? $"Last import: {value.ToLocalTime():g}"
		: "No imported knowledge yet";

	public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

	public bool HasManifestValidation => ManifestValidation is not null;

	public bool HasArtifact => !string.IsNullOrWhiteSpace(ArtifactProvider) ||
		!string.IsNullOrWhiteSpace(ArtifactFeed) ||
		!string.IsNullOrWhiteSpace(ArtifactName) ||
		!string.IsNullOrWhiteSpace(ArtifactPath) ||
		!string.IsNullOrWhiteSpace(BuildPipelineUrl);

	public string ArtifactSummary
	{
		get
		{
			var provider = string.IsNullOrWhiteSpace(ArtifactProvider) ? "artifact" : ArtifactProvider;
			var name = string.IsNullOrWhiteSpace(ArtifactName) ? ArtifactPath : ArtifactName;
			return string.IsNullOrWhiteSpace(name)
				? provider
				: $"{provider}: {name}";
		}
	}

	partial void OnDescriptionChanged(string? value) => OnPropertyChanged(nameof(HasDescription));

	partial void OnManifestValidationChanged(ManifestValidationViewModel? value) => OnPropertyChanged(nameof(HasManifestValidation));

	private void ApplyFrom(ApplicationResponse application)
	{
		Name = application.Name;
		Slug = application.Slug;
		RuntimeType = application.RuntimeType;
		RepositoryUrl = application.RepositoryUrl;
		DefaultBranch = application.DefaultBranch;
		Description = application.Description;
		ArtifactProvider = application.ArtifactProvider;
		ArtifactFeed = application.ArtifactFeed;
		ArtifactName = application.ArtifactName;
		ArtifactPath = application.ArtifactPath;
		BuildPipelineUrl = application.BuildPipelineUrl;
		IsActive = application.IsActive;
		VersionCount = application.Versions.Count;
		ConfigurationKeyCount = application.Versions.Sum(v => v.ConfigurationKeyCount);
		DependencyCount = application.Versions.Sum(v => v.DependencyCount);
		PlaceholderCount = application.Versions.Sum(v => v.PlaceholderCount);
		LastImportedAtUtc = application.Versions
			.Select(v => v.LastImportedAtUtc)
			.Max();

		OnPropertyChanged(nameof(VersionCountText));
		OnPropertyChanged(nameof(KnowledgeSummary));
		OnPropertyChanged(nameof(LastImportText));
		OnPropertyChanged(nameof(HasArtifact));
		OnPropertyChanged(nameof(ArtifactSummary));
	}

	[RelayCommand]
	private async Task UploadManifestAsync()
	{
		IsManifestUploadBusy = true;
		_parent.Error = null;

		try
		{
			var result = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = $"Select Iris manifest for {Name}",
				FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
				{
					[DevicePlatform.WinUI] = [".json"],
					[DevicePlatform.macOS] = ["json"],
					[DevicePlatform.iOS] = ["public.json"],
					[DevicePlatform.Android] = ["application/json"]
				})
			});

			if (result is null)
			{
				return;
			}

			await using var stream = await result.OpenReadAsync();
			using var reader = new StreamReader(stream);
			var json = await reader.ReadToEndAsync();
			ManifestValidation = ManifestValidator.Validate(result.FileName, json, _parent.Applications, Name, Slug);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			ManifestValidation = ManifestValidationViewModel.FromFailure("Manifest upload", ex.Message, Name, Slug);
		}
		finally
		{
			IsManifestUploadBusy = false;
		}
	}

	[RelayCommand]
	private void ClearManifestValidation()
	{
		ManifestValidation = null;
	}

	[ObservableProperty] private string _editName = string.Empty;
	[ObservableProperty] private string _editRuntimeType = "CSharp";
	[ObservableProperty] private string _editRepositoryUrl = string.Empty;
	[ObservableProperty] private string _editDefaultBranch = "main";
	[ObservableProperty] private string _editDescription = string.Empty;
	[ObservableProperty] private string _editArtifactProvider = string.Empty;
	[ObservableProperty] private string _editArtifactFeed = string.Empty;
	[ObservableProperty] private string _editArtifactName = string.Empty;
	[ObservableProperty] private string _editArtifactPath = string.Empty;
	[ObservableProperty] private string _editBuildPipelineUrl = string.Empty;
	[ObservableProperty] private bool _editActive;
	[ObservableProperty] private bool _isEditBusy;
	[ObservableProperty] private string? _editError;
	[ObservableProperty] private string? _editLockNotice;

	public bool HasEditError => !string.IsNullOrEmpty(EditError);

	public bool HasEditLockNotice => !string.IsNullOrEmpty(EditLockNotice);

	partial void OnEditErrorChanged(string? value) => OnPropertyChanged(nameof(HasEditError));

	partial void OnEditLockNoticeChanged(string? value) => OnPropertyChanged(nameof(HasEditLockNotice));

	[RelayCommand]
	private async Task OpenEditAsync()
	{
		EditLockNotice = null;
		EditError = null;

		try
		{
			var slot = await _api.AcquireEditLockAsync(LockResource, _applicationId);
			if (!slot.Mine)
			{
				EditLockNotice = $"{slot.HolderDisplayName} is editing this application right now - try again in a moment.";
				return;
			}
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			EditLockNotice = ex.Message;
			return;
		}

		StartHeartbeat();

		EditName = Name;
		EditRuntimeType = RuntimeType;
		EditRepositoryUrl = RepositoryUrl;
		EditDefaultBranch = DefaultBranch;
		EditDescription = Description ?? string.Empty;
		EditArtifactProvider = ArtifactProvider ?? string.Empty;
		EditArtifactFeed = ArtifactFeed ?? string.Empty;
		EditArtifactName = ArtifactName ?? string.Empty;
		EditArtifactPath = ArtifactPath ?? string.Empty;
		EditBuildPipelineUrl = BuildPipelineUrl ?? string.Empty;
		EditActive = IsActive;
		_parent.RaiseEditRequested(this);
	}

	private void StartHeartbeat()
	{
		_heartbeatCts?.Cancel();
		var cts = new CancellationTokenSource();
		_heartbeatCts = cts;
		_ = HeartbeatAsync(cts.Token);
	}

	private async Task HeartbeatAsync(CancellationToken token)
	{
		try
		{
			using var timer = new PeriodicTimer(TimeSpan.FromSeconds(HeartbeatSeconds));
			while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
			{
				try
				{
					await _api.AcquireEditLockAsync(LockResource, _applicationId, token).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
				{
					// A dropped heartbeat just lets the lock lapse sooner.
				}
			}
		}
		catch (OperationCanceledException)
		{
			// editor closed
		}
	}

	public void NotifyEditorClosed()
	{
		if (_heartbeatCts is null)
		{
			return;
		}

		_heartbeatCts.Cancel();
		_heartbeatCts.Dispose();
		_heartbeatCts = null;
		_ = SafeReleaseLockAsync();
	}

	private async Task SafeReleaseLockAsync()
	{
		try
		{
			await _api.ReleaseEditLockAsync(LockResource, _applicationId).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			// The lock will expire on its own if the release didn't land.
		}
	}

	public event EventHandler? EditCompleted;

	[RelayCommand]
	private async Task SaveEditAsync()
	{
		var name = EditName.Trim();
		var repositoryUrl = EditRepositoryUrl.Trim();
		var defaultBranch = EditDefaultBranch.Trim();

		if (name.Length == 0 || repositoryUrl.Length == 0 || defaultBranch.Length == 0)
		{
			EditError = "Name, repository URL and default branch are required.";
			return;
		}

		IsEditBusy = true;
		EditError = null;

		try
		{
			var updated = await _api.UpdateApplicationAsync(_applicationId, new UpdateApplicationRequest(
				name,
				EditRuntimeType,
				repositoryUrl,
				defaultBranch,
				EditDescription,
				EditActive,
				ApplicationsViewModel.Clean(EditArtifactProvider),
				ApplicationsViewModel.Clean(EditArtifactFeed),
				ApplicationsViewModel.Clean(EditArtifactName),
				ApplicationsViewModel.Clean(EditArtifactPath),
				ApplicationsViewModel.Clean(EditBuildPipelineUrl)));

			ApplyFrom(updated);
			EditCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			EditError = ex.Message;
		}
		finally
		{
			IsEditBusy = false;
		}
	}
}
