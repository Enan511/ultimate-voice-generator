# Ultimate Voice Generator

A local Windows desktop app for batch voice generation using Qwen3-TTS 1.7B Base. Built with C#/.NET 8, WebView2, React and native C++ inference engines. No Python or paid speech API is required.

![Application icon](assets/ultimate_voice_generator.ico)

## Features

- One prompt per line; natural punctuation is preserved.
- Review, edit and delete staged prompts before starting a background queue.
- Reference-audio voice cloning, pronunciation rules and local Whisper verification with retries.
- FLAC, WAV and MP3 exports to a chosen folder.
- History, editable regeneration, per-output deletion and inline playback.
- Playback starts at 1.0x; permanent speed changes preserve pitch and are checked again.
- Native installer with optional shortcuts and uninstaller.

## Get started

**Using a packaged app:** keep the setup EXE and all matching BIN files together, run setup, choose a writable installation folder and launch Ultimate Voice Generator. See [usage](docs/USAGE.md).

**Building from source:** follow [setup and build instructions](docs/SETUP.md). This repository contains source, not a ready-to-run installer. Models, runtime binaries, recordings and personal settings are deliberately excluded. Initial setup downloads several gigabytes; inference then runs locally. WebView2 may need an initial online installation.

Windows x64 and an AVX2-capable CPU are required. Vulkan is the default GPU backend, with a CPU fallback. The prototype was exercised on a Ryzen 5600X / RX 6700 XT; RTX 3070 Ti performance has not been verified. CUDA is an experimental build route. Generation is computationally expensive even though it runs outside the UI thread.

## Documentation

- [Setup, development and tests](docs/SETUP.md)
- [Using the app](docs/USAGE.md)
- [Architecture and folder structure](docs/ARCHITECTURE.md)
- [Packaging and release checklist](docs/RELEASING.md)
- [Validation and limitations](PERFORMANCE.md)
- [Credits and third-party licenses](THIRD_PARTY.md)
- [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md) · [Changelog](CHANGELOG.md)

Voice cloning is reference conditioning, not training a new model on your PC. ASR checks words, not identity, emotion or subjective quality. The Base model follows the reference delivery; comparison labels do not guarantee an emotion. Use recordings you have permission to use. This project is independent of NiceVoice and does not include its voice sample or model.

## License

Original application code is available under the [MIT license](LICENSE). Third-party code, model weights and runtime components retain their own licenses; MIT does not relicense them. Supplied branding assets are included with this project. See [third-party notices](THIRD_PARTY.md) before distributing bundled dependencies.
