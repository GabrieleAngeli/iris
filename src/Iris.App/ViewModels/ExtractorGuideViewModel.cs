using System.Collections.ObjectModel;
using Iris.App.Controls;

namespace Iris.App.ViewModels;

/// <summary>Static how-to for downloading/using the Iris Extractor CLI. No API calls — the content
/// mirrors <c>docs/application-assimilation.md</c> so operators without repo access still see it.</summary>
public sealed partial class ExtractorGuideViewModel : ObservableObject
{
    public ExtractorGuideViewModel()
    {
        SharedManifestTabs =
        [
            new TabGroupItem
            {
                Title = "Import",
                Content = ManualImportPowerShell,
            },
            new TabGroupItem
            {
                Title = "Empty manifest",
                Content = ManualTemplate,
            },
        ];

        TechnologyGuides =
        [
            new ExtractorTechnologyGuide(
                "C# / .NET",
                [
                    new TabGroupItem
                    {
                        Title = "Automatic",
                        Content = $"""
                            The .NET extractor is available today.

                            It scans appsettings*.json, IConfiguration usage, ConnectionStrings and launchSettings.json without building the target project.

                            Package:
                            {PackCommand}

                            Install:
                            {InstallCommand}

                            Run:
                            {RunCommand}

                            Pipeline:
                            {PipelineSnippet}
                            """,
                    },
                    new TabGroupItem
                    {
                        Title = "Manual manifest",
                        Content = $"""
                            Compose the manifest by inspecting appsettings files, ConnectionStrings, launchSettings, IConfiguration usage and options binding.

                            {DotNetManualSample}
                            """,
                    },
                ]),
            new ExtractorTechnologyGuide(
                "Java / Spring",
                [
                    new TabGroupItem
                    {
                        Title = "Automatic",
                        Content = """
                            Extractor not built yet.

                            The planned scanner should read application.yml/properties, profile files, spring.datasource, spring.data.redis, spring.kafka, @Value, @ConfigurationProperties, server.port and Actuator endpoints.
                            """,
                    },
                    new TabGroupItem
                    {
                        Title = "Manual manifest",
                        Content = $"""
                            Compose the manifest from Spring config, datasource/cache/message broker settings and any value injected through code annotations.

                            {JavaManualSample}
                            """,
                    },
                ]),
            new ExtractorTechnologyGuide(
                "Node / JavaScript",
                [
                    new TabGroupItem
                    {
                        Title = "Automatic",
                        Content = """
                            Extractor not built yet.

                            The planned scanner should read .env.example, process.env usage, framework config files, package scripts, build output, health endpoints and PORT/HOST settings.
                            """,
                    },
                    new TabGroupItem
                    {
                        Title = "Manual manifest",
                        Content = $"""
                            Compose the manifest from .env templates, process.env references, next/vite/nuxt config, health endpoints and PORT/HOST settings.

                            {NodeManualSample}
                            """,
                    },
                ]),
            new ExtractorTechnologyGuide(
                "Docker / container",
                [
                    new TabGroupItem
                    {
                        Title = "Automatic",
                        Content = """
                            Extractor not built yet.

                            The planned scanner should read Dockerfile ENV/ARG/EXPOSE/HEALTHCHECK, compose files, Helm values, ConfigMap, Secret and immutable image coordinates.
                            """,
                    },
                    new TabGroupItem
                    {
                        Title = "Manual manifest",
                        Content = $"""
                            Compose the manifest from container env vars, compose/Helm config, exposed ports, healthcheck and the registry image tag used in deploy.

                            {DockerManualSample}
                            """,
                    },
                ]),
            new ExtractorTechnologyGuide(
                "Ansible Jinja2 template",
                [
                    new TabGroupItem
                    {
                        Title = "Automatic",
                        Content = """
                            Good standardization target, not built yet.

                            A future parser can read .j2 files and extract variables such as {{ app_db_url }} as configuration keys, while marking conditional or loop-generated values as warnings.

                            This is useful because Ansible templates often show the final runtime config shape even when the source app does not contain production settings.
                            """,
                    },
                    new TabGroupItem
                    {
                        Title = "Manual manifest",
                        Content = $"""
                            Compose the manifest from Jinja variables, rendered target files, Ansible defaults/vars and template conditions. Use targetKind ansible:j2.

                            {AnsibleJinjaManualSample}
                            """,
                    },
                ]),
        ];
    }

    public ObservableCollection<TabGroupItem> SharedManifestTabs { get; }

    public ObservableCollection<ExtractorTechnologyGuide> TechnologyGuides { get; }

    public const string PackCommand = "dotnet pack src/Iris.Extractor -c Release -o ./nupkg";

    public const string InstallCommand = "dotnet tool install --global Iris.Extractor --add-source ./nupkg";

    public const string RunCommand = "iris-extractor dotnet --root src/MyApp --output iris-package.json";

    public const string PipelineSnippet = """
        - script: iris-extractor dotnet --root src/MyApp --output iris-package.json
          env:
            IRIS_API: $(IRIS_API)
            IRIS_APPLICATION_ID: $(IRIS_APPLICATION_ID)
            IRIS_VERSION_ID: $(IRIS_VERSION_ID)
            IRIS_TOKEN: $(IRIS_TOKEN)
        """;

    public const string ManualImportPowerShell = """
        $irisApi = "http://localhost:5000"
        $applicationId = "<application-id>"
        $versionId = "<version-id>"
        $token = "<iris-session-token>"

        Invoke-RestMethod `
          -Method Post `
          -Uri "$irisApi/applications/$applicationId/versions/$versionId/import" `
          -Headers @{ Authorization = "Bearer $token" } `
          -ContentType "application/json" `
          -InFile ".\iris-package.json"
        """;

    public const string ManualTemplate = """
        {
          "schemaVersion": "1.0",
          "configurationKeys": [],
          "dependencies": [],
          "placeholders": [],
          "warnings": []
        }
        """;

    public const string DotNetManualSample = """
        {
          "schemaVersion": "1.0",
          "configurationKeys": [
            {
              "key": "ConnectionStrings:Main",
              "targetKind": "appsettings.json",
              "required": true,
              "secret": true,
              "defaultValue": null,
              "description": "Primary application database",
              "purpose": "database",
              "placeholderKey": "domain.orders.db.connectionString"
            },
            {
              "key": "Integrations:Payments:BaseUrl",
              "targetKind": "code:IConfiguration",
              "required": true,
              "secret": false,
              "defaultValue": null,
              "description": "Payments API base URL",
              "purpose": "http",
              "placeholderKey": "domain.payments.api.baseUrl"
            }
          ],
          "dependencies": [
            {
              "name": "main-db",
              "category": "database",
              "required": true,
              "description": "Primary relational database",
              "placeholderKey": "domain.orders.db",
              "providerApplicationSlug": null,
              "providerPlaceholderKey": null
            }
          ],
          "placeholders": [
            {
              "key": "domain.orders.api.baseUrl",
              "category": "http",
              "description": "Base URL exposed by Orders API",
              "required": true
            }
          ],
          "warnings": [
            "launchSettings.json exposes port 5080; set RequiredPorts on the Iris application version."
          ]
        }
        """;

    public const string NodeManualSample = """
        {
          "schemaVersion": "1.0",
          "configurationKeys": [
            {
              "key": "DATABASE_URL",
              "targetKind": ".env.example",
              "required": true,
              "secret": true,
              "defaultValue": null,
              "description": "Database URL used by the Node service",
              "purpose": "database",
              "placeholderKey": "domain.catalog.db.url"
            },
            {
              "key": "PUBLIC_API_BASE_URL",
              "targetKind": "code:process.env",
              "required": true,
              "secret": false,
              "defaultValue": null,
              "description": "Backend API URL used by the frontend",
              "purpose": "http",
              "placeholderKey": "domain.catalog.api.baseUrl"
            }
          ],
          "dependencies": [
            {
              "name": "redis-cache",
              "category": "cache",
              "required": false,
              "description": "Optional Redis cache",
              "placeholderKey": "domain.catalog.cache.redis",
              "providerApplicationSlug": null,
              "providerPlaceholderKey": null
            }
          ],
          "placeholders": [],
          "warnings": []
        }
        """;

    public const string JavaManualSample = """
        {
          "schemaVersion": "1.0",
          "configurationKeys": [
            {
              "key": "spring.datasource.url",
              "targetKind": "application.yml",
              "required": true,
              "secret": false,
              "defaultValue": null,
              "description": "JDBC database URL",
              "purpose": "database",
              "placeholderKey": "domain.billing.db.jdbcUrl"
            },
            {
              "key": "spring.datasource.password",
              "targetKind": "application.yml",
              "required": true,
              "secret": true,
              "defaultValue": null,
              "description": "Database password",
              "purpose": "database",
              "placeholderKey": "domain.billing.db.password"
            }
          ],
          "dependencies": [
            {
              "name": "billing-db",
              "category": "database",
              "required": true,
              "description": "PostgreSQL database for Billing",
              "placeholderKey": "domain.billing.db",
              "providerApplicationSlug": null,
              "providerPlaceholderKey": null
            }
          ],
          "placeholders": [
            {
              "key": "domain.billing.api.baseUrl",
              "category": "http",
              "description": "Base URL exposed by Billing service",
              "required": true
            }
          ],
          "warnings": []
        }
        """;

    public const string DockerManualSample = """
        {
          "schemaVersion": "1.0",
          "configurationKeys": [
            {
              "key": "APP_ENV",
              "targetKind": "dockerfile:ENV",
              "required": true,
              "secret": false,
              "defaultValue": "Production",
              "description": "Runtime environment",
              "purpose": "runtime",
              "placeholderKey": null
            },
            {
              "key": "OPENBAO_TOKEN",
              "targetKind": "compose:environment",
              "required": true,
              "secret": true,
              "defaultValue": null,
              "description": "Token or reference used to resolve secrets at runtime",
              "purpose": "secret-bootstrap",
              "placeholderKey": "platform.openbao.token"
            }
          ],
          "dependencies": [],
          "placeholders": [
            {
              "key": "domain.worker.health.url",
              "category": "http",
              "description": "Worker health endpoint exposed through the platform",
              "required": false
            }
          ],
          "warnings": [
            "Dockerfile EXPOSE 8080 must be copied into RequiredPorts when creating the application version."
          ]
        }
        """;

    public const string AnsibleJinjaManualSample = """
        {
          "schemaVersion": "1.0",
          "configurationKeys": [
            {
              "key": "ConnectionStrings:Main",
              "targetKind": "ansible:j2",
              "required": true,
              "secret": true,
              "defaultValue": null,
              "description": "Value rendered into appsettings.json.j2 from {{ iris_connectionstrings_main }}",
              "purpose": "database",
              "placeholderKey": "domain.orders.db.connectionString"
            },
            {
              "key": "Integrations:Payments:BaseUrl",
              "targetKind": "ansible:j2",
              "required": true,
              "secret": false,
              "defaultValue": null,
              "description": "Value rendered into appsettings.json.j2 from {{ iris_payments_base_url }}",
              "purpose": "http",
              "placeholderKey": "domain.payments.api.baseUrl"
            }
          ],
          "dependencies": [
            {
              "name": "orders-db",
              "category": "database",
              "required": true,
              "description": "Database reached through the rendered appsettings file",
              "placeholderKey": "domain.orders.db",
              "providerApplicationSlug": null,
              "providerPlaceholderKey": null
            }
          ],
          "placeholders": [],
          "warnings": [
            "Review Jinja variables used only in conditionals or loops; automatic interpretation should mark uncertain values as warnings."
          ]
        }
        """;

    [RelayCommand]
    private async Task CopyAsync(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            await Clipboard.Default.SetTextAsync(text);
        }
    }
}

public sealed partial class ExtractorTechnologyGuide : ObservableObject
{
    public ExtractorTechnologyGuide(string title, IEnumerable<TabGroupItem> tabs)
    {
        Title = title;
        Tabs = new ObservableCollection<TabGroupItem>(tabs);
    }

    public string Title { get; }

    public ObservableCollection<TabGroupItem> Tabs { get; }

    [ObservableProperty] private int _selectedIndex;
}
