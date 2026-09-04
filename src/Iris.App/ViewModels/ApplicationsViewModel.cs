using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Iris.Contracts.Applications;
using Iris.Contracts.Infrastructure;

namespace Iris.App.ViewModels;

/// <summary>Workspace > Applications: catalog inventory with create + guarded edit.</summary>
public partial class ApplicationsViewModel : ObservableObject
{
	private const string ReadPermission = "applications.read";
	private const string WritePermission = "applications.write";
	private const string ImportPermission = "applications.import";
	private const string DeploymentsWritePermission = "deployments.write";

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

	public bool CanImportApplicationKnowledge => _auth.Me?.EffectivePermissions.Contains(ImportPermission) == true;

	public bool CanManageDeployments => _auth.Me?.EffectivePermissions.Contains(DeploymentsWritePermission) == true;

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

	public event EventHandler<ApplicationRowViewModel>? ImportManifestRequested;

	public void RaiseImportManifestRequested(ApplicationRowViewModel row) => ImportManifestRequested?.Invoke(this, row);

	public event EventHandler<ApplicationRowViewModel>? NewApplicationInstallationRequested;

	public void RaiseNewApplicationInstallationRequested(ApplicationRowViewModel row) => NewApplicationInstallationRequested?.Invoke(this, row);

	internal Task ReloadAsync() => RefreshAsync();

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
		IEnumerable<ManifestPreviewItemViewModel> configurationKeyPreview,
		IEnumerable<ManifestPreviewItemViewModel> dependencyPreview,
		IEnumerable<ManifestPreviewItemViewModel> placeholderPreview,
		IEnumerable<ManifestPreviewItemViewModel> profilePreview,
		IEnumerable<ManifestPreviewItemViewModel> applicationUnitPreview,
		IEnumerable<ManifestPreviewItemViewModel> assimilationDecisions,
		ManifestImportDraft? importDraft,
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
		ConfigurationKeyPreview = new ObservableCollection<ManifestPreviewItemViewModel>(configurationKeyPreview);
		DependencyPreview = new ObservableCollection<ManifestPreviewItemViewModel>(dependencyPreview);
		PlaceholderPreview = new ObservableCollection<ManifestPreviewItemViewModel>(placeholderPreview);
		ProfilePreview = new ObservableCollection<ManifestPreviewItemViewModel>(profilePreview);
		ApplicationUnitPreview = new ObservableCollection<ManifestPreviewItemViewModel>(applicationUnitPreview);
		AssimilationDecisions = new ObservableCollection<ManifestPreviewItemViewModel>(assimilationDecisions);
		ImportDraft = importDraft;
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

	public ObservableCollection<ManifestPreviewItemViewModel> ConfigurationKeyPreview { get; }

	public ObservableCollection<ManifestPreviewItemViewModel> DependencyPreview { get; }

	public ObservableCollection<ManifestPreviewItemViewModel> PlaceholderPreview { get; }

	public ObservableCollection<ManifestPreviewItemViewModel> ProfilePreview { get; }

	public ObservableCollection<ManifestPreviewItemViewModel> ApplicationUnitPreview { get; }

	public ObservableCollection<ManifestPreviewItemViewModel> AssimilationDecisions { get; }

	public ManifestImportDraft? ImportDraft { get; }

	public ObservableCollection<ManifestValidationIssueViewModel> Issues { get; }

	public bool HasIssues => Issues.Count > 0;

	public bool IsValid => ErrorCount == 0;

	public bool IsInvalid => !IsValid;

	public bool HasWarnings => ManifestWarningCount > 0;

	public bool HasConfigurationKeyPreview => ConfigurationKeyPreview.Count > 0;

	public bool HasDependencyPreview => DependencyPreview.Count > 0;

	public bool HasPlaceholderPreview => PlaceholderPreview.Count > 0;

	public bool HasProfilePreview => ProfilePreview.Count > 0;

	public bool HasApplicationUnitPreview => ApplicationUnitPreview.Count > 0;

	public bool HasAssimilationDecisions => AssimilationDecisions.Count > 0;

	public bool HasAssimilationPreview => IsValid && (
		HasConfigurationKeyPreview ||
		HasDependencyPreview ||
		HasPlaceholderPreview ||
		HasProfilePreview ||
		HasApplicationUnitPreview ||
		HasAssimilationDecisions);

	public bool CanStartImport => IsValid && ImportDraft is not null;

	public string StatusText => IsValid ? "Valid manifest" : "Manifest needs fixes";

	public string Summary =>
		$"{ConfigurationKeyCount} keys | {DependencyCount} dependencies | {PlaceholderCount} placeholders | {WarningCount} import warnings";

	public string TypeSummary => TypedDefaultValueCount == 0
		? "No typed default values detected yet"
		: $"{TypedDefaultValueCount} typed default values detected";

	public string LinkSummary => ApplicationDependencyCount == 0
		? "No application-to-application links declared"
		: $"{ApplicationDependencyCount} application-to-application links declared";

	public string PreviewSummary => IsValid
		? "These items are ready for the import wizard. Nothing is persisted yet."
		: "Fix validation errors before building the assimilation preview.";

	public string DecisionSummary => HasAssimilationDecisions
		? $"{AssimilationDecisions.Count} decisions to resolve before binding"
		: "No immediate decisions detected";

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
			[],
			[],
			[],
			[],
			[],
			[],
			null,
			[new ManifestValidationIssueViewModel(ManifestIssueSeverity.Error, message)]);
}

public sealed record ManifestImportDraft(
	string SuggestedVersion,
	string SuggestedSourceReference,
	string RuntimeSummary,
	string ExecutionTargetsSummary,
	string OsSupportSummary,
	string MinimumResourcesSummary,
	string PortPolicySummary,
	RuntimeMetadataRequest SuggestedRuntimeMetadata,
	ImportConfigurationPackageRequest Package);

public sealed class ApplicationOptionViewModel(string slug, string name)
{
	public string Slug { get; } = slug;

	public string Name { get; } = name;

	public string DisplayName => $"{Name} ({Slug})";
}

public sealed partial class ManifestAssociationViewModel : ObservableObject
{
	public ManifestAssociationViewModel(
		string dependencyName,
		string category,
		bool required,
		string? placeholderKey,
		string? providerPlaceholderKey,
		string? manifestProviderSlug,
		IEnumerable<ApplicationOptionViewModel> applicationOptions)
	{
		DependencyName = dependencyName;
		Category = category;
		Required = required;
		PlaceholderKey = placeholderKey;
		ProviderPlaceholderKey = providerPlaceholderKey;
		ManifestProviderSlug = manifestProviderSlug;
		ApplicationOptions = new ObservableCollection<ApplicationOptionViewModel>(applicationOptions);
		SelectedApplication = ApplicationOptions.FirstOrDefault(option =>
			string.Equals(option.Slug, manifestProviderSlug, StringComparison.OrdinalIgnoreCase));
	}

	public string DependencyName { get; }

	public string Category { get; }

	public bool Required { get; }

	public string? PlaceholderKey { get; }

	public string? ProviderPlaceholderKey { get; }

	public string? ManifestProviderSlug { get; }

	public ObservableCollection<ApplicationOptionViewModel> ApplicationOptions { get; }

	[ObservableProperty] private ApplicationOptionViewModel? _selectedApplication;

	public bool IsResolved => SelectedApplication is not null;

	public bool IsUnresolvedRequired => Required && !IsResolved;

	public string RequiredText => Required ? "required" : "optional";

	public string ProviderText => string.IsNullOrWhiteSpace(ManifestProviderSlug)
		? "No provider declared by manifest"
		: $"Manifest provider: {ManifestProviderSlug}";

	public string PlaceholderText => string.IsNullOrWhiteSpace(PlaceholderKey)
		? "No consumer placeholder"
		: $"Consumer placeholder: {PlaceholderKey}";

	public string StatusText => IsResolved
		? $"Resolved to {SelectedApplication!.Slug}"
		: Required ? "Required association missing" : "Association not selected";

	partial void OnSelectedApplicationChanged(ApplicationOptionViewModel? value)
	{
		OnPropertyChanged(nameof(IsResolved));
		OnPropertyChanged(nameof(IsUnresolvedRequired));
		OnPropertyChanged(nameof(StatusText));
	}
}

public sealed class ApplicationVersionOptionViewModel(ApplicationVersionSummaryResponse version)
{
	public Guid Id { get; } = version.Id;

	public string Version { get; } = version.Version;

	public string? SourceReference { get; } = version.SourceReference;

	public RuntimeMetadataResponse RuntimeMetadata { get; } = version.RuntimeMetadata;

	public DateTimeOffset? LastImportedAtUtc { get; } = version.LastImportedAtUtc;

	public string DisplayName => string.IsNullOrWhiteSpace(SourceReference)
		? Version
		: $"{Version} - {SourceReference}";

	public string Detail => $"{RuntimeMetadata.RuntimeName} | {version.ApplicationUnitCount} unit(s) | {version.InstallationProfileCount} profile(s)";
}

public sealed class ApplicationUnitOptionViewModel(ApplicationUnitResponse unit)
{
	public string Key { get; } = unit.Key;

	public string DisplayName { get; } = string.IsNullOrWhiteSpace(unit.DisplayName)
		? unit.Key
		: $"{unit.DisplayName} ({unit.Key})";

	public string Detail { get; } = string.Join(" | ", new[]
	{
		unit.Kind,
		unit.EntryPoint,
		unit.ArtifactPath,
		unit.ExecutionTargets.Count == 0 ? null : string.Join(", ", unit.ExecutionTargets)
	}.Where(value => !string.IsNullOrWhiteSpace(value)))!;
}

public sealed class InstallationProfileOptionViewModel(InstallationProfileResponse profile)
{
	public string Key { get; } = profile.Key;

	public bool Required { get; } = profile.Required;

	public string DisplayName { get; } = string.IsNullOrWhiteSpace(profile.DisplayName)
		? profile.Key
		: $"{profile.DisplayName} ({profile.Key})";

	public string Detail => profile.ConfigurationKeys.Count == 0
		? "No scoped keys"
		: $"{profile.ConfigurationKeys.Count} scoped key(s)";
}

public sealed class ServerOptionViewModel(ServerResponse server)
{
	public Guid Id { get; } = server.Id;

	public string Name { get; } = server.Name;

	public string Environment { get; } = server.Environment;

	public string DisplayName => $"{server.Name} - {server.Environment}";

	public string Detail
	{
		get
		{
			var os = string.IsNullOrWhiteSpace(server.OsVersion)
				? server.Os
				: $"{server.Os} {server.OsVersion}";
			var resources = server.Resources is null
				? "resources unknown"
				: $"{server.Resources.CpuCores?.ToString(CultureInfo.InvariantCulture) ?? "-"} CPU, {server.Resources.MemoryMb?.ToString(CultureInfo.InvariantCulture) ?? "-"} MB, {server.Resources.ApplicationDiskGb?.ToString(CultureInfo.InvariantCulture) ?? server.Resources.DiskGb?.ToString(CultureInfo.InvariantCulture) ?? "-"} GB app";
			return $"{server.HostingType} | {os} | {resources}";
		}
	}
}

public sealed class DataServiceOptionViewModel(DataServiceResponse service)
{
	public Guid Id { get; } = service.Id;

	public string Name { get; } = service.Name;

	public string Kind { get; } = service.Kind;

	public string DisplayName => $"{service.Name} - {service.Kind}";

	public string Detail
	{
		get
		{
			var endpoint = service.Port is null
				? service.Endpoint
				: $"{service.Endpoint}:{service.Port}";
			var version = string.IsNullOrWhiteSpace(service.Version) ? "version unknown" : service.Version;
			return $"{service.Environment} | {endpoint} | {version}";
		}
	}
}

public sealed partial class InstallationBindingViewModel : ObservableObject
{
	private InstallationBindingViewModel(
		string title,
		string category,
		string placeholderKey,
		bool required,
		string targetKind,
		string? description,
		string? providerApplicationSlug,
		string? providerPlaceholderKey,
		ObservableCollection<DataServiceOptionViewModel>? dataServiceOptions)
	{
		Title = title;
		Category = category;
		PlaceholderKey = placeholderKey;
		Required = required;
		TargetKind = targetKind;
		Description = description;
		ProviderApplicationSlug = providerApplicationSlug;
		ProviderPlaceholderKey = providerPlaceholderKey;
		DataServiceOptions = dataServiceOptions ?? [];
	}

	public string Title { get; }

	public string Category { get; }

	public string PlaceholderKey { get; }

	public bool Required { get; }

	public string TargetKind { get; }

	public string? Description { get; }

	public string? ProviderApplicationSlug { get; }

	public string? ProviderPlaceholderKey { get; }

	public ObservableCollection<DataServiceOptionViewModel> DataServiceOptions { get; }

	[ObservableProperty] private DataServiceOptionViewModel? _selectedDataService;

	public bool IsDataServiceBinding => string.Equals(TargetKind, "dataService", StringComparison.OrdinalIgnoreCase);

	public bool IsApplicationBinding => string.Equals(TargetKind, "application", StringComparison.OrdinalIgnoreCase);

	public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

	public bool IsMissingRequired => Required && IsDataServiceBinding && SelectedDataService is null;

	public string RequiredText => Required ? "Required" : "Optional";

	public string TargetText => IsApplicationBinding
		? $"Application: {ProviderApplicationSlug ?? "not resolved"}"
		: "Data service";

	public string StatusText => IsApplicationBinding
		? $"Resolved by manifest association ({ProviderPlaceholderKey ?? PlaceholderKey})"
		: SelectedDataService is null
			? "Select the concrete service for this installation"
			: SelectedDataService.Detail;

	partial void OnSelectedDataServiceChanged(DataServiceOptionViewModel? value)
	{
		OnPropertyChanged(nameof(IsMissingRequired));
		OnPropertyChanged(nameof(StatusText));
	}

	public static InstallationBindingViewModel ForApplication(
		string title,
		string category,
		string? placeholderKey,
		bool required,
		string? providerApplicationSlug,
		string? providerPlaceholderKey) =>
		new(
			title,
			category,
			placeholderKey ?? providerPlaceholderKey ?? title,
			required,
			"application",
			null,
			providerApplicationSlug,
			providerPlaceholderKey,
			null);

	public static InstallationBindingViewModel ForDataService(
		string title,
		string category,
		string placeholderKey,
		bool required,
		string? description,
		ObservableCollection<DataServiceOptionViewModel> dataServiceOptions) =>
		new(title, category, placeholderKey, required, "dataService", description, null, null, dataServiceOptions);

	public ApplicationInstallationBindingInput? ToInput()
	{
		if (IsApplicationBinding)
		{
			return string.IsNullOrWhiteSpace(ProviderApplicationSlug)
				? null
				: new ApplicationInstallationBindingInput(
					PlaceholderKey,
					"application",
					null,
					ProviderApplicationSlug,
					ProviderPlaceholderKey,
					null);
		}

		if (SelectedDataService is null)
		{
			return null;
		}

		return new ApplicationInstallationBindingInput(
			PlaceholderKey,
			"dataService",
			SelectedDataService.Id,
			null,
			SelectedDataService.DisplayName,
			Description);
	}
}

public sealed class ManifestPreviewItemViewModel(
	string title,
	string subtitle,
	string detail = "",
	bool requiresDecision = false,
	string decisionText = "")
{
	public string Title { get; } = title;

	public string Subtitle { get; } = subtitle;

	public string Detail { get; } = detail;

	public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

	public bool RequiresDecision { get; } = requiresDecision;

	public string DecisionText { get; } = decisionText;

	public bool HasDecisionText => !string.IsNullOrWhiteSpace(DecisionText);
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
	private const int PreviewLimit = 12;

	public static ManifestValidationViewModel Validate(
		string fileName,
		string json,
		IEnumerable<ApplicationRowViewModel> applications,
		string targetApplicationName,
		string targetApplicationSlug,
		string targetRuntimeType)
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

			var manifestVersion = ReadSuggestedVersion(root);
			if (string.IsNullOrWhiteSpace(manifestVersion))
			{
				issues.Add(Error("The manifest must declare the application release version."));
			}

			var manifestSourceReference = ReadSuggestedSourceReference(root);
			if (string.IsNullOrWhiteSpace(manifestSourceReference))
			{
				issues.Add(Error("The manifest must declare sourceReference for traceability."));
			}

			var configurationKeys = ReadArray(root, "configurationKeys", issues);
			var dependencies = ReadArray(root, "dependencies", issues);
			var placeholders = ReadArray(root, "placeholders", issues);
			var warnings = ReadArray(root, "warnings", issues, required: false);
			var profiles = ReadProfileArrays(root, issues);
			var applicationUnits = ReadApplicationUnitArrays(root, issues);
			issues.Add(Info($"Manifest will be associated with Iris application '{targetApplicationSlug}'."));

			var typedDefaultValueCount = ValidateConfigurationKeys(configurationKeys, schemaVersion, issues);
			var applicationDependencyCount = ValidateDependencies(dependencies, applications, issues);
			ValidatePlaceholders(placeholders, issues);
			var configurationPreview = BuildConfigurationKeyPreview(configurationKeys);
			var dependencyPreview = BuildDependencyPreview(dependencies, applications);
			var placeholderPreview = BuildPlaceholderPreview(placeholders);
			var profilePreview = BuildProfilePreview(profiles);
			var applicationUnitPreview = BuildApplicationUnitPreview(applicationUnits);
			var decisions = BuildAssimilationDecisions(configurationKeys, dependencies, applications);
			var importDraft = BuildImportDraft(root, schemaVersion, configurationKeys, dependencies, placeholders, warnings, profiles, applicationUnits, targetRuntimeType);

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
				configurationPreview,
				dependencyPreview,
				placeholderPreview,
				profilePreview,
				applicationUnitPreview,
				decisions,
				importDraft,
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

	private static ManifestImportDraft BuildImportDraft(
		JsonElement root,
		string schemaVersion,
		IReadOnlyList<JsonElement> configurationKeys,
		IReadOnlyList<JsonElement> dependencies,
		IReadOnlyList<JsonElement> placeholders,
		IReadOnlyList<JsonElement> warnings,
		IReadOnlyList<JsonElement> profiles,
		IReadOnlyList<JsonElement> applicationUnits,
		string targetRuntimeType)
	{
		var importWarnings = BuildImportWarnings(root, schemaVersion, warnings, configurationKeys, profiles, applicationUnits);
		var package = new ImportConfigurationPackageRequest(
			schemaVersion,
			configurationKeys
				.Where(item => item.ValueKind == JsonValueKind.Object)
				.Select(item => new ConfigurationKeyInput(
					ReadString(item, "key") ?? string.Empty,
					ReadString(item, "targetKind") ?? string.Empty,
					ReadBoolean(item, "required") == true,
					ReadBoolean(item, "secret") == true,
					ReadDefaultValueForImport(item),
					ReadString(item, "description"),
					ReadString(item, "purpose"),
					ReadString(item, "placeholderKey"),
					ReadString(item, "valueType"),
					ReadString(item, "itemType"),
					ReadString(item, "scope"),
					ReadJsonProperty(item, "serialization"),
					ReadJsonProperty(item, "resolution"),
					ReadJsonProperty(item, "profiles"),
					ReadJsonProperty(item, "profileDefaults"),
					ReadJsonProperty(item, "itemSchema")))
				.ToArray(),
			dependencies
				.Where(item => item.ValueKind == JsonValueKind.Object)
				.Select(item => new DependencyInput(
					ReadString(item, "name") ?? string.Empty,
					ReadString(item, "category") ?? string.Empty,
					ReadBoolean(item, "required") == true,
					ReadString(item, "description"),
					ReadString(item, "placeholderKey"),
					ReadString(item, "providerApplicationSlug"),
					ReadString(item, "providerPlaceholderKey")))
				.ToArray(),
			placeholders
				.Where(item => item.ValueKind == JsonValueKind.Object)
				.Select(item => new PlaceholderInput(
					ReadString(item, "key") ?? string.Empty,
					ReadString(item, "category"),
					ReadString(item, "description"),
					ReadBoolean(item, "required") == true))
				.ToArray(),
			importWarnings,
			BuildApplicationUnitInputs(applicationUnits),
			BuildInstallationProfileInputs(profiles),
			BuildDependencyConstraintInputs(root));

		return new ManifestImportDraft(
			ReadSuggestedVersion(root),
			ReadSuggestedSourceReference(root) ?? string.Empty,
			ReadRuntimeSummary(root, targetRuntimeType),
			ReadExecutionTargetsSummary(root),
			ReadOsSupportSummary(root),
			ReadMinimumResourcesSummary(root),
			ReadPortPolicySummary(root),
			ReadSuggestedRuntimeMetadata(root, configurationKeys, targetRuntimeType),
			package);
	}

	private static IReadOnlyList<ApplicationUnitInput> BuildApplicationUnitInputs(IReadOnlyList<JsonElement> applicationUnits) =>
		applicationUnits
			.Where(item => item.ValueKind == JsonValueKind.Object)
			.Select((item, index) => new ApplicationUnitInput(
				ReadString(item, "key") ?? ReadString(item, "slug") ?? ReadString(item, "name") ?? $"application-unit-{index + 1}",
				ReadString(item, "displayName") ?? ReadString(item, "name"),
				ReadString(item, "kind"),
				ReadString(item, "entryPoint"),
				ReadString(item, "artifactPath"),
				ReadStringArray(item, "executionTargets"),
				ReadStringArray(item, "profiles")))
			.ToArray();

	private static IReadOnlyList<InstallationProfileInput> BuildInstallationProfileInputs(IReadOnlyList<JsonElement> profiles) =>
		profiles
			.Where(item => item.ValueKind == JsonValueKind.Object)
			.Select((item, index) => new InstallationProfileInput(
				ReadString(item, "key") ?? ReadString(item, "name") ?? ReadString(item, "profile") ?? $"profile-{index + 1}",
				ReadString(item, "displayName") ?? ReadString(item, "name"),
				ReadBoolean(item, "required") == true,
				ReadBoolean(item, "multiple") == true,
				ReadFirstStringArray(item, "configurationKeys", "keys")))
			.ToArray();

	private static IReadOnlyList<DependencyConstraintInput> BuildDependencyConstraintInputs(JsonElement root)
	{
		var constraints = ReadArray(root, "dependencyConstraints", new List<ManifestValidationIssueViewModel>(), required: false);
		return constraints
			.Where(item => item.ValueKind == JsonValueKind.Object)
			.Select(item => new DependencyConstraintInput(
				ReadString(item, "placeholderKey"),
				ReadString(item, "serviceKind") ?? ReadString(item, "category"),
				ReadVersionExpression(item),
				item.GetRawText()))
			.ToArray();
	}

	private static IReadOnlyList<RuntimeOsSupportInfo> ReadRuntimeOsSupport(JsonElement runtime)
	{
		if (runtime.ValueKind != JsonValueKind.Object ||
			!runtime.TryGetProperty("osSupport", out var osSupport) ||
			osSupport.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		return osSupport.EnumerateArray()
			.Select(item =>
			{
				if (item.ValueKind == JsonValueKind.String)
				{
					return new RuntimeOsSupportInfo(item.GetString() ?? "Unknown", null, null);
				}

				if (item.ValueKind != JsonValueKind.Object)
				{
					return null;
				}

				var type = ReadFirstString(item, "type", "family", "name", "os") ?? "Unknown";
				return new RuntimeOsSupportInfo(
					type,
					ReadString(item, "distribution"),
					ReadFirstString(item, "version", "minVersion", "testedVersion"),
					ReadBoolean(item, "tested") != false);
			})
			.Where(item => item is not null)
			.Cast<RuntimeOsSupportInfo>()
			.ToArray();
	}

	private static IReadOnlyList<string> BuildImportWarnings(
		JsonElement root,
		string schemaVersion,
		IReadOnlyList<JsonElement> manifestWarnings,
		IReadOnlyList<JsonElement> configurationKeys,
		IReadOnlyList<JsonElement> profiles,
		IReadOnlyList<JsonElement> applicationUnits)
	{
		var warnings = manifestWarnings
			.Select(ReadWarningText)
			.Where(warning => !string.IsNullOrWhiteSpace(warning))
			.Cast<string>()
			.ToList();

		return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static string? ReadWarningText(JsonElement item)
	{
		return item.ValueKind switch
		{
			JsonValueKind.String => item.GetString(),
			JsonValueKind.Object => ReadString(item, "message") ?? ReadString(item, "code") ?? item.GetRawText(),
			_ => item.ToString()
		};
	}

	private static string? ReadDefaultValueForImport(JsonElement item)
	{
		if (!item.TryGetProperty("defaultValue", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
		{
			return null;
		}

		return value.ValueKind == JsonValueKind.String
			? value.GetString()
			: value.GetRawText();
	}

	private static string ReadSuggestedVersion(JsonElement root) =>
		ReadString(root, "releaseVersion") ??
		ReadString(root, "version") ??
		ReadNestedString(root, "release", "version", "releaseVersion") ??
		ReadNestedString(root, "application", "version") ??
		ReadNestedString(root, "artifact", "version") ??
		"";

	private static string? ReadSuggestedSourceReference(JsonElement root) =>
		ReadString(root, "sourceReference") ??
		ReadNestedString(root, "release", "sourceReference", "commit", "buildId") ??
		ReadNestedString(root, "application", "sourceReference") ??
		ReadNestedString(root, "artifact", "sourceReference") ??
		ReadNestedString(root, "build", "id", "number", "sourceReference");

	private static string ReadRuntimeSummary(JsonElement root, string targetRuntimeType)
	{
		var runtime = ReadObject(root, "runtime");
		var framework = ReadFirstString(runtime, "runtimeName", "name", "framework") ?? targetRuntimeType;
		var version = ReadFirstString(runtime, "version", "javaVersion", "dotnetVersion", "nodeVersion");
		return JoinParts(framework, !string.IsNullOrWhiteSpace(version) ? $"version {version}" : null);
	}

	private static string ReadExecutionTargetsSummary(JsonElement root)
	{
		var runtime = ReadObject(root, "runtime");
		var targets = ReadStringArray(runtime, "executionTargets")
			.Concat(ReadStringArray(root, "executionTargets"))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		return targets.Length == 0 ? "service or container target not declared" : string.Join(", ", targets);
	}

	private static string ReadOsSupportSummary(JsonElement root)
	{
		var runtime = ReadObject(root, "runtime");
		if (runtime.ValueKind == JsonValueKind.Object &&
			runtime.TryGetProperty("osSupport", out var osSupport) &&
			osSupport.ValueKind == JsonValueKind.Array)
		{
			var values = osSupport.EnumerateArray()
				.Select(ReadOsSupportText)
				.Where(value => !string.IsNullOrWhiteSpace(value))
				.ToArray();
			return values.Length == 0 ? "No tested OS declared" : string.Join(", ", values);
		}

		var os = ReadFirstString(runtime, "preferredOs", "os");
		return string.IsNullOrWhiteSpace(os) ? "No tested OS declared" : os;
	}

	private static string ReadMinimumResourcesSummary(JsonElement root)
	{
		var minimum = ReadObject(ReadObject(root, "runtime"), "minimumResources");
		if (minimum.ValueKind != JsonValueKind.Object)
		{
			return "No minimum capacity declared";
		}

		return JoinParts(
			ReadNullableInt(minimum, "cpuCores", "requiredCpuCores", "cpu") is { } cpu ? $"min CPU {cpu}" : null,
			ReadNullableInt(minimum, "memoryMb", "requiredMemoryMb", "memory") is { } memory ? $"min memory {memory} MB" : null);
	}

	private static string ReadPortPolicySummary(JsonElement root)
	{
		var runtime = ReadObject(root, "runtime");
		var declaredPorts = ReadStringArray(runtime, "portKeys")
			.Concat(ReadStringArray(root, "portKeys"))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		return declaredPorts.Length == 0
			? "Ports are resolved per installation instance"
			: $"Instance-bound keys: {string.Join(", ", declaredPorts)}";
	}

	private static RuntimeMetadataRequest ReadSuggestedRuntimeMetadata(
		JsonElement root,
		IReadOnlyList<JsonElement> configurationKeys,
		string targetRuntimeType)
	{
		var runtime = ReadObject(root, "runtime");
		var application = ReadObject(root, "application");
		var minimumResources = ReadObject(runtime, "minimumResources");
		var runtimeName = ReadFirstString(runtime, "runtimeName", "name", "framework") ??
			ReadFirstString(application, "runtimeType", "runtime") ??
			targetRuntimeType;

		return new RuntimeMetadataRequest(
			string.IsNullOrWhiteSpace(runtimeName) ? "Unknown" : runtimeName,
			ReadFirstOsFamily(runtime) ?? ReadFirstString(runtime, "preferredOs", "os"),
			null,
			null,
			[],
			ReadStringArray(runtime, "executionTargets")
				.Concat(ReadStringArray(root, "executionTargets"))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray(),
			ReadRuntimeOsSupport(runtime),
			ReadNullableInt(minimumResources, "requiredCpuCores", "cpuCores", "cpu"),
			ReadNullableInt(minimumResources, "requiredMemoryMb", "memoryMb", "memory"),
			ReadStringArray(runtime, "portKeys")
				.Concat(ReadStringArray(root, "portKeys"))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray());
	}

	private static IReadOnlyList<int> ReadRequiredPorts(JsonElement runtime, IReadOnlyList<JsonElement> configurationKeys)
	{
		var ports = new List<int>();
		if (runtime.ValueKind == JsonValueKind.Object && runtime.TryGetProperty("requiredPorts", out var declaredPorts) && declaredPorts.ValueKind == JsonValueKind.Array)
		{
			ports.AddRange(declaredPorts.EnumerateArray().Select(ReadInt).Where(value => value is > 0).Cast<int>());
		}

		foreach (var item in configurationKeys.Where(item => item.ValueKind == JsonValueKind.Object))
		{
			var purpose = ReadString(item, "purpose");
			if (!string.IsNullOrWhiteSpace(purpose) &&
				purpose.Contains("network:", StringComparison.OrdinalIgnoreCase) &&
				item.TryGetProperty("defaultValue", out var defaultValue) &&
				ReadInt(defaultValue) is { } port && port > 0)
			{
				ports.Add(port);
			}
		}

		return ports.Distinct().Order().ToArray();
	}

	private static int? ReadNullableInt(JsonElement item, params string[] names)
	{
		if (item.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		foreach (var name in names)
		{
			if (item.TryGetProperty(name, out var value) && ReadInt(value) is { } integer)
			{
				return integer;
			}
		}

		return null;
	}

	private static int? ReadInt(JsonElement value)
	{
		if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
		{
			return number;
		}

		if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
		{
			return parsed;
		}

		return null;
	}

	private static IReadOnlyList<ManifestPreviewItemViewModel> BuildConfigurationKeyPreview(IReadOnlyList<JsonElement> configurationKeys)
	{
		return configurationKeys
			.Take(PreviewLimit)
			.Select((item, index) =>
			{
				if (item.ValueKind != JsonValueKind.Object)
				{
					return new ManifestPreviewItemViewModel($"configurationKeys[{index}]", "Invalid item", "The item is not a JSON object.", true, "Fix manifest shape");
				}

				var key = ReadString(item, "key") ?? $"configurationKeys[{index}]";
				var targetKind = ReadString(item, "targetKind") ?? "unknown target";
				var valueType = ReadString(item, "valueType") ?? DetectDefaultOrDeclaredType(item);
				var required = ReadBoolean(item, "required") == true ? "required" : "optional";
				var secret = ReadBoolean(item, "secret") == true ? "secret" : "plain";
				var resolution = ReadResolutionSummary(item);
				var subtitle = JoinParts(targetKind, valueType, required, secret, resolution);
				var detail = JoinParts(
					ReadString(item, "description"),
					ReadString(item, "purpose") is { Length: > 0 } purpose ? $"purpose: {purpose}" : null,
					ReadString(item, "placeholderKey") is { Length: > 0 } placeholder ? $"placeholder: {placeholder}" : null,
					ReadDefaultDisplay(item));
				var requiresDecision = RequiresConfigurationDecision(item);
				var decisionText = requiresDecision ? DescribeConfigurationDecision(item) : string.Empty;
				return new ManifestPreviewItemViewModel(key, subtitle, detail, requiresDecision, decisionText);
			})
			.ToArray();
	}

	private static IReadOnlyList<ManifestPreviewItemViewModel> BuildDependencyPreview(
		IReadOnlyList<JsonElement> dependencies,
		IEnumerable<ApplicationRowViewModel> applications)
	{
		var applicationSlugs = applications.Select(a => a.Slug).ToHashSet(KeyComparer);
		return dependencies
			.Take(PreviewLimit)
			.Select((item, index) =>
			{
				if (item.ValueKind != JsonValueKind.Object)
				{
					return new ManifestPreviewItemViewModel($"dependencies[{index}]", "Invalid item", "The item is not a JSON object.", true, "Fix manifest shape");
				}

				var name = ReadString(item, "name") ?? $"dependencies[{index}]";
				var category = ReadString(item, "category") ?? "unknown category";
				var required = ReadBoolean(item, "required") == true ? "required" : "optional";
				var providerApplicationSlug = ReadString(item, "providerApplicationSlug");
				var providerPlaceholderKey = ReadString(item, "providerPlaceholderKey");
				var providerState = string.IsNullOrWhiteSpace(providerApplicationSlug)
					? "provider unresolved"
					: applicationSlugs.Contains(providerApplicationSlug) ? "provider in catalog" : "provider missing";
				var subtitle = JoinParts(category, required, providerState);
				var detail = JoinParts(
					ReadString(item, "description"),
					ReadString(item, "placeholderKey") is { Length: > 0 } placeholder ? $"consumer placeholder: {placeholder}" : null,
					!string.IsNullOrWhiteSpace(providerApplicationSlug) ? $"provider app: {providerApplicationSlug}" : null,
					!string.IsNullOrWhiteSpace(providerPlaceholderKey) ? $"provider placeholder: {providerPlaceholderKey}" : null);
				var requiresDecision = string.IsNullOrWhiteSpace(providerApplicationSlug) ||
					!applicationSlugs.Contains(providerApplicationSlug);
				var decisionText = requiresDecision
					? "Choose an Iris application or an infrastructure service during assimilation."
					: "Application link can be pre-selected in the wizard.";
				return new ManifestPreviewItemViewModel(name, subtitle, detail, requiresDecision, decisionText);
			})
			.ToArray();
	}

	private static IReadOnlyList<ManifestPreviewItemViewModel> BuildPlaceholderPreview(IReadOnlyList<JsonElement> placeholders)
	{
		return placeholders
			.Take(PreviewLimit)
			.Select((item, index) =>
			{
				if (item.ValueKind != JsonValueKind.Object)
				{
					return new ManifestPreviewItemViewModel($"placeholders[{index}]", "Invalid item", "The item is not a JSON object.", true, "Fix manifest shape");
				}

				var key = ReadString(item, "key") ?? $"placeholders[{index}]";
				var category = ReadString(item, "category") ?? "unclassified";
				var required = ReadBoolean(item, "required") == true ? "required" : "optional";
				var detail = ReadString(item, "description") ?? "Exposed value available to consuming applications.";
				return new ManifestPreviewItemViewModel(key, JoinParts(category, required), detail);
			})
			.ToArray();
	}

	private static IReadOnlyList<ManifestPreviewItemViewModel> BuildProfilePreview(IReadOnlyList<JsonElement> profiles)
	{
		return profiles
			.Take(PreviewLimit)
			.Select((item, index) =>
			{
				if (item.ValueKind != JsonValueKind.Object)
				{
					return new ManifestPreviewItemViewModel($"profiles[{index}]", "Invalid item", "The item is not a JSON object.", true, "Fix manifest shape");
				}

				var name = ReadString(item, "name") ?? ReadString(item, "key") ?? ReadString(item, "profile") ?? $"profiles[{index}]";
				var appliesTo = ReadString(item, "appliesTo") ?? ReadString(item, "role") ?? ReadString(item, "kind") ?? "installation profile";
				var keyCount = CountArrayProperty(item, "configurationKeys") +
					CountArrayProperty(item, "keys") +
					CountArrayProperty(item, "overrides");
				var detail = JoinParts(
					ReadString(item, "description"),
					keyCount > 0 ? $"{keyCount} profile-specific configuration entries" : "No profile-specific entries detected");
				return new ManifestPreviewItemViewModel(name, appliesTo, detail, keyCount > 0, "Select this profile when creating the application installation.");
			})
			.ToArray();
	}

	private static IReadOnlyList<ManifestPreviewItemViewModel> BuildApplicationUnitPreview(IReadOnlyList<JsonElement> applicationUnits)
	{
		return applicationUnits
			.Take(PreviewLimit)
			.Select((item, index) =>
			{
				if (item.ValueKind != JsonValueKind.Object)
				{
					return new ManifestPreviewItemViewModel($"applicationUnits[{index}]", "Invalid item", "The item is not a JSON object.", true, "Fix manifest shape");
				}

				var key = ReadString(item, "key") ?? ReadString(item, "slug") ?? ReadString(item, "name") ?? $"applicationUnits[{index}]";
				var displayName = ReadString(item, "displayName") ?? ReadString(item, "name");
				var targets = ReadStringArray(item, "executionTargets");
				var profiles = ReadStringArray(item, "profiles");
				var subtitle = JoinParts(
					ReadString(item, "kind") ?? "launchable application",
					targets.Count > 0 ? string.Join(", ", targets) : null,
					profiles.Count > 0 ? $"profiles: {string.Join(", ", profiles)}" : null);
				var detail = JoinParts(
					displayName,
					ReadString(item, "entryPoint") is { Length: > 0 } entryPoint ? $"entry point: {entryPoint}" : null,
					ReadString(item, "artifactPath") is { Length: > 0 } artifactPath ? $"artifact: {artifactPath}" : null);
				return new ManifestPreviewItemViewModel(key, subtitle, detail, true, "Persist as launchable application unit for installation binding.");
			})
			.ToArray();
	}

	private static IReadOnlyList<ManifestPreviewItemViewModel> BuildAssimilationDecisions(
		IReadOnlyList<JsonElement> configurationKeys,
		IReadOnlyList<JsonElement> dependencies,
		IEnumerable<ApplicationRowViewModel> applications)
	{
		var decisions = new List<ManifestPreviewItemViewModel>();
		var applicationSlugs = applications.Select(a => a.Slug).ToHashSet(KeyComparer);

		foreach (var item in configurationKeys)
		{
			if (item.ValueKind != JsonValueKind.Object)
			{
				continue;
			}

			var key = ReadString(item, "key") ?? "configuration key";
			var valueType = ReadString(item, "valueType") ?? DetectDefaultOrDeclaredType(item);
			var referencedApplicationSlug = ReadApplicationReference(item);
			if (!string.IsNullOrWhiteSpace(referencedApplicationSlug))
			{
				var state = applicationSlugs.Contains(referencedApplicationSlug) ? "available in catalog" : "not found in catalog";
				decisions.Add(new ManifestPreviewItemViewModel(
					key,
					"Application reference",
					$"Select or confirm provider application '{referencedApplicationSlug}' ({state}).",
					true,
					"Resolve during application-to-application mapping"));
				continue;
			}

			if (ReadBoolean(item, "secret") == true)
			{
				decisions.Add(new ManifestPreviewItemViewModel(
					key,
					"Secret value",
					"Bind to OpenBao, environment secret, pipeline variable or another approved secret source.",
					true,
					"Resolve secret source"));
				continue;
			}

			if (IsListType(valueType))
			{
				decisions.Add(new ManifestPreviewItemViewModel(
					key,
					"List value",
					"Compile as an ordered list; the deployment binding must decide delimiter or native JSON serialization.",
					true,
					"Resolve list composition"));
				continue;
			}

			if (ReadBoolean(item, "required") == true && !HasDefaultValue(item))
			{
				decisions.Add(new ManifestPreviewItemViewModel(
					key,
					"Required value",
					"No default value was declared. The deployment or installation profile must provide it.",
					true,
					"Resolve deployment value"));
			}
		}

		foreach (var item in dependencies)
		{
			if (item.ValueKind != JsonValueKind.Object)
			{
				continue;
			}

			var name = ReadString(item, "name") ?? "dependency";
			var providerApplicationSlug = ReadString(item, "providerApplicationSlug");
			if (string.IsNullOrWhiteSpace(providerApplicationSlug))
			{
				decisions.Add(new ManifestPreviewItemViewModel(
					name,
					"Dependency provider",
					"No provider application is declared. Choose an Iris application or infrastructure service.",
					true,
					"Resolve provider"));
			}
			else if (!applicationSlugs.Contains(providerApplicationSlug))
			{
				decisions.Add(new ManifestPreviewItemViewModel(
					name,
					"Missing provider application",
					$"Provider application '{providerApplicationSlug}' must be created or replaced before import.",
					true,
					"Resolve provider"));
			}
		}

		return decisions.Take(PreviewLimit * 2).ToArray();
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

	private static IReadOnlyList<JsonElement> ReadProfileArrays(JsonElement root, List<ManifestValidationIssueViewModel> issues)
	{
		var profiles = new List<JsonElement>();
		foreach (var name in new[] { "profiles", "deploymentProfiles", "installationProfiles", "variants" })
		{
			if (!root.TryGetProperty(name, out var value))
			{
				continue;
			}

			if (value.ValueKind != JsonValueKind.Array)
			{
				issues.Add(Warning($"{name} should be an array when present."));
				continue;
			}

			profiles.AddRange(value.EnumerateArray());
		}

		return profiles;
	}

	private static IReadOnlyList<JsonElement> ReadApplicationUnitArrays(JsonElement root, List<ManifestValidationIssueViewModel> issues)
	{
		var applicationUnits = new List<JsonElement>();
		foreach (var name in new[] { "applicationUnits", "launchables", "components" })
		{
			if (!root.TryGetProperty(name, out var value))
			{
				continue;
			}

			if (value.ValueKind != JsonValueKind.Array)
			{
				issues.Add(Warning($"{name} should be an array when present."));
				continue;
			}

			applicationUnits.AddRange(value.EnumerateArray());
		}

		return applicationUnits;
	}

	private static bool? ReadBoolean(JsonElement item, string name)
	{
		if (!item.TryGetProperty(name, out var value))
		{
			return null;
		}

		return value.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			_ => null
		};
	}

	private static string DetectDefaultOrDeclaredType(JsonElement item)
	{
		var declared = ReadString(item, "valueType");
		if (!string.IsNullOrWhiteSpace(declared))
		{
			return declared;
		}

		return item.TryGetProperty("defaultValue", out var defaultValue) && defaultValue.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
			? DetectValueType(defaultValue)
			: "string";
	}

	private static string? ReadDefaultDisplay(JsonElement item)
	{
		if (!item.TryGetProperty("defaultValue", out var value) || value.ValueKind == JsonValueKind.Undefined)
		{
			return null;
		}

		if (value.ValueKind == JsonValueKind.Null)
		{
			return "default: null";
		}

		var text = value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: value.GetRawText();
		return $"default: {Truncate(text, 90)}";
	}

	private static string ReadResolutionSummary(JsonElement item)
	{
		var scope = ReadString(item, "scope");
		var resolutionKind = ReadNestedString(item, "resolution", "kind", "type", "source");
		var applicationReference = ReadApplicationReference(item);
		return JoinParts(
			!string.IsNullOrWhiteSpace(scope) ? $"scope {scope}" : null,
			!string.IsNullOrWhiteSpace(resolutionKind) ? $"resolution {resolutionKind}" : null,
			!string.IsNullOrWhiteSpace(applicationReference) ? $"app {applicationReference}" : null);
	}

	private static bool RequiresConfigurationDecision(JsonElement item)
	{
		var valueType = DetectDefaultOrDeclaredType(item);
		return ReadBoolean(item, "secret") == true ||
			ReadBoolean(item, "required") == true && !HasDefaultValue(item) ||
			IsListType(valueType) ||
			!string.IsNullOrWhiteSpace(ReadApplicationReference(item));
	}

	private static string DescribeConfigurationDecision(JsonElement item)
	{
		var applicationReference = ReadApplicationReference(item);
		if (!string.IsNullOrWhiteSpace(applicationReference))
		{
			return "Resolve application reference";
		}

		if (ReadBoolean(item, "secret") == true)
		{
			return "Resolve secret source";
		}

		if (IsListType(DetectDefaultOrDeclaredType(item)))
		{
			return "Resolve list composition";
		}

		return "Resolve deployment value";
	}

	private static string? ReadApplicationReference(JsonElement item)
	{
		var direct = ReadFirstString(
			item,
			"applicationSlug",
			"providerApplicationSlug",
			"sourceApplicationSlug",
			"targetApplicationSlug");
		if (!string.IsNullOrWhiteSpace(direct))
		{
			return direct;
		}

		return ReadNestedString(
			item,
			"resolution",
			"applicationSlug",
			"providerApplicationSlug",
			"sourceApplicationSlug",
			"targetApplicationSlug");
	}

	private static string? ReadFirstString(JsonElement item, params string[] names)
	{
		foreach (var name in names)
		{
			var value = ReadString(item, name);
			if (!string.IsNullOrWhiteSpace(value))
			{
				return value;
			}
		}

		return null;
	}

	private static JsonElement ReadObject(JsonElement item, string name)
	{
		if (item.ValueKind != JsonValueKind.Object)
		{
			return default;
		}

		return item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
			? value
			: default;
	}

	private static IReadOnlyList<string> ReadStringArray(JsonElement item, string name)
	{
		if (item.ValueKind != JsonValueKind.Object ||
			!item.TryGetProperty(name, out var value) ||
			value.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		return value.EnumerateArray()
			.Select(element => element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString())
			.Where(text => !string.IsNullOrWhiteSpace(text))
			.Cast<string>()
			.ToArray();
	}

	private static IReadOnlyList<string> ReadFirstStringArray(JsonElement item, params string[] names)
	{
		foreach (var name in names)
		{
			var values = ReadStringArray(item, name);
			if (values.Count > 0)
			{
				return values;
			}
		}

		return [];
	}

	private static string? ReadJsonProperty(JsonElement item, string name)
	{
		if (item.ValueKind != JsonValueKind.Object ||
			!item.TryGetProperty(name, out var value) ||
			value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
		{
			return null;
		}

		return value.GetRawText();
	}

	private static string? ReadVersionExpression(JsonElement item)
	{
		var direct = ReadFirstString(item, "versionExpression", "versionRequirement", "constraint");
		if (!string.IsNullOrWhiteSpace(direct))
		{
			return direct;
		}

		if (item.ValueKind != JsonValueKind.Object ||
			!item.TryGetProperty("version", out var version) ||
			version.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
		{
			return null;
		}

		if (version.ValueKind == JsonValueKind.String)
		{
			return version.GetString();
		}

		if (version.ValueKind != JsonValueKind.Object)
		{
			return version.GetRawText();
		}

		var @operator = ReadString(version, "operator");
		var value = ReadString(version, "value");
		if (!string.IsNullOrWhiteSpace(@operator) && !string.IsNullOrWhiteSpace(value))
		{
			return $"{@operator} {value}";
		}

		var parts = new List<string>();
		if (ReadString(version, "minInclusive") is { Length: > 0 } minInclusive)
		{
			parts.Add($">= {minInclusive}");
		}

		if (ReadString(version, "minExclusive") is { Length: > 0 } minExclusive)
		{
			parts.Add($"> {minExclusive}");
		}

		if (ReadString(version, "maxInclusive") is { Length: > 0 } maxInclusive)
		{
			parts.Add($"<= {maxInclusive}");
		}

		if (ReadString(version, "maxExclusive") is { Length: > 0 } maxExclusive)
		{
			parts.Add($"< {maxExclusive}");
		}

		return parts.Count > 0 ? string.Join(" ", parts) : version.GetRawText();
	}

	private static string? ReadFirstOsFamily(JsonElement runtime)
	{
		if (runtime.ValueKind != JsonValueKind.Object ||
			!runtime.TryGetProperty("osSupport", out var osSupport) ||
			osSupport.ValueKind != JsonValueKind.Array)
		{
			return null;
		}

		return osSupport.EnumerateArray()
			.Select(item => ReadFirstString(item, "type", "family", "name", "os"))
			.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
	}

	private static string ReadOsSupportText(JsonElement item)
	{
		if (item.ValueKind == JsonValueKind.String)
		{
			return item.GetString() ?? string.Empty;
		}

		if (item.ValueKind != JsonValueKind.Object)
		{
			return item.ToString();
		}

		var family = ReadFirstString(item, "type", "family", "name", "os");
		var version = ReadFirstString(item, "version", "minVersion", "testedVersion");
		return JoinParts(family, version);
	}

	private static string? ReadNestedString(JsonElement item, string objectName, params string[] names)
	{
		if (item.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		if (!item.TryGetProperty(objectName, out var nested) || nested.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		return ReadFirstString(nested, names);
	}

	private static bool HasDefaultValue(JsonElement item) =>
		item.ValueKind == JsonValueKind.Object &&
		item.TryGetProperty("defaultValue", out var defaultValue) &&
		defaultValue.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

	private static bool IsListType(string? valueType)
	{
		if (string.IsNullOrWhiteSpace(valueType))
		{
			return false;
		}

		var normalized = valueType.Trim().ToLowerInvariant();
		return normalized.Contains("list", StringComparison.Ordinal) ||
			normalized.Contains("array", StringComparison.Ordinal) ||
			normalized.EndsWith("[]", StringComparison.Ordinal);
	}

	private static int CountArrayProperty(JsonElement item, string name)
	{
		return item.ValueKind == JsonValueKind.Object &&
			item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
			? value.GetArrayLength()
			: 0;
	}

	private static string JoinParts(params string?[] parts) =>
		string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

	private static string Truncate(string value, int maxLength)
	{
		if (value.Length <= maxLength)
		{
			return value;
		}

		return value[..Math.Max(0, maxLength - 3)] + "...";
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
		if (item.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

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

	public bool CanImportApplicationKnowledge => _parent.CanImportApplicationKnowledge;

	public bool CanManageDeployments => _parent.CanManageDeployments;

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
	[ObservableProperty] private bool _isImportingManifest;
	[ObservableProperty] private string? _importManifestError;
	[ObservableProperty] private ApplicationVersionOptionViewModel? _selectedInstallVersion;
	[ObservableProperty] private ApplicationUnitOptionViewModel? _selectedInstallUnit;
	[ObservableProperty] private InstallationProfileOptionViewModel? _selectedInstallProfile;
	[ObservableProperty] private ServerOptionViewModel? _selectedInstallServer;
	[ObservableProperty] private string _installName = string.Empty;
	[ObservableProperty] private string _installNotes = string.Empty;
	[ObservableProperty] private bool _isPreparingInstallation;
	[ObservableProperty] private bool _isCreatingInstallation;
	[ObservableProperty] private string? _installError;
	private int _installationDetailRequestId;

	public ObservableCollection<ManifestAssociationViewModel> ManifestAssociations { get; } = [];

	public ObservableCollection<ApplicationVersionOptionViewModel> VersionOptions { get; } = [];

	public ObservableCollection<ApplicationUnitOptionViewModel> InstallUnitOptions { get; } = [];

	public ObservableCollection<InstallationProfileOptionViewModel> InstallProfileOptions { get; } = [];

	public ObservableCollection<ServerOptionViewModel> InstallServerOptions { get; } = [];

	public ObservableCollection<DataServiceOptionViewModel> InstallDataServiceOptions { get; } = [];

	public ObservableCollection<InstallationBindingViewModel> InstallationBindings { get; } = [];

	public string VersionCountText => VersionCount == 1 ? "1 version" : $"{VersionCount} versions";

	public string KnowledgeSummary => $"{ConfigurationKeyCount} keys | {DependencyCount} dependencies | {PlaceholderCount} placeholders";

	public string LastImportText => LastImportedAtUtc is { } value
		? $"Last import: {value.ToLocalTime():g}"
		: "No imported knowledge yet";

	public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

	public bool HasManifestValidation => ManifestValidation is not null;

	public bool CanStartManifestImport => CanImportApplicationKnowledge && ManifestValidation?.CanStartImport == true;

	public bool HasImportManifestError => !string.IsNullOrWhiteSpace(ImportManifestError);

	public bool HasManifestAssociations => ManifestAssociations.Count > 0;

	public bool HasUnresolvedRequiredManifestAssociations => ManifestAssociations.Any(association => association.IsUnresolvedRequired);

	public bool HasInstallError => !string.IsNullOrWhiteSpace(InstallError);

	public bool HasInstallUnitOptions => InstallUnitOptions.Count > 0;

	public bool HasInstallProfileOptions => InstallProfileOptions.Count > 0;

	public bool HasInstallationBindings => InstallationBindings.Count > 0;

	public bool HasNoInstallationBindings => !HasInstallationBindings;

	public bool CanRequestNewInstallation => CanManageDeployments && VersionOptions.Count > 0;

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

	partial void OnManifestValidationChanged(ManifestValidationViewModel? value)
	{
		OnPropertyChanged(nameof(HasManifestValidation));
		OnPropertyChanged(nameof(CanStartManifestImport));
		RequestManifestImportCommand.NotifyCanExecuteChanged();
		PrepareManifestImportFields(value);
	}

	partial void OnImportManifestErrorChanged(string? value) => OnPropertyChanged(nameof(HasImportManifestError));

	partial void OnInstallErrorChanged(string? value) => OnPropertyChanged(nameof(HasInstallError));

	partial void OnSelectedInstallVersionChanged(ApplicationVersionOptionViewModel? value)
	{
		SetDefaultInstallName();
		if (value is not null)
		{
			_ = LoadInstallVersionDetailAsync(value);
		}
	}

	partial void OnSelectedInstallUnitChanged(ApplicationUnitOptionViewModel? value) => SetDefaultInstallName();

	partial void OnSelectedInstallProfileChanged(InstallationProfileOptionViewModel? value) => SetDefaultInstallName();

	partial void OnSelectedInstallServerChanged(ServerOptionViewModel? value) => SetDefaultInstallName();

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

		VersionOptions.Clear();
		foreach (var version in application.Versions
			.OrderByDescending(v => v.LastImportedAtUtc ?? DateTimeOffset.MinValue)
			.ThenByDescending(v => v.Version, StringComparer.OrdinalIgnoreCase))
		{
			VersionOptions.Add(new ApplicationVersionOptionViewModel(version));
		}

		OnPropertyChanged(nameof(VersionCountText));
		OnPropertyChanged(nameof(KnowledgeSummary));
		OnPropertyChanged(nameof(LastImportText));
		OnPropertyChanged(nameof(HasArtifact));
		OnPropertyChanged(nameof(ArtifactSummary));
		OnPropertyChanged(nameof(CanRequestNewInstallation));
		RequestNewInstallationCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand(CanExecute = nameof(CanRequestNewInstallation))]
	private async Task RequestNewInstallationAsync()
	{
		ResetInstallationDraft();
		_parent.RaiseNewApplicationInstallationRequested(this);
		await PrepareInstallationAsync();
	}

	private void ResetInstallationDraft()
	{
		InstallName = string.Empty;
		InstallNotes = string.Empty;
		InstallError = null;
		SelectedInstallUnit = null;
		SelectedInstallProfile = null;
		SelectedInstallServer = null;
		InstallUnitOptions.Clear();
		InstallProfileOptions.Clear();
		InstallServerOptions.Clear();
		InstallDataServiceOptions.Clear();
		InstallationBindings.Clear();
		NotifyInstallationCollectionsChanged();
		SelectedInstallVersion = VersionOptions.FirstOrDefault();
	}

	private async Task PrepareInstallationAsync()
	{
		IsPreparingInstallation = true;
		InstallError = null;

		try
		{
			var serversTask = _api.GetServersAsync();
			var dataServicesTask = _api.GetDataServicesAsync();
			var servers = await serversTask;
			var dataServices = await dataServicesTask;

			InstallServerOptions.Clear();
			foreach (var server in servers.Where(s => s.IsActive).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
			{
				InstallServerOptions.Add(new ServerOptionViewModel(server));
			}

			InstallDataServiceOptions.Clear();
			foreach (var service in dataServices.Where(s => s.IsActive).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
			{
				InstallDataServiceOptions.Add(new DataServiceOptionViewModel(service));
			}

			SelectedInstallServer = InstallServerOptions.FirstOrDefault();

			if (SelectedInstallVersion is { } version)
			{
				await LoadInstallVersionDetailAsync(version);
			}
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			InstallError = ex.Message;
		}
		finally
		{
			IsPreparingInstallation = false;
		}
	}

	private async Task LoadInstallVersionDetailAsync(ApplicationVersionOptionViewModel version)
	{
		var requestId = Interlocked.Increment(ref _installationDetailRequestId);
		IsPreparingInstallation = true;
		InstallError = null;

		try
		{
			var detail = await _api.GetApplicationVersionDetailAsync(_applicationId, version.Id);
			if (requestId != _installationDetailRequestId)
			{
				return;
			}

			InstallUnitOptions.Clear();
			foreach (var unit in detail.ApplicationUnits.OrderBy(u => u.Key, StringComparer.OrdinalIgnoreCase))
			{
				InstallUnitOptions.Add(new ApplicationUnitOptionViewModel(unit));
			}

			InstallProfileOptions.Clear();
			foreach (var profile in detail.InstallationProfiles.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
			{
				InstallProfileOptions.Add(new InstallationProfileOptionViewModel(profile));
			}

			InstallationBindings.Clear();
			foreach (var binding in BuildInstallationBindingRows(detail))
			{
				InstallationBindings.Add(binding);
			}

			SelectedInstallUnit = InstallUnitOptions.FirstOrDefault();
			SelectedInstallProfile = InstallProfileOptions.FirstOrDefault(profile => profile.Required) ?? InstallProfileOptions.FirstOrDefault();
			NotifyInstallationCollectionsChanged();
			SetDefaultInstallName();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			if (requestId == _installationDetailRequestId)
			{
				InstallError = ex.Message;
			}
		}
		finally
		{
			if (requestId == _installationDetailRequestId)
			{
				IsPreparingInstallation = false;
			}
		}
	}

	private IEnumerable<InstallationBindingViewModel> BuildInstallationBindingRows(ApplicationVersionDetailResponse detail)
	{
		var rows = new List<InstallationBindingViewModel>();
		foreach (var dependency in detail.Dependencies.Where(IsApplicationDependencyResponse))
		{
			rows.Add(InstallationBindingViewModel.ForApplication(
				dependency.Name,
				dependency.Category,
				dependency.PlaceholderKey,
				dependency.Required,
				dependency.ProviderApplicationSlug,
				dependency.ProviderPlaceholderKey));
		}

		var dependencyPlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var dependency in detail.Dependencies.Where(IsDataServiceDependency))
		{
			if (string.IsNullOrWhiteSpace(dependency.PlaceholderKey))
			{
				continue;
			}

			dependencyPlaceholders.Add(dependency.PlaceholderKey);
			rows.Add(InstallationBindingViewModel.ForDataService(
				dependency.Name,
				dependency.Category,
				dependency.PlaceholderKey,
				dependency.Required,
				dependency.Description,
				InstallDataServiceOptions));
		}

		foreach (var key in detail.ConfigurationKeys.Where(IsServiceReferenceKey))
		{
			if (string.IsNullOrWhiteSpace(key.PlaceholderKey) || dependencyPlaceholders.Contains(key.PlaceholderKey))
			{
				continue;
			}

			rows.Add(InstallationBindingViewModel.ForDataService(
				key.Key,
				key.ValueType ?? key.Scope ?? "serviceReference",
				key.PlaceholderKey,
				key.Required,
				key.Description,
				InstallDataServiceOptions));
		}

		return rows.OrderBy(row => row.TargetKind).ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase);
	}

	private static bool IsApplicationDependencyResponse(DependencyResponse dependency) =>
		string.Equals(dependency.Category, "application", StringComparison.OrdinalIgnoreCase) ||
		!string.IsNullOrWhiteSpace(dependency.ProviderApplicationSlug);

	private static bool IsDataServiceDependency(DependencyResponse dependency) =>
		!IsApplicationDependencyResponse(dependency) &&
		!string.IsNullOrWhiteSpace(dependency.PlaceholderKey);

	private static bool IsServiceReferenceKey(ConfigurationKeyResponse key) =>
		string.Equals(key.Scope, "serviceReference", StringComparison.OrdinalIgnoreCase) ||
		(!string.IsNullOrWhiteSpace(key.ResolutionJson) &&
		 key.ResolutionJson.Contains("serviceReference", StringComparison.OrdinalIgnoreCase));

	private void NotifyInstallationCollectionsChanged()
	{
		OnPropertyChanged(nameof(HasInstallUnitOptions));
		OnPropertyChanged(nameof(HasInstallProfileOptions));
		OnPropertyChanged(nameof(HasInstallationBindings));
		OnPropertyChanged(nameof(HasNoInstallationBindings));
	}

	private void SetDefaultInstallName()
	{
		if (!string.IsNullOrWhiteSpace(InstallName) ||
			SelectedInstallVersion is null ||
			SelectedInstallServer is null)
		{
			return;
		}

		var unit = SelectedInstallUnit?.Key;
		var profile = SelectedInstallProfile?.Key;
		var parts = new[] { Slug, unit, profile, SelectedInstallServer.Name }
			.Where(part => !string.IsNullOrWhiteSpace(part))
			.Select(part => part!.Trim().ToLowerInvariant());
		InstallName = string.Join("-", parts);
	}

	public event EventHandler? ApplicationInstallationCompleted;

	[RelayCommand]
	private async Task CreateInstallationAsync()
	{
		if (SelectedInstallVersion is null)
		{
			InstallError = "Select the release to install.";
			return;
		}

		if (SelectedInstallServer is null)
		{
			InstallError = "Select the server that will host this application.";
			return;
		}

		var name = InstallName.Trim();
		if (name.Length == 0)
		{
			InstallError = "Installation name is required.";
			return;
		}

		var missing = InstallationBindings.Where(binding => binding.IsMissingRequired).ToArray();
		if (missing.Length > 0)
		{
			InstallError = $"Resolve required binding: {missing[0].Title}.";
			return;
		}

		IsCreatingInstallation = true;
		InstallError = null;

		try
		{
			var bindings = InstallationBindings
				.Select(binding => binding.ToInput())
				.Where(binding => binding is not null)
				.Cast<ApplicationInstallationBindingInput>()
				.ToArray();

			await _api.CreateApplicationInstallationAsync(_applicationId, new CreateApplicationInstallationRequest(
				name,
				SelectedInstallVersion.Id,
				SelectedInstallServer.Id,
				SelectedInstallServer.Environment,
				SelectedInstallUnit?.Key,
				SelectedInstallProfile?.Key,
				ApplicationsViewModel.Clean(InstallNotes),
				bindings));

			ApplicationInstallationCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			InstallError = ex.Message;
		}
		finally
		{
			IsCreatingInstallation = false;
		}
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
			ManifestValidation = ManifestValidator.Validate(result.FileName, json, _parent.Applications, Name, Slug, RuntimeType);
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

	private void PrepareManifestImportFields(ManifestValidationViewModel? validation)
	{
		ClearManifestAssociations();
		var draft = validation?.ImportDraft;
		if (draft is null)
		{
			ImportManifestError = null;
			OnPropertyChanged(nameof(HasManifestAssociations));
			OnPropertyChanged(nameof(HasUnresolvedRequiredManifestAssociations));
			return;
		}

		var applicationOptions = _parent.Applications
			.Select(application => new ApplicationOptionViewModel(application.Slug, application.Name))
			.OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
			.ToArray();
		foreach (var dependency in draft.Package.Dependencies.Where(IsApplicationDependency))
		{
			var association = new ManifestAssociationViewModel(
				dependency.Name,
				dependency.Category,
				dependency.Required,
				dependency.PlaceholderKey,
				dependency.ProviderPlaceholderKey,
				dependency.ProviderApplicationSlug,
				applicationOptions);
			association.PropertyChanged += OnManifestAssociationChanged;
			ManifestAssociations.Add(association);
		}

		ImportManifestError = null;
		OnPropertyChanged(nameof(HasManifestAssociations));
		OnPropertyChanged(nameof(HasUnresolvedRequiredManifestAssociations));
	}

	private void OnManifestAssociationChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(ManifestAssociationViewModel.SelectedApplication) or nameof(ManifestAssociationViewModel.IsUnresolvedRequired))
		{
			OnPropertyChanged(nameof(HasUnresolvedRequiredManifestAssociations));
		}
	}

	private void ClearManifestAssociations()
	{
		foreach (var association in ManifestAssociations)
		{
			association.PropertyChanged -= OnManifestAssociationChanged;
		}

		ManifestAssociations.Clear();
	}

	[RelayCommand(CanExecute = nameof(CanStartManifestImport))]
	private void RequestManifestImport()
	{
		ImportManifestError = null;
		_parent.RaiseImportManifestRequested(this);
	}

	public event EventHandler? ManifestImportCompleted;

	[RelayCommand]
	private async Task ImportManifestAsync()
	{
		var draft = ManifestValidation?.ImportDraft;
		if (draft is null)
		{
			ImportManifestError = "Upload and validate a manifest before importing.";
			return;
		}

		if (string.IsNullOrWhiteSpace(draft.SuggestedVersion) || string.IsNullOrWhiteSpace(draft.SuggestedSourceReference))
		{
			ImportManifestError = "The manifest must declare release version and sourceReference before import.";
			return;
		}

		if (ManifestAssociations.Any(association => association.IsUnresolvedRequired))
		{
			ImportManifestError = "Resolve every required application association before importing.";
			OnPropertyChanged(nameof(HasUnresolvedRequiredManifestAssociations));
			return;
		}

		IsImportingManifest = true;
		ImportManifestError = null;

		try
		{
			var package = BuildPackageForImport(draft);
			var createdVersion = await _api.AddApplicationVersionAsync(
				_applicationId,
				new AddApplicationVersionRequest(draft.SuggestedVersion.Trim(), draft.SuggestedSourceReference.Trim(), draft.SuggestedRuntimeMetadata));
			await _api.ImportConfigurationPackageAsync(_applicationId, createdVersion.Id, package);
			await _parent.ReloadAsync();
			ManifestImportCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			ImportManifestError = ex.Message;
		}
		finally
		{
			IsImportingManifest = false;
		}
	}

	private ImportConfigurationPackageRequest BuildPackageForImport(ManifestImportDraft draft)
	{
		var dependencies = draft.Package.Dependencies
			.Select(dependency =>
			{
				var association = ManifestAssociations.FirstOrDefault(item =>
					string.Equals(item.DependencyName, dependency.Name, StringComparison.OrdinalIgnoreCase));
				return association?.SelectedApplication is null
					? dependency
					: dependency with { ProviderApplicationSlug = association.SelectedApplication.Slug };
			})
			.ToArray();
		var warnings = draft.Package.Warnings?.ToList() ?? [];
		warnings.AddRange(ManifestAssociations
			.Where(association => association.SelectedApplication is not null)
			.Select(association => $"Application dependency '{association.DependencyName}' associated to Iris application '{association.SelectedApplication!.Slug}' during import."));
		return draft.Package with { Dependencies = dependencies, Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
	}

	private static bool IsApplicationDependency(DependencyInput dependency) =>
		string.Equals(dependency.Category, "application", StringComparison.OrdinalIgnoreCase) ||
		!string.IsNullOrWhiteSpace(dependency.ProviderApplicationSlug);

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
