param([ValidateSet('vulkan','cpu','cuda')][string]$Backend='vulkan')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
npm.cmd ci
if($LASTEXITCODE -ne 0){throw 'Dependency setup failed.'}
node scripts/download-qwen.mjs
if($LASTEXITCODE -ne 0){throw 'Model download failed.'}
Expand-Archive -LiteralPath engines/downloads/whisper.zip -DestinationPath engines/downloads/whisper -Force
Copy-Item engines/downloads/whisper/Release/*.dll engines/whisper -Force
Copy-Item engines/downloads/whisper/Release/whisper-cli.exe engines/whisper -Force
Copy-Item node_modules/ffmpeg-static/ffmpeg.exe engines/ffmpeg.exe -Force
if(!(Test-Path vendor/qwentts/CMakeLists.txt)){
    git clone https://github.com/ServeurpersoCom/qwentts.cpp.git vendor/qwentts
    if($LASTEXITCODE -ne 0){throw 'Qwen source download failed.'}
    git -C vendor/qwentts checkout 6fae92914045cd83364d2845ceaa0f7969727319
    git -C vendor/qwentts submodule update --init --recursive
    if($LASTEXITCODE -ne 0){throw 'Qwen source checkout failed.'}
}
& "$PSScriptRoot/build-qwen.ps1" -Backend $Backend
if($Backend -ne 'cpu'){& "$PSScriptRoot/build-qwen.ps1" -Backend cpu}
if($Backend -eq 'cpu'){Copy-Item engines/qwen/tts-server-cpu.exe engines/qwen/tts-server.exe -Force}
New-Item -ItemType Directory -Force references | Out-Null
Write-Output 'Before packaging, supply references/reference.mp3 and its exact transcript in references/reference.txt. See docs/SETUP.md.'
dotnet restore desktop/JarvisStudio.csproj -r win-x64 --configfile NuGet.Config
if($LASTEXITCODE -ne 0){throw 'Desktop dependencies failed.'}
dotnet restore tests/CoreTests.csproj --configfile NuGet.Config
if($LASTEXITCODE -ne 0){throw 'Test dependencies failed.'}
