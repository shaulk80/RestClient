# RestDraft: status

Updated: 2026-10-31. Keep this file short and current. The old milestone log is in `history/implementation-progress-M0-M11.txt`.

## Now

Version 0.1.1 is released (Windows x64 and Linux x64, unsigned). All planned milestones (M0 to M11) are done and nothing is in progress. New work starts from a pull request; see `CLAUDE.md` for the workflow.

## Releases

| Version | Date | Assets | Notes |
|---|---|---|---|
| v0.1.1 | 2026-10-31 | `RestDraft-win-x64.zip`, `RestDraft-linux-x64.tar.gz`, `SHA256SUMS.txt` | Built by CI from the tag. Linux added. |
| v0.1.0 | 2026-10-30 | `RestDraft-win-x64.zip`, `SHA256SUMS.txt` | Windows only, built on the maintainer's machine. |

v0.1.1 assets: zip `df389225b6bc39757e006cae2ca0f6761f9ce0019e96f7ae1f7f8a3f7d98b88b`, tarball `9d2c2b819fcbe7b0f406cc882c0ff3c961c7ba46ec936bfa7952882064df0cfd`.

## Open

- **Code signing:** SignPath Foundation declined (2026-10, not enough public visibility; a paid subscription was declined by the owner). Reapply when the project has stars, forks, contributors and external mentions. If approved: SignPath GitHub Action in CI, MFA on the SignPath account, version without the `+<hash>` suffix, README policy wording (see the archive, M10).
- **Backlog:** the known limitations in `RELEASE_NOTES.md` (0.1.0 section), and anything new. No issues are filed yet; GitHub Issues is the place for them.

## How things are set up

- **CI:** `ci.yml` has a Windows job and a Linux job (`ubuntu-24.04`); both run `scripts/build.ps1`, upload their package from `main` and from tags. `release.yml` builds both from a `v*` tag and drafts the release (dry run: manual run from `main`).
- **Branch protection:** rulesets `protect-main` (pull request, both checks required, up to date, no force push or deletion; admin bypass) and `protect-release-tags` (`v*`, no deletion or force push).
- **Coverage:** a warning below 80% for Domain and Application; never fails a build.
- **Verified:** the Windows build (portability checklist section 1), accessibility, Smart App Control (section 3), and Linux on Ubuntu 24.04 under WSL2 (section 5, 13 checks passed).
- **Not verified:** a full Linux desktop and other distributions; signing.

## Decisions worth remembering

- Data stays next to the executable; no installer.
- Linux packages are `.tar.gz` built on Linux (a zip made on Windows cannot keep the executable bit).
- Linux limitations and the unsigned warnings are in `RELEASE_NOTES.md` and the README.
