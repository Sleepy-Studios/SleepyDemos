param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [switch]$Deploy
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$dependencyLock = Get-Content (Join-Path $projectRoot 'Native/Streamline/dependencies.lock.json') -Raw | ConvertFrom-Json
$unityRoot = Split-Path (Resolve-Path -LiteralPath $UnityEditor).Path
$editorVersion = (Get-Item -LiteralPath $UnityEditor).VersionInfo.ProductVersion
if (-not $editorVersion.StartsWith($dependencyLock.unityVersion + '_')) {
    throw "Expected Unity $($dependencyLock.unityVersion), found $editorVersion."
}
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual Studio C++ x64 toolchain is required.' }
$cmake = Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
if (-not (Test-Path -LiteralPath $cmake)) { $cmake = (Get-Command cmake -ErrorAction Stop).Source }
$sdk = Join-Path $projectRoot "Library/Streamline/sdk-$($dependencyLock.streamline.version)"
if (-not (Test-Path -LiteralPath (Join-Path $sdk '.verified'))) {
    throw 'Run Prepare-Dependencies.ps1 before building the native bridge.'
}
$headers = Join-Path $projectRoot "Library/Streamline/Vulkan-Headers-$($dependencyLock.vulkanHeaders.version)"
$buildRoot = Join-Path $projectRoot 'Library/Streamline/build'
& $cmake -S (Join-Path $projectRoot 'Native/Streamline') -B $buildRoot -G 'Visual Studio 17 2022' -A x64 "-DUNITY_PLUGIN_API=$unityRoot/Data/PluginAPI" "-DSTREAMLINE_SDK=$sdk" "-DVULKAN_HEADERS=$headers"
if ($LASTEXITCODE -ne 0) { throw 'CMake configure failed.' }
& $cmake --build $buildRoot --config RelWithDebInfo
if ($LASTEXITCODE -ne 0) { throw 'Native bridge build failed.' }
if ($Deploy) {
    function Publish-NativeFile([string]$Source, [string]$Destination) {
        if (Test-Path -LiteralPath $Destination) {
            $sourceHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash
            $destinationHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
            if ($sourceHash -eq $destinationHash) { return }
            # 原子替换磁盘文件，已运行的 Editor 仍持有旧映像，必须重启才会使用新版本。
            # 备份仅位于当前项目的 Library，不删除仍被进程占用的旧二进制。
            $backupRoot = Join-Path $projectRoot 'Library/Streamline/deployed-backups'
            New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
            $backup = Join-Path $backupRoot ($destinationHash + '-' + [IO.Path]::GetFileName($Destination))
            $staged = $Destination + '.staging~'
            Copy-Item -LiteralPath $Source -Destination $staged -Force
            [IO.File]::Replace($staged, $Destination, $backup)
        } else {
            Copy-Item -LiteralPath $Source -Destination $Destination
        }
    }
    $destination = Join-Path $projectRoot 'Assets/Plugins/Streamline/x86_64'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Publish-NativeFile (Join-Path $buildRoot 'RelWithDebInfo/GfxPluginSleepyStreamline.dll') (Join-Path $destination 'GfxPluginSleepyStreamline.dll')
    # NVIDIA DLLs are explicitly loaded by absolute path, not imported as Unity plugins.
    $runtime = Join-Path $destination 'Streamline~'
    New-Item -ItemType Directory -Path $runtime -Force | Out-Null
    $files = @('sl.interposer.dll','sl.common.dll','sl.pcl.dll','sl.dlss.dll','sl.reflex.dll','sl.dlss_g.dll','sl.dlss_d.dll','nvngx_dlss.dll','nvngx_dlssg.dll','nvngx_dlssd.dll','NvLowLatencyVk.dll','nvngx_dlss.license.txt','reflex.license.txt')
    foreach ($file in $files) { Publish-NativeFile (Join-Path $sdk "bin/x64/$file") (Join-Path $runtime $file) }
    Copy-Item -LiteralPath (Join-Path $sdk 'license.txt') -Destination (Join-Path $runtime 'streamline.license.txt') -Force
    Write-Output "Deployed bridge and production runtime: $destination"
    Write-Output 'Restart any Editor or Player that already loaded the bridge before validating this build.'
}
