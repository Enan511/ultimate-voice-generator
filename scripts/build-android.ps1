param([switch]$SkipNative)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location -LiteralPath $root
$env:JAVA_HOME=(Get-ChildItem .tools -Directory -Filter 'jdk-21*' | Select-Object -First 1).FullName
$env:ANDROID_HOME=Join-Path $root '.tools/android-sdk'
$env:GRADLE_USER_HOME=Join-Path $root '.tools/gradle-home'
$env:PATH="$env:JAVA_HOME/bin;"+$env:PATH
$modelAssets=Join-Path $root 'android/app/src/main/assets/models'
New-Item -ItemType Directory -Force $modelAssets | Out-Null
$bundledModels=@(
 @{Path='engines/qwen/models/qwen-talker-1.7b-base-Q8_0.gguf';Hash='4b9a33a236908dd9435a42f7a396e38038329d053b704342a6413c08544c4fda'},
 @{Path='engines/qwen/models/qwen-tokenizer-12hz-Q8_0.gguf';Hash='1883beeed99348fc35e23dd225e9082f93f6f8c109330a33d935baa8acdbfd94'},
 @{Path='engines/whisper/ggml-small.en.bin';Hash='c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d'}
)
foreach($model in $bundledModels){
 $source=Join-Path $root $model.Path
 if(!(Test-Path -LiteralPath $source)){throw "Required offline model is missing: $($model.Path)"}
 if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $model.Hash){throw "Model checksum failed: $($model.Path)"}
 Copy-Item -LiteralPath $source -Destination $modelAssets -Force
}
Write-Output 'Verified and bundled Qwen 1.7B, its codec, and Whisper transcription models.'
if(!$SkipNative){
 $cmake=Join-Path $env:ANDROID_HOME 'cmake/3.22.1/bin/cmake.exe'
 $ninja=Join-Path $env:ANDROID_HOME 'cmake/3.22.1/bin/ninja.exe'
 foreach($engine in @('qwen','whisper','audio')){
  $build="android/native-build/$engine"
  & $cmake -S android/native -B $build -G Ninja "-DCMAKE_MAKE_PROGRAM=$ninja" "-DCMAKE_TOOLCHAIN_FILE=$env:ANDROID_HOME/ndk/28.2.13676358/build/cmake/android.toolchain.cmake" -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-35 -DANDROID_STL=c++_shared -DCMAKE_BUILD_TYPE=Release "-DENGINE=$engine"
  if($LASTEXITCODE -ne 0){throw "$engine configuration failed"}
  & $cmake --build $build --target "uvg_$engine" -j 5
  if($LASTEXITCODE -ne 0){throw "$engine build failed"}
  Get-ChildItem $build -Recurse -Filter *.so | Copy-Item -Destination android/app/src/main/jniLibs/arm64-v8a -Force
 }
 Copy-Item "$env:ANDROID_HOME/ndk/28.2.13676358/toolchains/llvm/prebuilt/windows-x86_64/sysroot/usr/lib/aarch64-linux-android/libc++_shared.so" android/app/src/main/jniLibs/arm64-v8a -Force
}
npm.cmd run build
if($LASTEXITCODE -ne 0){throw 'UI build failed'}
$uiAssets=Join-Path $root 'android/app/src/main/assets/ui'
if(Test-Path -LiteralPath $uiAssets){
 $resolvedAssets=(Resolve-Path -LiteralPath $uiAssets).Path
 if($resolvedAssets -ne [IO.Path]::GetFullPath($uiAssets)){throw 'Unexpected generated UI asset path'}
 Remove-Item -LiteralPath $resolvedAssets -Recurse -Force
}
New-Item -ItemType Directory -Force $uiAssets | Out-Null
Copy-Item dist/* $uiAssets -Recurse -Force
Copy-Item THIRD_PARTY.md android/app/src/main/assets/notices/THIRD_PARTY.md -Force
& ./.tools/gradle-8.13/bin/gradle.bat -p android assembleDebug --no-daemon
if($LASTEXITCODE -ne 0){throw 'Android build failed'}
New-Item -ItemType Directory -Force release | Out-Null
Copy-Item android/app/build/outputs/apk/debug/app-debug.apk 'release/Ultimate Voice Generator Android.apk' -Force
Write-Output 'APK built in release/Ultimate Voice Generator Android.apk'
