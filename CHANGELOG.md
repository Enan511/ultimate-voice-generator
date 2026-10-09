# Changelog

## 3.2.1 — Windows and Android preview

- Added an Android 16+ ARM64 app with a responsive dark interface and entirely on-device Qwen/Whisper processing.
- Bundled pretrained Android models with automatic, verified first-use preparation; no phone training or model download is needed.
- Added automatic reference transcription and editable transcripts on both platforms.
- Added Android queue/history, export, playback, regeneration and pitch-preserving speed updates.
- Published source for both platforms together; packaged software and large models are kept in release downloads.
- Windows release packaging excludes personal reference audio and starts with an empty reference selection.
- Android build/unit/UI/signature checks passed. Actual phone generation performance remains unverified.

## Unreleased — repository preparation

- Added MIT license, source setup, usage, architecture, contribution and release documentation.
- Excluded local models, binaries, recordings, private reference material and machine-specific validation reports.
- Removed the obsolete engine downloader from the active project.
- Setup builds the CPU fallback alongside Vulkan and no longer downloads a third-party voice demo implicitly.
- Build accepts an alternate Microsoft redistributable location and protects payloads containing app data.

## 3.1.0 — local prototype

- Native Qwen3-TTS 1.7B Base inference with local Whisper checks and retries.
- Preserved natural punctuation; reviewable batch queue and pronunciation rules.
- History, per-output deletion, editable regeneration and pitch-preserving file speed updates.
- Custom application/installer icon and optional shortcuts/uninstaller.

Earlier entries describe local development milestones.
