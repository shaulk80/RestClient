# RestDraft: Architectural Plan (High-Level)

Status (2026-10-31): **living design document.** The product is built and released (0.1.1). Section 6 records the decisions. Sections 7 and 8 and the wording "proposed" are history from before the build: milestones M0 to M11 are done, and the current state is in [STATUS.md](STATUS.md).

## 1. Goal

A desktop REST client for Windows and Linux, similar in spirit to early versions of Postman. It must:

- Send REST requests to chosen endpoints and show the responses.
- Offer an intuitive, fast UX.
- Organize requests into collections with folders and subfolders.
- Preserve request history.
- Make request body editing easy, with syntax highlighting.
- Be fully portable: no installer, no admin rights, no machine-specific state required to run.

### Scope

**In scope (v1)**

- Request builder: method, URL, query params, headers, body, auth (none, Basic, Bearer, API key).
- Body types: none, raw (JSON, XML, text), form-urlencoded, multipart/form-data.
- Response viewer: status, time, size, headers, body (pretty / raw), with syntax highlighting.
- Cancel an in-flight request.
- Collections tree with unlimited folder nesting, drag-and-drop reordering, rename, duplicate, delete.
- Request history: chronological list, filter/search, reopen or re-send an entry.
- Environments with `{{variable}}` substitution.
- Tabs for multiple open requests.
- Settings: timeouts, redirects, SSL verification, proxy.
- Keyboard shortcuts (for example, Ctrl+Enter to send, Ctrl+S to save).
- Unit test project with coverage reporting (section 5).

**Out of scope (v1, candidates for later)**

- Pre-request scripts and post-response test scripts (see Q8).
- OAuth2 flows.
- GraphQL, gRPC, WebSocket, and SSE.
- Team sync, cloud accounts, and collaboration.
- Mock servers and API documentation generation.
- Postman collection import and export.
- macOS builds and support.

## 2. Non-Functional Requirements

| Area | Requirement |
|---|---|
| Portability | One native executable per OS and architecture. Runs from any folder, including a USB stick. All user data lives in a folder next to the executable, with an optional override. |
| Performance | Cold start under 2 s on a typical laptop. Responses of tens of MB stay responsive: stream the body and truncate the display above a threshold. |
| Responsiveness | Network I/O never blocks the UI thread. Every request is cancellable. |
| Data safety | Autosave of open tabs. Every write is a transaction. No data loss on crash. |
| Security | Secrets (tokens, passwords) are encrypted at rest with a master password. TLS verification on by default. |
| Accessibility | Full keyboard navigation. Respects OS theme (light and dark). |
| Maintainability | Clear layering (see section 4). Unit-testable core with no UI dependency. Unit tests with coverage (section 5). |

## 3. Decisions: Language, Stack and Storage

### 3.1 Language and UI Stack (Decided: Option A)

The stack was chosen on best-practice criteria for this application, as set in CLAUDE.md ("Language Selection"):

- **Ease of use:** how quickly the stack gets to a working, maintainable app.
- **Performance:** startup time, memory use, and responsiveness with large responses.
- **Open-source ecosystem:** number and maturity of open-source libraries for HTTP, desktop UI, code editing, and storage.
- **Portability:** one executable per OS, with a reasonable footprint. This is a requirement of the app (section 2), so it is a criterion too.
- **Editor support:** a mature code-editor component with syntax highlighting. This is central to the body editor and response viewer.

The app is a single program, so one language covers the whole application.

**Options considered**

| Option | Ease of use | Performance | OSS ecosystem | Portability / footprint | Editor support |
|---|---|---|---|---|---|
| **A. C# / .NET 10 LTS + Avalonia UI** | High | High | High (NuGet; Avalonia is MIT) | High: single-file, self-contained executable per target | High: AvaloniaEdit (MIT) with TextMateSharp grammars |
| B. Rust + Tauri (web UI in a system WebView) | Medium: Rust learning curve, UI in web tech | High | High (crates, npm) | High: small binaries. The WebView differs per OS | High: Monaco in the web UI |
| C. TypeScript + Electron | High | Medium: ships Chromium, high memory use | Very high | Low: 100+ MB per build | High: Monaco |
| D. Kotlin + Compose Multiplatform (desktop) | Medium to high | High | High (Maven) | Medium: bundles a JVM runtime | Lower: fewer mature code-editor options; needs evaluation |
| E. Dart + Flutter (desktop) | High | High | Medium | High | Lower: limited widgets for complex code editing |
| F. C++ + Qt | Low | Very high | High | High | High, but more effort to build and maintain |
| G. Python + Qt bindings (PySide6 or PyQt) | High | Low to medium: slow for CPU-heavy work such as highlighting large bodies; most request time is I/O | Very high | Low to medium: bundled interpreter, large packages (tens of MB), antivirus false positives are common | Medium: code editor via Qt widgets (for example, QScintilla) or Pygments-based highlighting; needs evaluation |

**Decision: Option A, C# / .NET 10 LTS with Avalonia UI.** It was chosen on the five criteria above, not on any assumption about existing team skills. Under this decision, the rules files `csharp.md` and `csharp-tests.md` apply (section 3.4).

### 3.2 Portability Strategy

- Build one native executable per target: `win-x64` and `linux-x64`. Package each as a zip. No installer (Q2).
- No registry writes. No admin rights needed.
- Data folder: `./data/` next to the executable, created on first run. An optional `--data-dir` argument or environment variable overrides it, so the app can run from a read-only location.
- The whole state (settings, collections, history) is one SQLite file in the data folder. Copying the folder moves all state.
- Build mechanism for Option A: self-contained single-file publish per runtime identifier.
- Signing: apply to SignPath Foundation's free open-source program before the first public release (Q7). Internal builds are unsigned until then.

### 3.3 Storage (Decided: single SQLite database)

All state lives in one SQLite file, `data/app.db`, accessed through Microsoft.Data.Sqlite (Q3). Sharing collections is not a requirement, so the git-friendly JSON files from the earlier draft are dropped.

- **Collections:** `collections` and `folders`, with `requests` in a single table. Parent-child links model the tree, and a position column keeps the order.
- **Environments:** `environments` and `environment_variables`. A secret flag marks encrypted values (Q5).
- **History:** `history_entries`, indexed on time and URL. Capped by a configurable retention limit, for example 1000 entries or 30 days.
- **Settings and UI state:** `settings` (key-value) for app settings, and `open_tabs` for restoring tabs at startup.
- **Writes:** each user action is one transaction, so atomic-file handling is not needed.
- **Schema versioning:** `PRAGMA user_version` with migrations written as code, applied at startup.
- **Secrets (Q5):** secret values are encrypted with AES-GCM (System.Security.Cryptography, built into .NET). The key is derived from the master password with PBKDF2 (built into .NET) and held in memory only while the app is unlocked.
- **Risk to verify in M0:** SQLite native binaries must survive the single-file publish on both targets. The portability smoke test covers this.

## 4. Architecture Overview

### 4.1 Layers

```
+--------------------------------------------------------------+
|  Presentation (Avalonia)                                     |
|  Views, ViewModels, editor integration, shortcuts            |
+--------------------------------------------------------------+
|  Application services                                        |
|  RequestSender, CollectionService, HistoryService,           |
|  EnvironmentService, TabService, SettingsService             |
+--------------------------------------------------------------+
|  Domain                                                      |
|  Collection, Folder, RequestDefinition, Body, Auth,          |
|  Environment, Variable, HistoryEntry, ResponseSnapshot       |
+--------------------------------------------------------------+
|  Infrastructure                                              |
|  HTTP executor, SQLite repositories, secret encryption,      |
|  data folder locator, schema migrations                      |
+--------------------------------------------------------------+
```

Rules:

- The domain and application layers have no UI dependency. This keeps them testable without a UI.
- The UI talks only to application services, never directly to the HTTP client or the database.
- Infrastructure implements interfaces defined in the application layer (repository pattern), so tests can replace them with fakes.

### 4.2 Key Components

- **RequestSender:** builds the HTTP request from the domain model after variable substitution. Executes it with a cancellation token. Measures timing (DNS, connect, TTFB, total), size, and returns a `ResponseSnapshot`. Streams large bodies and caps the in-memory preview (Q9).
- **VariableResolver:** resolves `{{name}}` placeholders from the active environment, then from collection variables. Unresolved placeholders are reported, not silently sent.
- **CollectionService:** CRUD for the folder tree. Enforces unique names per folder. Persists through the collection repository.
- **HistoryService:** records each send (request snapshot, response summary, timestamp). Applies retention. Supports search and restore.
- **SecretService:** encrypts and decrypts secret values. Locks and unlocks with the master password.
- **TabService:** tracks open tabs, dirty state, and autosave.
- **Error handling:** operations return a result wrapper (result or error message) rather than throwing for expected failures (invalid URL, file not found, network error). This follows CLAUDE.md, Code Quality rule 3. The concrete wrapper type is created in the chosen language if it does not already exist.

### 4.3 Request Lifecycle

1. User edits a request in a tab (dirty state set).
2. On send: resolve variables, validate URL and headers.
3. Start the request in the background with a cancellation token. The UI shows progress and a Cancel button.
4. On completion: display the response, record the history entry, and update the tab.
5. On failure or cancellation: display a structured error (category, message, optional details). Still record the attempt in history.

### 4.4 UI Layout (Conceptual)

- Left sidebar: collections tree (top) and history (switchable tab).
- Main area: tabs of open requests. Each tab has a request builder (top) and response viewer (bottom, resizable).
- Request builder: method dropdown, URL bar, Send and Cancel buttons, then sub-tabs for Params, Headers, Auth, Body, Settings.
- Body editor: editor with syntax highlighting. Format button for JSON and XML. Language selector.
- Status bar: active environment selector, last request time and size, lock state for secrets.

## 5. Quality and Testing

### 5.1 Unit Tests (Required in v1)

- A separate unit test project (`*.Tests`) is part of the solution from M0.
- Covers the domain and application layers: variable resolution, collection tree operations, history retention, secret encryption round-trips, serializers, and the result wrapper.
- Group similar cases as parameterized test cases (CLAUDE.md, Testing rule 1). Test frameworks come from the C# rules: NUnit and FakeItEasy, with `Assert.That()` (`rules/csharp-tests.md`).
- Coverage: **proposed target of 80% line coverage** on the domain and application layers, reported in CI as a warning when missed (it does not fail the build; changed 2026-10-31). Infrastructure and UI code are measured and reported, without a target. Adjust the target if you prefer a different number.
- Every milestone adds its own unit tests. No milestone is complete without them.

### 5.2 Other Test Levels

- **Integration tests:** RequestSender against a local test server. Covers timeouts, redirects, cancellation, and TLS toggles.
- **Repository tests:** round-trip tests for the SQLite repositories, including schema migration.
- **UI tests:** key flows only: create a collection, send a request, reopen from history. Use headless test support (Avalonia.Headless under Option A).
- **Portability checks:** CI builds Windows now. The Linux CI job is added in the Linux stage. A smoke test runs each published executable on its OS and verifies the data folder is created beside it.

## 6. Decisions and Open Questions

| # | Question | Decision | Status |
|---|---|---|---|
| Q1 | Stack | C# / .NET 10 LTS with Avalonia UI (Option A). | Decided |
| Q2 | Portability | Single executable plus data folder beside it, distributed as a zip. No installer. | Decided |
| Q3 | Storage | Single SQLite database. Sharing collections is not a requirement. | Decided |
| Q4 | Postman import/export | Not related to this project. Out of scope. | Decided |
| Q5 | Secrets | Encrypted at rest with a master password, using AES-GCM and PBKDF2. | Decided |
| Q6 | OAuth2 | Not in v1. Basic, Bearer and API key only. | Decided |
| Q7 | Code signing | Apply for free signing through SignPath Foundation's open-source program before the first public release. SignPath requires the project to be already released in signable form, so the first public release may ship unsigned, and signing starts with the next release. If SignPath declines, ship unsigned, with SHA-256 checksums and a note on the SmartScreen bypass. Test Windows 11 Smart App Control on a clean install before release (M8). macOS is out of scope. | Decided (apply before first public release) |
| Q8 | Scripting and tests | Pre-request and post-response scripts are out of v1 (see 6.1). Unit tests are in v1 (section 5.1). | Decided |
| Q9 | Large responses | Bodies up to 1 MB stay in memory. Larger bodies stream to disk, and the preview is the first 1 MB. Decided 2026-10-24, replacing the earlier 5 MB value. | Decided |
| Q10 | Application license | Apache-2.0. Full text in `LICENSE`, attribution in `NOTICE`. OSI-approved, so eligible for SignPath (Q7). All planned dependencies are permissive; a license check runs in CI from M0. | Decided |
| Q11 | Target OS versions | Windows 10 22H2+ and Windows 11, Ubuntu 22.04+ and equivalent. macOS is out of scope. | Decided |
| Q12 | Weighting of selection criteria | Irrelevant now that Q1 is decided. | Resolved |

### 6.1 Q8 Explained

- **Pre-request script:** a small JavaScript snippet that runs before a request is sent. It might set a header, compute a signature, or generate a timestamp.
- **Post-response test script:** a snippet that runs after the response arrives and asserts on it, for example that the status is 200 or that a JSON field has a given value. This is the "Tests" tab in Postman.
- Neither of these is a unit test. Both are deferred to a later version. Unit tests for the application itself are required in v1 (section 5.1).

### 6.2 Storage Consequence (Q3)

Dropping collection sharing removes the main reason for JSON files. The single SQLite file is simpler, transactional, and needs no atomic-replace logic. If sharing becomes a requirement later, collection export can be added without changing the storage model.

## 7. Milestones (High-Level)

| Milestone | Content |
|---|---|
| M0 | Project skeleton, layering, dependency injection, logging, data-folder locator, SQLite and migrations, unit test project with coverage reporting, CI build for Windows (Linux later), portability smoke test. |
| M1 | Request builder and RequestSender: send, cancel, response viewer, timing, large-response streaming. |
| M2 | Collections tree with folders and subfolders, SQLite persistence. |
| M3 | History: list, search, restore, retention. |
| M4 | Body editor with syntax highlighting, JSON and XML formatting, form and multipart bodies. |
| M5 | Environments and variable resolution, secret encryption and master password, auth (Basic, Bearer, API key). |
| M6 | Tabs and autosave, keyboard shortcuts, settings (timeouts, redirects, SSL, proxy). |
| M7 | Polish (UX only): accessibility pass, main menu, borders, three-panel layout with splitters, shortcuts. |
| M8 | Release: portability smoke tests, SignPath application, Smart App Control check, first packaged Windows release. |
| M9 | Linux verification and the Linux release (when a Linux machine is available). |
| M10 | Handling the SignPath outcome: sign the releases if approved, or record the decision to stay unsigned. Needs M8 only. |

Each milestone includes its unit tests. The 80% coverage target (a warning, not a gate) applies from M0 onward.

## 8. Next Steps

1. Confirm the proposed 80% coverage target (section 5.1). Q10 is decided (Apache-2.0); the license files are added in M0.
2. Draft the detailed implementation plan: class-level design, file layout, and per-milestone task breakdown.
3. No code is written until the implementation plan is approved.
