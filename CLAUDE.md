# RestDraft

A desktop REST API client (Avalonia, .NET 10, SQLite). Windows and Linux x64, released as single-file self-contained packages. All user data lives in a `data` folder next to the executable (`--data-dir` or `RESTDRAFT_DATA_DIR` moves it). The product was called RestClient until 2026-10-28.

Your global rules in `~/.claude` (language, code quality, tests, commits, reviews) apply. This file only adds what is specific to this repository.

## Layout

```
src/RestDraft.Domain          no project references
src/RestDraft.Application     -> Domain (services, interfaces)
src/RestDraft.Infrastructure  -> Application, Domain (HTTP executor, SQLite, cipher, migrations in Persistence/MigrationScripts)
src/RestDraft.UI              Avalonia executable and composition root; the only project that references Avalonia
tests/*.Tests                 NUnit, FakeItEasy, Assert.That; Integration.Tests uses an in-process Kestrel server; SampleApi is a manual test server
scripts/                      build.ps1 (+ LicenseCheck, Checksums, CoverageReport)
docs/                         design, plan, status, checklists (see "Where to look")
```

Conventions (details in `docs/implementation-plan.md` section 1): one type per file, records for data, errors as `Result` values, async with `CancellationToken`, no reflection in tests. Warnings are errors (`TreatWarningsAsErrors`). Commit messages use Conventional Commits: `type(scope): summary`.

## Commands

```powershell
.\scripts\build.ps1                  # restore, license check, build, test, win-x64 publish, zip, checksums, smoke test
.\scripts\build.ps1 -SkipSmokeTest
dotnet test RestDraft.slnx -c Release
```

- `build.ps1` overwrites the untracked `dist\RestDraft-win-x64.zip`. A local zip never has the hash of a published release asset (to compare with a release, download the asset).
- `linux-x64` is built only on a Linux host (CI does it, as a `.tar.gz` with the executable bit); on Windows `-Runtimes linux-x64` is skipped with a warning.
- Coverage is a warning, not a gate (80% target for Domain and Application, `scripts/CoverageReport.ps1`). Failing tests do fail the build.
- Working-tree files are CRLF (autocrlf); git's "LF will be replaced by CRLF" warning is harmless.

## Git and CI

- `main` is protected by rulesets: changes go through a branch and a pull request with the Windows and Linux checks green and the branch up to date. The repository admin can bypass for small docs-only pushes. Merge with a regular merge commit. Tags `v*` cannot be deleted or moved (disable the ruleset for a moment to fix one).
- CI (`.github/workflows/ci.yml`): a Windows job and a Linux job (`ubuntu-24.04`, pinned on purpose: `ubuntu-latest` is 26.04 and has no `libicu74`). Packages are uploaded from `main` and from `v*` tags. The concurrency group includes the workflow name, so a release run cannot cancel a push run.
- Release (`.github/workflows/release.yml`): on a `v*` tag it builds both packages by calling `ci.yml`, merges the two checksum files, verifies them, takes the notes from the `## <version> ` section of `RELEASE_NOTES.md`, and creates a **draft** release; the owner publishes it. A manual run (`gh workflow run release.yml --ref main`) is a dry run: no release, files in the `release-preview` artifact. GitHub cannot dispatch a new workflow file from a branch, only from `main`. Use `/release <version>` for the whole procedure.
- Do not push tags yourself: the owner pushes `v<version>`.
- The release is unsigned. SignPath Foundation declined (not enough visibility); reapply when the project has stars and mentions. The README's "Code signing policy" explains the status.

## Gotchas

- Linux checks ran on Ubuntu 24.04 under WSL2 (distro `Ubuntu-24.04`). A full Linux desktop is untested. Closing popups lags about a second under WSLg; that is WSLg, not the app.
- A Windows path in a test breaks on Linux (`Path.GetFileName` does not split `\`), and `Directory.EnumerateDirectories` has no guaranteed order. Tests run on both OSes in CI.
- Do not write files outside the data folder; the one exception is the native libraries the single-file app unpacks to `%TEMP%\.net\RestDraft` (Linux: `~/.net/RestDraft`).
- Version lives in `Directory.Build.props`. The About window strips a `+<hash>` suffix.

## Where to look

- `docs/STATUS.md` - current state, releases, open items. Start here to continue work, and keep it short and current.
- `docs/ARCHITECTURE_PLAN.md` - the design and the decisions (living document).
- `docs/implementation-plan.md` - detailed design and the milestone plan. Milestones M0 to M11 are done; sections 1 to 7, 9 and 10 still describe how the code is built.
- `docs/history/implementation-progress-M0-M11.txt` - the frozen log of milestones M0 to M11. Large (75 KB): search it, do not read it whole.
- `docs/portability-checklist.md` - manual checks for a release (Windows, accessibility, Smart App Control, Linux).
- `README.md` and `RELEASE_NOTES.md` - what users see. Keep them in step with the version.

New work: describe it in a pull request (what, why, test plan). Update `docs/STATUS.md` at the end of a piece of work, in a few lines. Do not grow a new long log.
