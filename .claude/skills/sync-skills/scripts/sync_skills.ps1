param(
    [ValidatePattern('^[a-z0-9][a-z0-9-]*$')]
    [string]$SkillName,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
$sourceRoot = Join-Path $projectRoot ".agents\skills"
$targetRoot = Join-Path $projectRoot ".claude\skills"

function Assert-PublishPath {
    param([string]$Path)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $boundary = [System.IO.Path]::GetFullPath($targetRoot) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($boundary, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Publish path is outside .claude/skills: $fullPath"
    }
    # 核对整条父链，禁止沿已有链接写入共享源目录。
    $cursor = $fullPath
    while ($cursor -and $cursor -ne $projectRoot) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw "Publishing through a link is not allowed: $cursor"
            }
        }
        $cursor = Split-Path -Parent $cursor
    }
}

$skills = @(Get-ChildItem -LiteralPath $sourceRoot -Directory -Force | Where-Object {
    $_.Name -ne "unity-skills" -and (Test-Path -LiteralPath (Join-Path $_.FullName "SKILL.md"))
} | Sort-Object Name)
if ($SkillName) {
    $skills = @($skills | Where-Object Name -eq $SkillName)
    if ($skills.Count -ne 1) { throw "Self-authored source skill not found: $SkillName" }
}

Write-Output "Project: $projectRoot"
foreach ($skill in $skills) {
    if ($skill.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        throw "Source skill must be a physical directory: $($skill.FullName)"
    }
    $destination = Join-Path $targetRoot $skill.Name
    Assert-PublishPath $destination
    $sourceFiles = @{}
    $pending = [System.Collections.Generic.Queue[string]]::new()
    $pending.Enqueue($skill.FullName)
    while ($pending.Count -gt 0) {
        foreach ($item in Get-ChildItem -LiteralPath $pending.Dequeue() -Force) {
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw "Source link is not published: $($item.FullName)"
            }
            if ($item.PSIsContainer) { $pending.Enqueue($item.FullName); continue }
            $relative = $item.FullName.Substring($skill.FullName.Length + 1)
            if ($relative -eq "agents\openai.yaml") { continue }
            $sourceFiles[$relative] = $item.FullName
        }
    }
    foreach ($relative in ($sourceFiles.Keys | Sort-Object)) {
        $target = Join-Path $destination $relative
        Assert-PublishPath $target
        if ((Test-Path -LiteralPath $target -PathType Leaf) -and
            (Get-FileHash -LiteralPath $target).Hash -eq (Get-FileHash -LiteralPath $sourceFiles[$relative]).Hash) {
            continue
        }
        Write-Output "$(if ($DryRun) { '[dry-run] ' })Publish $($skill.Name)/$relative"
        if (-not $DryRun) {
            $parent = Split-Path -Parent $target
            if (-not (Test-Path -LiteralPath $parent)) { [System.IO.Directory]::CreateDirectory($parent) | Out-Null }
            Copy-Item -LiteralPath $sourceFiles[$relative] -Destination $target
        }
    }
    if (-not (Test-Path -LiteralPath $destination)) { continue }
    $targetFiles = @()
    $targetDirectories = @()
    $pending.Enqueue($destination)
    while ($pending.Count -gt 0) {
        foreach ($item in Get-ChildItem -LiteralPath $pending.Dequeue() -Force) {
            Assert-PublishPath $item.FullName
            if ($item.PSIsContainer) { $targetDirectories += $item.FullName; $pending.Enqueue($item.FullName) }
            else { $targetFiles += $item.FullName }
        }
    }
    foreach ($file in $targetFiles) {
        $relative = $file.Substring($destination.Length + 1)
        if ($sourceFiles.ContainsKey($relative)) { continue }
        Write-Output "$(if ($DryRun) { '[dry-run] ' })Remove derived file $($skill.Name)/$relative"
        if (-not $DryRun) { Remove-Item -LiteralPath $file }
    }
    foreach ($directory in ($targetDirectories | Sort-Object Length -Descending)) {
        if (@(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) {
            Write-Output "$(if ($DryRun) { '[dry-run] ' })Remove empty derived directory $directory"
            if (-not $DryRun) { Remove-Item -LiteralPath $directory }
        }
    }
}
Write-Output "Done. Published source selection: $($skills.Count). Upstream skills and unknown target skills were not changed."
