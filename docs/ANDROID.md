# Android setup and build

## Install the release

Download both `Ultimate-Voice-Generator-Android.apk.001` and `.002`, `Android-parts.json`, and `Join-Android-APK.ps1` from the same [release](https://github.com/Enan511/ultimate-voice-generator/releases). Keep them in one folder and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./Join-Android-APK.ps1
```

The script checks each part and the resulting APK before publishing the final file. Transfer `Ultimate Voice Generator Android.apk` to your phone and open it to install. Android may ask you to allow installation from the file manager. The parts are consecutive bytes of the original signed APK, not separate installable packages. On Linux/macOS, concatenate them in numeric order and compare the resulting SHA-256 to `Android-parts.json`.

Android 16+ and ARM64 are required. This preview is intended for a Nothing Phone (3a) with 12 GB physical RAM. No phone benchmark or real-generation validation has been performed. Models are included; first launch copies and verifies them, without internet or training. Keep about 10 GB free for the APK, installed package and prepared models. The APK can be deleted after installation to recover its space.

Choose reference audio in Settings. Automatic local transcription fills the editable transcript. Correct any words and save. Stage one prompt per line, review the text, and confirm generation. CPU inference may be slow; start with a short prompt. The installed application never depends on a PC. The APK uses a development signing key; rebuilding with a different key requires uninstalling the previous installation, so export recordings first.

## Build from source on Windows

Use Node.js 22.12+, Git, JDK 21, Gradle 8.13, and an Android SDK with platform 36, build-tools 36.0.0, NDK 28.2.13676358 and CMake 3.22.1. Install the Android SDK components with Android Studio's SDK Manager, accepting their licenses.

The current script expects this ignored local tool layout:

```text
.tools/
  jdk-21.../bin/java.exe
  gradle-8.13/bin/gradle.bat
  android-sdk/
    platforms/android-36/
    build-tools/36.0.0/
    ndk/28.2.13676358/
    cmake/3.22.1/
```

Download JDK 21 from [Adoptium](https://adoptium.net/temurin/releases/?version=21), Gradle from [Gradle distributions](https://services.gradle.org/distributions/), and the SDK from [Android Studio](https://developer.android.com/studio). Extract tools into the matching folders above. Native inference uses the API 35 baseline supported by this NDK; the application minimum remains API 36.

```powershell
npm ci
node scripts/download-qwen.mjs
```

This downloads checksum-verified models into ignored `engines/` folders (plus the shared Windows Whisper dependency, unused by Android). Obtain the pinned native sources by extracting **only the `vendor/` directory** from the release's `Android-native-sources.zip` into the repository root; keep the current repository's application and build scripts. Alternatively, clone the dependencies as follows:

```powershell
git clone https://github.com/ServeurpersoCom/qwentts.cpp.git vendor/qwentts
git -C vendor/qwentts checkout 6fae92914045cd83364d2845ceaa0f7969727319
git -C vendor/qwentts submodule update --init --recursive
git clone https://github.com/ggml-org/whisper.cpp.git vendor/whisper
git -C vendor/whisper checkout a8d002cfd879315632a579e73f0148d06959de36
git clone https://github.com/xiph/flac.git vendor/flac
git -C vendor/flac checkout 28e4f0528c76b296c561e922ba67d43751990599
git clone https://codeberg.org/soundtouch/soundtouch.git vendor/soundtouch
git -C vendor/soundtouch checkout f738b1132ec1fd56efc90367898244cf52d9e6a5
git clone https://github.com/lameproject/lame.git vendor/lame
git -C vendor/lame checkout 1f5cc9487284d5950343aa5d4f70de433468070a
./scripts/build-android.ps1
```

The APK is written to `release/Ultimate Voice Generator Android.apk`. Model hashes are checked before packaging. No personal references or recordings are included. Subsequent UI/Java-only builds may use `-SkipNative`. See [native rebuild details](ANDROID-NATIVE-BUILD.md) and [third-party notices](../THIRD_PARTY.md).

## Verify

```powershell
$env:JAVA_HOME=(Get-ChildItem .tools -Directory -Filter 'jdk-21*' | Select-Object -First 1).FullName
$env:ANDROID_HOME=Join-Path $PWD '.tools/android-sdk'
$env:GRADLE_USER_HOME=Join-Path $PWD '.tools/gradle-home'
$env:PATH="$env:JAVA_HOME/bin;"+$env:PATH
./.tools/gradle-8.13/bin/gradle.bat -p android testDebugUnitTest lintDebug --no-daemon
npm test
npm run build
$env:UVG_MOBILE_TEST='1'
npm run test:ui
```

Browser tests require Edge and the project's FFmpeg test helper. They use mocked host responses and do not establish on-phone inference quality or speed. Record real phone testing separately.
