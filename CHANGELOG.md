# Changelog

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

These entries describe local development milestones, not published GitHub releases.
