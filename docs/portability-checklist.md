# Portability and release checklist

Run this against the **extracted release zip**, not the build output. It is the file a user receives. Record the date, the machine and the result for every step. A step that fails stops the release.

Release under test: `dist/RestClient-win-x64.zip` (from `scripts/build.ps1`)
SHA-256 (from `dist/SHA256SUMS.txt`): ______________________________

## 1. Windows portability (plan M7 task 2)

Machine: ______________________  Windows version: ______________  Date: ____________

| # | Step | Expected | Result |
|---|---|---|---|
| 1 | Compare the zip hash with `SHA256SUMS.txt` | Hashes are equal | |
| 2 | Unzip into a new folder that is not the repository, for example `C:\Temp\RestClientCheck` | `RestClient.exe` and `MigrationScripts\` exist; no `.pdb` files | |
| 3 | Run `RestClient.exe` | The window opens. `data\app.db` and `data\logs\<file>` exist beside the executable | |
| 4 | Look for `data` anywhere else (`%APPDATA%`, `%LOCALAPPDATA%`, the current directory) | Nothing there | |
| 5 | Create a collection, a folder, and a request. Enter `https://example.com`, send | Status 200 shows, the response body is displayed | |
| 5a | Open the Params tab and add `q` = `1`, open the Headers tab and add `X-Test` = `abc`, then send to an echo endpoint such as `https://httpbin.org/anything` | The echo shows `q=1` in the query and `X-Test: abc` as a header. The tab titles show `Params (1)` and `Headers (1)` | |
| 6 | Save the request (Ctrl+S) | The tab loses its `*` | |
| 7 | Close the app and run it again | The collection tree, the open tab (with its parameter and header) and the History entry are back | |
| 8 | Close the app. Run `RestClient.exe --data-dir D:\RestClientData` (any other folder) | A new empty data folder is created there. The first one is untouched | |
| 9 | Set `RESTCLIENT_DATA_DIR` to another folder and run without the argument | That folder is used | |
| 10 | Make the data folder impossible to create (for example `--data-dir Z:\missing` for a drive that does not exist) | A message box "REST Client" explains the problem. The process ends | |
| 11 | Delete the unzipped folder | Nothing is left behind except the files you made yourself | |

## 2. Accessibility check (plan M7 task 1)

Do it once per release, in light and in dark mode (Windows *Settings > Personalization > Colors*).

| # | Step | Expected | Result |
|---|---|---|---|
| 1 | Use only the keyboard: Tab through the main window | Focus reaches every control and is visible; no focus trap | |
| 2 | In the Collections tree: arrow keys, F2, Delete, Enter, Shift+F10, Esc | Moves, renames, asks to delete, opens a request, opens and closes the menu | |
| 2a | Tab into the body editor (choose a raw body first), then press Esc / Shift+Esc | Focus moves to the next / previous control | |
| 3 | New collection / New folder | The name box has focus with its text selected | |
| 4 | Open each dialog (Save, Settings, Unlock, Set master password, Environments, Delete, Unsaved changes) | Focus starts in the first input. Enter confirms where it is safe, Esc cancels | |
| 5 | Switch Windows between light and dark while the app runs | Text stays readable. Error and warning text is readable. JSON and XML colors follow the theme | |
| 6 | Run Narrator (Win+Ctrl+Enter) and visit the controls | Each control is announced with a name, not just its type | |

## 3. Smart App Control (plan M7 task 6)

Needs a clean Windows 11 install or VM where Smart App Control is **On**. (It can only be turned on in a fresh install, so a VM snapshot is the easiest way to repeat this.)

Machine / VM: ______________________  Build: ______________  Date: ____________

| # | Step | Expected | Result |
|---|---|---|---|
| 1 | Download or copy the zip to the machine, so it carries the mark of the web | | |
| 2 | Unzip and run `RestClient.exe` | Record exactly what happens: runs, SmartScreen prompt, or blocked | |
| 3 | If it is blocked: **stop and revisit signing (Q7 in ARCHITECTURE_PLAN.md) before the release** | | |

## 4. Before the first public release (plan M7 task 5)

- [ ] Apply to SignPath Foundation (Q7). Outcome: ______________________
- [ ] If not eligible yet, the first release ships unsigned, with checksums and the README notes. Signing starts with the next release.
- [ ] `README.md`, `RELEASE_NOTES.md` and the version in `Directory.Build.props` agree.
- [ ] CI is green on `main` for the commit being released, and its uploaded zip has the same hash as the one checked above.
