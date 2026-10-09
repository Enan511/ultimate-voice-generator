param([string]$Folder=$PSScriptRoot)
$ErrorActionPreference='Stop'
$folderPath=(Resolve-Path -LiteralPath $Folder).Path
$manifest=Get-Content -LiteralPath (Join-Path $folderPath 'Android-parts.json') -Raw | ConvertFrom-Json
if($manifest.fileName -ne 'Ultimate Voice Generator Android.apk' -or $manifest.parts.Count -ne 2){throw 'Unexpected Android package manifest'}
$destination=Join-Path $folderPath $manifest.fileName
if(Test-Path -LiteralPath $destination){
 if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $manifest.sha256){Write-Output "APK already verified: $destination";exit 0}
 throw 'An APK already exists with different contents. Move it aside before joining these parts.'
}
$temporary=Join-Path $folderPath ('android-join-'+[Guid]::NewGuid().ToString('N')+'.partial')
try{
 $output=[IO.File]::Open($temporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
 try{
  for($i=0;$i -lt 2;$i++){
   $part=$manifest.parts[$i]
   $expected='Ultimate-Voice-Generator-Android.apk.'+($i+1).ToString('000')
   if($part.name -ne $expected){throw 'Unexpected part filename or ordering'}
   $path=Join-Path $folderPath $part.name
   if((Get-Item -LiteralPath $path).Length -ne $part.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $part.sha256){throw "Part failed its integrity check: $($part.name). Download it again."}
   Write-Output "Verified $($part.name). Joining..."
   $inputFile=[IO.File]::OpenRead($path)
   try{$inputFile.CopyTo($output,1048576)}finally{$inputFile.Dispose()}
  }
 }finally{$output.Dispose()}
 if((Get-Item -LiteralPath $temporary).Length -ne $manifest.bytes -or (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $manifest.sha256){throw 'Final APK verification failed'}
 [IO.File]::Move($temporary,$destination)
 Write-Output "Ready: $destination"
 Write-Output 'Copy this APK to your phone, install it, and let first launch prepare the included models. No download or training is needed.'
}finally{if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}}
