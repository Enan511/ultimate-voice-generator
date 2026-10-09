# Android native source bundle

The Android 3.2.1 prototype targets Android 16 or later on ARM64 phones. Inference uses the phone CPU. Phone performance has not yet been measured. The APK is signed with a development key for manual testing.

`release/Android-native-sources.zip` contains the pinned native source trees, JNI adapters, native build configuration and license notices used in the APK. Dependency revisions are listed in `THIRD_PARTY.md`. No personal recordings, transcripts or model weights are included.

To rebuild the native libraries, install Android NDK 28.2.13676358, CMake 3.22.1 and Ninja. Extract the archive, then configure and build each of `qwen`, `whisper` and `audio` separately from its root:

```powershell
$sdk = 'C:/path/to/Android/Sdk'
foreach ($engine in @('qwen', 'whisper', 'audio')) {
  & "$sdk/cmake/3.22.1/bin/cmake.exe" -S android/native -B "native-build/$engine" -G Ninja "-DCMAKE_MAKE_PROGRAM=$sdk/cmake/3.22.1/bin/ninja.exe" "-DCMAKE_TOOLCHAIN_FILE=$sdk/ndk/28.2.13676358/build/cmake/android.toolchain.cmake" -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-35 -DANDROID_STL=c++_shared -DCMAKE_BUILD_TYPE=Release "-DENGINE=$engine"
  & "$sdk/cmake/3.22.1/bin/cmake.exe" --build "native-build/$engine" --target "uvg_$engine"
}
```

The native API 35 baseline is compatible with the application's API 36 minimum. SoundTouch and LAME remain separate shared libraries. Modified compatible libraries may replace `libSoundTouch.so` and `libmp3lame.so` in `android/app/src/main/jniLibs/arm64-v8a` in the full app source. Run `scripts/build-android.ps1 -SkipNative` with the documented local SDK, JDK 21, Gradle 8.13 and installed npm dependencies to package them. Re-sign the resulting APK with your own Android key. A different signing key requires uninstalling the previous app first; export recordings before doing that. No application restriction prevents debugging changes to these libraries.

The build script expects the three verified model files under the existing `engines/qwen/models` and `engines/whisper` directories. It includes them in the APK; personal reference audio, outputs and transcripts are excluded.

For installation, open `Ultimate Voice Generator Android.apk` on the phone. Allow installation from the file manager when Android requests it. Keep about 10 GB free before installation: the APK, installed application and extracted models each need space. Open the app and let it prepare its included models once. This only copies and verifies pretrained files; it does not train or download anything. Choose reference audio in Settings; transcription runs on the phone and remains editable. Stage a short prompt, review it, and confirm generation. Neither internet nor a PC is required for setup or generation. After installation, the copied APK may be removed to reclaim its space; the installed app retains the models.
