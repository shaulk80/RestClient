# REST Client

A desktop REST client for Windows. Build requests, send them, and keep them in collections. Everything is stored on your machine, in a folder next to the program.

## Features

- Requests with any method, URL, query parameters, headers, body and authorization. Parameters and headers are rows that can be switched on and off. Bodies can be raw text (JSON and XML are highlighted and can be formatted), form-urlencoded, or multipart with files.
- Authorization: Basic, Bearer token, and API key (in a header or in the query).
- Responses show status, timing, size, headers, and a body that can be viewed pretty-printed or raw. Large bodies are kept on disk, and the first 1 MB is shown.
- Collections with folders and subfolders. Drag and drop to move items.
- History of every request that was sent, with search and restore. History keeps the URL as you typed it, with `{{placeholders}}`, so secret values never reach it.
- Environments with `{{variables}}`. Variables can be marked secret. Secrets are encrypted with a master password.
- Several request tabs. Open tabs and unsaved edits come back after a restart.
- Settings for the request timeout, redirects, SSL certificate checks, and a proxy.
- Light and dark themes, following the system setting.
- A main menu (File, Edit, View, Help) with the shortcuts shown next to each item. Settings are in the File menu.
- Three panels: Collections and History, the request, and the response on the right. Drag the splitters to change the widths. The widths are remembered. The View menu hides the Collections and Response panels.
- A setting to open a request with a single click in the Collections tree. It is off by default, which keeps the double-click.

## Install and run

1. Download `RestClient-win-x64.zip` and `SHA256SUMS.txt` from the release page.
2. Check the download. In PowerShell, in the folder with both files:

   ```powershell
   (Get-FileHash .\RestClient-win-x64.zip -Algorithm SHA256).Hash.ToLower()
   Get-Content .\SHA256SUMS.txt
   ```

   The two hashes must be the same.
3. Unzip the folder anywhere you can write to, and run `RestClient.exe`. There is no installer. It needs no .NET installation.

### Windows warnings

The release is **not code-signed yet**. Windows may react in one of these ways:

- **SmartScreen** shows "Windows protected your PC". Choose *More info*, then *Run anyway*, after you have checked the hash above.
- **Smart App Control** (Windows 11, when turned on) can block unsigned programs without an option to run them. If that happens, the program cannot be started on that machine until Smart App Control is turned off or the program is signed. Smart App Control cannot be turned on again once it is off.

## Your data

All data is kept in a `data` folder:

| Path | Content |
|---|---|
| `data/app.db` | Collections, history, environments, settings and open tabs (SQLite) |
| `data/logs/` | Log files |
| `data/temp/` | Large response bodies saved by the app. Files are removed when their history entry expires |

By default `data` is created next to `RestClient.exe`. To use another folder, use either of these. The command-line argument wins when both are set.

```powershell
.\RestClient.exe --data-dir D:\RestClientData
$env:RESTCLIENT_DATA_DIR = 'D:\RestClientData'; .\RestClient.exe
```

To move the app to another computer or back it up, copy the whole folder, including `data`.

### Secrets and the master password

Secret variables are encrypted with AES-256-GCM, with a key derived from your master password (PBKDF2, 600,000 iterations). The password itself is never stored. **There is no way to recover a forgotten master password.** The secrets then have to be entered again.

Secrets are unlocked for the running session only. Use *Lock* in the bar under the menu to lock them sooner.

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

The script restores packages, checks the licenses of all dependencies, builds with warnings as errors, runs the tests with a coverage gate, publishes `win-x64` as a single self-contained file, zips it to `dist/RestClient-win-x64.zip`, writes `dist/SHA256SUMS.txt`, and starts the unzipped program once to check that it creates its `data` folder. Close the sample API before running it if you started it.

Project layout and design are described in [ARCHITECTURE_PLAN.md](ARCHITECTURE_PLAN.md) and [implementation-plan.md](implementation-plan.md).

## License

Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE). Dependencies are checked by the build to carry MIT, Apache-2.0, BSD, ISC or public-domain licenses.
