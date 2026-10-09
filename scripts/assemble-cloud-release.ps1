param([Parameter(Mandatory=$true)][string]$Tag)
$ErrorActionPreference='Stop'
if($Tag -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+$'){throw 'Invalid release tag'}
$root=(Get-Location).Path
$cloud=Join-Path $root 'release/cloud'
$assets=Join-Path $root 'release/github-v3.2.1'
New-Item -ItemType Directory -Force $cloud,$assets | Out-Null
gh release download $Tag --pattern Windows-runtime-seed.zip --pattern 'Android-template.*' --pattern Cloud-inputs.json --dir $cloud
if($LASTEXITCODE -ne 0){throw 'Could not download the prepared release inputs'}
$manifest=Get-Content -LiteralPath (Join-Path $cloud 'Cloud-inputs.json') -Raw | ConvertFrom-Json
$expected=@('Windows-runtime-seed.zip','Android-template.bin','Android-template.json')
if($manifest.Count -ne 3){throw 'Incomplete input manifest'}
foreach($entry in $manifest){
 if($entry.name -notin $expected){throw 'Unexpected input name'}
 $file=Join-Path $cloud $entry.name
 if((Get-Item -LiteralPath $file).Length -ne $entry.bytes -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.sha256){throw "Release input checksum mismatch: $($entry.name)"}
}
& 7z x (Join-Path $cloud 'Windows-runtime-seed.zip') '-y' "-o$root"
if($LASTEXITCODE -ne 0){throw 'Runtime extraction failed'}
if(Test-Path installer/payload/user-data){throw 'Runtime seed contains personal data'}
if(Test-Path installer/payload/recordings){throw 'Runtime seed contains recordings'}
if(@(Get-ChildItem installer/payload/references -File -ErrorAction SilentlyContinue).Count){throw 'Runtime seed contains reference audio'}
Copy-Item -LiteralPath installer/QUICK-START.txt,THIRD_PARTY.md,LICENSE -Destination installer/payload -Force

$cache=Join-Path $root '.model-cache'
New-Item -ItemType Directory -Force $cache | Out-Null
$models=@(
 @{Name='qwen-talker-1.7b-base-Q8_0.gguf';Hash='4b9a33a236908dd9435a42f7a396e38038329d053b704342a6413c08544c4fda';Folder='qwen/models'},
 @{Name='qwen-tokenizer-12hz-Q8_0.gguf';Hash='1883beeed99348fc35e23dd225e9082f93f6f8c109330a33d935baa8acdbfd94';Folder='qwen/models'},
 @{Name='ggml-small.en.bin';Hash='c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d';Folder='whisper'}
)
foreach($model in $models){
 $file=Join-Path $cache $model.Name
 $url=if($model.Folder -eq 'whisper'){'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/'+$model.Name}else{'https://huggingface.co/Serveurperso/Qwen3-TTS-GGUF/resolve/main/'+$model.Name}
 & curl.exe --fail --location --retry 5 --retry-all-errors --output $file $url
 if($LASTEXITCODE -ne 0){throw "Model download failed: $($model.Name)"}
 if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $model.Hash){throw 'Model checksum mismatch'}
 $destination=Join-Path $root ('installer/payload/engines/'+$model.Folder)
 New-Item -ItemType Directory -Path $destination -Force | Out-Null
 New-Item -ItemType HardLink -Path (Join-Path $destination $model.Name) -Target $file | Out-Null
}

node scripts/release-apk-template.mjs restore $cache
if($LASTEXITCODE -ne 0){throw 'APK restoration failed'}
node scripts/package-android-release.mjs
if($LASTEXITCODE -ne 0){throw 'APK splitting failed'}
# The two release parts now contain the byte-verified APK; recover runner disk space.
Remove-Item -LiteralPath (Join-Path $root 'release/Ultimate Voice Generator Android.apk') -Force

$compilerSetup=Join-Path $cloud 'inno-setup.exe'
Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe' -OutFile $compilerSetup
$signature=Get-AuthenticodeSignature $compilerSetup
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Pyrsys'){throw 'Unexpected compiler signature'}
$compiler=Join-Path $cloud 'inno'
$p=Start-Process -FilePath $compilerSetup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS',('/DIR="'+$compiler+'"')) -WindowStyle Hidden -Wait -PassThru
if($p.ExitCode -ne 0){throw 'Compiler installation failed'}
& (Join-Path $compiler 'ISCC.exe') installer/UltimateVoiceGenerator.iss
if($LASTEXITCODE -ne 0){throw 'Windows installer build failed'}
Get-ChildItem installer/output -File | Move-Item -Destination $assets
Copy-Item -LiteralPath installer/QUICK-START.txt -Destination (Join-Path $assets 'Windows-Quick-Start.txt')

$files=@(Get-ChildItem -LiteralPath $assets -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name)
$checks=@{}
foreach($file in $files){
 if($file.Length -ge 2GB){throw "Release asset exceeds GitHub's limit: $($file.Name)"}
 $checks[$file.Name]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
[IO.File]::WriteAllLines((Join-Path $assets 'SHA256SUMS.txt'),@($files | ForEach-Object {$checks[$_.Name]+'  '+$_.Name}),[Text.UTF8Encoding]::new($false))
$checks['SHA256SUMS.txt']=(Get-FileHash -LiteralPath (Join-Path $assets 'SHA256SUMS.txt') -Algorithm SHA256).Hash.ToLowerInvariant()
foreach($file in Get-ChildItem -LiteralPath $assets -File){
 gh release upload $Tag $file.FullName --clobber
 if($LASTEXITCODE -ne 0){throw "Release upload failed: $($file.Name)"}
}
$releaseId=gh release view $Tag --json databaseId --jq .databaseId
$release=gh api "repos/$env:GITHUB_REPOSITORY/releases/$releaseId" | ConvertFrom-Json
foreach($file in Get-ChildItem -LiteralPath $assets -File){
 $uploaded=$release.assets | Where-Object name -eq $file.Name
 if(!$uploaded -or $uploaded.state -ne 'uploaded' -or $uploaded.size -ne $file.Length -or $uploaded.digest -ne ('sha256:'+$checks[$file.Name])){throw "Uploaded asset verification failed: $($file.Name)"}
}
foreach($name in @('Windows-runtime-seed.zip','Android-template.bin','Android-template.json','Cloud-inputs.json')){
 gh release delete-asset $Tag $name --yes
 if($LASTEXITCODE -ne 0){throw 'Could not remove temporary assembly input'}
}
if($release.assets.name -contains 'Ultimate.Voice.Generator.Setup.exe'){
 gh release delete-asset $Tag Ultimate.Voice.Generator.Setup.exe --yes
 if($LASTEXITCODE -ne 0){throw 'Could not remove superseded draft installer'}
}
gh release edit $Tag --target $env:GITHUB_SHA --draft=false --prerelease
if($LASTEXITCODE -ne 0){throw 'Release publishing failed'}
"Published [$Tag](https://github.com/$env:GITHUB_REPOSITORY/releases/tag/$Tag) after verifying every asset's size and SHA-256." | Out-File -LiteralPath $env:GITHUB_STEP_SUMMARY -Append
