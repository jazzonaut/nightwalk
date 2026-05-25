# Releasing

Releases are cut manually via GitHub Actions.

## To cut a release

1. Make sure `main` is in a releasable state (builds locally, smoke-tested).
2. Decide a version (SemVer, no prefix — e.g. `0.1.0`, `0.2.0-rc1`).
3. Go to **Actions → Release (Windows) → Run workflow**.
4. Fill the inputs:
   - **version**: e.g. `0.1.0`.
   - **draft**: keep `true` until you've eyeballed the output; flip to `false` to publish straight away.
   - **prerelease**: `true` for `-rc`, `-beta`, `-alpha` builds.
5. Click **Run workflow**.
6. When the job finishes, open **Releases**. The new release will exist (as draft if you left draft=true).
7. Download the attached zip and smoke-test it. If good, edit the release on GitHub and click **Publish**.

## What the workflow does

- Validates the version string format.
- Sets up Godot 4.6.3 (.NET edition) + .NET 10 SDK on a `windows-latest` runner.
- Injects the version into `export_presets.cfg` (file_version + product_version).
- Runs `godot --headless --import`, then `godot --headless --export-release "Windows Desktop"`.
- Bundles `bin/` output + `LICENSE` + `README.md` into `Nightwalk-<version>-windows.zip`.
- Tags `v<version>` on the workflow's commit and creates a GitHub Release with the zip attached.

## License obligation

This project is GPL-3.0. The release zip includes the `LICENSE` file — keep it included if you ever build outside of CI.

## Troubleshooting

- **`Godot --import` exits non-zero but seems to succeed.** Known quirk on fresh runners. The workflow checks for the `.godot/` cache directory instead of trusting the exit code.
- **`Export template not found`.** The `chickensoft-games/setup-godot` action's `include-templates: true` flag didn't take effect — check the pinned action version against a current release and confirm the Godot version exists.
- **C# build errors during export.** Run `dotnet build Nightwalk.csproj` locally; CI uses .NET 10 SDK. Make sure your local SDK matches.
- **`.NET: Failed to load project assembly` in godot output.** The SDK version, runtime version, and `<TargetFramework>` in the csprojs must all align. The project targets `net10.0`; if you bump anything, bump all three together.
- **Workflow can't push tag / create release.** The `permissions: contents: write` block must be present in the workflow.
