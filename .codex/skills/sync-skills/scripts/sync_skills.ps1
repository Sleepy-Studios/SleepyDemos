param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Find-ProjectRoot {
    $current = (Get-Location).Path

    while ($current) {
        if ((Test-Path -LiteralPath (Join-Path $current ".codex")) -or
            (Test-Path -LiteralPath (Join-Path $current ".claude")) -or
            (Test-Path -LiteralPath (Join-Path $current ".agents")) -or
            (Test-Path -LiteralPath (Join-Path $current ".git"))) {
            return $current
        }

        $parent = Split-Path -Parent $current
        if ($parent -eq $current) {
            break
        }
        $current = $parent
    }

    throw "Cannot find project root. Run this script inside the project."
}

function Ensure-Directory {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        return
    }

    if ($DryRun) {
        Write-Host "[dry-run] create directory: $Path"
        return
    }

    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Get-SkillMap {
    param([string]$SkillsPath)

    $map = @{}

    if (-not (Test-Path -LiteralPath $SkillsPath)) {
        return $map
    }

    Get-ChildItem -LiteralPath $SkillsPath -Directory | ForEach-Object {
        if ($_.Name -eq "sync-skills") {
            return
        }

        $skillFile = Join-Path $_.FullName "SKILL.md"
        if (Test-Path -LiteralPath $skillFile) {
            $map[$_.Name] = $_.FullName
        }
    }

    return $map
}

function Copy-MissingDirectory {
    param(
        [string]$Name,
        [string]$SourcePath,
        [string]$DestinationRoot,
        [string]$DestinationLabel
    )

    $destination = Join-Path $DestinationRoot $Name

    if (Test-Path -LiteralPath $destination) {
        return $false
    }

    if ($DryRun) {
        Write-Host "[dry-run] copy missing '$Name' -> $DestinationLabel"
        return $true
    }

    Copy-Item -LiteralPath $SourcePath -Destination $destination -Recurse -Force
    Write-Host "Copied missing '$Name' -> $DestinationLabel"
    return $true
}

function Copy-MissingRules {
    param(
        [string]$SourceRoot,
        [string]$DestinationRoot,
        [string]$SourceLabel,
        [hashtable]$KnownRuleMap
    )

    if (-not (Test-Path -LiteralPath $SourceRoot)) {
        return
    }

    Get-ChildItem -LiteralPath $SourceRoot -File | ForEach-Object {
        if ($KnownRuleMap.ContainsKey($_.Name)) {
            return
        }

        $destination = Join-Path $DestinationRoot $_.Name

        if ($DryRun) {
            Write-Host "[dry-run] copy rule '$($_.Name)' from $SourceLabel -> .codex/rules"
        }
        else {
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
            Write-Host "Copied rule '$($_.Name)' from $SourceLabel -> .codex/rules"
        }

        $KnownRuleMap[$_.Name] = $destination
        $script:copiedRules++
    }
}

function Remove-LegacyAgentsDirectory {
    param([string]$AgentsPath)

    if (-not (Test-Path -LiteralPath $AgentsPath)) {
        return $false
    }

    if ($DryRun) {
        Write-Host "[dry-run] remove legacy directory: $AgentsPath"
        return $true
    }

    Remove-Item -LiteralPath $AgentsPath -Recurse -Force
    Write-Host "Removed legacy directory: $AgentsPath"
    return $true
}

$projectRoot = Find-ProjectRoot

$claudeSkills = Join-Path $projectRoot ".claude\skills"
$agentsSkills = Join-Path $projectRoot ".agents\skills"
$codexSkills = Join-Path $projectRoot ".codex\skills"

$claudeRules = Join-Path $projectRoot ".claude\rules"
$agentsRules = Join-Path $projectRoot ".agents\rules"
$codexRules = Join-Path $projectRoot ".codex\rules"
$agentsRoot = Join-Path $projectRoot ".agents"

Ensure-Directory -Path $codexSkills
Ensure-Directory -Path $codexRules

$sourceSkillMaps = @(
    @{ Label = ".claude/skills"; Map = Get-SkillMap -SkillsPath $claudeSkills },
    @{ Label = ".agents/skills"; Map = Get-SkillMap -SkillsPath $agentsSkills }
)

$codexMap = Get-SkillMap -SkillsPath $codexSkills
$codexRuleMap = @{}
$codexRulesExists = Test-Path -LiteralPath $codexRules
if ($codexRulesExists) {
    Get-ChildItem -LiteralPath $codexRules -File | ForEach-Object {
        $codexRuleMap[$_.Name] = $_.FullName
    }
}
$copiedSkills = 0
$script:copiedRules = 0

Write-Host "Project: $projectRoot"
Write-Host "Codex skills: $($codexMap.Count)"
Write-Host "Claude skills: $($sourceSkillMaps[0].Map.Count)"
Write-Host "Agents skills: $($sourceSkillMaps[1].Map.Count)"

foreach ($source in $sourceSkillMaps) {
    foreach ($name in ($source.Map.Keys | Sort-Object)) {
        if ($codexMap.ContainsKey($name)) {
            continue
        }

        $copied = Copy-MissingDirectory -Name $name -SourcePath $source.Map[$name] -DestinationRoot $codexSkills -DestinationLabel ".codex/skills"
        if ($copied) {
            $copiedSkills++
            $codexMap[$name] = Join-Path $codexSkills $name
        }
    }
}

Copy-MissingRules -SourceRoot $claudeRules -DestinationRoot $codexRules -SourceLabel ".claude/rules" -KnownRuleMap $codexRuleMap
Copy-MissingRules -SourceRoot $agentsRules -DestinationRoot $codexRules -SourceLabel ".agents/rules" -KnownRuleMap $codexRuleMap

$removedAgents = Remove-LegacyAgentsDirectory -AgentsPath $agentsRoot

Write-Host "Summary: $copiedSkills skills copied to .codex, $script:copiedRules rules copied to .codex."
if ($removedAgents) {
    Write-Host "Legacy .agents directory scheduled for removal."
}

if ($copiedSkills -gt 0 -or $script:copiedRules -gt 0 -or $removedAgents) {
    Write-Host "Restart Codex or start a new session if newly synced skills or rules do not appear."
}
