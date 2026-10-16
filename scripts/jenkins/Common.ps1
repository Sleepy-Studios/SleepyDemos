$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Read-MachineConfig([string]$Path) {
    $config = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $root = [IO.Path]::GetFullPath($config.Root).TrimEnd('\', '/')
    if ($root -match '\s' -or $root -eq [IO.Path]::GetPathRoot($root).TrimEnd('\')) {
        throw 'Jenkins Root 必须是无空格的独立绝对目录。'
    }
    if ($root.Equals([IO.Path]::GetFullPath($config.DailyProjectPath).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Jenkins Root 不能是日常项目目录。'
    }
    $config.Root = $root
    if (-not $config.PSObject.Properties['WorkspaceRoot'] -or
        $config.WorkspaceRoot -notmatch '\A[A-Za-z]:[\\/]' -or $config.WorkspaceRoot -match '\s') {
        throw 'WorkspaceRoot 必须配置为无空格的本机构建目录绝对路径。'
    }
    $workspace = [IO.Path]::GetFullPath($config.WorkspaceRoot).TrimEnd('\', '/')
    $daily = [IO.Path]::GetFullPath($config.DailyProjectPath).TrimEnd('\', '/')
    if ($workspace -eq [IO.Path]::GetPathRoot($workspace).TrimEnd('\') -or
        $workspace.Equals($daily, [StringComparison]::OrdinalIgnoreCase) -or
        $workspace.StartsWith($daily + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $daily.StartsWith($workspace + '\', [StringComparison]::OrdinalIgnoreCase) -or
        (Test-Path -LiteralPath $workspace -PathType Leaf)) {
        throw 'WorkspaceRoot 必须是独立目录，不能包含日常工程或位于日常工程内。'
    }
    Assert-OwnedPath (Join-Path $workspace '.ownership-check') $workspace | Out-Null
    $config.WorkspaceRoot = $workspace
    if (-not $config.PSObject.Properties['JenkinsUrl']) {
        $config | Add-Member -NotePropertyName JenkinsUrl -NotePropertyValue 'http://127.0.0.1:18082'
    }
    $uri = $null
    if ($config.JenkinsUrl -notmatch '\Ahttp://(?:127\.0\.0\.1|localhost|\[::1\])(?::[0-9]{1,5})?/?\z' -or
        -not [Uri]::TryCreate($config.JenkinsUrl, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Port -lt 1 -or $uri.Port -gt 65535 -or -not [string]::IsNullOrEmpty($uri.UserInfo) -or
        -not [string]::IsNullOrEmpty($uri.Query) -or -not [string]::IsNullOrEmpty($uri.Fragment) -or $uri.AbsolutePath -ne '/') {
        throw 'JenkinsUrl 必须是本机 loopback HTTP 根地址，不能包含凭据、查询、片段或路径。'
    }
    $config.JenkinsUrl = $uri.GetLeftPart([UriPartial]::Authority)
    return $config
}

function Assert-OwnedPath([string]$Path, [string]$Root) {
    $resolved = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + '\'
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝操作 Jenkins 根目录外的路径：$resolved"
    }
    $item = $resolved
    while (-not [string]::IsNullOrEmpty($item)) {
        if ((Test-Path -LiteralPath $item) -and ((Get-Item -LiteralPath $item).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Jenkins 路径不能经过链接：$item"
        }
        $item = Split-Path -Parent $item
    }
    return $resolved
}

function Get-ReleaseIdentity([string]$ReleaseTarget, [string]$GameId) {
    switch -CaseSensitive ($ReleaseTarget) {
        'PC' { return Get-BuildIdentity 'Native' 'PC' $GameId }
        'Android' { return Get-BuildIdentity 'Native' 'Android' $GameId }
        'TapTapH5' { return Get-BuildIdentity 'TapTap' 'WebGL' $GameId }
        default { throw 'ReleaseTarget 必须是 PC / Android / TapTapH5。' }
    }
}

function Get-BuildIdentity([string]$BuildChannel, [string]$Platform, [string]$GameId) {
    if ([string]::IsNullOrWhiteSpace($BuildChannel)) { $BuildChannel = 'Native' }
    if ([string]::IsNullOrWhiteSpace($GameId)) { $GameId = 'hub' }
    if ($GameId -notmatch '^[a-z][a-z0-9_]{0,63}$' -or
        ($BuildChannel -eq 'Native' -and $Platform -notin @('PC', 'Android')) -or
        ($BuildChannel -eq 'TapTap' -and ($Platform -ne 'WebGL' -or $GameId -eq 'hub')) -or
        $BuildChannel -notin @('Native', 'TapTap')) {
        throw '非法构建渠道、平台或游戏标识。'
    }
    $workspace = if ($BuildChannel -eq 'TapTap') { 'TapTap/Project' } else { $Platform }
    $stateKey = if ($BuildChannel -eq 'TapTap') { "TapTap/$GameId/$Platform/H5" }
        elseif ($GameId -eq 'hub') { $Platform } else { "$Platform/$GameId" }
    $buildTarget = switch ($Platform) { 'PC' { 'StandaloneWindows64' } 'Android' { 'Android' } 'WebGL' { 'WebGL' } }
    return [pscustomobject]@{
        BuildChannel = $BuildChannel; Platform = $Platform; GameId = $GameId
        TapTapFormat = $(if ($BuildChannel -eq 'TapTap') { 'H5' } else { '' })
        Workspace = $workspace; StateKey = $stateKey; BuildTarget = $buildTarget
    }
}

function Assert-ProjectClosed([string]$Project) {
    $target = [IO.Path]::GetFullPath($Project).TrimEnd('\', '/')
    foreach ($process in Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") {
        if ($process.ExecutablePath -and $process.ExecutablePath -notmatch '(?i)[\\/]Editor[\\/]Unity\.exe$') { continue }
        # 仅在内存中核对项目参数，绝不输出完整命令行。
        if (-not $process.CommandLine) { continue }
        $match = [regex]::Match($process.CommandLine, '(?i)-projectPath\s+(?:"([^"]+)"|([^\s]+))')
        if ($match.Success) {
            $path = if ($match.Groups[1].Success) { $match.Groups[1].Value } else { $match.Groups[2].Value }
            if ([IO.Path]::GetFullPath($path).TrimEnd('\', '/').Equals($target, [StringComparison]::OrdinalIgnoreCase)) {
                throw "构建工作区已被 Unity 打开（PID $($process.ProcessId)）：$target"
            }
        }
    }
    $lock = Join-Path $target 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $lock) {
        $handle = $null
        try { $handle = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None') }
        catch { throw "工作区 UnityLockfile 被占用：$target" }
        finally { if ($handle) { $handle.Dispose() } }
    }
}

function Invoke-Git([string]$Directory, [string[]]$Arguments) {
    $preference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $credentialArguments = if ($env:GH_TOKEN) { @('-c', 'credential.helper=', '-c', 'credential.helper=!gh auth git-credential') } else { @() }
        # WinPS 将原生 stderr 包装为 ErrorRecord；返回前转成文本，避免 Out-Host 向 Jenkins 外层再次发送错误流。
        $result = & $script:machine.Git @credentialArguments -C $Directory @Arguments 2>&1 | ForEach-Object { $_.ToString() }
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $preference }
    if ($code -ne 0) { throw "Git 操作失败（exit $code）：$($result -join [Environment]::NewLine)" }
    return $result
}

function Write-JsonFile([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
}

function Write-AtomicJsonFile([string]$Path, $Value) {
    $staged = $Path + '.writing-' + [guid]::NewGuid().ToString('N')
    try {
        $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes(($Value | ConvertTo-Json -Depth 12))
        $stream = [IO.File]::Open($staged, 'CreateNew', 'Write', 'None')
        try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
        if ([IO.File]::Exists($Path)) { [IO.File]::Replace($staged, $Path, [NullString]::Value) }
        else { [IO.File]::Move($staged, $Path) }
    } finally {
        if ([IO.File]::Exists($staged)) { Remove-Item -LiteralPath $staged }
    }
}

function Register-AndroidVersionCode([string]$Path, [string]$Baseline, [string]$RequestedCode, $Record) {
    $previous = 0
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $registration = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $registration.PSObject.Properties['LastVersionCode'] -or
            -not [int]::TryParse([string]$registration.LastVersionCode, [ref]$previous) -or $previous -le 0) {
            throw 'Android versionCode 登记无效，拒绝重新从低版本开始。'
        }
    } elseif (Test-Path -LiteralPath (Join-Path $Baseline 'baseline.json') -PathType Leaf) {
        throw '旧 Android 在线基线尚未登记 versionCode 下限，请先根据已发布 APK/AAB 的 request.json 初始化登记。'
    }
    $versionCode = 0
    if (-not [string]::IsNullOrWhiteSpace($RequestedCode)) {
        if (-not [int]::TryParse($RequestedCode, [ref]$versionCode) -or $versionCode -le 0) { throw 'AndroidVersionCode 必须是正整数。' }
    } else {
        if ($previous -eq [int]::MaxValue) { throw 'Android versionCode 已达到整数上限。' }
        $versionCode = $previous + 1
    }
    if ($versionCode -le $previous) { throw "AndroidVersionCode 必须大于已登记值 $previous。" }
    $Record.LastVersionCode = $versionCode
    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    # 单执行器串行分配；失败和取消也保留登记，已分发的安装包不会被后续低版本覆盖。
    Write-AtomicJsonFile $Path $Record
    return $versionCode
}
