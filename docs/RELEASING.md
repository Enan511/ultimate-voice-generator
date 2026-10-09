# Packaging and releasing

Local build scripts do not publish. The manually dispatched `assemble-release.yml` workflow uploads and publishes an existing prepared draft release after validating its assets; do not dispatch it until the prepared inputs have been reviewed.

1. Complete [setup](SETUP.md). Build with scripts/build.ps1; default release builds exclude personal reference audio. Users choose their own reference in Settings.
2. Install [Inno Setup](https://jrsoftware.org/isdl.php) compatible with this script (the prototype was built with Inno Setup 7).
3. Download Microsoft's [Evergreen WebView2 bootstrapper](https://developer.microsoft.com/microsoft-edge/webview2/#download-section) into `installer/tools/MicrosoftEdgeWebview2Setup.exe`. Check its valid Microsoft digital signature. It is an installer prerequisite, not source.
4. Compile using the installed compiler, adjusting its path if needed:

```powershell
& 'C:/Program Files (x86)/Inno Setup 7/ISCC.exe' './installer/UltimateVoiceGenerator.iss'
```

5. Find setup and its BIN slices in installer/output. Keep every slice beside the EXE. Supply installer/QUICK-START.txt and SHA-256 checksums alongside the files.
6. Test on a separate writable directory or clean Windows account: launch, reference import, real synthesis and ASR, playback, regeneration, output deletion, speed update, shortcuts and uninstallation with unrelated files present.

## Distribution checks

- Exclude user-data, recordings, private references, logs, test reports and machine-specific paths. Review the payload before compiling.
- Retain LICENSE, THIRD_PARTY.md and component notices. MIT covers original application code only.
- Include applicable source/notices for redistributed dependencies. In particular, FFmpeg supplied here is GPLv3: provide corresponding source/build material for the exact binary as required by that license. A generic upstream link is not a substitute for satisfying its distribution terms.
- Confirm reference redistribution rights separately from model licenses. No NiceVoice reference is included in this source repository.
- GPU drivers and shared WebView2 are system dependencies. Do not distribute driver installations as part of the app.
- Sign executables if you have a signing certificate. The prototype is not represented as signed.

## GitHub repository versus release assets

Upload the source repository folder only. Model weights, compiled engines, installers and generated recordings are excluded by .gitignore. GitHub's automatic source ZIP will therefore require the documented setup process.

If publishing installers later, attach setup and each BIN separately to a release. Each GitHub release asset must be under 2 GiB; the existing combined multi-gigabyte repack ZIP exceeds that limit. See [GitHub release limits](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases#storage-and-bandwidth-quotas). Do not add the large repack ZIP to Git history.

For Android, build using [the Android guide](ANDROID.md), then run `node scripts/package-android-release.mjs`. This produces two APK byte parts, an integrity manifest and a joining script in `release/github-v3.2.1`. Test the join script in a separate output folder before publishing. Upload both parts, `Android-parts.json`, `Join-Android-APK.ps1`, native dependency source/notices, installation instructions and checksums as release assets. Do not publish the parts without the manifest and instructions. Retain the original signed APK locally.

Create a draft release for the exact source commit, attach and verify all assets, then publish it. Android remains a preview until actual phone generation has been tested; distinguish build/UI checks from phone performance claims.

## Optional cloud assembly for slow upload connections

The prepared-release workflow downloads the same public model weights directly on a GitHub runner. It uses a clean Windows native-engine seed and a small template of the original Android APK, both attached to a draft release, with SHA-256 values in `Cloud-inputs.json`. `release-apk-template.mjs` removes only the stored model byte ranges from the APK; restoring the exact model bytes must reproduce the original whole-file SHA-256, including its existing signature. No signing key is uploaded. The workflow compiles the Windows .NET host and React UI, retrieves the checksum-pinned FFmpeg dependency, builds the installer, splits the restored APK, verifies GitHub's checksum for every final asset, deletes temporary assembly assets and publishes the preview. Source remains in Git; executable payloads only travel through draft release assets. The native engines are previously tested binaries, so this is not a complete native source compilation pipeline.
