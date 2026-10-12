param(
    [string]$SkillName,

    [ValidateSet("claude", "codex")]
    [string]$From,

    [ValidateSet("claude", "codex")]
    [string]$To,

    [switch]$NoRules,
    [switch]$UseLinks,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Test-SkillDirectory {
    param([System.IO.DirectoryInfo]$Directory)

    return Test-Path -LiteralPath (Join-Path $Directory.FullName "SKILL.md")
}

function Get-SideRoot {
    param([string]$ProjectRoot, [string]$Side)

    return Join-Path $ProjectRoot ".$Side"
}

function Get-SkillMap {
    param([string]$Root)

    $map = @{}
    if (-not (Test-Path -LiteralPath $Root)) {
        return $map
    }

    Get-ChildItem -LiteralPath $Root -Directory | Where-Object {
        $_.Name -ne "sync-skills" -and (Test-SkillDirectory $_)
    } | ForEach-Object {
        $map[$_.Name] = $_.FullName
    }

    return $map
}

function Get-RuleMap {
    param([string]$Root)

    $map = @{}
    if (-not (Test-Path -LiteralPath $Root)) {
        return $map
    }

    Get-ChildItem -LiteralPath $Root -Force | ForEach-Object {
        $map[$_.Name] = $_.FullName
    }

    return $map
}

function Copy-Directory {
    param(
        [string]$Source,
        [string]$Destination,
        [bool]$Overwrite
    )

    if (Test-Path -LiteralPath $Destination) {
        if (-not $Overwrite) {
            Write-Host "Skip existing: $Destination"
            return
        }

        if ($DryRun) {
            Write-Host "[dry-run] replace $Destination with $Source"
            return
        }

        Remove-Item -LiteralPath $Destination -Recurse -Force
    }

    if ($DryRun) {
        if ($UseLinks) {
            Write-Host "[dry-run] link $Destination -> $Source"
        }
        else {
            Write-Host "[dry-run] copy $Source -> $Destination"
        }
        return
    }

    if ($UseLinks) {
        New-Item -ItemType Junction -Path $Destination -Target $Source | Out-Null
        Write-Host "Linked $Destination -> $Source"
    }
    else {
        Copy-Item -LiteralPath $Source -Destination $Destination -Recurse -Force
        Write-Host "Copied $Source -> $Destination"
    }
}

function Copy-RuleItem {
    param(
        [string]$Source,
        [string]$Destination,
        [bool]$Overwrite
    )

    if (Test-Path -LiteralPath $Destination) {
        if (-not $Overwrite) {
            Write-Host "Skip existing: $Destination"
            return
        }

        if ($DryRun) {
            Write-Host "[dry-run] replace $Destination with $Source"
            return
        }

        Remove-Item -LiteralPath $Destination -Recurse -Force
    }

    if ($DryRun) {
        Write-Host "[dry-run] copy $Source -> $Destination"
        return
    }

    Copy-Item -LiteralPath $Source -Destination $Destination -Recurse -Force
    Write-Host "Copied $Source -> $Destination"
}

function Sync-MissingMapItems {
    param(
        [hashtable]$SourceMap,
        [hashtable]$TargetMap,
        [string]$TargetRoot,
        [string]$Label,
        [string]$Kind
    )

    $missing = $SourceMap.Keys | Where-Object { -not $TargetMap.ContainsKey($_) } | Sort-Object
    if (-not $missing) {
        Write-Host "${Label}: no missing $Kind."
        return 0
    }

    foreach ($name in $missing) {
        $destination = Join-Path $TargetRoot $name
        if ($Kind -eq "skills") {
            Copy-Directory -Source $SourceMap[$name] -Destination $destination -Overwrite $false
        }
        else {
            Copy-RuleItem -Source $SourceMap[$name] -Destination $destination -Overwrite $false
        }
    }

    return $missing.Count
}

function Sync-RulesOneWay {
    param([string]$SourceRoot, [string]$TargetRoot)

    $rules = Get-RuleMap -Root $SourceRoot
    if ($rules.Count -eq 0) {
        Write-Host "Rules: no source rules found."
        return 0
    }

    foreach ($name in ($rules.Keys | Sort-Object)) {
        Copy-RuleItem -Source $rules[$name] -Destination (Join-Path $TargetRoot $name) -Overwrite $true
    }

    return $rules.Count
}

$SkillRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $SkillRoot)
$claudeRoot = Get-SideRoot -ProjectRoot $ProjectRoot -Side "claude"
$codexRoot = Get-SideRoot -ProjectRoot $ProjectRoot -Side "codex"
$claudeSkills = Join-Path $claudeRoot "skills"
$codexSkills = Join-Path $codexRoot "skills"
$claudeRules = Join-Path $claudeRoot "rules"
$codexRules = Join-Path $codexRoot "rules"

foreach ($path in @($claudeSkills, $codexSkills, $claudeRules, $codexRules)) {
    if (-not (Test-Path -LiteralPath $path)) {
        if ($DryRun) {
            Write-Host "[dry-run] create $path"
        }
        else {
            New-Item -ItemType Directory -Force -Path $path | Out-Null
        }
    }
}

Write-Host "Project: $ProjectRoot"

if ($SkillName) {
    if (-not $From -or -not $To) {
        throw "When -SkillName is used, pass both -From claude|codex and -To claude|codex."
    }
    if ($From -eq $To) {
        throw "-From and -To must be different."
    }
    if ($SkillName -eq "sync-skills") {
        throw "sync-skills itself is not synced by this script."
    }

    $sourceRoot = Get-SideRoot -ProjectRoot $ProjectRoot -Side $From
    $targetRoot = Get-SideRoot -ProjectRoot $ProjectRoot -Side $To
    $sourceSkill = Join-Path (Join-Path $sourceRoot "skills") $SkillName
    $targetSkill = Join-Path (Join-Path $targetRoot "skills") $SkillName

    if (-not (Test-Path -LiteralPath (Join-Path $sourceSkill "SKILL.md"))) {
        throw "Source skill not found or invalid: $sourceSkill"
    }

    Write-Host "Sync skill: $SkillName ($From -> $To)"
    Copy-Directory -Source $sourceSkill -Destination $targetSkill -Overwrite $true

    if (-not $NoRules) {
        Write-Host "Sync rules: $From -> $To"
        $count = Sync-RulesOneWay -SourceRoot (Join-Path $sourceRoot "rules") -TargetRoot (Join-Path $targetRoot "rules")
        Write-Host "Rules synced: $count"
    }

    Write-Host "Done. Restart Claude/Codex or start a new session if synced changes do not appear."
    return
}

if ($From -or $To) {
    throw "-From and -To are only valid with -SkillName."
}

$claudeSkillMap = Get-SkillMap -Root $claudeSkills
$codexSkillMap = Get-SkillMap -Root $codexSkills
$claudeRuleMap = Get-RuleMap -Root $claudeRules
$codexRuleMap = Get-RuleMap -Root $codexRules

Write-Host "Claude skills: $($claudeSkillMap.Count)"
Write-Host "Codex skills:  $($codexSkillMap.Count)"
Write-Host "Claude rules:  $($claudeRuleMap.Count)"
Write-Host "Codex rules:   $($codexRuleMap.Count)"

$addedSkillsToCodex = Sync-MissingMapItems -SourceMap $claudeSkillMap -TargetMap $codexSkillMap -TargetRoot $codexSkills -Label "Missing in .codex/skills" -Kind "skills"
$addedSkillsToClaude = Sync-MissingMapItems -SourceMap $codexSkillMap -TargetMap $claudeSkillMap -TargetRoot $claudeSkills -Label "Missing in .claude/skills" -Kind "skills"
$addedRulesToCodex = Sync-MissingMapItems -SourceMap $claudeRuleMap -TargetMap $codexRuleMap -TargetRoot $codexRules -Label "Missing in .codex/rules" -Kind "rules"
$addedRulesToClaude = Sync-MissingMapItems -SourceMap $codexRuleMap -TargetMap $claudeRuleMap -TargetRoot $claudeRules -Label "Missing in .claude/rules" -Kind "rules"

Write-Host "Done. Added skills to Codex: $addedSkillsToCodex; added skills to Claude: $addedSkillsToClaude."
Write-Host "Done. Added rules to Codex: $addedRulesToCodex; added rules to Claude: $addedRulesToClaude."
Write-Host "Restart Claude/Codex or start a new session if newly synced items do not appear."
