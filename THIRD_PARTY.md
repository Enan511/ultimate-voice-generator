# Third-party components

This app includes or uses:

- Microsoft .NET 8 runtime — MIT; https://github.com/dotnet/runtime
- Microsoft WebView2 SDK; runtime supplied by Microsoft Edge — Microsoft license; https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.2903.40
- React — MIT; https://github.com/facebook/react
- Tailwind CSS — MIT; https://github.com/tailwindlabs/tailwindcss
- Lucide icons — ISC; https://github.com/lucide-icons/lucide
- Qwen3-TTS 1.7B Base and audio tokenizer — Apache 2.0; https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-Base and https://github.com/QwenLM/Qwen3-TTS
- GGUF conversion — https://huggingface.co/Serveurperso/Qwen3-TTS-GGUF ; pinned download checksums in scripts/download-qwen.mjs; setup generates engines/manifest.json
- qwentts.cpp — MIT, commit 6fae92914045cd83364d2845ceaa0f7969727319; https://github.com/ServeurpersoCom/qwentts.cpp
- GGML — MIT, pinned submodule 40e16e4a814f7fe851a0c486fb9e8c722e957830; https://github.com/ServeurpersoCom/ggml
- whisper.cpp b5130 — MIT; https://github.com/ggml-org/whisper.cpp
- Whisper small.en — MIT; https://github.com/openai/whisper and https://huggingface.co/ggerganov/whisper.cpp
- MinGW-w64 / GCC runtime DLLs — license notices and GCC runtime exception are included in licenses/mingw-runtime.
- FFmpeg Windows binary supplied by `ffmpeg-static` — consult the bundled binary's `-L` and build configuration. The retained build report identifies FFmpeg 6.1.1 essentials from https://www.gyan.dev/ffmpeg/builds/ . The downloader/wrapper is https://github.com/eugeneware/ffmpeg-static (GPLv3); FFmpeg source is https://ffmpeg.org/ . Redistributors must supply corresponding source/build material for their exact GPL binary
- Historical prototype reference: NiceVoice public Jarvis demo. This audio and its transcript are excluded from this source repository; setup does not download them. NiceVoice is not affiliated with this project.

Dependency notices are preserved under `licenses/`. Review source licenses before redistributing this app or its model/reference assets. The local application itself does not depend on a paid generation API.

Supply a recording you have permission to use and redistribute. Model licensing does not grant rights to a person's voice or a separate reference recording.

The installer uses Inno Setup (https://jrsoftware.org/). App-local Microsoft Visual C++ CRT/OpenMP redistributable files support Whisper. Microsoft WebView2 Runtime is shared and is never removed by this app's uninstaller.


Build/test tooling also includes Vite, Vitest, Playwright, TypeScript and Prettier (upstream licenses apply). Exact JavaScript dependency versions and license metadata are recorded in package-lock.json. The app icon was supplied by the project owner. Original application code is MIT; see LICENSE.

