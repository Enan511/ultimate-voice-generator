param([ValidateSet('vulkan','cpu','cuda')][string]$Backend='vulkan')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
$cmake=Join-Path (Get-Location) '.tools/cmake-4.4.3-windows-x86_64/bin/cmake.exe'
if (!(Test-Path -LiteralPath $cmake)) { $cmake=(Get-Command cmake -ErrorAction Stop).Source }
$build="vendor/qwentts/build-$Backend"
$configure=@('-S','vendor/qwentts','-B',$build,'-DCMAKE_BUILD_TYPE=Release','-DGGML_NATIVE=OFF','-DGGML_AVX2=ON','-DGGML_FMA=ON','-DGGML_F16C=ON','-DGGML_OPENMP=OFF','-DBUILD_SHARED_LIBS=OFF')
if ($Backend -eq 'cuda') {
    # Run from the x64 Visual Studio developer shell with the CUDA toolkit installed.
    $configure+=@('-DGGML_CUDA=ON','-DCMAKE_CUDA_ARCHITECTURES=86')
} else {
    $configure+=@('-G','MinGW Makefiles')
    if ($Backend -eq 'vulkan') {
        $configure+=@('-DGGML_VULKAN=ON')
        if(Test-Path '.tools/Vulkan-Headers/include') {
            $configure+=@("-DVulkan_INCLUDE_DIR=$((Resolve-Path '.tools/Vulkan-Headers/include').Path)","-DVulkan_LIBRARY=$env:WINDIR/System32/vulkan-1.dll","-DVulkan_GLSLC_EXECUTABLE=$((Resolve-Path '.tools/vulkan/Bin/glslc.exe').Path)","-DCMAKE_PREFIX_PATH=$((Resolve-Path '.tools/spirv').Path)")
        }
        # Otherwise CMake discovers the installed Vulkan SDK via VULKAN_SDK.
    }
}
& $cmake @configure
if($LASTEXITCODE -ne 0){throw 'Qwen configuration failed.'}
& $cmake --build $build --config Release --target tts-server -j 5
if($LASTEXITCODE -ne 0){throw 'Qwen build failed.'}
$exe=Join-Path $build 'tts-server.exe';if(!(Test-Path $exe)){$exe=Join-Path $build 'Release/tts-server.exe'}
New-Item -ItemType Directory -Force engines/qwen | Out-Null
$name=if($Backend -eq 'cpu'){'tts-server-cpu.exe'}else{'tts-server.exe'}
Copy-Item -LiteralPath $exe -Destination "engines/qwen/$name" -Force
if($Backend -ne 'cuda') {
    $bin=Split-Path -Parent (Get-Command gcc -ErrorAction Stop).Source
    foreach($dll in 'libgcc_s_seh-1.dll','libstdc++-6.dll','libwinpthread-1.dll'){Copy-Item -LiteralPath (Join-Path $bin $dll) -Destination engines/qwen -Force}
}
Write-Output "Built $Backend engine. CUDA builds also require their matching CUDA runtime DLLs beside the executable."
