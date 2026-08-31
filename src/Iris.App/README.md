# Iris.App — .NET MAUI desktop client

The Iris operator client (brand name **Iris**), themed to the **Fluent Design
System**. Login screen, dashboard, an **Access** page backed by the live Iris API,
and a component gallery. MVVM (CommunityToolkit.Mvvm) + DI + Shell navigation.

> Ported from `Project Reference/Demo UI`. This project keeps its own
> `Directory.Build.props` so the repo-root MSBuild props (single-TFM + Central
> Package Management) do not apply to the MAUI head.

Built on the official `dotnet new maui` template — no third‑party UI kit — so the
Fluent look comes from the native WinUI 3 renderer plus the theme resources in
`Resources/Styles`.

## Requirements

| Tool | Version |
|------|---------|
| .NET SDK | 9.0.x |
| MAUI workload | `maui-windows` (or full `maui` for cross‑platform) |
| OS | Windows 10 19041+ / Windows 11 (for the Fluent system fonts) |

Install the workload once (needs an elevated shell):

```powershell
dotnet workload install maui-windows
```

## Run

From VS Code the **run-app** task starts the API in the background first, then
builds and launches this client; the **Iris (API + App)** compound launch config
debugs both together.

Manually (two shells, or start the API separately):

```powershell
dotnet run --project ..\Iris.Api                       # shell 1
dotnet build -t:Run -f net9.0-windows10.0.19041.0      # shell 2 (from src/Iris.App)
```

The window opens on the **login** screen.

* **User name** – a configured Iris dev user, e.g. `admin@iris.local`,
  `lucia@contoso.example`, `gio@globex.example` (see the API's
  `appsettings.Development.json`)
* **Password** – ignored in dev mode

Sign-in calls `GET /me` on the API with an `X-Dev-User` header; on success you
land on the **Dashboard**. The flyout switches between Dashboard, **Access**
(identity + effective permissions + visible customers, live from the API) and the
**Components** gallery.

### API location

`Services/IrisApiClient.cs` → `IrisApiOptions.BaseUrl` defaults to
`http://localhost:5006` (the API's Kestrel HTTP profile). Change it there, or
register a configured `IrisApiOptions` in `MauiProgram.cs`, to point elsewhere.

## What's inside

```
App.xaml / AppShell.xaml        App bootstrap, Shell routes (login / main / activitydetail)
MauiProgram.cs                  DI registration (services, view models, pages)
Converters.cs                   Value converters used by the XAML

Resources/Styles/Colors.xaml    Fluent light/dark palette tokens
Resources/Styles/Styles.xaml    Typography + implicit/keyed control styles

Controls/
  LoadingView.cs                Fluent loading splash overlay (dimming scrim + spinner card,
                                fades in/out; bind IsActive to a view-model flag)

Models/                         Plain data records for the dashboard
Services/
  IrisApiClient.cs              Typed client over the Iris AAA endpoints (/me, /customers, …)
  AuthService.cs                Dev-header sign-in via GET /me
  DashboardDataService.cs       Static sample data (KPIs, activity, projects, chart)

ViewModels/
  LoginViewModel.cs             Credentials, busy state, validation, navigation
  DashboardViewModel.cs         Async load (IsLoading), greeting, collections, refresh & drill‑down
  AccessViewModel.cs            Loads /me + /customers, exposes permissions & customers
  ComponentsViewModel.cs        Async load (IsLoading) + state for every control on the gallery

Views/
  LoginPage.xaml               Split brand panel + Fluent login card
  DashboardPage.xaml           KPI cards, bar chart, projects list, activity feed
  AccessPage.xaml              Identity, effective permissions, visible customers (live)
  ComponentsPage.xaml          Buttons, inputs, selection, range, collection view, expander…
  ActivityDetailPage.xaml      Example of Shell route + query parameters
```

## Theming notes

* Colours are defined once as `*Light` / `*Dark` pairs in `Colors.xaml` and
  consumed through `AppThemeBinding`, so the app follows the OS light/dark
  setting automatically.
* Typography uses **Segoe UI Variable** and icons use **Segoe Fluent Icons**
  (both ship with Windows 11), with the bundled *OpenSans* faces as fallback.
* Cards, fields and buttons use 6–8 px corner radii, hairline strokes and a soft
  shadow to match Fluent surfaces.

## Branding / logo

The **Iris** mark is a hand‑built, symmetric line‑art iris (single‑stroke, no
fills), authored as SVG and rasterised by the MAUI Resizetizer at build time.

| File | Purpose |
|------|---------|
| `Resources/Images/iris_mark.svg` | Dark mark — flyout header, login badge (`Source="iris_mark.png"`) |
| `Resources/Images/iris_mark_light.svg` | Light mark — hero mark on the login brand panel |
| `Resources/AppIcon/appicon.svg` + `appiconfg.svg` | Launcher icon (paper plate `#F3EDE1` + simplified mark) |
| `Resources/Splash/splash.svg` | Splash screen — mark + monoline "Iris" wordmark on `#F3EDE1` |

Notes:
* MAUI converts `*.svg` under `MauiImage` to PNG; reference them in XAML with the
  **`.png`** extension.
* The wordmark is drawn as monoline stroke paths, so it needs no bundled font.
* Colours are literal (`#2E343D` / `#F4F0E6`); swap them if the palette changes.

## Going cross‑platform

The `.csproj` is currently pinned to `net9.0-windows10.0.19041.0`. To target
Android / iOS / macOS as well, install the full `maui` workload and uncomment the
extra `TargetFrameworks` line in `Iris.App.csproj`. The Fluent look on those
platforms will approximate the palette rather than use native Fluent controls.
