# Iris — UI & navigation standard

The visual and interaction contract for **Iris.App** (the .NET MAUI / WinUI 3 desktop
client). Every screen, dialog and control follows this document. It is the source of
truth: the XAML resources implement it, this file explains and constrains it.

- **Tokens** — `src/Iris.App/Resources/Styles/Colors.xaml`
- **Styles** — `src/Iris.App/Resources/Styles/Styles.xaml`
- **Shell / navigation** — `src/Iris.App/AppShell.xaml` (+ `ViewModels/AppShellViewModel.cs`)
- **Reusable controls** — `src/Iris.App/Controls/`
- **Dialog windows** — `src/Iris.App/Services/DialogService.cs`, `Platforms/Windows/NativeWindowConfigurator.cs`, `src/Iris.App/Views/Dialogs/`
- **Windows chrome fixes** — `src/Iris.App/Platforms/Windows/WindowsInputStyling.cs`

---

## 1. Principles

1. **Fluent-flavoured, native-first.** Approximate the Windows 11 / WinUI 3 look
   (spacing, radii, accent, elevation). Do not import a third-party UI kit.
2. **Workflow-first, not CRUD.** Screens are organised around the operator's task
   (register → credential → validate → …), not around database tables.
3. **Theme-aware, always.** Every colour is a light/dark **token pair** consumed via
   `AppThemeBinding`. No raw hex in a page except one-off decorative gradients.
4. **One system.** A control looks and behaves the same on every screen. If a screen
   needs something new, add a *style* or a *control*, don't restyle inline.
5. **Desktop modality is real.** Secondary flows open as owned OS windows that block
   the window below (see §7), not as in-page overlays.

---

## 2. Colour

All colours live in `Colors.xaml` as `*Light` / `*Dark` pairs (or a single value where
theme-neutral). **Consume them through `Styles.xaml` or `AppThemeBinding` — never
hard-code a hex in a page.**

| Role | Token(s) | Use for |
|---|---|---|
| Accent | `AccentLight` `#0F6CBD` / `AccentDark` `#479EF5` (+ `…Hover`, `…Pressed`) | Primary buttons, links, selected state, spinners, switches, progress |
| Page ground | `AppBackgroundLight` `#F3F3F3` / `AppBackgroundDark` `#202020` | `Page` background (set by the implicit `Page` style) |
| Layer (card) | `LayerLight` `#FFFFFF` / `LayerDark` `#2C2C2C` | `Card`, dialog footer bar, primary surfaces |
| Layer alt | `LayerAltLight` `#FBFBFB` / `LayerAltDark` `#272727` | `SubtleCard`, flyout background, account strip |
| Subtle fill | `SubtleFillLight` `#F5F5F5` / `SubtleFillDark` `#323232` | Input fill (dark), secondary button fill (dark), hover |
| Stroke / divider | `StrokeLight` `#E5E5E5` / `StrokeDark` `#1F1F1F` | Card borders, `BoxView` dividers |
| Control stroke | `ControlStrokeLight` `#D1D1D1` / `ControlStrokeDark` `#3D3D3D` | Field borders, secondary/icon button borders |
| Text primary | `TextPrimaryLight` `#1B1B1B` / `TextPrimaryDark` `#FFFFFF` | Body copy, titles |
| Text secondary | `TextSecondaryLight` `#5E5E5E` / `TextSecondaryDark` `#C7C7C7` | Captions, placeholders, section labels |
| On accent | `TextOnAccent` `#FFFFFF` | Text on a primary button / accent fill |
| Success | `Success` `#0E700E` / `SuccessDark` `#5EC75E` | "Healthy", "on track" |
| Warning | `Warning` `#9D5D00` / `WarningDark` `#FCE100` | "At risk", non-blocking issues |
| Danger | `Danger` `#C42B1C` / `DangerDark` `#FF99A4` | Errors, destructive actions, "delete" zones |

**Rules**

- A colour must always be defined on bare scope first; theme variants only override.
- Status colours are for *meaning*, never decoration.
- `#F4F0E6` (the "paper" cream) is reserved for the brand mark plate only.

---

## 3. Typography

Family tokens (Windows 11 *Segoe UI Variable*, bundled *OpenSans* fallback):

| Token | Value |
|---|---|
| `FontRegular` | `Segoe UI Variable Text, OpenSansRegular` |
| `FontSemibold` | `Segoe UI Variable Text Semibold, OpenSansSemibold` |
| `FontDisplay` | `Segoe UI Variable Display, OpenSansSemibold` |
| `FontIcons` | `Segoe Fluent Icons, Segoe MDL2 Assets` |

Label styles (use these keys — do not set `FontSize` ad hoc):

| Style key | Size / family | Use |
|---|---|---|
| `Display` | 34 · Display | Marketing / hero only (login) |
| `TitleLarge` | 24 · Semibold | Page title (one per screen) |
| `Subtitle` | 18 · Semibold | Card / dialog section title |
| `Body` | 14 · Regular | Default running text (also the implicit `Label` style) |
| `Caption` | 12 · Regular · TextSecondary | Field labels, helper text, metadata, section headers |
| `Icon` | 16 · FontIcons · centred | Glyph inside a decorative circle |

Line length: cap long paragraphs with `MaximumWidthRequest` (≈ 460–480).

---

## 4. Spacing, shape, elevation

**Grid**: 4 px base. Common steps: 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 28.

| Context | Value |
|---|---|
| Page content padding | `28` |
| Page content max width | `1080` |
| Vertical rhythm between cards / sections | `18` (page), `10–14` (inside a card) |
| `Card` padding | `20` · `SubtleCard` / dialog body | `16–22` |
| Dialog footer bar padding | `22,12` |

**Corner radius**

| Element | Radius |
|---|---|
| Inputs (`FieldBorder`), buttons | `6` |
| Cards, dialog surfaces | `8` |
| Badge / pill | `10` |
| Avatar / glyph circle | half the box (`18` for 36, `24` for 48) |

**Elevation** — only `Card` carries a shadow (`Offset 0,2`, `Radius 12`,
`Opacity 0.08` light / `0.30` dark). Everything else is flat; separation comes from
the 1 px stroke.

---

## 5. Components

### Surfaces

- **`Card`** — the primary container: white/`LayerDark`, 1 px stroke, radius 8, padding
  20, soft shadow. One card per logical group. Lists are a `VerticalStackLayout` of
  cards with `Spacing="14"`.
- **`SubtleCard`** — nested/inset panel inside a card (`LayerAlt` fill, no shadow).

### Buttons

| Style | Look | Use | Height |
|---|---|---|---|
| *(implicit `Button`)* | Accent fill, white text, radius 6 | **Primary** action — exactly one per screen/dialog | 40 |
| `SecondaryButton` | `Layer`/`SubtleFillDark` fill, 1 px stroke | Secondary actions (Refresh, Cancel) | 40 |
| `LinkButton` | Transparent, accent text, 13 px | Inline low-emphasis ("Revoke") | — |
| `IconButton` | 36×36 square, 1 px stroke, glyph only | Row actions (edit, add) — always with `ToolTipProperties.Text` | 36 |
| *Destructive* | `Danger`/`DangerDark` fill + white text (set inline) | Only inside a danger zone, only after a confirm gate (§7) | 40 |

Every button carries `Normal` / `PointerOver` / `Pressed` / `Disabled` visual states
(the styles already do). Never disable a button by hiding it — set `IsEnabled`.

### Inputs

The design wraps every `Entry` / `Editor` in a **`FieldBorder`** (the visible box:
`LayerLight`/`SubtleFillDark` fill, `ControlStroke`, radius 6, `Padding 12,2`,
`MinimumHeightRequest 40`). The inner control is `BackgroundColor="Transparent"`, and
`WindowsInputStyling` flattens the native WinUI `TextBox` chrome so only the outer
border shows. **Always** use:

```xml
<VerticalStackLayout Spacing="4">
    <Label Text="Field name" Style="{StaticResource Caption}" />
    <Border Style="{StaticResource FieldBorder}">
        <Entry Text="{Binding …}" Placeholder="example value" />
    </Border>
</VerticalStackLayout>
```

`Picker` / `DatePicker` / `Switch` / `CheckBox` are used bare (styled by implicit
styles: accent `OnColor` / `Color`, transparent background).

**Secret fields** (server credential form) adapt to the chosen auth method and to
the operator's role:

- **Password** → single-line `Entry` with `IsPassword="True"` (masked).
- **SSH key** → multi-line `Editor` (`AutoSize="TextChanges"`, ~140 px) in a
  `FieldBorder` — never masked; a key is pasted and needs to be readable.
- Switching auth method clears the field (a password and a key aren't the same text).
- The secret input is always shown when **creating** a credential. When **editing**
  an existing one it is hidden unless the caller holds
  **`infrastructure.secrets.manage`** (lead role — seeded on `platform-admin` and
  `customer-admin`); only they may rotate the stored password/key. Gate:
  `CredentialFormViewModel.ShowSecretField`.

### Badge / pill

`Badge` — `Secondary`/`SubtleFillDark` fill, radius 10, `Padding 10,3`, `Caption`
text inside. Use for at-a-glance attributes (OS, environment, credential kind,
"Pending").

### Feedback

- **Inline error** — a `Caption`-sized `Label`, `Danger`/`DangerDark` colour, bound to
  `{Binding SomethingError}` with `IsVisible="{Binding HasSomethingError}"`. Sits just
  above the action button.
- **Error banner** (page-level load failure) — a `Border` with
  `#FDE7E9`/`#442726` fill, `#F1BBBB`/`#7A3B38` stroke, `Padding 12,10`, radius 6,
  `Danger` text.
- **Busy** — `ActivityIndicator` (accent) shown where the triggering button was; the
  button is `IsEnabled="{Binding IsBusy, Converter={StaticResource InvertBoolConverter}}"`.
- **`LoadingView`** control — full-page dimming scrim + centred spinner card. Dropped
  as the **last child of the page root `Grid`**, `IsActive="{Binding IsLoading}"`.

---

## 6. Iconography

Font: **Segoe Fluent Icons** (`FontIcons` token). Reference glyphs by code point in
XAML (`&#xE70F;`).

| Action | Glyph | Code |
|---|---|---|
| Edit (opens edit/delete dialog) | ✎ | `E70F` |
| Add (add credential / assignment) | ＋ | `E710` |
| Refresh | ⟳ | `E72C` |
| Close / dismiss | ✕ | `E711` |
| Server | 🖥 | `E7F4` |
| User | 👤 | `E77B` |

Rules: an icon-only control **must** have `ToolTipProperties.Text`. Pair a decorative
glyph with the `Icon` label style inside a coloured circle
(`Secondary`/`SubtleFillDark`, accent glyph).

---

## 7. Dialogs — real modal windows

Secondary flows (**New server**, **Edit server**, **Add credential**, **New user**,
**Edit user**, **Assign a role**, **Confirm deletion**, **Send invitation**,
**New customer**, **Add context**) are **separate top-level OS windows**, not in-page
panels.

**Mechanics** (`IDialogService.ShowAsync` → `NativeWindowConfigurator`):

- New MAUI `Window` hosting a `ContentPage`; opened with `Application.OpenWindow`.
- On Windows it is made **owned** (`GWLP_HWNDPARENT` = main window) and modal
  (`OverlappedPresenter.IsModal = true` + `EnableWindow(owner, false)`), so it blocks
  the window below like the Windows elevation prompt. Minimise/maximise are removed;
  resize stays.
- **Geometry is persisted per dialog key** (`dlg.new-server`, `dlg.edit-user`, …) and
  the main window (`win.main`) via `WindowGeometryStore` (local `Preferences`),
  applied to the WinUI `AppWindow`. Position/size are restored on next open, and
  recentred if the saved rectangle is off every screen. Nothing runs on window
  teardown — geometry is saved continuously from `AppWindow.Changed`.

**Layout** — every dialog page is:

```
Grid RowDefinitions="*,Auto"          ← page background = AppBackground
├── ScrollView                        ← the form, Padding 22, Spacing 10
│     └── Caption (one line of context) + field groups (§5) + inline error
└── Grid (footer bar)                 ← Layer fill, Padding 22,12, ColumnDefinitions="*,Auto,Auto"
      ├── ActivityIndicator (col 0, bound to Is…Busy)
      ├── "Cancel"  → SecondaryButton, closes the window  (col 1)
      └── primary   → the VM command, disabled while busy (col 2, right-most)
```

**Open / close contract** (MVVM):

- A row/page VM raises **`…Requested`** (e.g. `EditServerRequested`); the *page code-
  behind* turns it into `_dialogs.ShowAsync(new XxxDialog(vm), "dlg.key", w, h)`.
- The VM command does the API call; on success it raises **`…Completed`**; the dialog
  page subscribes and closes its window. Cancel and the native ✕ also close it (the
  page unsubscribes on `Unloaded`, and close is idempotent).
- One dialog hands off to the next via `MainThread.BeginInvokeOnMainThread`, so the
  first window finishes closing before the second opens: creating an entity chains
  into its setup dialog (server → add credential, user → assign role); the edit
  dialog hands off to **confirm-delete**, **assign a role**, or **send invitation**.

**Destructive actions** are their **own confirmation window**, not an inline panel.
The edit dialog carries a `LinkButton` in `Danger` colour ("Delete this user…"); it
raises **`DeleteRequested`** which closes the edit window, then the page opens
**`ConfirmDeleteDialog`** on the next UI tick. That dialog is bound through the
`IConfirmDeletable` interface (implemented by `UserRowViewModel` and
`ServerRowViewModel`) and gates its Delete button with **type-to-confirm** — enabled
only when `DeleteConfirmName` equals the entity's exact name ordinally
(`CanDelete`). On success the row is removed from its list and `DeleteCompleted`
closes the window. Same interface, same window for both users and servers.

**Concurrent edit locks.** Opening any edit window first calls
`POST /locks/{type}/{id}` (`AcquireEditLockAsync`). If `Mine` is false the window
does **not** open — the row shows a `Warning`-coloured caption
("*X is editing this … right now*"). While the window is open the VM heartbeats the
lock every 45 s (TTL is 2 min server-side); on close (`NotifyEditorClosed`, called
from the dialog's `Detach`) it cancels the heartbeat and `DELETE`s the lock.
Locks are advisory — the PUT itself is not lock-checked — and lapse on their own if
a client disappears. `platform.admin` may `DELETE …?force=true`.

**Invitations.** The edit-user dialog's "Send invitation…" button raises
`InviteRequested` (closes the edit window) → the page opens **`InvitationDialog`**.
It calls `POST /governance/users/{id}/invitation`, shows the one-time link in a
read-only `Editor` with **Copy link**, and an expiry caption. The link/token is
returned once (Iris stores only a SHA-256 hash); re-generating supersedes the
previous link. Delivery is otherwise a stub (`IInvitationNotifier` logs it).

---

## 8. Screen anatomy

A standard list screen (`ServersPage`, `UsersPage`):

```
Grid (page root)
└── ScrollView
      └── VerticalStackLayout  Padding=28  Spacing=18  MaximumWidthRequest=1080
            ├── Header  Grid ColumnDefinitions="*,Auto,Auto"
            │     ├── VerticalStackLayout: TitleLarge + Caption (what this screen is)
            │     ├── "⟳ Refresh"  → SecondaryButton                 (col 1)
            │     └── primary "New …"  → RequestNewXxxCommand         (col 2)
            ├── Error banner (IsVisible bound to HasError)
            └── VerticalStackLayout Spacing=14  (BindableLayout over the collection)
                  └── Card per row
                        ├── Row header: Grid "Auto,*,Auto"
                        │     ├── glyph circle
                        │     ├── name (Semibold 15) + badges + metadata captions
                        │     └── HorizontalStackLayout of IconButtons (edit, add)
                        └── nested list(s) — e.g. credentials / assignments
      (LoadingView as the last child of the page root Grid)
```

Rules: one `TitleLarge` per screen; the primary action is the right-most header
button; row-level actions are `IconButton`s with tooltips; anything shown that can
change at runtime is an `[ObservableProperty]` so an in-place edit reflects without a
reload.

---

## 9. Navigation

### Model

- **Shell + custom flyout.** `Shell.MenuItemTemplate` is unreliable on the Windows
  handler (blank bound text), so the flyout body is a hand-built
  `Shell.FlyoutContentTemplate`. `FlyoutItem`/`ShellContent` entries still exist —
  they only register the routes `GoToAsync` uses (`FlyoutItemIsVisible="False"`).
- Each flyout row is a transparent, borderless **`Button` layered over a `Label`**:
  the Button supplies the real click / keyboard / UI-Automation target, the Label the
  visible left-aligned text. Never a bare `Grid` + `TapGestureRecognizer` (no Invoke
  pattern, not focusable).
- Navigation is a single command: `NavigateCommand` with an absolute route parameter
  (`//servers`), which also closes the flyout.

### Flyout structure

```
FlyoutHeader
  ├── Brand strip   — accent (light) / LayerDark (dark); mark on #F4F0E6 plate;
  │                   "Iris" (Semibold 17) + "ICP · Infrastructure Control Plane" (12)
  └── Account strip  — LayerAlt; initials avatar + display name + email  (AppShellViewModel)

FlyoutContent  (sections; a "Section" header is Semibold 12, TextSecondary, Padding 20,18,20,6)
  ├── Governance      [visible when CanManageUsers]           → Users            (//users),
  │                                                             Customers        (//customers)
  ├── Infrastructure  [visible when CanManageInfrastructure]  → Servers          (//servers)
  └── Workspace                                               → Dashboard (//dashboard),
                                                                Access (//access),
                                                                Components (//components)

FlyoutFooter
  └── Sign out  (confirms, then GoToAsync("//login"))

Outside the flyout:  Login  (FlyoutBehavior disabled, no nav bar)
```

### Section visibility

Menu sections are **permission-gated**, from the signed-in user's **Global-scope**
effective permissions (`IAuthService.Me.EffectivePermissions`, refreshed on
`StateChanged`):

| Section | Guard property | Permission |
|---|---|---|
| Governance | `AppShellViewModel.CanManageUsers` | `governance.read` |
| Infrastructure | `AppShellViewModel.CanManageInfrastructure` | `infrastructure.read` |
| Workspace | always | — |

`/me`, `/customers`, `/servers`, `/governance/*` have no `customerId`/`contextId`
route parameter, so the API always evaluates them at **Global** scope — which is why
the unscoped `/me` call answers the flyout's gating question.

### Target information architecture

The project brief's operator navigation is the destination:

```
Overview · Infrastructure · Applications · Deployments · Actions · Governance
```

Today's flyout is an early slice: **Governance → Users / Customers**,
**Infrastructure → Servers**, plus a `Workspace` group carrying the template's
Dashboard/Access/Components while the real sections are built. When adding a section:

1. Add its pages under `Views/`, routes as hidden `FlyoutItem`s in `AppShell.xaml`.
2. Add a section + rows to `Shell.FlyoutContentTemplate`, following the
   Button-over-Label pattern.
3. Gate the section on the matching `*.read` permission via a `CanX` property on
   `AppShellViewModel` (raise it in `OnAuthStateChanged`).
4. Order sections by the brief's IA; keep `Workspace` last until it's retired.

---

## 10. MVVM & interaction conventions

- **Pages are thin.** Code-behind only: `InitializeComponent`, set `BindingContext`,
  kick the load command in `OnAppearing`, and translate VM `…Requested` events into
  `IDialogService` calls.
- **Row view-models take a reference to their parent VM** (`ServerRowViewModel(… ,
  ServersViewModel parent)`) so a row button can raise a page-level event or ask the
  parent to remove it from the collection.
- **State/error pattern per async action:** `Is<Verb>Busy` (bool) + `<Verb>Error`
  (string?) + `Has<Verb>Error` (computed) + a `On<Verb>ErrorChanged` partial that
  raises `Has…`. The button binds to the busy flag; the inline error label binds to
  the two.
- **Anything rendered is `[ObservableProperty]`.** After an edit, `ApplyFrom(response)`
  copies the server/user response back onto the row so the list updates in place.
- **API failures** surface as `IrisApiException` (RFC 7807 `detail`) or
  `HttpRequestException`; VMs catch exactly those two and put the message in
  `<Verb>Error`, never let them escape.
- **`CommunityToolkit.Mvvm`** only: `[ObservableProperty]`, `[RelayCommand]`,
  `[RelayCommand(CanExecute = nameof(...))]` + `Command.NotifyCanExecuteChanged()`.

---

## 11. Accessibility

- Every actionable element is a real focusable control with a UIA pattern (see the
  flyout Button-over-Label rule).
- Icon-only buttons carry `ToolTipProperties.Text`.
- Colour never carries meaning alone — pair status colour with text/badge.
- Contrast comes from the token pairs; don't tune colours per screen.
- The app follows the OS light/dark setting automatically via `AppThemeBinding`; test
  both.

---

## 12. Do / Don't

**Do**

- Reuse a style key or add one; put shared markup in a `Controls/` view.
- Use `Caption` for every field label and helper line.
- Keep one `TitleLarge` and one primary button per screen/dialog.
- Wrap `Entry`/`Editor` in `FieldBorder`.
- Open secondary flows as dialog windows via `IDialogService`.
- Gate a destructive action behind type-to-confirm.

**Don't**

- Hard-code a hex, font size, or radius in a page.
- Use a bare `Grid`+`TapGestureRecognizer` as a button.
- Disable a control by collapsing it — set `IsEnabled`.
- Build an in-page modal overlay (the `ModalPanel` control was removed).
- Let an API exception reach the UI unhandled.
