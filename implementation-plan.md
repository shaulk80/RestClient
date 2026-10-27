# REST Client: Detailed Implementation Plan

Status: **Draft for review.** Based on [ARCHITECTURE_PLAN.md](ARCHITECTURE_PLAN.md). No code has been written yet. Decisions in the architecture plan are treated as fixed; this document covers how they are built.

## 1. Conventions

These apply to every milestone. They come from the rules in `~/.claude/` and are restated here so the plan is self-contained.

- **One type per file.** The file name matches the type name. Inner classes are the only exception.
- **Records for data.** Domain types and DTOs are immutable records where possible. Mutable state lives only in ViewModels and in services that own it.
- **Errors as values.** Services return `Result` or `Result<T>` for expected failures (validation, not found, network, decryption failure). Exceptions are caught only at boundaries (HTTP executor, SQLite repositories, cipher), converted to a `Result`, and logged. Nothing is swallowed silently.
- **Async all the way.** All I/O is asynchronous. No `GetAwaiter().GetResult()`. Every async call takes a `CancellationToken`.
- **Dependency inversion.** Application code depends on interfaces defined in the Application layer. Infrastructure implements them. Tests use FakeItEasy fakes for those interfaces.
- **Tests.** NUnit, FakeItEasy, `Assert.That()`. Similar cases use parameterized `[TestCase]`. No tests for log messages. No reflection in tests.
- **Persistence DTOs.** Serialization attributes and storage shapes live in Infrastructure DTOs, mapped to and from domain records. The Domain project has no serialization dependency.
- **Method size.** Each method and class covers one responsibility. When a service grows a second responsibility, it is split.

## 2. Solution Structure

```
RestClient.slnx
Directory.Build.props          (shared settings: nullable, analyzers, warnings)
Directory.Packages.props       (central package versions)
src/
  RestClient.Domain/           (no project references)
  RestClient.Application/      (-> Domain)
  RestClient.Infrastructure/   (-> Application, Domain)
  RestClient.UI/               (-> Application, Infrastructure; Avalonia executable)
tests/
  RestClient.Domain.Tests/
  RestClient.Application.Tests/
  RestClient.Infrastructure.Tests/
  RestClient.Integration.Tests/  (in-process Kestrel server, real HttpClient)
  RestClient.UI.Tests/           (view models, Avalonia.Headless smoke tests)
scripts/
  build.ps1                    (restore, build, test with coverage, publish win-x64; linux-x64 deferred)
```

The solution uses the `.slnx` format, the default in the .NET 10 SDK.

Dependency rules:

- Domain depends on nothing.
- Application depends only on Domain.
- Infrastructure implements Application interfaces and depends on Application and Domain.
- UI is the composition root. It registers services from Application and Infrastructure and holds the Avalonia code.
- Nothing except UI references Avalonia.

Namespaces follow folders: `RestClient.Domain.Collections`, `RestClient.Application.Requests`, and so on.

## 3. Cross-Cutting Foundations

### 3.1 Result Types (Domain)

- `Results/Result.cs`: non-generic `Result` with `IsSuccess`, `Error`.
- ``Results/Result`1.cs``: `Result<T>` with `Value`.
- `Results/Error.cs`: `Category` (`ErrorCategory`) and `Message`. Optional `Details` string for technical context.
- `Results/ErrorCategory.cs`: `Validation`, `NotFound`, `Conflict`, `Network`, `Timeout`, `Cancelled`, `Storage`, `Security`, `Unexpected`.

### 3.2 Clock and Identifiers (Application abstractions)

- `IClock`: current UTC time. Lets tests control timestamps.
- `IIdGenerator`: new identifiers. Production uses `Guid.NewGuid()`; tests use fixed sequences.

### 3.3 Data Folder and Logging (Infrastructure)

- `DataFolderLocator`: resolves the data folder. Priority: `--data-dir` argument, then the `RESTCLIENT_DATA_DIR` environment variable, then `./data` beside the executable. Creates the folder on first run.
- `LoggingSetup`: Serilog with a rolling file sink in `data/logs/`. Exposed to the rest of the app through `Microsoft.Extensions.Logging`.

### 3.4 Composition Root (UI)

- `Program.cs` builds the host, resolves the data folder, and calls `AddApplication()` and `AddInfrastructure(options)`.
- Each project provides its own extension method (`ApplicationServiceCollectionExtensions`, `InfrastructureServiceCollectionExtensions`). The UI project registers its ViewModels and dialogs.

### 3.5 Build Configuration

- `Directory.Build.props`: `Nullable` enabled, `TreatWarningsAsErrors` enabled, analyzers enabled.
- `Directory.Packages.props`: all NuGet versions pinned in one place.
- Each project targets `net10.0`.

## 4. Domain Model

Grouped by folder. Every type is a separate file.

**Results**: `Result`, `Result<T>`, `Error`, `ErrorCategory` (section 3.1).

**Collections**
- `Collection`: `Id`, `Name`, `Root` (a `Folder`).
- `Folder`: `Id`, `Name`, `Folders` (child folders), `Requests` (child requests).
- `RequestDefinition`: `Id`, `Name`, `Method`, `Url`, `QueryParams`, `Headers`, `Body`, `Auth`.
- `KeyValueEntry`: `Key`, `Value`, `IsEnabled`.
- `RequestMethod`: enum (`Get`, `Post`, `Put`, `Patch`, `Delete`, `Head`, `Options`).

**Bodies**
- `BodyDefinition`: abstract base.
- `NoBody`, `RawBody` (`ContentType`, `Text`), `FormUrlEncodedBody` (`Fields`), `MultipartBody` (`Parts`).
- `MultipartPart`: `Name`, `Value` or `FilePath` (exactly one).

**Auth**
- `AuthDefinition`: abstract base.
- `NoAuth`, `BasicAuth` (`Username`, `Password`), `BearerAuth` (`Token`), `ApiKeyAuth` (`Key`, `Value`, `Location`).
- `ApiKeyLocation`: enum (`Header`, `Query`).
- Auth fields may contain `{{variables}}`, which are resolved at send time.

**Environments**
- `Environment`: `Id`, `Name`, `Variables`.
- `Variable`: `Name`, `Value`, `IsSecret`.

**Responses**
- `ResponseSnapshot`: `StatusCode`, `ReasonPhrase`, `Headers` (`KeyValueEntry`), `BodyPreview`, `BodySizeBytes`, `BodyFilePath` (nullable, set when the body was spilled to disk), `Timing`.
- `ResponseTiming`: `ConnectMs`, `TimeToFirstByteMs`, `TotalMs`. DNS timing is not recorded in v1 (see section 11).

**History**
- `HistoryEntry`: `Id`, `SentAt`, `Method`, `ResolvedUrl`, `RequestSnapshot` (a `RequestDefinition` copy), `StatusCode` (nullable), `DurationMs`, `ErrorMessage` (nullable).

**Validation**
- `RequestValidator`: checks the URL is an absolute http or https URL, method is set, header and key names are non-empty, and multipart parts have exactly one of value or file path.

## 5. Application Layer

### 5.1 Abstractions (interfaces)

- Persistence: `ICollectionRepository`, `IEnvironmentRepository`, `IHistoryRepository`, `ISettingsStore`, `IOpenTabsStore`.
- Execution: `IHttpExecutor` (sends an `OutgoingRequest`, returns `Result<ResponseSnapshot>`), `IFileReader` (reads multipart file parts).
- Security: `ISecretCipher` (encrypt, decrypt), `IKeyDeriver` (master password to key).
- Infrastructure services: `IClock`, `IIdGenerator`.

### 5.2 Request Execution

- `VariableResolver`: replaces `{{name}}` with the active environment value, then collection variables. Returns `Result<string>`. Unresolved names produce a `Validation` error that lists them. Secret variables are decrypted through `SecretService`, which fails with `Security` when locked.
- `OutgoingRequest`: record with `Method`, `Url`, `Headers`, `Content` (bytes, or a stream reference for files), and `Timeout`.
- `RequestBuilder`: converts a `RequestDefinition` to an `OutgoingRequest`. Applies query params, auth, and body. Uses `VariableResolver` and `IFileReader`.
- `RequestSender`: orchestrates one send. Validate, then build, then execute, then record history, then return the result. Records failed and cancelled attempts too.

### 5.3 Collections

- `CollectionService`: create, rename, delete, and list collections.
- `FolderService`: create, rename, and delete folders and requests. Enforces unique names among siblings.
- `CollectionTreeService`: move a folder or request to another folder, and reorder siblings. Rejects moves that would place a folder inside itself.

### 5.4 Environments, Secrets, History, Settings, Tabs

- `EnvironmentService`: CRUD for environments and variables. Tracks the active environment.
- `SecretService`: `SetUp(masterPassword)` on first run, `Unlock`, `Lock`, `Encrypt`, `Decrypt`. Holds the derived key in memory only while unlocked. Keeps the salt, iteration count, and a verifier value in settings, so a wrong password is detected without storing the password.
- `HistoryService`: record, search, restore a `RequestDefinition` from history, and apply retention.
- `RetentionPolicy`: keeps the most recent N entries and drops entries older than D days.
- `SettingsService`: reads and writes `AppSettings` (timeouts, redirects, SSL verification, proxy).
- `TabService`: open, close, and track dirty state. Writes tab drafts through `IOpenTabsStore`.

### 5.5 Body Formatting

- `IBodyFormatter` with `JsonBodyFormatter` (System.Text.Json) and `XmlBodyFormatter` (System.Xml.Linq). Return `Result<string>`. Invalid input gives a `Validation` error with the parser message.

## 6. Infrastructure Layer

- **Persistence:** `SqliteConnectionFactory` (Microsoft.Data.Sqlite), `SqliteMigrator`, `MigrationSetBuilder`, `IMigrationSource` (`SqlScriptMigrationSource` reads scripts, `CodeMigrationSource` wraps code migrations), `IMigration` (`SqlScriptMigration` applies one version's scripts; code migrations are possible but unused).
    - **Schema scripts:** `Persistence/MigrationScripts/NNNN/`, one folder per version. The folder name is a four-digit version of 1 or more. Each folder holds one or more `.sql` files, run in filename order (for example `0001_create_settings.sql`). The folder is copied to the output as `MigrationScripts/NNNN/`, with copy-if-newer.
    - **Versions:** planned versions are `0002` Collections, `0003` History, `0004` Environments, `0005` Open tabs. Gaps are allowed. Each version runs in its own transaction, with its `PRAGMA user_version` update in the same transaction. After a run the stored version is the highest applied version.
    - **Conventions, not validation:** scripts are not checked. Never edit a script that has shipped, and never add a version below the current one, because it would never run. Invalid folder names and empty folders are logged and skipped. A duplicate version or a failed script stops startup with the full exception in the error details.
- **DTOs and mappers:** `CollectionDto`, `FolderDto`, `RequestDto`, `BodyDto`, `AuthDto`, `EnvironmentDto`, `VariableDto`, `RequestSnapshotDto`. Mappers convert between DTOs and domain records, one mapper class per aggregate.
- **Repositories:** `SqliteCollectionRepository`, `SqliteEnvironmentRepository`, `SqliteHistoryRepository`, `SqliteSettingsStore`, `SqliteOpenTabsStore`. Each write runs in a transaction. Exceptions become `Storage` errors and are logged.
- **HTTP:** `RequestHttpClientFactory` builds an `HttpClient` from `AppSettings` (timeouts, redirects, SSL verification, proxy). `HttpRequestExecutor` implements `IHttpExecutor`. `ResponseBodyReader` keeps bodies up to 1 MB in memory and spills larger bodies to a temp file in the data folder. The preview is the first 1 MB of the body.
- **Security:** `AesGcmSecretCipher` (AES-256-GCM, 12-byte nonce, 16-byte tag, `System.Security.Cryptography`). `Pbkdf2KeyDeriver` (PBKDF2-HMAC-SHA256, 600,000 iterations, 16-byte salt).
- **Services:** `SystemClock`, `IdGenerator`, `LocalFileReader`.

## 7. UI Layer (Avalonia)

- **Shell:** `App`, `MainWindow` with `MainWindowViewModel`, main menu (File, Edit, View, Help), a thin top bar (environment, secrets), a thin bottom line for messages, and three resizable panels: Collections and History, request editor, Response (M7).
- **Sidebar:** `CollectionsTreeViewModel` with `CollectionNodeViewModel`, `FolderNodeViewModel`, `RequestNodeViewModel`. Drag-and-drop, context menu. `HistoryListViewModel` with `HistoryItemViewModel`.
- **Request editing:** `RequestTabViewModel`, `RequestBuilderViewModel`, `KeyValueEditorViewModel` with `KeyValueRowViewModel`, `AuthEditorViewModel`, `BodyEditorViewModel`.
- **Response:** `ResponseViewModel`, which shows status, timing, size, headers, and a body that can be pretty or raw.
- **Environments and secrets:** `EnvironmentSelectorViewModel`, `EnvironmentEditorViewModel`, `VariableRowViewModel`, `UnlockDialogViewModel`, `MasterPasswordSetupViewModel`.
- **Settings:** `SettingsViewModel` and `SettingsWindow`.
- **Editor integration:** `EditorLanguageMapper` maps body type to a TextMate grammar (JSON, XML, plain text). AvaloniaEdit with `AvaloniaEdit.TextMate`.
- **Services:** `IDialogService`, `IFilePickerService`, `IClipboardService`, `ErrorMessageFormatter` (turns `Error` into user text).
- **Keyboard shortcuts:** defined in `MainWindow` (Ctrl+Enter sends, Ctrl+S saves, Ctrl+Shift+S saves all, Ctrl+T opens a new tab, Ctrl+F4 closes one, Ctrl+Tab and Ctrl+Shift+Tab switch request tabs in recently used order). The menu items call the same commands.

ViewModels contain no Avalonia types where possible, so they can be tested with plain NUnit.

## 8. Milestones

Dependencies: M0 → M1 → M2 → M3. M4 needs M1. M5 needs M1 and M2. M6 needs M1 and M2. M7 needs all others. M8 needs M7. M9 needs M8 and a Linux machine (section 8.1).

Each milestone ends with the **Definition of Done**: all tests pass, the coverage gate holds (section 10), the solution builds with no warnings on Windows, and the milestone's manual checks are done.

### M0 Foundations

**Goal:** a running empty shell with the architecture in place, and CI-ready scripts.

Tasks:
1. Create the solution, the four `src` projects, and the five `tests` projects (section 2).
2. Add `Directory.Build.props` and `Directory.Packages.props`.
3. Implement the `Result` types (section 3.1) with unit tests.
4. Implement `DataFolderLocator` with its tests (argument, environment variable, and default paths).
5. Implement `LoggingSetup` (Serilog to file) and confirm the log file is created in the data folder.
6. Implement `SqliteConnectionFactory`, `SqliteMigrator`, and `IMigration`. Tests run migrations against an in-memory SQLite database.
7. Implement the SQL-script migrations: `MigrationScripts/0001/0001_create_settings.sql` (the `settings` table), with the source, builder, and migrator tests (discovery, ordering, gaps, multi-statement scripts, rollback).
8. Implement `SystemClock` and `IdGenerator`, plus their interfaces.
9. Create the Avalonia shell: `App`, an empty `MainWindow`, and the composition root in `Program.cs`.
10. Write `scripts/build.ps1`: restore, build, test with coverage, and publish `win-x64` as a single-file, self-contained zip. The `linux-x64` publish is deferred to a later version (section 8.1).
11. Pin Avalonia, AvaloniaEdit, and test package versions, after checking the current stable releases.
12. Add `LICENSE` (full Apache-2.0 text) and `NOTICE` at the repository root (Q10).
13. Add a dependency license check to `scripts/build.ps1`. It lists the licenses of all NuGet packages and fails the build on any license outside the allowed set (MIT, Apache-2.0, BSD, ISC, public domain). The check runs in the same build.

Deliverables: the solution builds and runs an empty window. The data folder and log file are created. The coverage gate runs (passes on the Result types).

Tests: `ResultTests`, `DataFolderLocatorTests`, `SqliteMigratorTests`.

Manual checks: the published executable runs from a folder with no write access elsewhere, and creates `data/` beside itself. The Linux repeat is deferred to the Linux verification stage (section 8.1).

### M1 Request Builder and Sending

**Goal:** type a URL, choose a method, send, and see the response. No collections yet.

Tasks:
1. Domain: `RequestDefinition`, `KeyValueEntry`, `RequestMethod`, body and auth types (section 4), with `RequestValidator`.
2. Application: `VariableResolver` (no secrets yet), `RequestBuilder` (params, headers, raw body, no auth), `OutgoingRequest`, `RequestSender`.
3. Infrastructure: `RequestHttpClientFactory`, `HttpRequestExecutor` (maps `HttpRequestException`, `TaskCanceledException`, and timeouts to `Network`, `Timeout`, `Cancelled` errors, and logs them), `ResponseBodyReader` (1 MB preview, spill above 1 MB).
4. Connect timing via the socket connect callback. TTFB and total via `HttpCompletionOption.ResponseHeadersRead`.
5. UI: `MainWindow` shows one request tab. `RequestBuilderView` has method, URL, Send, Cancel. `ResponseView` shows status, time, size, headers, and body. Body input is a plain multi-line text box until M4.
6. Show errors through `ErrorMessageFormatter`.

Tests:
- Application: `VariableResolverTests` (parameterized: resolved, unresolved, empty name, nested braces), `RequestBuilderTests`, `RequestSenderTests` (with a faked `IHttpExecutor`: success, validation failure, network failure, cancellation).
- Domain: `RequestValidatorTests` (parameterized: valid and invalid URLs, methods, headers, multipart parts).
- Integration: `HttpRequestExecutorTests` against an in-process Kestrel server: 200 response, redirect followed and not followed, timeout, cancellation, large body spilled to disk.
- UI: `RequestTabViewModelTests` for send and cancel state transitions.

Manual checks: send GET and POST to a public echo endpoint, cancel a slow request, and confirm the UI stays responsive during a large download.

### M2 Collections

**Goal:** organize requests into collections, folders, and subfolders, persisted in SQLite.

Tasks:
1. Domain: `Collection`, `Folder`, and the sibling-name rule.
2. Application: `CollectionService`, `FolderService`, `CollectionTreeService` (move, reorder, cycle prevention), and the `ICollectionRepository` interface.
3. Infrastructure: `Migration0002Collections`, `CollectionDto`, `FolderDto`, `RequestDto`, `BodyDto`, `AuthDto`, the matching mappers, and `SqliteCollectionRepository`.
4. UI: `CollectionsTreeViewModel` with the node view models, a tree view with drag-and-drop, and a context menu (new collection, new folder, rename, duplicate, delete).
5. Tabs link to saved requests, or are unsaved (not in any collection). Edits mark the tab dirty. Save writes a linked request back to its collection. Save on an unsaved tab asks for a folder and a name, creates the request there, and links the tab. Decided 2026-10-25.
6. Duplicate request copies the definition with a new identifier.

Tests:
- Domain: `FolderTests` (sibling names, parameterized).
- Application: `CollectionServiceTests`, `FolderServiceTests`, `CollectionTreeServiceTests` (move into a child folder is rejected, reorder keeps unique positions, moving to a missing folder returns `NotFound`).
- Infrastructure: `SqliteCollectionRepositoryTests` (round-trip of a deep tree, body and auth variants, deletes cascade).
- UI: `CollectionsTreeViewModelTests` for node selection and rename.

Manual checks: a three-level folder tree survives an app restart.

### M3 History

**Goal:** every send is recorded. History can be searched and restored.

Tasks:
1. Domain: `HistoryEntry`.
2. Application: `HistoryService`, `RetentionPolicy`, `IHistoryRepository`. `RequestSender` records every attempt, including failures and cancellations.
3. Infrastructure: `Migration0003History` (table and indexes on `SentAt` and `ResolvedUrl`), `RequestSnapshotDto`, `SqliteHistoryRepository`. Retention runs after each insert.
4. UI: `HistoryListViewModel` with search by URL and method, and a restore action that opens an unsaved tab with the restored request. Nothing is saved until the user saves it. Decided 2026-10-25.

Tests:
- Application: `HistoryServiceTests` (record success and failure, search, restore creates an independent copy), `RetentionPolicyTests` (parameterized over count and age limits).
- Infrastructure: `SqliteHistoryRepositoryTests` (search, retention deletes oldest first).
- Application: `RequestSenderTests` extended to assert that every attempt is recorded.

Manual checks: send ten requests, search one, restore it, and confirm the restored request matches the original.

### M4 Body Editor and Syntax Highlighting

**Goal:** comfortable body editing with highlighting and formatting.

Tasks:
1. Application: `IBodyFormatter`, `JsonBodyFormatter`, `XmlBodyFormatter`.
2. UI: `BodyEditorViewModel` and `BodyEditorView` with AvaloniaEdit. `EditorLanguageMapper` selects JSON, XML, or plain text. Format button.
3. Multipart body editor: rows with name, value or file path, and a file picker (`IFilePickerService`). Form-urlencoded body uses `KeyValueEditorViewModel`.
4. Response body uses the same highlighting in read-only mode, with pretty and raw toggles.
5. Body type selector: none, raw (with content type), form-urlencoded, multipart.
6. Large bodies: highlighting is limited to the first 1 MB, so the editor stays responsive. Above that, the editor shows plain text.

Tests:
- Application: `JsonBodyFormatterTests` and `XmlBodyFormatterTests` (parameterized: valid, invalid, empty, nested).
- Application: `RequestBuilderTests` extended for each body type (multipart with file, form-urlencoded encoding).
- UI: `BodyEditorViewModelTests` for body type switching and the large-body limit.

Manual checks: edit a 2 MB JSON body and confirm the editor stays responsive. Format a malformed JSON body and confirm the error message.

### M5 Environments, Variables, Secrets, Auth

**Goal:** reuse values across requests, keep secrets encrypted, and send authenticated requests.

Tasks:
1. Domain: `Environment`, `Variable`, `IsSecret`.
2. Application: `EnvironmentService`, `SecretService`, the `ISecretCipher` and `IKeyDeriver` interfaces. `VariableResolver` decrypts secrets through `SecretService`, and fails with `Security` while locked. `RequestBuilder` applies auth.
3. Infrastructure: `Migration0004Environments` (environments, variables, secret flag and ciphertext columns; settings rows for the KDF salt, iterations, and verifier), `EnvironmentDto`, `VariableDto`, `SqliteEnvironmentRepository`, `AesGcmSecretCipher`, `Pbkdf2KeyDeriver`.
4. UI: `EnvironmentSelectorViewModel` in the status bar, `EnvironmentEditorViewModel` with secret toggle, `UnlockDialogViewModel`, `MasterPasswordSetupViewModel`. Auth editor for None, Basic, Bearer, and API key.
5. Lock state is shown in the status bar. Sending a request that needs a secret while locked prompts for unlock.

Tests:
- Application: `EnvironmentServiceTests`, `SecretServiceTests` (setup, unlock with correct and wrong password, lock clears the key, encrypt and decrypt round-trip), `RequestBuilderTests` extended for each auth type.
- Infrastructure: `AesGcmSecretCipherTests` (round-trip, wrong key returns `Security`, tampered ciphertext returns `Security`, each with a logged error), `Pbkdf2KeyDeriverTests` (same password and salt give the same key, different salt gives a different key), `SqliteEnvironmentRepositoryTests`.
- UI: `UnlockDialogViewModelTests`.

Manual checks: store a token as a secret, restart, confirm the database contains no plaintext, unlock, and send an authenticated request.

### M6 Tabs, Autosave, Settings, Shortcuts

**Goal:** a working multi-tab editor that survives restarts, with configurable transport settings.

Tasks:
1. Domain: no new types beyond `AppSettings` (Application).
2. Application: `TabService` (open, close, dirty tracking, draft storage), `SettingsService`, `AppSettings`, `IOpenTabsStore`.
3. Infrastructure: `Migration0005OpenTabs` (open tabs and drafts), `SqliteOpenTabsStore`, `SqliteSettingsStore` (typed values). `RequestHttpClientFactory` reads `AppSettings` and applies changes without restart.
4. UI: tab strip with close buttons, `SettingsViewModel` and `SettingsWindow` (timeouts, redirects, SSL verification, proxy), keyboard shortcuts (Ctrl+Enter, Ctrl+S, Ctrl+T, Ctrl+F4).
5. Autosave: drafts are written on change, debounced (proposed 1 second). On startup, open tabs are restored, including unsaved drafts, and marked dirty.

Tests:
- Application: `TabServiceTests` (open, close, dirty tracking, restore drafts), `SettingsServiceTests` (defaults and validation of timeouts and proxy URL).
- Infrastructure: `SqliteOpenTabsStoreTests`, `SqliteSettingsStoreTests`, `RequestHttpClientFactoryTests` (settings map to the handler: redirect policy, timeouts, SSL callback behavior).
- Integration: a redirect and a timeout test that changes settings and expects a different result.
- UI: `TabStripViewModelTests`, and a headless smoke test that starts the main window.

Manual checks: edit two tabs without saving, close the app, reopen it, and confirm both tabs and drafts are restored.

### M7 Polish (UX)

**Goal:** a window that is easier to read and use. This milestone is UX only. Packaging, signing, and the hard non-UX checks are in M8. Changed 2026-10-27 (decision recorded in `implementation-progress.txt`); the UX list below comes from `C:\zzz_temp\PlanChange.txt` and the mock-up `improve_ux.png`.

Tasks, done before the plan change:
1. Accessibility pass: keyboard navigation through every control, automation names on controls, check light and dark themes. The keyboard and Narrator checks are rerun after the UX tasks below, because the layout changes.
2. Headers and query params editor (open since M1 and M4), and the other polish items carried from M2 to M6.
3. Switch commit messages to Conventional Commits (`type(scope): summary`), as decided in section 12, item 4. Record the switch in the progress tracker.

Tasks, UX changes (details decided 2026-10-27):
4. **Main menu.** A menu bar with File, Edit, View, and Help. Every item that has a shortcut shows it, and calls the same command as the key binding, so the logic is not duplicated. Contents:
    - File: New request (Ctrl+T), Save (Ctrl+S), Save All (Ctrl+Shift+S), Close tab (Ctrl+F4), Settings, Exit.
    - Edit: Cut, Copy, Paste, Select all (on the focused text control), Send (Ctrl+Enter), Cancel send.
    - View: show or hide the Collections panel and the Response panel, next and previous request tab (Ctrl+Tab, Ctrl+Shift+Tab).
    - Help: About (name, version, license), README link.
5. **Borders.** Controls, panels, and tab headers get a border, so they are visually distinct. Border colors are theme resources and work in light and dark.
6. **Layout.** Three panels from left to right: Collections and History, the request editor, and the Response. The Response moves to the right side (it is below the request now). The response body has a vertical scroll bar.
7. **Resizable panels.** A splitter between the Collections panel and the request editor, and between the request editor and the Response, so each width can be changed. Each panel has a minimum width. The widths are remembered in the settings across restarts.
8. **Request tab order.** The body tab comes first: Body, Params, Headers, Authorization. Each new request opens with the Body tab selected and the body editor focused. The same holds for a request opened from the tree or from history. Each tab keeps its own page while the app runs. A tab restored at the next start opens on Body, because the page is not stored.
9. **Tab header font.** Tab headers use a smaller font, the font size of the Collections tree plus 1. This applies to all tab headers (the request tabs, the Params, Headers, Body, Authorization tabs, and Collections and History).
10. **Top bar.** The secrets lock state, environment selector, and Manage move from the bottom bar to a thin bar at the top, below the menu. Settings are no longer a button: they open from the menu only. The bottom bar stays as a thin line that shows only the status and autosave error messages (they remain live regions).
11. **Single-click open.** A new setting, "Open a request with a single click" (`AppSettings.OpenOnSingleClick`, default off, which keeps the double-click). It is shown in the Settings window, stored with the other settings, and applies without a restart.
12. **Save All.** Ctrl+Shift+S saves every changed tab. A tab that is not linked to a collection asks for a folder and a name, one after the other, and Cancel stops the rest.
13. **Ctrl+Tab.** Ctrl+Tab and Ctrl+Shift+Tab switch the open request tabs in the order they were last used, not in strip order. The request tabs are the only tabs affected. The sidebar tabs (Collections, History) and the Params, Headers, Body, Authorization tabs do not change. The most-recently-used order lives in `TabStripViewModel`. Each press moves one step in the order, with no pop-up list.

Tasks, UX follow-ups (from `C:\zzz_temp\update.txt`, 2026-10-27):
14. **Rename commits on focus loss.** In the Collections tree, leaving the rename box with the mouse or Tab commits the name, like Enter. Escape still cancels.
15. **Selected tab.** The selected tab header has a gray fill (a theme resource, light and dark) instead of the underline.
16. **Response panel order.** From the top: a thin Pretty and Raw bar, the body, a splitter, and the other response information (error, status, timing, size, note, headers). The body and the information have borders.
17. **Collections toolbar.** Three small icon buttons with tooltips: New collection, New folder, New request. New folder and New request create in the selected node and need a selection. The context menu of a collection also has New collection.
18. **Dialog buttons.** Cancel is the rightmost button in every dialog that has one.
19. **Body for a new request.** Choosing POST, PUT or PATCH on a request that has no body selects the Raw body type with Content-Type `application/json`. A body type or content type already chosen is kept.
20. **Content-Type box.** The Content-Type of a raw body is an editable combo box: choose a common type, type another, or clear it.
21. **Pretty switch bug.** After switching the response from Pretty to Raw, Pretty stayed disabled. Fixed.
22. **Compact body tab.** The controls that belong to the body type are to its right: Content-Type and Format for Raw, Add field or Add part for the form and multipart types.
23. **Theme setting.** A new setting, "Theme" (`AppSettings.Theme`: System, Light or Dark, default System), is shown in the Settings window, stored with the other settings and applied to the whole application without a restart. System follows the operating system.

Tests:
- UI: `TabStripViewModelTests` for the recently used order (parameterized: switch, close, reopen, wrap around, one tab only) and for Save All (changed linked tabs, unlinked tab asks for a name, cancel stops).
- Application and UI: `SettingsServiceTests` and `SettingsViewModelTests` for `OpenOnSingleClick` (default, store, read back). Panel widths too, if they are stored.
- UI: headless tests for single-click and double-click open in the Collections tree, for the default sub-tab and focus of a new request, and for Ctrl+Tab affecting only the request tabs.
- UI: the existing smoke tests (`EveryInteractiveControlHasAnAccessibleName`, dialogs) cover the menu items and the splitters.

Manual checks: compare the window with `improve_ux.png` in light and dark. Drag both splitters. Use every menu item and confirm its shortcut. Run the accessibility checklist again (`docs/portability-checklist.md`, section 2).

Tests: the full suite runs green on Windows. Coverage gate holds.

### M8 Release

**Goal:** a first packaged release for Windows. Linux follows in M9 (section 8.1). Depends on M7. These are the release tasks and the hard checks that are not UX. Moved from the former M7 on 2026-10-27.

Tasks:
1. Portability smoke test, recorded as a checklist in `docs/`: on Windows, unzip, run, send a request, create a collection, restart, and confirm `data/` is beside the executable. The Linux run is part of M9 (section 8.1).
2. `scripts/build.ps1` publishes the release zips with SHA-256 checksums.
3. Release notes and a short README covering the data folder, the `--data-dir` option, the SmartScreen warning for unsigned builds, and the Apache-2.0 license. Updated at the end of M7 for the UX changes (menu, layout, shortcuts, the new setting).
4. Signing is out of the code. Before the first public release, apply to SignPath Foundation (Q7 in the architecture plan). If the project is not yet eligible, the first release ships unsigned and signing starts with the next release. If SignPath declines, the release stays unsigned.
5. Smart App Control check: on a clean Windows 11 install with Smart App Control on, unzip the release, run it, and record the result in the portability checklist. If the unsigned build is blocked, stop and revisit Q7 before release.

Tests: the full suite runs green on Windows. Coverage gate holds. `scripts/build.ps1` runs end to end on the release commit, and CI is green on `main` for it.

### 8.1 M9 Linux Verification and Release Order (Amended)

No Linux machine is available during the Windows phase, so the Linux work is separated from the other milestones and runs after the Windows release (M8). It is M9. The milestone checklists above cover Windows only. Linux is deferred to a later version: there is no `linux-x64` publish and no Linux CI job until that stage starts. `scripts/build.ps1 -Runtimes win-x64,linux-x64` re-enables the Linux publish.

1. **Windows release (M8).** `scripts/build.ps1` produces `dist/RestClient-win-x64.zip`. Its smoke test runs against the extracted zip, the artifact a user receives. The Windows release is verified before any Linux work starts.
2. **Linux verification.** When a Linux machine is available, run the `linux-x64` build from `dist/RestClient-linux-x64.zip` and check the following:
   - Unzip to a new folder, run, and confirm `data/app.db` and a log file are created beside the executable.
   - The SQLite native library loads from the single-file publish (the risk in section 11).
   - The `--data-dir` argument and the `RESTCLIENT_DATA_DIR` variable redirect the data folder.
   - From M1 onward: send a request, create a collection, restart, and confirm the state is kept.
3. **Fixes.** Linux issues found in step 2 are fixed in the codebase, then the milestone's Windows checks are rerun so the fix does not break Windows. Linux fixes are tracked in `implementation-progress.txt`.
4. **Linux release.** When the Linux checks pass, the same build script produces the Linux zip for release.

Linux verification adds no features. Its output is fixes and a verified Linux zip.

## 9. Parameter Defaults (Proposed)

These values are proposed here and are easy to change in settings or code. Confirm or adjust them during review.

| Parameter | Proposed value | Source |
|---|---|---|
| Body preview limit | 1 MB | Architecture plan Q9 |
| Spill-to-disk threshold | 1 MB, same as the preview limit. Bodies up to 1 MB are kept in full; larger bodies are written to disk and the preview is their first 1 MB. Decided 2026-10-24, replacing the earlier 5 MB value. | Architecture plan Q9 (decision recorded in implementation-progress.txt) |
| History retention | 1000 entries or 30 days | Architecture plan 3.3 |
| PBKDF2 iterations | 600,000 (SHA-256) | Proposed; follows current OWASP guidance |
| AES-GCM key size | 256 bits | Proposed |
| Autosave debounce | 1 second | Proposed |
| Default request timeout | 30 seconds | Proposed |
| Default redirects | follow, up to 10 | Proposed |
| Connect timing | recorded; DNS not separated | Section 4 |

## 10. Test and Coverage Strategy

- **Unit tests** (Domain, Application, UI view models): fast, no I/O. FakeItEasy fakes for every interface.
- **Infrastructure tests:** SQLite in-memory databases for repositories. Cipher tests use fixed test vectors and random keys.
- **Integration tests:** in-process Kestrel server. These cover real HTTP behavior: redirects, timeouts, cancellation, and large bodies.
- **UI tests:** view models in plain NUnit. Avalonia.Headless (`Avalonia.Headless.NUnit`, verify the package at M0) for a small number of smoke tests.
- **Coverage gate:** coverlet with the threshold properties of `coverlet.msbuild`, set to 80% line coverage. The gate covers `RestClient.Domain` and `RestClient.Application` only. Infrastructure and UI are reported, not gated.
- **Portability checks:** `build.ps1` publishes win-x64 on every run. Manual smoke tests on Windows as in M0 and M8. Linux smoke tests follow section 8.1 in a later version.

## 11. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| SQLite native binaries missing from single-file publish | App fails to start on one target | Verify in M0 with the smoke test. Enable self-extract for native libraries if needed. |
| Avalonia or AvaloniaEdit API changes between versions | Rework in UI | Pin versions in M0. Upgrade only between milestones. |
| Connect timing unavailable for some proxy setups | Missing timing values | Show "n/a" and keep total and TTFB. |
| Large responses slow the UI | Poor UX | Preview cap, spill to disk, background reading (M1, M4). |
| Master password forgotten | Secrets unrecoverable | State this clearly in the setup dialog. There is no recovery in v1. |
| Linux desktop differences (fonts, drag-and-drop, file pickers) | Visual or behavior bugs | Linux verification after the Windows release (section 8.1), then fixes. |
| Smart App Control blocks unsigned builds on Windows 11 | Windows 11 users cannot run the release | Test on a clean Windows 11 install in M8. If blocked, revisit Q7 before release. |
| SignPath application declined or delayed | First releases stay unsigned | Unsigned fallback with checksums and SmartScreen notes (M8). Reapply at later releases. |

## 12. Open Items Before Starting M0

1. **CI host.** Decided 2026-10-23: GitHub Actions, Windows job only (`.github/workflows/ci.yml`). The job runs `scripts/build.ps1` and uploads the win-x64 zip. The Linux CI job waits for the Linux stage (section 8.1). SignPath (Q7) signs from an automated build, so this choice also affects signing and should be settled before the first public release.
2. **Linux test machine.** Not available for now. Linux verification is deferred until after the Windows release (section 8.1), when a machine is available.
3. **Parameter defaults** in section 9. Confirm or change.
4. **Commit message convention.** Decided 2026-10-23. Until M7, use plain imperative subjects of 72 characters or fewer, starting with a capital letter and with no period. When a commit completes or changes a milestone task, name the milestone in the subject, for example "M1: add RequestBuilder". Wrap the body at 72 characters and explain why. Leave out `Co-Authored-By` lines, per `CLAUDE.md`. Keep one logical change per commit. From M7, switch to Conventional Commits (`type(scope): summary`), as M7 task 3 sets out.
5. **Warnings as errors.** Proposed on in `Directory.Build.props`. Confirm.
6. **SignPath eligibility.** SignPath requires a project that is already released in signable form. Confirm with SignPath whether an earlier pre-release is enough to qualify, or whether the first public release ships unsigned (Q7).

Once these are settled, M0 is the first implementation step. No code is written until this plan is approved.
