# Release notes

## 0.1.0 (first Windows release)

First packaged release. Windows x64 only: `RestDraft-win-x64.zip`, a single self-contained executable plus its `MigrationScripts` folder. Linux follows in a later version.

### What is in it

- Request builder: method, URL, query parameters and headers (rows that can be switched off), raw / form-urlencoded / multipart bodies, JSON and XML highlighting and formatting.
- Authorization: none, Basic, Bearer, API key.
- Response viewer: status, timing (connect, first byte, total), size, headers, pretty or raw body, large bodies spilled to disk.
- Collections, folders and subfolders, with drag and drop, rename, duplicate and delete (with confirmation).
- History with search by URL and method, and restore into a new unsaved tab. An entry shows the URL and enabled query parameters as typed, with `{{placeholders}}` left in place. Resolved values, including secrets, are never stored in it.
- Environments, `{{variables}}` and secret variables encrypted with a master password.
- Tabs with autosaved drafts that survive a restart.
- Settings: timeout, redirects, SSL verification, proxy.
- Main menu (File, Edit, View, Tools, Help) and a three-panel window: Collections and History, the request, and the response on the right, with splitters. Panel widths are remembered. The View menu hides the panels and the bar with the secrets and the environment, and the choice is remembered. The Tools menu locks or unlocks secrets and opens the environments.
- Environments window: names are edited in the list, the current environment is marked, and Create, Rename and Delete are small buttons and context menu items. Dialogs have no taskbar button of their own.
- A new request opens on its Body page with the body focused. Save All (Ctrl+Shift+S). Ctrl+Tab switches request tabs in the order they were last used.
- Setting: open a request with a single click in the Collections tree (off by default).
- Keyboard use throughout: tree shortcuts, dialog defaults, Esc to cancel, named controls for screen readers, light and dark themes.

### Not signed

The executable is not code-signed in this release. See the README for the SmartScreen and Smart App Control notes, and verify the download against `SHA256SUMS.txt`.

### Known limitations

- Query parameters in the Params tab and a query string typed into the URL are both sent, and are not kept in sync.
- A forgotten master password cannot be recovered.
- A proxy with credentials is not supported. Turning SSL verification off disables all certificate checks.
- A multipart file is read fully into memory before sending.
- Pasting a single line of about 2 MB into the body editor takes several seconds. Format the body first.
- Collections cannot be moved or duplicated as a whole. Folders cannot be duplicated.
- Each change to the collection tree saves the whole tree. This is fine for normal sizes.
- Drag and drop in the tree needs a mouse.
- Startup problems (for example a data folder that cannot be created) are shown in a message box. Other platforms print them to the terminal only.
