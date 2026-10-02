# Prototype validation — October 2, 2026

Test hardware: Ryzen 5 5600X, RX 6700 XT, Windows x64. This is not an RTX 3070 Ti benchmark.

The speech backend is native Qwen3-TTS 1.7B Base Q8_0 through qwentts.cpp. Whisper small.en runs locally on four CPU threads for independent speech verification. No Python was used by the application or native inference tests.

## Actual audio tests

- CPU: “Good morning, sir.” generated and passed the word check, including “sir,” in 15.2 seconds including startup/reference preparation/export/ASR.
- CPU: “Good morning, sir. Systems awake. Driver status remains under evaluation.” passed the word check in 19.0 seconds.
- Vulkan: the same full sentence passed the word check in 12.2 seconds. Logs identify `Vulkan0` and the RX 6700 XT.
- A 1.25× permanent file update on the full sentence completed and passed a new ASR check on both CPU-generated and Vulkan-generated samples.
- The self-contained Windows EXE generated the full sentence, passed ASR, and played/paused/scrubbed its 4.64-second FLAC through the actual native range handler. That run took 30.9 seconds including model preparation/export/verification.
- During that packaged test, the shell working set was about 80 MiB and summed WebView processes about 492 MiB (shared pages may be counted more than once; excludes the separate ML process and GPU memory). Animation callbacks averaged 7.0 ms with a 120.7 ms maximum, so the test does not demonstrate uninterrupted 60fps.

These are individual cold-process samples, not averaged benchmarks or latency guarantees. ASR matching the words does not establish voice similarity, emotional fidelity, or perfect pronunciation. The default reference transcript was drafted by Whisper; version 3.1 removes the mandatory listening acknowledgement.

## Automated checks

- Seven frontend tests: punctuation/Unicode preservation, line splitting, whole-word lexicon boundaries, longest phrase precedence, non-cascading replacements.
- Native service tests: exact word alignment, dropped “sir,” substitutions, retries/review status, FIFO, cancellation, reference fingerprint changes, batch atomicity, history retention, deletion, rating persistence, safe speed replacement and failure preservation, and scratch cleanup.
- Browser tests at 1280, 760, 420 and 390px: staging/revert/edit/delete, pronunciation preview, actual generated FLAC playback, default speed/reset, update confirmation, two-take regeneration, history deletion/cancel, listening scores, automatic reference transcript, no horizontal overflow, and no React errors.

The machine-specific validation report is retained privately and is excluded from this source repository. Smoke/integration test modes write reports beside the tested EXE; those temporary test folders are not distributed. No reference acknowledgement is required by the app.

## Practical limits

- Qwen Base does not accept direct emotion instructions. Delivery labels are listening goals. A reference with appropriate delivery and listening comparison are still required.
- Numbers, acronyms, spelling variants, and recognizer errors can cause false flags; zero word errors is deliberately strict.
- One line is limited to 1,500 input characters; synthesis has a bounded audio-token budget. Longer scripts should be divided into lines.
- The target 3070 Ti's memory use/speed and CUDA backend are untested here. The delivered build uses Vulkan with CPU compatibility mode.
- The interface remains separate from model work, but universal 60fps, instant startup, or low total system memory use are not claimed.


## Version 3.1 installer validation

The final losslessly compressed repack is 2,732,792,682 bytes, about 2.55 GiB,
versus 3,133,649,767 bytes uncompressed (12.8% smaller). It contains the same
Q8_0 voice models and full Whisper small.en model; model quality was not reduced.

A fresh installation generated the complete test sentence, including "sir,"
and passed the local word check. Its FLAC played, paused and sought through the
native app's audio handler. Generation/export/verification took 27.3 seconds in the final installed-app test; this individual sample is not a benchmark.

Installer tests confirmed the default uninstaller, desktop and Start menu
shortcuts, and that deselecting them creates none of those items or registry entries.
Uninstall removed installed files, app settings/cache, owned recordings and
shortcuts. Three unrelated files and an unrelated empty directory survived.
The provided icon was checked in the EXE, Setup and shortcut configuration.
No user history or generated outputs were present after fresh installation.

Machine-specific validation details are retained privately. Large test installations, temporary
audio and build caches were removed after validation.


Additional cleanup regression: a JSON file and an empty folder added inside WebView's cache while the app was running both survived cleanup. All recognized app cache files were removed. Cache ownership is limited to recognized WebView artifacts; arbitrary new files are not claimed.





