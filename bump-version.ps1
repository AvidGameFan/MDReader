param(
    [string]$Version,
    [ValidateSet('Major','Minor','Patch','Revision')]
    [string]$Increment = 'Revision'
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$VersionProps = Join-Path $RepositoryRoot 'Version.props'

if (-not (Test-Path $VersionProps)) {
    throw "Version file not found at $VersionProps"
}

[xml]$versionXml = Get-Content -Path $VersionProps
$currentText = $versionXml.Project.PropertyGroup.MDReaderVersion
if ([string]::IsNullOrWhiteSpace($currentText)) {
    throw 'MDReaderVersion is missing in Version.props'
}

$currentVersion = $null
if (-not [Version]::TryParse($currentText, [ref]$currentVersion)) {
    throw "Invalid current version in Version.props: $currentText"
}

function Normalize-Version([Version]$v) {
    $build = if ($v.Build -lt 0) { 0 } else { $v.Build }
    $revision = if ($v.Revision -lt 0) { 0 } else { $v.Revision }
    return "$($v.Major).$($v.Minor).$build.$revision"
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $newVersion = $null
    if (-not [Version]::TryParse($Version, [ref]$newVersion)) {
        throw "Invalid version supplied: $Version"
    }
}
else {
    $major = $currentVersion.Major
    $minor = $currentVersion.Minor
    $patch = if ($currentVersion.Build -lt 0) { 0 } else { $currentVersion.Build }
    $revision = if ($currentVersion.Revision -lt 0) { 0 } else { $currentVersion.Revision }

    switch ($Increment) {
        'Major' {
            $major += 1
            $minor = 0
            $patch = 0
            $revision = 0
        }
        'Minor' {
            $minor += 1
            $patch = 0
            $revision = 0
        }
        'Patch' {
            $patch += 1
            $revision = 0
        }
        'Revision' {
            $revision += 1
        }
    }

    $newVersion = New-Object Version($major, $minor, $patch, $revision)
}

$newVersionText = Normalize-Version $newVersion
$versionXml.Project.PropertyGroup.MDReaderVersion = $newVersionText
$versionXml.Save($VersionProps)

Write-Host "Updated MDReaderVersion: $currentText -> $newVersionText"
