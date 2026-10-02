# Contributing

Follow [setup](docs/SETUP.md). Keep changes focused and describe the behavior before/after, checks run and remaining limitations. Run the frontend and core service checks listed there; run browser or real-engine tests when changing those paths.

Never commit recordings, reference voices/transcripts, output history, credentials, local paths or downloaded model/runtime binaries. Use synthetic fixtures for tests. Preserve punctuation and the review-before-generation flow. Keep inference and file work off the UI thread. File deletion and uninstall changes must preserve unrelated user files.

Contributions to original application code are made under the repository's MIT license. Preserve upstream notices and disclose newly introduced dependencies. This project does not promise a review or release schedule.
