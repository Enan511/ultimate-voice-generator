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


## Android 3.2 prototype

The Android app runs Qwen and Whisper on the phone through JNI; it has no PC/server dependency. The Android 3.2.1 offline APK includes the pretrained models from the documented Hugging Face repositories. Their SHA-256 hashes are checked during packaging and first-use extraction. Inference and reference transcription run offline; no training is required. It does not include a third-party reference voice.

Additional native components:

- FLAC 1.4.3, Xiph.Org Foundation and contributors — BSD-style notice in licenses/flac.txt; https://github.com/xiph/flac (commit 28e4f0528c76b296c561e922ba67d43751990599).
- SoundTouch 2.4.1, Olli Parviainen and contributors — LGPL 2.1, licenses/soundtouch.txt; https://codeberg.org/soundtouch/soundtouch (commit f738b1132ec1fd56efc90367898244cf52d9e6a5).
- LAME, LAME developers — LGPL 2 or later, licenses/lame.txt; https://github.com/lameproject/lame (commit 1f5cc9487284d5950343aa5d4f70de433468070a).
- whisper.cpp v1.7.6 (commit a8d002cfd879315632a579e73f0148d06959de36), GGML community — MIT; licenses/whisper-android.txt. Qwen retains the existing pinned native source and models.
- AndroidX DocumentFile 1.0.1 — Android Open Source Project, Apache 2.0; https://android.googlesource.com/platform/frameworks/support/ .
- Android NDK libc++ — LLVM project, Apache 2.0 with LLVM exceptions; https://github.com/llvm/llvm-project .

SoundTouch and LAME are separate shared libraries. Corresponding source snapshots and rebuild instructions accompany the local Android build in Android-native-sources.zip. Replacing these libraries and rebuilding/re-signing is allowed; the application does not restrict reverse engineering for debugging modifications to them. Their licenses are not replaced by the app's MIT license.
