# RestDraft

RestDraft - a REST API client. A desktop application for Windows and Linux (x64). Build requests, send them, and keep them in collections. Everything is stored on your machine, in a folder next to the program.

## Features

- Requests with any method, URL, query parameters, headers, body and authorization. Parameters and headers are rows that can be switched on and off. Bodies can be raw text (JSON and XML are highlighted and can be formatted), form-urlencoded, or multipart with files.
- Authorization: Basic, Bearer token, and API key (in a header or in the query).
- Responses show status, timing, size, headers, and a body that can be viewed pretty-printed or raw. Large bodies are kept on disk, and the first 1 MB is shown.
- Collections with folders and subfolders. Drag and drop to move items.
- History of every request that was sent, with search and restore. History keeps the URL as you typed it, with `{{placeholders}}`, so secret values never reach it.
- Environments with `{{variables}}`. Variables can be marked secret. Secrets are encrypted with a master password. In the Environments window the names are edited in the list, and one environment is marked as the current one.
- Several request tabs. Open tabs and unsaved edits come back after a restart.
- Settings for the request timeout, redirects, SSL certificate checks, and a proxy.
- Light and dark themes. The Theme setting is System (follows the operating system, the default), Light or Dark.
- A main menu (File, Edit, View, Tools, Help) with the shortcuts shown next to each item. Settings are in the File menu. Lock or unlock secrets and manage environments are in the Tools menu.
- Three panels: Collections and History, the request, and the response on the right. Drag the splitters to change the widths. The widths are remembered. The View menu hides the Collections panel, the Response panel and the bar with the secrets and the environment. What is shown is remembered.
- A setting to open a request with a single click in the Collections tree. It is off by default, which keeps the double-click.

## Install and run

1. Download `RestDraft-win-x64.zip` and `SHA256SUMS.txt` from the release page.
2. Check the download. In PowerShell, in the folder with both files:

   ```powershell
   (Get-FileHash .\RestDraft-win-x64.zip -Algorithm SHA256).Hash.ToLower()
   Get-Content .\SHA256SUMS.txt
   ```

   The two hashes must be the same.
3. Unzip the folder anywhere you can write to, and run `RestDraft.exe`. There is no installer. It needs no .NET installation.

### Uninstall

There is no installer, so to uninstall, delete the program folder. It includes the `data` folder, so copy `data` somewhere first if you want to keep your collections, history and environments.

All your data stays in the `data` folder. The one thing the program puts elsewhere is its native libraries (graphics, fonts, SQLite). On the first run it unpacks them into a temporary folder, `%TEMP%\.net\RestDraft`, and a new version unpacks into a new subfolder there. They contain no data of yours. You can delete `%TEMP%\.net\RestDraft` at any time, and the program recreates it when it needs it.

### Windows warnings

The release is **not code-signed yet**. Windows may react in one of these ways:

- **SmartScreen** shows "Windows protected your PC". Choose *More info*, then *Run anyway*, after you have checked the hash above.
- **Smart App Control** (Windows 11, when turned on) can block unsigned programs without an option to run them. If that happens, the program cannot be started on that machine until Smart App Control is turned off or the program is signed. Smart App Control cannot be turned on again once it is off.

### Linux

Since 0.1.1 the release has a Linux x64 package, `RestDraft-linux-x64.tar.gz`, next to the Windows zip. It passed the checks on Ubuntu 24.04 (under WSL2); other distributions and a full Linux desktop are untested. To use it:

1. Check the download, in the folder with both files (`--ignore-missing` skips the Windows zip): `sha256sum -c SHA256SUMS.txt --ignore-missing`.
2. Install the system libraries the program needs (names from Ubuntu 24.04; other distributions use similar names):

   ```bash
   sudo apt install -y libicu74 libfontconfig1 fonts-dejavu-core libx11-6 libice6 libsm6 libxext6 libxrandr2 libxi6 libxcursor1 libgl1
   ```

3. Extract and run. The archive keeps the executable permission, so no `chmod` is needed:

   ```bash
   mkdir RestDraft && tar -xzf RestDraft-linux-x64.tar.gz -C RestDraft
   ./RestDraft/RestDraft
   ```

The `data` folder, `--data-dir` and `RESTDRAFT_DATA_DIR` work as described under [Your data](#your-data), with `/` in paths. A startup error is printed in the terminal, because there is no message box on Linux.

To uninstall, delete the program folder (copy `data` first if you want to keep it) and the folder `~/.net/RestDraft`, where the program unpacks its native libraries on the first run.

On WSL2 with WSLg, closing a window or a popup can lag by about a second. This comes from WSLg, not from the program.

## Your data

All data is kept in a `data` folder:

| Path | Content |
|---|---|
| `data/app.db` | Collections, history, environments, settings and open tabs (SQLite) |
| `data/logs/` | Log files |
| `data/temp/` | Large response bodies saved by the app. Files are removed when their history entry expires |

By default `data` is created next to `RestDraft.exe`. To use another folder, use either of these. The command-line argument wins when both are set.

```powershell
.\RestDraft.exe --data-dir D:\RestDraftData
$env:RESTDRAFT_DATA_DIR = 'D:\RestDraftData'; .\RestDraft.exe
```

To move the app to another computer or back it up, copy the whole folder, including `data`.

### Secrets and the master password

Secret variables are encrypted with AES-256-GCM, with a key derived from your master password (PBKDF2, 600,000 iterations). The password itself is never stored. **There is no way to recover a forgotten master password.** The secrets then have to be entered again.

Secrets are unlocked for the running session only. Use *Lock* in the bar under the menu, or *Lock secrets* in the Tools menu, to lock them sooner.

## Privacy

RestDraft does not collect, store remotely or send any information about you or your use of the program. It has no telemetry, no analytics, no crash reporting and no update check.

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it. The only network traffic is the requests you choose to send, and they go only to the addresses you enter (through the proxy you configure, if any). Collections, history, environments, settings and secrets stay in the local `data` folder described above. None of the third-party components used by the program sends data on its own.

## Keyboard

| Keys | Action |
|---|---|
| Ctrl+Enter | Send the request in the active tab |
| Ctrl+S | Save the active tab |
| Ctrl+Shift+S | Save all changed tabs. A tab that is not in a collection yet asks for a folder and a name. Cancel stops the rest |
| Ctrl+T | Open a new tab |
| Ctrl+F4 | Close the active tab |
| Ctrl+Tab / Ctrl+Shift+Tab | Switch the request tabs in the order they were last used. Keep Ctrl down to go further back. Other tabs (Collections, History, Body, Params, and so on) are not affected |
| F2 / Delete / Enter | In the Collections tree: rename, delete, or open the selected item |
| Shift+F10 or the Menu key | In the Collections tree: open the context menu. Esc closes it |
| Esc / Shift+Esc | In a text editor (request or response body): move focus to the next / previous control. Tab types a tab character there |
| Esc / Enter | In dialogs: cancel / confirm (Enter does not confirm a delete) |

## Known limitations

See [RELEASE_NOTES.md](RELEASE_NOTES.md).

## Build from source

You need the .NET 10 SDK.

```powershell
.\scripts\build.ps1
```

The Linux package is built on a Linux host (the CI Linux job does it): `pwsh ./scripts/build.ps1 -Runtimes linux-x64`, which needs `xvfb` for its smoke test. On Windows that runtime is skipped.

The script restores packages, checks the licenses of all dependencies, builds with warnings as errors, runs the tests with a coverage gate, publishes `win-x64` as a single self-contained file, zips it to `dist/RestDraft-win-x64.zip`, writes `dist/SHA256SUMS.txt`, and starts the unzipped program once to check that it creates its `data` folder. Close the sample API before running it if you started it.

Project layout and design are described in [ARCHITECTURE_PLAN.md](ARCHITECTURE_PLAN.md) and [implementation-plan.md](implementation-plan.md).

## Code signing policy

Releases are not code-signed. The application to [SignPath Foundation](https://signpath.org) for free code signing through [SignPath.io](https://signpath.io) was declined for now, because the project does not yet have enough public visibility. The project will apply again when it has. If a certificate is issued, this is the policy that will apply.

Only binaries built from the source in this repository, by a GitHub Actions workflow, will be submitted for signing. Every signing request will be approved manually.

| Role | Member |
|---|---|
| Authors (commit access) | Shaul Kipod ([@shaulk80](https://github.com/shaulk80)) |
| Reviewers | Shaul Kipod ([@shaulk80](https://github.com/shaulk80)) |
| Approvers (approve each signing request) | Shaul Kipod ([@shaulk80](https://github.com/shaulk80)) |

Privacy: see the [Privacy](#privacy) section above.

> Status: unsigned. See [Windows warnings](#windows-warnings) for what Windows may show, and verify each download against `SHA256SUMS.txt`. When a certificate is issued and a signed release is published, rewrite the first paragraph to "Free code signing provided by SignPath.io, certificate by SignPath Foundation" and delete this note.

## License

Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE). Dependencies are checked by the build to carry MIT, Apache-2.0, BSD, ISC or public-domain licenses.
