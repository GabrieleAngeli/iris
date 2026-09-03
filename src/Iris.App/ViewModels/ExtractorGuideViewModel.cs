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
                Title = "Fields",
                Blocks =
                [
                    Text("What the manifest describes", "iris-package.json is not a secrets file and is not the final environment file. It is the contract Iris imports for one application version: what the application needs, what external resources it depends on, and what values it exposes to other applications or deployment automation."),
                    Text("configurationKeys", "Use configurationKeys for values the application must receive at runtime or deploy time. key is the exact key as the technology sees it, targetKind says where it was found, required marks whether deploy must block without it, secret marks values that must be resolved from a secret store, defaultValue is allowed only for non-sensitive safe defaults, purpose groups the intent, and placeholderKey gives Iris a stable logical binding."),
                    Text("dependencies", "Use dependencies for resources consumed by the application: PostgreSQL, SQL Server, Redis, HTTP APIs, queues, topics, object storage, filesystem paths, OpenBao, Ansible/AWX, or another Iris application. When the dependency is satisfied by another application, set providerApplicationSlug and providerPlaceholderKey."),
                    Text("placeholders", "Use placeholders for values this application or its deployment exposes for others to consume. Examples: public base URL, internal service URL, health URL, queue name, topic name, exported connection name, or a generated endpoint."),
                    Note("Sensitive values", "Never put real passwords, tokens, private keys or production connection strings in defaultValue. Mark the key as secret and let Iris bind placeholderKey to OpenBao, a managed data service, a pipeline variable group, or a deployment-time resolver."),
                    Code("PostgreSQL connection string", PostgresConnectionStringExample, "json"),
                    Code("Redis endpoint and HTTP provider", RedisAndHttpExample, "json"),
                    Code("Naming convention", PlaceholderNamingExamples, "text"),
                ],
            },
            new TabGroupItem
            {
                Title = "Import",
                Blocks =
                [
                    Text("When to use it", "Use this request when the package has already been composed by the extractor or by hand."),
                    Code("PowerShell import", ManualImportPowerShell, "powershell"),
                ],
            },
            new TabGroupItem
            {
                Title = "Empty manifest",
                Blocks =
                [
                    Text("Minimum contract", "Start from this shape when a technology does not have an automatic extractor yet."),
                    Code("iris-package.json", ManualTemplate, "json"),
                ],
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
                        Blocks =
                        [
                            Text("Available today", "Scans appsettings*.json, IConfiguration usage, ConnectionStrings and launchSettings.json without building the target project."),
                            Code("Package", PackCommand, "shell"),
                            Code("Install", InstallCommand, "shell"),
                            Code("Run", RunCommand, "shell"),
                            Code("Azure DevOps pipeline", PipelineSnippet, "yaml"),
                        ],
                    },
                    new TabGroupItem
                    {
                        Title = "Manual manifest",
                        Blocks =
                        [
                            Text("How to compose it", "Inspect appsettings files, ConnectionStrings, launchSettings, IConfiguration usage and options binding."),
                            Code("iris-package.json", DotNetManualSample, "json"),
                        ],
                    },
                ]),
            new ExtractorTechnologyGuide(
                "Java / Spring",
                [
                    new TabGroupItem
                    {
                        Title = "Automatic",
                        Blocks =
                        [
                            Note("Planned extractor", "The scanner is not built yet."),
                            Text("Expected sources", "Read application.yml/properties, profile files, spring.datasource, spring.data.redis, spring.kafka, @Value, @ConfigurationProperties, server.port and Actuator endpoints."),
                        ],
                    },
                    new TabGroupItem
                    {
                        Title = "Manual manifest",
                        Blocks =
                        [
                            Text("How to compose it", "Use Spring config, datasource/cache/message broker settings and values injected through code annotations."),
                            Code("iris-package.json", JavaManualSample, "json"),
                        ],
                    },
                ]),
            new ExtractorTechnologyGuide(
                "Node / JavaScript",
                [
                    new TabGroupItem
                    {
                        Title = "Automatic",
                        Blocks =
                        [
                            Note("Planned extractor", "The scanner is not built yet."),
                            Text("Expected sources", "Read .env.example, process.env usage, framework config files, package scripts, build output, health endpoints and PORT/HOST settings."),
                        ],
                    },
                    new TabGroupItem
                    {
                        Title = "Manual manifest",
                        Blocks =
                        [
                            Text("How to compose it", "Use .env templates, process.env references, next/vite/nuxt config, health endpoints and PORT/HOST settings."),
                            Code("iris-package.json", NodeManualSample, "json"),
                        ],
                    },
                ]),
            new ExtractorTechnologyGuide(
                "Docker / container",
                [
                    new TabGroupItem
                    {
                        Title = "Automatic",
                        Blocks =
                        [
                            Note("Planned extractor", "The scanner is not built yet."),
                            Text("Expected sources", "Read Dockerfile ENV/ARG/EXPOSE/HEALTHCHECK, compose files, Helm values, ConfigMap, Secret and immutable image coordinates."),
                        ],
                    },
                    new TabGroupItem
                    {
                        Title = "Manual manifest",
                        Blocks =
                        [
                            Text("How to compose it", "Use container env vars, compose/Helm config, exposed ports, healthcheck and the registry image tag used in deploy."),
                            Code("iris-package.json", DockerManualSample, "json"),
                        ],
                    },
                ]),
            new ExtractorTechnologyGuide(
                "Ansible Jinja2 template",
                [
                    new TabGroupItem
                    {
                        Title = "Automatic",
                        Blocks =
                        [
                            Note("Good standardization target", "The parser is not built yet."),
                            Text("Expected behavior", "Read .j2 files and extract variables such as {{ app_db_url }} as configuration keys, while marking conditional or loop-generated values as warnings."),
                            Text("Why it matters", "Ansible templates often show the final runtime config shape even when the source app does not contain production settings."),
                        ],
                    },
                    new TabGroupItem
                    {
                        Title = "Manual manifest",
                        Blocks =
                        [
                            Text("How to compose it", "Use Jinja variables, rendered target files, Ansible defaults/vars and template conditions. Set targetKind to ansible:j2."),
                            Code("iris-package.json", AnsibleJinjaManualSample, "json"),
                        ],
                    },
                ]),
        ];
    }

    public ObservableCollection<TabGroupItem> SharedManifestTabs { get; }

    public ObservableCollection<ExtractorTechnologyGuide> TechnologyGuides { get; }

    private static TabContentBlock Text(string title, string text) =>
        new()
        {
            Kind = TabContentBlockKind.Text,
            Title = title,
            Text = text,
        };

    private static TabContentBlock Code(string title, string text, string language = "") =>
        new()
        {
            Kind = TabContentBlockKind.Code,
            Title = title,
            Language = language,
            Text = text,
        };

    private static TabContentBlock Note(string title, string text) =>
        new()
        {
            Kind = TabContentBlockKind.Note,
            Title = title,
            Text = text,
        };

    public const string PackCommand = "dotnet pack src/Iris.Extractor -c Release -o ./nupkg";

    public const string InstallCommand = "dotnet tool install --global Iris.Extractor --add-source ./nupkg";

    public const string RunCommand = "iris-extractor dotnet --root src/MyApp --output iris-package.json";

    public const string PostgresConnectionStringExample = """
        {
          "configurationKeys": [
            {
              "key": "ConnectionStrings:Main",
              "targetKind": "appsettings.json",
              "required": true,
              "secret": true,
              "defaultValue": null,
              "description": "Npgsql connection string for the primary PostgreSQL database",
              "purpose": "database:postgresql:connection-string",
              "placeholderKey": "domain.orders.db.postgresql.connectionString"
            }
          ],
          "dependencies": [
            {
              "name": "orders-postgres",
              "category": "database:postgresql",
              "required": true,
              "description": "Managed PostgreSQL instance configured in Iris Infrastructure",
              "placeholderKey": "domain.orders.db.postgresql",
              "providerApplicationSlug": null,
              "providerPlaceholderKey": null
            }
          ]
        }
        """;

    public const string RedisAndHttpExample = """
        {
          "configurationKeys": [
            {
              "key": "Redis:ConnectionString",
              "targetKind": "appsettings.json",
              "required": false,
              "secret": true,
              "defaultValue": null,
              "description": "Redis cache endpoint, enabled only when cache is provisioned",
              "purpose": "cache:redis:connection-string",
              "placeholderKey": "domain.orders.cache.redis.connectionString"
            },
            {
              "key": "Payments:BaseUrl",
              "targetKind": "code:IConfiguration",
              "required": true,
              "secret": false,
              "defaultValue": null,
              "description": "Base URL of the Payments API consumed by this service",
              "purpose": "http:client:base-url",
              "placeholderKey": "domain.payments.api.baseUrl"
            }
          ],
          "dependencies": [
            {
              "name": "payments-api",
              "category": "application:http",
              "required": true,
              "description": "Another Iris application exposing the Payments HTTP API",
              "placeholderKey": "domain.payments.api",
              "providerApplicationSlug": "payments-api",
              "providerPlaceholderKey": "domain.payments.api.baseUrl"
            }
          ],
          "placeholders": [
            {
              "key": "domain.orders.api.baseUrl",
              "category": "http:server:base-url",
              "description": "Base URL exposed by this application after deployment",
              "required": true
            }
          ]
        }
        """;

    public const string PlaceholderNamingExamples = """
        domain.<bounded-context>.<resource>.<technology>.<value>
        domain.orders.db.postgresql.connectionString
        domain.orders.cache.redis.connectionString
        domain.payments.api.baseUrl
        domain.billing.queue.invoiceCreated.name
        platform.openbao.secretPath
        platform.ansible.inventoryGroup
        """;

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
