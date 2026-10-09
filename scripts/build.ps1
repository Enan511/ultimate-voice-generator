param([switch]$SkipRestore,[switch]$IncludeReference,[string]$MsvcRedistPath='C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/VC/Redist/MSVC')
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
Set-Location -LiteralPath $project
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
foreach($required in @('engines/qwen/tts-server.exe','engines/qwen/tts-server-cpu.exe','engines/ffmpeg.exe')){
    if(!(Test-Path -LiteralPath $required)){throw "Missing $required. Follow docs/SETUP.md before building."}
}
npm.cmd run build
if($LASTEXITCODE -ne 0){throw 'Frontend build failed.'}
$payload=[IO.Path]::GetFullPath((Join-Path $project 'installer/payload'))
if($payload -ne (Join-Path $project 'installer\payload') -or !$payload.StartsWith($project+[IO.Path]::DirectorySeparatorChar)){throw 'Unsafe build directory.'}
if(Test-Path -LiteralPath $payload){
    foreach($data in @('user-data','recordings')){if(Test-Path -LiteralPath (Join-Path $payload $data)){throw 'Payload contains local app data. Preserve it outside the build folder before rebuilding.'}}
    if((Get-Item -LiteralPath $payload).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked build directory.'}
    Remove-Item -LiteralPath $payload -Recurse -Force
}
$restore=@();if($SkipRestore){$restore=@('--no-restore')}
dotnet publish desktop/JarvisStudio.csproj -c Release -r win-x64 --self-contained true -o $payload -p:PublishReadyToRun=false @restore
if($LASTEXITCODE -ne 0){throw 'Desktop build failed.'}
# WinForms uses neither WPF nor the WPF WebView2 wrapper. Keep the complete core runtime.
$unused=@('PresentationCore.dll','PresentationFramework.dll','PresentationFramework.Aero.dll','PresentationFramework.Aero2.dll','PresentationFramework.AeroLite.dll','PresentationFramework.Classic.dll','PresentationFramework.Luna.dll','PresentationFramework.Royale.dll','PresentationFramework-SystemCore.dll','PresentationFramework-SystemData.dll','PresentationFramework-SystemDrawing.dll','PresentationFramework-SystemXml.dll','PresentationFramework-SystemXmlLinq.dll','PresentationUI.dll','ReachFramework.dll','System.Printing.dll','System.Windows.Controls.Ribbon.dll','System.Windows.Input.Manipulations.dll','Microsoft.Web.WebView2.Wpf.dll','PenImc_cor3.dll','wpfgfx_cor3.dll','D3DCompiler_47_cor3.dll','vcruntime140_cor3.dll','Ultimate Voice Generator.pdb')
foreach($name in $unused){$path=Join-Path $payload $name;if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}}
New-Item -ItemType Directory -Force "$payload/ui","$payload/engines/qwen/models","$payload/engines/whisper","$payload/references","$payload/licenses" | Out-Null
Copy-Item dist/* "$payload/ui" -Recurse -Force
Copy-Item assets/ultimate_voice_generator.ico "$payload/app.ico"
Copy-Item engines/qwen/*.exe,engines/qwen/*.dll "$payload/engines/qwen"
Copy-Item engines/qwen/models/*.gguf "$payload/engines/qwen/models"
$whisper=@('whisper-cli.exe','whisper.dll','ggml.dll','ggml-base.dll','ggml-cpu-haswell.dll','ggml-cpu-x64.dll','ggml-small.en.bin')
foreach($name in $whisper){Copy-Item -LiteralPath (Join-Path 'engines/whisper' $name) -Destination "$payload/engines/whisper"}
# App-local Microsoft redistributable CRT/OpenMP: no shared runtime installation.
foreach($name in @('msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll','vcomp140.dll')){
    $file=Get-ChildItem -Path "$MsvcRedistPath/*/x64" -Recurse -Filter $name | Sort-Object FullName -Descending | Select-Object -First 1
    if(!$file){throw "Missing Microsoft redistributable: $name."}
    Copy-Item -LiteralPath $file.FullName -Destination "$payload/engines/whisper"
}
Copy-Item engines/ffmpeg.exe,engines/manifest.json "$payload/engines"
# Explicit allowlist: never copy user references, settings, history, outputs or test artifacts.
if($IncludeReference){Copy-Item references/reference.mp3,references/reference.txt "$payload/references"}
Copy-Item installer/QUICK-START.txt,THIRD_PARTY.md,LICENSE "$payload"
foreach($name in @('ffmpeg-binary.txt','ffmpeg-static.txt','ggml.txt','lucide.txt','qwen-model.txt','qwentts.txt','react.txt','tailwind.txt','dotnet.txt','whisper-cpp.txt','whisper-model.txt','mingw-runtime')){
    Copy-Item -LiteralPath (Join-Path licenses $name) -Destination "$payload/licenses" -Recurse
}
Set-Content -LiteralPath "$payload/product.id" -Value 'UltimateVoiceGenerator-3.1' -Encoding ascii
Write-Output "Clean payload: $payload"
