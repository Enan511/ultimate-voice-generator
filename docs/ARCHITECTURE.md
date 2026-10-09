# Architecture

| Folder | Purpose |
| --- | --- |
| desktop | .NET 8 WinForms/WebView2 host, bridge, worker queue, persistence, audio/file handling |
| android/app | Android host, background service, file picker, audio decoding, model setup and unit tests |
| android/native | ARM64 JNI adapters for Qwen, Whisper, FLAC, SoundTouch and LAME |
| src | React UI, staging, queue, history, settings and text processing |
| public, assets | Browser and native application icons |
| scripts | Dependency download, native compilation, application build |
| installer | Inno Setup definition and installed quick-start guide |
| tests | Frontend, core service and browser integration checks |
| licenses | Third-party notices retained for redistribution |
| docs | Setup, operation and packaging guides |

Generated or locally supplied folders (excluded from source control): engines, references audio/transcript, vendor, node_modules, .nuget, dist, installer/payload, installer/output, installer/tools, user-data, recordings and test-artifacts.

The React frontend sends commands through the WebView2 bridge. StudioService manages the queue and reports state back to the UI; native generation and file work run outside the UI thread. NativeEngine invokes qwentts.cpp, FFmpeg and Whisper. SpeechValidation compares recognized words against the synthesis text and controls retry/review outcomes. StudioLibrary manages history and safe output operations. RuntimeFiles tracks application-created files for conservative uninstall cleanup.

On Android, the same frontend communicates with MainActivity's local bridge. VoiceService owns an asynchronous queue, local history, model preparation and a foreground notification. JNI inference is serialized outside the UI thread. Audio stays in app-owned storage, with optional user-selected folder exports. Qwen and Whisper run on the phone CPU; Android does not contact a desktop server. Model preparation streams bundled weights to disk and verifies their hashes before use.

Qwen3-TTS Base uses a reference recording and transcript for conditioning. This is inference, not fine-tuning. Whisper performs independent local recognition; the expected prompt is not supplied as a recognition hint. A passing comparison establishes only a word-level match, not voice or emotional fidelity.

Keep binaries and model weights out of the source repository. The installer packages an explicit runtime payload; generated outputs and local history must never enter it. Internal project/namespace names such as JarvisStudio are retained for compatibility; the user-facing product is Ultimate Voice Generator.
