# Packaging and releasing

No script in this repository uploads or publishes anything.

1. Complete [setup](SETUP.md), including a reference recording you have permission to redistribute and an accurate transcript. Build with scripts/build.ps1.
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
