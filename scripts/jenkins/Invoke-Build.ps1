[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Checkout', 'Prepare', 'Build', 'Deploy', 'Package', 'Finalize', 'Cleanup')][string]$Stage,
    [string]$ConfigPath = 'E:\Jenkins\machine.json'
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$machine = Read-MachineConfig $ConfigPath
if (-not $env:JOB_NAME -or $env:BUILD_NUMBER -notmatch '^\d+$' -or -not $env:WORKSPACE) { throw '必须由 Jenkins job 调用。' }
$job = $env:JOB_NAME -replace '[^a-zA-Z0-9-]', '-'
$runId = "$job-$($env:BUILD_NUMBER)"
$runRoot = Assert-OwnedPath (Join-Path $machine.Root "runs/$runId") $machine.Root
$output = Join-Path $runRoot 'output'
$requestPath = Join-Path $runRoot 'request.json'
$summaryPath = Join-Path $output 'summary.json'
$marker = Join-Path $runRoot '.sleepydemos-run'
$failed = $false
$errorText = ''
$request = $null
$unitySettled = $false
$previousPath = $env:PATH
$env:PATH = (@($machine.Git, $machine.Gh, $machine.Dotnet | ForEach-Object { Split-Path -Parent $_ }) -join ';') + ';' + $env:PATH
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'Never'

function Invoke-Script([string]$Path, [string[]]$Arguments, [string]$LogName) {
    $shell = Join-Path $PSHOME 'powershell.exe'
    if (-not (Test-Path -LiteralPath $shell)) { $shell = Join-Path $PSHOME 'pwsh.exe' }
    $log = Join-Path $output "logs/$LogName"
    $process = Start-Process -FilePath $shell -WindowStyle Hidden -PassThru `
        -ArgumentList (@('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $Path + '"')) + $Arguments) `
        -RedirectStandardOutput "$log.out.log" -RedirectStandardError "$log.err.log"
    $null = $process.Handle
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "$LogName 失败（exit $($process.ExitCode)），详见归档日志。" }
}

function Invoke-Unity([string]$Method) {
    Assert-ProjectClosed $project
    $env:SLEEPY_BUILD_REQUEST = $requestPath
    $target = if ($request.Platform -eq 'PC') { 'StandaloneWindows64' } else { 'Android' }
    $log = [IO.Path]::GetFullPath((Join-Path $output "logs/$Method.log"))
    $recordPath = Join-Path $runRoot "unity-$Method.process.json"
    if (Test-Path -LiteralPath $recordPath) { throw "Unity $Method 已有进程记录，拒绝重复启动。" }
    $record = [pscustomobject]@{
        RunId = $runId; Method = $Method; Platform = $request.Platform
        Owner = [Security.Principal.WindowsIdentity]::GetCurrent().Name
        ExecutablePath = [IO.Path]::GetFullPath($machine.Unity); ProjectPath = [IO.Path]::GetFullPath($project)
        LogPath = $log; RequestPath = $requestPath; IntentUtcTicks = [DateTime]::UtcNow.Ticks
        Id = 0; StartedUtcTicks = 0; ExitedUtcTicks = 0; State = 'Launching'; Children = @()
    }
    # 先落盘唯一 run/log 意图；launcher 被 Jenkins 杀死时，post 仍可严格核对未写入 PID 的启动。
    Write-AtomicJsonFile $recordPath $record
    $process = $null
    try {
        $process = Start-Process -FilePath $machine.Unity -WindowStyle Hidden -PassThru `
            -ArgumentList @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $project + '"'), '-buildTarget', $target,
                '-executeMethod', "Core.Editor.Hotfix.HotfixBuildAutomation.$Method", '-logFile', ('"' + $log + '"'))
        $null = $process.Handle
        $details = Get-CimInstance Win32_Process -Filter "ProcessId=$($process.Id)"
        if ($details) {
            $record.Id = [int]$details.ProcessId
            $record.StartedUtcTicks = $details.CreationDate.ToUniversalTime().Ticks
            $record.State = 'Running'
            Assert-UnityProcess $details $record
        } elseif (-not $process.HasExited) { throw '无法记录新 Unity 的完整进程身份。' }
        Write-AtomicJsonFile $recordPath $record
    } catch {
        # 已持有实际启动句柄；记录写入失败时先停本次进程，不能留下无记录的 Unity。
        if ($record.Id -gt 0) {
            Stop-RecordedProcess $record -Unity
        } elseif ($process -and -not $process.HasExited) {
            $process.Kill()
            if (-not $process.WaitForExit(30000)) { throw 'Unity 记录失败且退出未确认；保留本次启动意图供 Finalize 收尾。' }
        }
        throw
    }
    $process.WaitForExit()
    $record.State = 'Exited'
    $record.ExitedUtcTicks = [DateTime]::UtcNow.Ticks
    Write-AtomicJsonFile $recordPath $record
    if ($process.ExitCode -ne 0) { throw "Unity $Method 失败（exit $($process.ExitCode)），详见归档日志。" }
    $resultPath = Join-Path $output 'result.json'
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "Unity $Method 没有写出 result.json。" }
}

function Get-UnityArgument([string]$CommandLine, [string]$Name) {
    $matches = [regex]::Matches($CommandLine, '(?i)(?:^|\s)-' + [regex]::Escape($Name) + '\s+(?:"([^"]+)"|([^\s]+))')
    if ($matches.Count -ne 1) { return $null }
    if ($matches[0].Groups[1].Success) { return $matches[0].Groups[1].Value }
    return $matches[0].Groups[2].Value
}

function Assert-UnityProcess($Process, $Record) {
    $owner = Invoke-CimMethod -InputObject $Process -MethodName GetOwner
    if ($owner.ReturnValue -ne 0 -or "$($owner.Domain)\$($owner.User)" -ne $Record.Owner -or
        $Process.ExecutablePath -ne $Record.ExecutablePath -or -not $Process.CommandLine -or
        ($Record.StartedUtcTicks -gt 0 -and $Process.CreationDate.ToUniversalTime().Ticks -ne [long]$Record.StartedUtcTicks) -or
        $Process.CreationDate.ToUniversalTime().Ticks -lt [long]$Record.IntentUtcTicks -or
        (Get-UnityArgument $Process.CommandLine 'projectPath') -ne $Record.ProjectPath -or
        (Get-UnityArgument $Process.CommandLine 'logFile') -ne $Record.LogPath -or
        (Get-UnityArgument $Process.CommandLine 'executeMethod') -ne "Core.Editor.Hotfix.HotfixBuildAutomation.$($Record.Method)") {
        throw "Unity PID $($Process.ProcessId) 的本人、创建时间或本次任务参数不匹配，拒绝处理。"
    }
}

function Find-RecordedUnity($Record) {
    if ($Record.Id -gt 0) {
        $found = Get-CimInstance Win32_Process -Filter "ProcessId=$($Record.Id)"
        if (-not $found) { return $null }
        if ($found.CreationDate.ToUniversalTime().Ticks -ne [long]$Record.StartedUtcTicks) {
            if ($Record.ExitedUtcTicks -gt 0) { return $null }
            throw "Unity PID $($Record.Id) 已复用，拒绝处理并保留现场。"
        }
        Assert-UnityProcess $found $Record
        return $found
    }
    $found = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
        $_.ExecutablePath -eq $Record.ExecutablePath -and $_.CommandLine -and
        $_.CreationDate.ToUniversalTime().Ticks -ge [long]$Record.IntentUtcTicks -and
        (Get-UnityArgument $_.CommandLine 'projectPath') -eq $Record.ProjectPath -and
        (Get-UnityArgument $_.CommandLine 'logFile') -eq $Record.LogPath -and
        (Get-UnityArgument $_.CommandLine 'executeMethod') -eq "Core.Editor.Hotfix.HotfixBuildAutomation.$($Record.Method)"
    })
    if ($found.Count -gt 1) { throw '同一启动意图匹配多个 Unity，拒绝处理。' }
    if ($found.Count -eq 0) { return $null }
    Assert-UnityProcess $found[0] $Record
    return $found[0]
}

function Get-OwnedUnityChildren($Unity, [string]$Owner) {
    $snapshot = @(Get-CimInstance Win32_Process)
    $parents = @([pscustomobject]@{ Id = [int]$Unity.ProcessId; StartedUtcTicks = $Unity.CreationDate.ToUniversalTime().Ticks; Depth = 0 })
    $children = @()
    while ($parents.Count -gt 0) {
        $next = @()
        foreach ($parent in $parents) {
            foreach ($child in $snapshot | Where-Object { $_.ParentProcessId -eq $parent.Id -and $_.Name -notmatch '(?i)licens' }) {
                if ($child.CreationDate.ToUniversalTime().Ticks -lt $parent.StartedUtcTicks -or -not $child.ExecutablePath) { continue }
                $ownerInfo = Invoke-CimMethod -InputObject $child -MethodName GetOwner
                if ($ownerInfo.ReturnValue -ne 0 -or "$($ownerInfo.Domain)\$($ownerInfo.User)" -ne $Owner) { continue }
                $identity = [pscustomobject]@{
                    Id = [int]$child.ProcessId; StartedUtcTicks = $child.CreationDate.ToUniversalTime().Ticks
                    ExecutablePath = $child.ExecutablePath; Owner = $Owner
                    ParentId = $parent.Id; ParentStartedUtcTicks = $parent.StartedUtcTicks; Depth = $parent.Depth + 1
                }
                $children += $identity
                $next += $identity
            }
        }
        $parents = $next
    }
    return $children
}

function Stop-RecordedProcess($Identity, [switch]$Unity) {
    $current = Get-CimInstance Win32_Process -Filter "ProcessId=$($Identity.Id)"
    if (-not $current) { return }
    $owner = Invoke-CimMethod -InputObject $current -MethodName GetOwner
    if ($current.CreationDate.ToUniversalTime().Ticks -ne [long]$Identity.StartedUtcTicks -or
        $current.ExecutablePath -ne $Identity.ExecutablePath -or $owner.ReturnValue -ne 0 -or
        "$($owner.Domain)\$($owner.User)" -ne $Identity.Owner -or
        (-not $Unity -and ($current.Name -match '(?i)licens' -or $current.ParentProcessId -ne $Identity.ParentId))) {
        throw "PID $($Identity.Id) 身份不匹配，拒绝终止。"
    }
    if ($Unity) { Assert-UnityProcess $current $Identity }
    # 不以 Get-Process 缺失作为退出证据；按已重新核对的 Win32_Process 实例终止。
    $stopped = Invoke-CimMethod -InputObject $current -MethodName Terminate -Arguments @{ Reason = [uint32]1 }
    if ($stopped.ReturnValue -ne 0) { throw "PID $($Identity.Id) 终止失败（Win32 $($stopped.ReturnValue)），保留现场。" }
}

function Complete-UnityRun([switch]$Cancel) {
    foreach ($file in Get-ChildItem -LiteralPath $runRoot -Filter 'unity-*.process.json' -File) {
        Assert-OwnedPath $file.FullName $machine.Root | Out-Null
        $record = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($record.RunId -ne $runId -or $record.Method -notin @('Prepare', 'Build', 'Deploy') -or
            $file.Name -ne "unity-$($record.Method).process.json" -or
            $record.Platform -notin @('PC', 'Android') -or $record.Owner -ne [Security.Principal.WindowsIdentity]::GetCurrent().Name -or
            $record.ExecutablePath -ne [IO.Path]::GetFullPath($machine.Unity) -or
            $record.ProjectPath -ne [IO.Path]::GetFullPath((Join-Path $machine.Root "workspaces/$($record.Platform)")) -or
            $record.LogPath -ne [IO.Path]::GetFullPath((Join-Path $output "logs/$($record.Method).log")) -or
            $record.RequestPath -ne $requestPath -or $record.IntentUtcTicks -le 0) { throw 'Unity 记录不属于当前任务，拒绝收尾。' }
        foreach ($child in @($record.Children)) {
            $parents = @(@($record) + @($record.Children) | Where-Object {
                $_.Id -eq $child.ParentId -and [long]$_.StartedUtcTicks -eq [long]$child.ParentStartedUtcTicks
            })
            if ($parents.Count -eq 0 -or $child.Owner -ne $record.Owner -or $child.Depth -le 0 -or
                [long]$child.StartedUtcTicks -lt [long]$child.ParentStartedUtcTicks) { throw 'Unity 子进程记录没有本次任务的完整父进程身份，拒绝收尾。' }
        }
        $current = Find-RecordedUnity $record
        if ($current) {
            if (-not $Cancel) { throw '本任务 Unity 仍在运行，拒绝归档清理。' }
            $record.Id = [int]$current.ProcessId
            $record.StartedUtcTicks = $current.CreationDate.ToUniversalTime().Ticks
            $record.Children = @($record.Children) + @(Get-OwnedUnityChildren $current $record.Owner)
            Write-AtomicJsonFile $file.FullName $record
            Stop-RecordedProcess $record -Unity
        }
        foreach ($child in @($record.Children) | Sort-Object Depth -Descending) {
            $live = Get-CimInstance Win32_Process -Filter "ProcessId=$($child.Id)"
            if (-not $live -or $live.CreationDate.ToUniversalTime().Ticks -ne [long]$child.StartedUtcTicks) { continue }
            if (-not $Cancel) { throw '本任务记录的 Unity 子进程仍在运行，拒绝清理。' }
            Stop-RecordedProcess $child
        }
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            $remaining = @(@($record) + @($record.Children) | Where-Object {
                $live = if ($_.Id -gt 0) { Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Id)" } else { $null }
                $live -and $live.CreationDate.ToUniversalTime().Ticks -eq [long]$_.StartedUtcTicks
            })
            if ($remaining.Count -eq 0) { break }
            Start-Sleep -Milliseconds 250
        } while ([DateTime]::UtcNow -lt $deadline)
        if ($remaining.Count -gt 0) { throw '本任务 Unity/子进程退出未确认，拒绝清理并保留现场。' }
        Assert-ProjectClosed $record.ProjectPath
        $record.State = if ($Cancel) { 'SettledAfterFailure' } else { 'Settled' }
        $record.ExitedUtcTicks = [DateTime]::UtcNow.Ticks
        Write-AtomicJsonFile $file.FullName $record
    }
}

function Remove-OwnedRun([string]$OwnedDirectory) {
    $resolved = Assert-OwnedPath $OwnedDirectory $machine.Root
    if ($resolved -ne $runRoot -or (Get-Content -LiteralPath $marker -Raw).Trim() -ne $runId) { throw '构建产物归属不匹配。' }
    $items = @(Get-ChildItem -LiteralPath $resolved -Recurse)
    if ($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw '产物包含链接，拒绝清理。' }
    foreach ($file in $items | Where-Object { -not $_.PSIsContainer -and $_.FullName -ne $marker }) { Remove-Item -LiteralPath $file.FullName }
    foreach ($directory in $items | Where-Object { $_.PSIsContainer } | Sort-Object { $_.FullName.Length } -Descending) { Remove-Item -LiteralPath $directory.FullName }
    Remove-Item -LiteralPath $marker
    Remove-Item -LiteralPath $resolved
}

try {
    if ($Stage -eq 'Cleanup') {
        if (Test-Path -LiteralPath $runRoot) { Complete-UnityRun; Remove-OwnedRun $runRoot }
        $archive = Assert-OwnedPath (Join-Path $env:WORKSPACE "ci-archive/$($env:BUILD_NUMBER)") $env:WORKSPACE
        $archiveMarker = Join-Path $archive '.sleepydemos-run'
        if (Test-Path -LiteralPath $archiveMarker) {
            if ((Get-Content -LiteralPath $archiveMarker -Raw).Trim() -ne $runId) { throw '归档目录归属不匹配。' }
            $items = @(Get-ChildItem -LiteralPath $archive -Recurse)
            if ($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw '归档目录包含链接，拒绝清理。' }
            foreach ($file in $items | Where-Object { -not $_.PSIsContainer -and $_.FullName -ne $archiveMarker }) { Remove-Item -LiteralPath $file.FullName }
            foreach ($directory in $items | Where-Object { $_.PSIsContainer } | Sort-Object { $_.FullName.Length } -Descending) { Remove-Item -LiteralPath $directory.FullName }
            Remove-Item -LiteralPath $archiveMarker
            Remove-Item -LiteralPath $archive
        }
        exit 0
    }
    if (Test-Path -LiteralPath $runRoot) {
        if (-not (Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw).Trim() -ne $runId) { throw '运行目录已存在且归属不明。' }
    } else {
        New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
        Set-Content -LiteralPath $marker -Value $runId -Encoding ascii
    }
    New-Item -ItemType Directory -Path (Join-Path $output 'logs') -Force | Out-Null
    if ($Stage -eq 'Finalize') {
        Complete-UnityRun -Cancel:($env:SLEEPY_JENKINS_RESULT -in @('ABORTED', 'FAILURE'))
        $unitySettled = $true
    } elseif ($Stage -eq 'Checkout') {
        if ($env:Platform -notin @('PC', 'Android') -or $env:BuildKind -notin @('Full', 'Hotfix') -or
            $env:Provider -notin @('Pages', 'Ssh')) { throw '非法构建参数。' }
        $baseline = Assert-OwnedPath (Join-Path $machine.Root "baselines/$($env:Platform)/current") $machine.Root
        $resourceMode = $env:ResourceMode
        $playerVersion = $env:PlayerVersion
        if ($env:BuildKind -eq 'Hotfix') {
            $baselinePath = Assert-OwnedPath (Join-Path $baseline 'baseline.json') $machine.Root
            if (-not (Test-Path -LiteralPath $baselinePath -PathType Leaf)) { throw '没有已发布的在线 Full 基线，请先构建并发布 Full。' }
            $baselineInfo = Get-Content -LiteralPath $baselinePath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($baselineInfo.Platform -ne $env:Platform -or $baselineInfo.ResourceMode -notin @('OnlineFull', 'OnlineDownload')) { throw '当前基线不是本平台的在线 Full。' }
            if ($baselineInfo.Development -ne ($env:Development -eq 'true')) { throw 'Development 必须与当前在线 Full 基线一致。' }
            $resourceMode = $baselineInfo.ResourceMode
            if ([string]::IsNullOrWhiteSpace($playerVersion)) { $playerVersion = $baselineInfo.PlayerVersion }
            elseif ($playerVersion -cne $baselineInfo.PlayerVersion) { throw 'Hotfix PlayerVersion 必须匹配当前在线 Full 基线。' }
        } elseif ($resourceMode -notin @('Offline', 'OnlineFull', 'OnlineDownload')) { throw '非法 ResourceMode。' }
        $androidFormat = 'Apk'
        $androidSigning = 'Debug'
        $versionCode = 1
        if ($env:Platform -eq 'Android' -and $env:BuildKind -eq 'Full') {
            if ($env:AndroidFormat -notin @('Apk', 'Aab') -or $env:AndroidSigning -notin @('Debug', 'Custom')) { throw '非法 Android 格式或签名参数。' }
            $androidFormat = $env:AndroidFormat
            $androidSigning = $env:AndroidSigning
            $versionCode = 0
            if ($env:AndroidVersionCode -and (-not [int]::TryParse($env:AndroidVersionCode, [ref]$versionCode) -or $versionCode -le 0)) { throw 'AndroidVersionCode 覆盖值必须是正整数。' }
            if ($versionCode -eq 0 -and (-not [int]::TryParse($env:BUILD_NUMBER, [ref]$versionCode) -or $versionCode -le 0)) { throw 'BUILD_NUMBER 超出 AndroidVersionCode 有效范围。' }
        }
        if ([string]::IsNullOrWhiteSpace($env:SourceRef) -or $env:SourceRef.StartsWith('-') -or $env:SourceRef -match '\s|[~^:?*\[\\]') { throw 'SourceRef 只允许明确的分支、标签或提交 SHA。' }
        $project = Assert-OwnedPath (Join-Path $machine.Root "workspaces/$($env:Platform)") $machine.Root
        if ($project.Equals([IO.Path]::GetFullPath($machine.DailyProjectPath).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) { throw '拒绝修改日常项目。' }
        Assert-ProjectClosed $project
        $ownership = Join-Path $machine.Root "workspaces/$($env:Platform).owned"
        New-Item -ItemType Directory -Path (Split-Path -Parent $project) -Force | Out-Null
        if (-not (Test-Path -LiteralPath $project)) {
            Set-Content -LiteralPath $ownership -Value $machine.RepositoryUrl -Encoding ascii
            Invoke-Git (Split-Path -Parent $project) @('clone', '--no-checkout', '--', $machine.RepositoryUrl, $project) | Out-Host
        } elseif (-not (Test-Path -LiteralPath $ownership) -or (Get-Content -LiteralPath $ownership -Raw).Trim() -ne $machine.RepositoryUrl) {
            throw 'CI 工作区未登记归属，拒绝检出。'
        }
        if ((Invoke-Git $project @('remote', 'get-url', 'origin') | Out-String).Trim() -ne $machine.RepositoryUrl) { throw 'CI origin 与机器配置不一致。' }
        Invoke-Git $project @('fetch', '--prune', 'origin', $env:SourceRef) | Out-Host
        $sha = (Invoke-Git $project @('rev-parse', 'FETCH_HEAD^{commit}') | Out-String).Trim()
        if ($sha -notmatch '^[0-9a-f]{40}$') { throw '无法解析固定源提交。' }
        # 原生 DLL 与其 importer meta 成对保留，避免热更检出重新生成 GUID 而改变首包指纹。
        $links = Get-ChildItem -LiteralPath $project -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }
        if ($links) { throw 'CI 工作区含根级链接，拒绝 Git 清理。' }
        Invoke-Git $project @('reset', '--hard', $sha) | Out-Host
        Invoke-Git $project @('clean', '-fd', '-e', 'Library/', '-e', 'Logs/', '-e', 'Temp/', '-e', 'UserSettings/', '-e', 'obj/', '-e', 'Assets/Plugins/Streamline/x86_64/') | Out-Host
        Invoke-Git $project @('submodule', 'sync', '--recursive') | Out-Host
        Invoke-Git $project @('submodule', 'update', '--init', '--recursive', '--force') | Out-Host
        $configCommit = (Invoke-Git (Join-Path $project 'SleepyConfigs') @('rev-parse', 'HEAD') | Out-String).Trim()
        $expectedConfig = (Invoke-Git $project @('rev-parse', 'HEAD:SleepyConfigs') | Out-String).Trim()
        if ($configCommit -ne $expectedConfig) { throw 'SleepyConfigs 未检出主仓库固定提交。' }
        $resourceVersion = $env:ResourceVersion
        if ([string]::IsNullOrWhiteSpace($resourceVersion)) { $resourceVersion = (Get-Date -Format 'yyyyMMdd-HHmmss') + "-$($env:BUILD_NUMBER)-" + $sha.Substring(0, 8) }
        if ($resourceVersion -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,100}$') { throw 'ResourceVersion 只能包含字母、数字、点、横线和下划线。' }
        $registrationPath = Assert-OwnedPath (Join-Path $machine.Root "resource-versions/$($env:Platform)/$resourceVersion.json") $machine.Root
        New-Item -ItemType Directory -Path (Split-Path -Parent $registrationPath) -Force | Out-Null
        try {
            $registration = [IO.File]::Open($registrationPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        } catch [IO.IOException] {
            if (Test-Path -LiteralPath $registrationPath) { throw 'ResourceVersion 已被登记；失败任务使用过的版本也不能再次使用。' }
            throw
        }
        try {
            $record = @{ Job = $env:JOB_NAME; Build = $env:BUILD_NUMBER; SourceCommit = $sha; ConfigCommit = $configCommit }
            $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes(($record | ConvertTo-Json))
            $registration.Write($bytes, 0, $bytes.Length)
            $registration.Flush($true)
        } finally { $registration.Dispose() }
        $pagesPath = Assert-OwnedPath $machine.Pages.Path $machine.Root
        $request = [ordered]@{
            Platform = $env:Platform; BuildKind = $env:BuildKind; ResourceMode = $resourceMode
            AndroidFormat = $androidFormat; AndroidSigning = $androidSigning; Development = $env:Development -eq 'true'
            PlayerVersion = $playerVersion; AndroidVersionCode = $versionCode; ResourceVersion = $resourceVersion
            Publish = $env:Publish -eq 'true'; Provider = $env:Provider; SourceCommit = $sha; ConfigCommit = $configCommit
            OutputDirectory = $output; BaselineDirectory = $baseline; PagesRepositoryPath = $pagesPath
            PagesSiteUrl = $machine.Pages.BaseUrl; PagesProjectName = $machine.Pages.ProjectName
            SshHost = $machine.Ssh.Host; SshPort = $machine.Ssh.Port; SshUser = $machine.Ssh.User
            SshRoot = $machine.Ssh.RootPath; SshServerUrl = $machine.Ssh.BaseUrl; SshHostKeySha256 = $machine.Ssh.HostKeySha256
        }
        Write-JsonFile $requestPath $request
        Write-JsonFile (Join-Path $output 'source.json') @{ SourceCommit = $sha; ConfigCommit = $configCommit; Submodules = @(Invoke-Git $project @('submodule', 'status', '--recursive')) }
        Write-Output "固定源提交 $sha；配置提交 $configCommit；版本 $resourceVersion。"
    } elseif ($Stage -ne 'Finalize') {
        $request = Get-Content -LiteralPath $requestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $project = Assert-OwnedPath (Join-Path $machine.Root "workspaces/$($request.Platform)") $machine.Root
        Assert-ProjectClosed $project
        if ((Invoke-Git $project @('rev-parse', 'HEAD') | Out-String).Trim() -ne $request.SourceCommit) { throw '工作区提交发生变化。' }
        switch ($Stage) {
            'Prepare' {
                Invoke-Script (Join-Path $project 'SleepyConfigs/gen.ps1') @('-ParentProjectRoot', ('"' + $project + '"')) 'Luban'
                if ($request.Platform -eq 'PC' -and $request.BuildKind -eq 'Full') {
                    Invoke-Script (Join-Path $project 'scripts/streamline/Prepare-Dependencies.ps1') @() 'StreamlineDependencies'
                    Invoke-Script (Join-Path $project 'scripts/streamline/Build-Bridge.ps1') @('-UnityEditor', ('"' + $machine.Unity + '"'), '-Deploy') 'StreamlineBridge'
                }
                Invoke-Unity 'Prepare'
            }
            'Build' { Invoke-Unity 'Build' }
            'Deploy' {
                if (-not $request.Publish) { throw '未请求发布。' }
                if ($request.Provider -eq 'Pages') {
                    $pagesPath = Assert-OwnedPath $request.PagesRepositoryPath $machine.Root
                    $pagesOwner = Join-Path $machine.Root 'pages/.sleepydemos-pages'
                    if (-not (Test-Path -LiteralPath $pagesPath)) {
                        New-Item -ItemType Directory -Path (Split-Path -Parent $pagesPath) -Force | Out-Null
                        Set-Content -LiteralPath $pagesOwner -Value $machine.Pages.Repository -Encoding ascii
                        Invoke-Git (Split-Path -Parent $pagesPath) @('clone', '--branch', $machine.Pages.Branch, '--', $machine.Pages.Repository, $pagesPath) | Out-Host
                    } elseif (-not (Test-Path -LiteralPath $pagesOwner) -or (Get-Content -LiteralPath $pagesOwner -Raw).Trim() -ne $machine.Pages.Repository) {
                        throw 'Pages 克隆归属未登记，拒绝发布。'
                    }
                    if ((Invoke-Git $pagesPath @('remote', 'get-url', 'origin') | Out-String).Trim() -ne $machine.Pages.Repository) { throw 'Pages origin 与机器配置不一致。' }
                    Invoke-Git $pagesPath @('config', '--local', 'credential.helper', '') | Out-Null
                    Invoke-Git $pagesPath @('config', '--local', '--add', 'credential.helper', '!gh auth git-credential') | Out-Null
                    Invoke-Git $pagesPath @('pull', '--ff-only', 'origin', $machine.Pages.Branch) | Out-Host
                }
                Invoke-Unity 'Deploy'
            }
            'Package' {
                Add-Type -AssemblyName System.IO.Compression.FileSystem
                foreach ($directory in @('resources', 'player')) {
                    if ($directory -eq 'player' -and $request.Platform -eq 'Android') { continue }
                    $source = Join-Path $output $directory
                    if (Test-Path -LiteralPath $source) {
                        $zip = Join-Path $output "$($request.Platform)-$directory.zip"
                        if (Test-Path -LiteralPath $zip) { throw "归档已经存在：$zip" }
                        [IO.Compression.ZipFile]::CreateFromDirectory($source, $zip, 'Optimal', $false)
                    }
                }
            }
        }
    }
} catch {
    $failed = $true
    $errorText = $_.Exception.Message
    Write-Error $errorText -ErrorAction Continue
} finally {
    $env:PATH = $previousPath
    if ($Stage -ne 'Cleanup' -and (Test-Path -LiteralPath (Join-Path $output 'logs'))) {
        $summary = if (Test-Path -LiteralPath $summaryPath) { Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json } else { [pscustomobject]@{ Job = $env:JOB_NAME; Build = $env:BUILD_NUMBER; Stages = @(); Result = 'RUNNING' } }
        $summary.Stages = @($summary.Stages) + @([pscustomobject]@{ Stage = $Stage; Success = -not $failed; Error = $errorText; Utc = [DateTime]::UtcNow.ToString('o') })
        if ($failed) { $summary.Result = 'FAILURE' }
        if ($Stage -eq 'Finalize' -and $summary.Result -ne 'FAILURE') { $summary.Result = $env:SLEEPY_JENKINS_RESULT }
        Write-JsonFile $summaryPath $summary
        if ($Stage -eq 'Finalize') {
            $archive = Join-Path $env:WORKSPACE "ci-archive/$($env:BUILD_NUMBER)"
            New-Item -ItemType Directory -Path $archive -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $archive '.sleepydemos-run') -Value $runId -Encoding ascii
            Copy-Item -LiteralPath $summaryPath -Destination $archive
            Get-ChildItem -LiteralPath $runRoot -Filter 'unity-*.process.json' -File | Copy-Item -Destination $archive
            if (-not $unitySettled) {
                Write-Output 'Unity 收尾未确认，仅归档收尾摘要与进程记录；本次产物现场保留，禁止清理。'
                exit 1
            }
            if (-not (Test-Path -LiteralPath (Join-Path $output 'result.json'))) {
                Write-JsonFile (Join-Path $output 'result.json') @{ Success = $false; Stage = 'Pipeline'; Error = 'Unity 尚未生成结果；详见 summary.json 与日志。' }
            }
            # 失败也归档已经生成的部分产物，避免 Package 阶段未执行而丢失证据。
            $archivePlatform = $env:Platform
            if (Test-Path -LiteralPath $requestPath) {
                try { $archivePlatform = (Get-Content -LiteralPath $requestPath -Raw -Encoding UTF8 | ConvertFrom-Json).Platform } catch { }
            }
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            foreach ($directory in @('resources', 'player', 'baseline')) {
                if ($directory -eq 'player' -and $archivePlatform -eq 'Android') { continue }
                $source = Join-Path $output $directory
                $zip = Join-Path $output "$directory.zip"
                if ((Test-Path -LiteralPath $source) -and -not (Test-Path -LiteralPath $zip) -and -not (Get-ChildItem -LiteralPath $output -Filter "*-$directory.zip")) {
                    [IO.Compression.ZipFile]::CreateFromDirectory($source, $zip, 'Optimal', $false)
                }
            }
            foreach ($file in @('summary.json', 'result.json', 'source.json', 'prepared.json', 'built.json', 'previous.version', 'android-tools-before.json')) {
                $source = Join-Path $output $file
                if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $archive }
            }
            if (Test-Path -LiteralPath $requestPath) { Copy-Item -LiteralPath $requestPath -Destination $archive }
            New-Item -ItemType Directory -Path (Join-Path $archive 'logs') -Force | Out-Null
            Get-ChildItem -LiteralPath (Join-Path $output 'logs') | Copy-Item -Destination (Join-Path $archive 'logs') -Recurse
            Get-ChildItem -LiteralPath $output -Filter '*.zip' | Copy-Item -Destination $archive
            $player = Join-Path $output 'player'
            if ($archivePlatform -eq 'Android' -and (Test-Path -LiteralPath $player)) {
                Get-ChildItem -LiteralPath $player -File | Where-Object { $_.Extension -in @('.apk', '.aab') } | Copy-Item -Destination $archive
            }
            function Read-ToolVersion([string]$Executable, [string[]]$Arguments) {
                $preference = $ErrorActionPreference
                try {
                    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { return 'unavailable' }
                    $ErrorActionPreference = 'Continue'
                    $version = (& $Executable @Arguments 2>&1 | Out-String).Trim()
                    if ($LASTEXITCODE -eq 0 -and $version) { return $version }
                } catch {
                    # 工具版本不可读时保留原始构建结果，不让附加摘要掩盖失败原因。
                } finally { $ErrorActionPreference = $preference }
                return 'unavailable'
            }
            $tools = [ordered]@{
                Unity = 'unavailable'
                Java = Read-ToolVersion $machine.Java @('--version')
                Git = Read-ToolVersion $machine.Git @('--version')
                Gh = Read-ToolVersion $machine.Gh @('--version')
                Dotnet = Read-ToolVersion $machine.Dotnet @('--version')
                Jenkins = 'unavailable'
            }
            foreach ($file in @('built.json', 'prepared.json')) {
                try {
                    $metadata = Get-Content -LiteralPath (Join-Path $archive $file) -Raw -Encoding UTF8 | ConvertFrom-Json
                    if ($metadata.UnityVersion) { $tools.Unity = $metadata.UnityVersion; break }
                } catch { }
            }
            try {
                $installation = Get-Content -LiteralPath (Join-Path $machine.Root 'installation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
                if ($installation.Jenkins.Version) { $tools.Jenkins = $installation.Jenkins.Version }
            } catch { }
            Write-JsonFile (Join-Path $archive 'tools.json') $tools
            $checksumPath = [IO.Path]::GetFullPath((Join-Path $archive 'checksums.json'))
            $archivePrefix = [IO.Path]::GetFullPath($archive).TrimEnd('\', '/') + '\'
            $checksums = @(Get-ChildItem -LiteralPath $archive -File -Recurse -Force |
                Where-Object { $_.FullName -ne $checksumPath } | Sort-Object FullName | ForEach-Object {
                    Assert-OwnedPath $_.FullName $archive | Out-Null
                    [pscustomobject]@{
                        File = $_.FullName.Substring($archivePrefix.Length).Replace('\', '/')
                        Bytes = $_.Length
                        Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                    }
                })
            Write-JsonFile $checksumPath @{ Files = $checksums }
            Write-Output "最终构建摘要：$($summary.Result)，归档 $archive。"
        }
    }
}
if ($failed) { exit 1 }
