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
        $result = & $script:machine.Git @credentialArguments -C $Directory @Arguments 2>&1
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
