[CmdletBinding()]
param(
    [ValidateSet('Start', 'Status', 'Stop')][string]$Action = 'Status',
    [string]$ConfigPath = 'E:\Jenkins\machine.json'
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$machine = Read-MachineConfig $ConfigPath
$statePath = Join-Path $machine.Root 'processes.json'
$url = $machine.JenkinsUrl
$jenkinsUri = [Uri]$url
$port = $jenkinsUri.Port
$listenAddress = if ($jenkinsUri.Host -eq 'localhost') { '127.0.0.1' } else { $jenkinsUri.Host.Trim('[', ']') }

function Get-OwnedProcess($Record, [string]$Jar) {
    if (-not $Record) { return $null }
    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($Record.Id)"
    if (-not $process) { return $null }
    $owner = Invoke-CimMethod -InputObject $process -MethodName GetOwner
    $identity = "$($owner.Domain)\$($owner.User)"
    if ($identity -ne [Security.Principal.WindowsIdentity]::GetCurrent().Name -or
        $process.ExecutablePath -ne $machine.Java -or
        $process.CreationDate.ToUniversalTime().Ticks -ne ([DateTime]$Record.Started).ToUniversalTime().Ticks -or
        -not $process.CommandLine.Contains($Jar)) {
        throw "PID $($Record.Id) 与本次 Jenkins 记录不匹配，拒绝操作。"
    }
    return $process
}

function Start-Java([string[]]$Arguments, [string]$Name) {
    $logRoot = Join-Path $machine.Root 'logs'
    New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
    $process = Start-Process -FilePath $machine.Java -ArgumentList $Arguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $logRoot "$Name.out.log") -RedirectStandardError (Join-Path $logRoot "$Name.err.log")
    $details = Get-CimInstance Win32_Process -Filter "ProcessId=$($process.Id)"
    return @{ Id = $process.Id; Started = $details.CreationDate.ToUniversalTime().ToString('o') }
}

function Invoke-JenkinsControl([ValidateSet('quietDown', 'cancelQuietDown')][string]$Command) {
    Add-Type -AssemblyName System.Net.Http
    $handler = New-Object System.Net.Http.HttpClientHandler
    # Jenkins 成功 POST 会重定向到首页；禁止跟跳，避免 PowerShell 丢弃认证头后误报失败。
    $handler.AllowAutoRedirect = $false
    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(30)
    $client.DefaultRequestHeaders.TryAddWithoutValidation('Authorization', $headers.Authorization) | Out-Null
    $content = New-Object System.Net.Http.StringContent('')
    $response = $null
    try {
        $response = $client.PostAsync("$url/$Command", $content).GetAwaiter().GetResult()
        $code = [int]$response.StatusCode
        if ($code -eq 302 -and $response.Headers.Location) {
            $redirect = New-Object Uri([Uri]"$url/$Command", $response.Headers.Location)
            if ($redirect.GetLeftPart([UriPartial]::Authority) -eq $url -and $redirect.AbsolutePath -eq '/' -and
                [string]::IsNullOrEmpty($redirect.UserInfo) -and [string]::IsNullOrEmpty($redirect.Query) -and
                [string]::IsNullOrEmpty($redirect.Fragment)) { return }
        }
        if (-not $response.IsSuccessStatusCode) { throw "Jenkins $Command 失败（HTTP $code），未停止进程。" }
    } finally {
        if ($response) { $response.Dispose() }
        $content.Dispose()
        $client.Dispose()
    }
}

$state = if (Test-Path -LiteralPath $statePath) { Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json } else { [pscustomobject]@{ Controller = $null; Agent = $null } }
$controller = Get-OwnedProcess $state.Controller $machine.JenkinsWar
$agent = Get-OwnedProcess $state.Agent $machine.AgentJar
if ($Action -eq 'Status') {
    [pscustomobject]@{ Url = $url; ControllerRunning = [bool]$controller; AgentRunning = [bool]$agent }
    exit 0
}
if ($Action -eq 'Start') {
    foreach ($file in @($machine.Java, $machine.JenkinsWar)) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "缺少运行文件：$file" }
    }
    $listener = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($listener -and (-not $controller -or @($listener.OwningProcess | Select-Object -Unique) -ne $controller.ProcessId)) {
        throw "$port 端口已被其他进程占用。"
    }
    New-Item -ItemType Directory -Path $machine.Root -Force | Out-Null
    if (-not $controller) {
        $state.Controller = Start-Java @(('-DJENKINS_HOME="' + (Join-Path $machine.Root 'home') + '"'), '-jar', ('"' + $machine.JenkinsWar + '"'), "--httpListenAddress=$listenAddress", "--httpPort=$port") 'controller'
        Write-JsonFile $statePath $state
    }
    if (-not $agent -and (Test-Path -LiteralPath $machine.AgentJar) -and (Test-Path -LiteralPath $machine.AgentSecretFile)) {
        # Remoting 在进程内读取 @file；命令行没有 agent secret。
        $state.Agent = Start-Java @('-jar', ('"' + $machine.AgentJar + '"'), '-url', $url, '-name', $machine.AgentName, '-secret', ('@' + $machine.AgentSecretFile), '-webSocket', '-workDir', (Join-Path $machine.Root 'agent')) 'agent'
        Write-JsonFile $statePath $state
    }
    Write-Output "Jenkins 已启动：$url；agent 是否可用请在节点页面核对。"
    exit 0
}
if ($controller) {
    if (-not (Test-Path -LiteralPath $machine.JenkinsApiAuthFile)) { throw '停止前必须提供仓库外 Jenkins API 用户/token 文件。' }
    $auth = Get-Content -LiteralPath $machine.JenkinsApiAuthFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $headers = @{ Authorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$($auth.User):$($auth.Token)")) }
    $queue = Invoke-RestMethod "$url/queue/api/json" -Headers $headers
    $computers = Invoke-RestMethod "$url/computer/api/json?depth=2" -Headers $headers
    if (@($queue.items).Count -gt 0 -or @($computers.computer | ForEach-Object { @($_.executors) + @($_.oneOffExecutors) } | Where-Object { $_.currentExecutable }).Count -gt 0) {
        throw 'Jenkins 存在排队或运行任务，拒绝停止。'
    }
    # quietDown 消除检查与停止之间的新任务竞态。
    Invoke-JenkinsControl 'quietDown'
    $queue = Invoke-RestMethod "$url/queue/api/json" -Headers $headers
    $computers = Invoke-RestMethod "$url/computer/api/json?depth=2" -Headers $headers
    if (@($queue.items).Count -gt 0 -or @($computers.computer | ForEach-Object { @($_.executors) + @($_.oneOffExecutors) } | Where-Object { $_.currentExecutable }).Count -gt 0) {
        Invoke-JenkinsControl 'cancelQuietDown'
        throw '停止期间出现任务，已取消 quietDown。'
    }
}
if ($agent) { Get-OwnedProcess $state.Agent $machine.AgentJar | Out-Null; Stop-Process -Id $agent.ProcessId }
if ($controller) { Get-OwnedProcess $state.Controller $machine.JenkinsWar | Out-Null; Stop-Process -Id $controller.ProcessId }
if (Test-Path -LiteralPath $statePath) { Remove-Item -LiteralPath $statePath }
Write-Output 'Jenkins controller 与本机 agent 已停止。'
