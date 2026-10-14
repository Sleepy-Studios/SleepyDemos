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
    $process = Start-Process -FilePath $machine.Unity -WindowStyle Hidden -PassThru `
        -ArgumentList @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $project + '"'), '-buildTarget', $target,
            '-executeMethod', "Core.Editor.Hotfix.HotfixBuildAutomation.$Method", '-logFile', ('"' + (Join-Path $output "logs/$Method.log") + '"'))
    $null = $process.Handle
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity $Method 失败（exit $($process.ExitCode)），详见归档日志。" }
    $resultPath = Join-Path $output 'result.json'
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "Unity $Method 没有写出 result.json。" }
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
        if (Test-Path -LiteralPath $runRoot) { Remove-OwnedRun $runRoot }
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
    if ($Stage -eq 'Checkout') {
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
