param(
    [string]$Filter = "",
    [ValidateSet("auto", "codex", "claude")]
    [string]$Prefer = "auto",
    [switch]$Json
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

function Strip-YamlValue {
    param([string]$Value)

    if ($null -eq $Value) {
        return ""
    }

    $text = $Value.Trim()
    if (($text.StartsWith('"') -and $text.EndsWith('"')) -or
        ($text.StartsWith("'") -and $text.EndsWith("'"))) {
        $text = $text.Substring(1, $text.Length - 2)
    }
    return ($text -replace "\s+", " ").Trim()
}

function Get-SkillMeta {
    param(
        [string]$SkillFile,
        [string]$FallbackName
    )

    $content = Get-Content -Path $SkillFile -Raw -Encoding UTF8
    $meta = [ordered]@{
        Name = $FallbackName
        Description = ""
    }

    $match = [regex]::Match($content, "(?s)^---\s*\r?\n(.*?)\r?\n---")
    if (-not $match.Success) {
        $descriptionMatch = [regex]::Match($content, "(?ms)^##\s+Description\s*\r?\n\s*(.+?)(\r?\n##|\z)")
        if ($descriptionMatch.Success) {
            $lines = $descriptionMatch.Groups[1].Value -split "\r?\n"
            $paragraph = @()
            foreach ($line in $lines) {
                $trimmed = $line.Trim()
                if (-not $trimmed -and $paragraph.Count -gt 0) {
                    break
                }
                if ($trimmed) {
                    $paragraph += $trimmed
                }
            }
            $meta.Description = Strip-YamlValue ($paragraph -join " ")
        }
        return [pscustomobject]$meta
    }

    $frontMatter = $match.Groups[1].Value
    $lines = $frontMatter -split "\r?\n"

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        if ($line -match "^\s*name\s*:\s*(.+?)\s*$") {
            $name = Strip-YamlValue $Matches[1]
            if ($name) {
                $meta.Name = $name
            }
            continue
        }

        if ($line -match "^\s*description\s*:\s*(.*?)\s*$") {
            $value = $Matches[1].Trim()
            if ($value -match "^[>|]") {
                $parts = @()
                for ($j = $i + 1; $j -lt $lines.Count; $j++) {
                    $next = $lines[$j]
                    if ($next -match "^[A-Za-z0-9_-]+\s*:") {
                        break
                    }
                    $parts += $next.Trim()
                }
                $meta.Description = Strip-YamlValue ($parts -join " ")
            }
            else {
                $meta.Description = Strip-YamlValue $value
            }
            continue
        }
    }

    return [pscustomobject]$meta
}

function ConvertTo-DisplayPath {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPath = [System.IO.Path]::GetFullPath($ProjectRoot)
    if (-not $rootPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $rootPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $rootUri = New-Object System.Uri($rootPath)
    $fileUri = New-Object System.Uri($fullPath)
    $relativePath = [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($fileUri).ToString())
    return ($relativePath -replace "\\", "/")
}

function Escape-MarkdownCell {
    param([string]$Text)

    if ($null -eq $Text) {
        return ""
    }
    return (($Text -replace "\|", "\|") -replace "`r?`n", " ").Trim()
}

function Get-SkillDirectories {
    param(
        [string]$Root,
        [string]$Side
    )

    if (-not (Test-Path $Root)) {
        return @()
    }

    Get-ChildItem -Path $Root -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName "SKILL.md") } |
        ForEach-Object {
            $skillFile = Join-Path $_.FullName "SKILL.md"
            $meta = Get-SkillMeta -SkillFile $skillFile -FallbackName $_.Name
            [pscustomobject]@{
                Name = $meta.Name
                Description = $meta.Description
                Side = $Side
                Path = (ConvertTo-DisplayPath $skillFile)
            }
        }
}

$preferredSide = $Prefer
if ($preferredSide -eq "auto") {
    $preferredSide = "codex"
}

$codexRoot = Join-Path $ProjectRoot ".codex\skills"
$claudeRoot = Join-Path $ProjectRoot ".claude\skills"
$rawSkills = @()
$rawSkills += Get-SkillDirectories -Root $codexRoot -Side "codex"
$rawSkills += Get-SkillDirectories -Root $claudeRoot -Side "claude"

$skillMap = [ordered]@{}
foreach ($skill in $rawSkills) {
    $key = $skill.Name
    if (-not $skillMap.Contains($key)) {
        $skillMap[$key] = [ordered]@{
            Name = $key
            CodexPath = $null
            ClaudePath = $null
            CodexDescription = ""
            ClaudeDescription = ""
        }
    }

    if ($skill.Side -eq "codex") {
        $skillMap[$key].CodexPath = $skill.Path
        $skillMap[$key].CodexDescription = $skill.Description
    }
    elseif ($skill.Side -eq "claude") {
        $skillMap[$key].ClaudePath = $skill.Path
        $skillMap[$key].ClaudeDescription = $skill.Description
    }
}

$items = foreach ($entry in $skillMap.Values) {
    $description = ""
    if ($preferredSide -eq "codex") {
        $description = if ($entry.CodexDescription) { $entry.CodexDescription } else { $entry.ClaudeDescription }
    }
    else {
        $description = if ($entry.ClaudeDescription) { $entry.ClaudeDescription } else { $entry.CodexDescription }
    }

    $status = "Codex+Claude"
    if ($entry.CodexPath -and -not $entry.ClaudePath) {
        $status = "Only Codex"
    }
    elseif ($entry.ClaudePath -and -not $entry.CodexPath) {
        $status = "Only Claude"
    }

    [pscustomobject]@{
        Name = $entry.Name
        Description = $description
        CodexPath = $entry.CodexPath
        ClaudePath = $entry.ClaudePath
        Status = $status
    }
}

if ($Filter) {
    $needle = $Filter.ToLowerInvariant()
    $items = $items | Where-Object {
        $_.Name.ToLowerInvariant().Contains($needle) -or
        $_.Description.ToLowerInvariant().Contains($needle)
    }
}

$items = @($items | Sort-Object Name)

if ($Json) {
    $items | ConvertTo-Json -Depth 4
    exit 0
}

Write-Output "# Skill Index"
Write-Output ""
if ($Filter) {
    Write-Output "Filter: ``$Filter``. Matched $($items.Count) skill(s)."
}
else {
    Write-Output "Found $($items.Count) skill(s). Click a link to open ``SKILL.md``; reply with a number or skill name to continue."
}
Write-Output ""
Write-Output "| # | Skill | Description | Links | Status |"
Write-Output "| ---: | --- | --- | --- | --- |"

$index = 1
foreach ($item in $items) {
    $links = @()
    if ($item.CodexPath) {
        $links += "[Codex]($($item.CodexPath))"
    }
    if ($item.ClaudePath) {
        $links += "[Claude]($($item.ClaudePath))"
    }

    $description = Escape-MarkdownCell $item.Description
    if (-not $description) {
        $description = "_No description_"
    }

    Write-Output "| $index | ``$($item.Name)`` | $description | $($links -join ' / ') | $($item.Status) |"
    $index++
}

Write-Output ""
Write-Output "New skills are included automatically when placed under ``.codex/skills/<name>/SKILL.md`` or ``.claude/skills/<name>/SKILL.md``."
