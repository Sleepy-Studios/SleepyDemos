param()
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$dependencyLock = Get-Content (Join-Path $projectRoot 'Native/Streamline/dependencies.lock.json') -Raw | ConvertFrom-Json
$cacheRoot = Join-Path $projectRoot 'Library/Streamline'
$archivePath = Join-Path $cacheRoot "streamline-$($dependencyLock.streamline.version).zip"
$sdkPath = Join-Path $cacheRoot "sdk-$($dependencyLock.streamline.version)"
New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $archivePath)) {
    & curl.exe --fail --location --retry 3 --output $archivePath $dependencyLock.streamline.url
    if ($LASTEXITCODE -ne 0) { throw 'Streamline SDK download failed.' }
}
$actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $dependencyLock.streamline.sha256) {
    throw "SDK SHA-256 mismatch. Inspect and remove the invalid archive before retrying: $archivePath"
}
if (-not (Test-Path -LiteralPath (Join-Path $sdkPath '.verified'))) {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $sdkPath -Force
    Set-Content -LiteralPath (Join-Path $sdkPath '.verified') -Value $actualHash -Encoding ascii
}
Write-Output "Verified Streamline SDK: $sdkPath"
$headersArchive = Join-Path $cacheRoot "vulkan-headers-$($dependencyLock.vulkanHeaders.version).zip"
if (-not (Test-Path -LiteralPath $headersArchive)) {
    & curl.exe --fail --location --retry 3 --output $headersArchive $dependencyLock.vulkanHeaders.url
    if ($LASTEXITCODE -ne 0) { throw 'Vulkan headers download failed.' }
}
if ((Get-FileHash -LiteralPath $headersArchive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $dependencyLock.vulkanHeaders.sha256) {
    throw "Vulkan headers SHA-256 mismatch: $headersArchive"
}
Expand-Archive -LiteralPath $headersArchive -DestinationPath $cacheRoot -Force
Write-Output "Verified Vulkan headers: $($dependencyLock.vulkanHeaders.version)"
