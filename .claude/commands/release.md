---
description: Prepare and verify a RestDraft release (version, notes, pull request, dry run, tag, draft, downloads)
argument-hint: <version, for example 0.1.2>
---

Release version `$ARGUMENTS` of RestDraft. Read `CLAUDE.md` (Git and CI) first. Work one step at a time, report each result, and stop where a step is marked **owner**. Never push a tag, publish a release, merge, commit or push without the owner's explicit word for that step.

1. **Check the start.** `git status` is clean, `main` is up to date, and `$ARGUMENTS` is higher than the latest tag (`git tag -l 'v*'`). Check that the latest CI run on `main` is green.
2. **Branch** `release-$ARGUMENTS` and change:
   - `Directory.Build.props`: `<Version>$ARGUMENTS</Version>`.
   - `RELEASE_NOTES.md`: a new section at the top, headed `## $ARGUMENTS (<short description>)`. The release workflow takes the notes from the section that starts with `## $ARGUMENTS ` (with the space), so the heading must match. Base it on `git log <last-tag>..HEAD` and the merged pull requests. Keep the "Not signed" and the known-limitations parts correct.
   - `README.md` where it names versions or packages, and `docs/STATUS.md` (releases table).
3. **Check locally:** `.\scripts\build.ps1`, exit code 0.
4. **Commit, push and open the pull request** (owner confirms). Wait for both checks (Windows and Linux). **Owner** says when they passed. Merge with a regular merge commit.
5. **Dry run:** `gh workflow run release.yml --ref main`, watch it, download the `release-preview` artifact, and check that `dist/` holds the zip, the tarball and one `SHA256SUMS.txt` (`sha256sum -c` OK), that the tarball's `RestDraft` is `-rwxr-xr-x`, and that `notes.md` is the new section. No release is created by a dry run.
6. **Owner pushes the tag** from a clean, up to date `main`: `git tag v$ARGUMENTS && git push origin v$ARGUMENTS`. Give those exact commands; do not run them. If the Windows job fails with "No coverage hits" or similar, it is only a flake: coverage is a warning now, so look for a real error, and re-run the failed job (`gh run rerun <id> --failed`).
7. **Check the draft:** `gh release view v$ARGUMENTS` shows a draft with three assets and the notes. **Owner** reviews it on the releases page and presses Publish ("Latest" label stays selected).
8. **Verify the public release:** download the assets into a new empty folder, `sha256sum -c SHA256SUMS.txt`, check the tarball's executable bit, start the Windows zip and the Linux tarball (WSL, distro `Ubuntu-24.04`) and confirm that `data/app.db` and a log appear. Delete the temporary folder.
9. **Record it:** update `docs/STATUS.md` (releases table, hashes, open items) and ask the owner whether to commit and push it.
