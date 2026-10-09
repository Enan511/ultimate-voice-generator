# Setup and development

Run commands in PowerShell from the repository root (the folder containing package.json). Paths in this guide are relative to that folder.

## Prerequisites

Install:

- Windows 10/11 x64; AVX2 CPU and current graphics drivers.
- [Git](https://git-scm.com/downloads/win).
- [Node.js](https://nodejs.org/) 22.12 or later in the 22.x line, including npm.
- [.NET SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 8.0.422 or a later 8.0.4xx patch (global.json pins that feature band).
- [CMake](https://cmake.org/download/) and a MinGW-w64 x64 GCC distribution with gcc and mingw32-make on PATH. Keep its runtime DLLs alongside the compiled engine.
- [Vulkan SDK](https://vulkan.lunarg.com/sdk/home#windows) for the default backend; CMake must find its headers, loader and glslc through VULKAN_SDK.
- [Visual Studio 2022 Build Tools](https://visualstudio.microsoft.com/downloads/) with Desktop development with C++ for the redistributable CRT/OpenMP files used by Whisper.
- [WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) to launch the desktop app.

Allow several GB for model downloads and additional space for dependencies and build output. Initial setup needs internet access. No Python installation is needed.

## Download and compile dependencies

```powershell
./scripts/setup.ps1 -Backend vulkan
```

This installs locked npm dependencies, downloads checksum-verified Qwen/Whisper assets, checks out the pinned native Qwen source, builds Vulkan and CPU engines, and restores .NET dependencies. For CPU-only operation use `-Backend cpu`.

The native source is pinned to commit `6fae92914045cd83364d2845ceaa0f7969727319`. If reusing vendor/qwentts, ensure it is at that commit with its recursive submodules initialized; setup does not overwrite an existing checkout. Download URLs and SHA-256 values are maintained in scripts/download-qwen.mjs. Downloads generate engines/manifest.json.

CUDA is experimental: it requires an x64 Visual Studio developer shell and a matching CUDA toolkit/runtime. The script targets compute capability 8.6. It is not the validated release configuration; prefer Vulkan for the prototype.

## Supply a reference

The default distributable contains no reference voice. Users choose one in Settings and receive an automatic, editable transcript. To opt into bundling a recording you are permitted to redistribute, create:

- `references/reference.mp3`: a clean recording you may use and redistribute.
- `references/reference.txt`: its exact spoken transcript, in UTF-8.

These two files are private/ignored by Git and are only copied when building with `./scripts/build.ps1 -IncludeReference`. Do not put generated prompts or unrelated recordings here. In the installed app, selecting a reference starts automatic transcription. Compare and edit the transcript before saving. No acknowledgement checkbox is required.

## Build and launch

```powershell
./scripts/build.ps1
& './installer/payload/Ultimate Voice Generator.exe'
```

The build produces a self-contained .NET app in installer/payload. If Visual Studio is installed in another edition/location, supply its VC/Redist/MSVC directory:

```powershell
./scripts/build.ps1 -MsvcRedistPath 'C:/Program Files/Microsoft Visual Studio/2022/Community/VC/Redist/MSVC'
```

Do not keep valuable data in installer/payload: it is a build destination. The build refuses to replace a payload containing user-data or recordings. Move that data out before rebuilding, or install the packaged application into a separate writable folder. See [packaging](RELEASING.md) to create setup files.

## Frontend-only development

```powershell
npm ci
npm run dev
```

Vite previews the interface in a browser; real speech generation, native dialogs and file access require the desktop host. `npm run build` compiles the UI into dist.

## Checks

```powershell
npm test
npm run build
dotnet restore tests/CoreTests.csproj --configfile NuGet.Config
dotnet run --project tests/CoreTests.csproj -c Release --no-restore
```

The core tests use controlled engine behavior to exercise queue and validation logic; they do not prove model quality. For browser interaction tests, install Microsoft Edge and make engines/ffmpeg.exe available, then run `npm run test:ui` after the frontend build. Screenshots and synthetic audio go into ignored test-artifacts.

For a release, additionally test real generation, ASR, playback, speed replacement and installation/uninstallation on a clean Windows account. See PERFORMANCE.md for the earlier prototype's measured results; a clean-machine native rebuild is not yet automated.

## Troubleshooting

- Missing compiler or Vulkan: check PATH and VULKAN_SDK, or use the CPU backend.
- Missing model/hash failure: rerun setup; do not disable integrity checks.
- Missing WebView2: install Microsoft's Evergreen Runtime.
- Missing reference at generation time: choose your own recording in Settings and save its transcript.
- DLL load error: keep the entire payload together, including native runtime DLLs; do not copy only the EXE.
- Failed or Needs review audio: inspect the recognized words, pronunciation rules and reference transcript, then retry or edit the prompt.

