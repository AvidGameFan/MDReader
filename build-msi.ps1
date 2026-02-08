param(
    [string]$Configuration = 'Release',
    [ValidateSet('x64','ARM64')]
    [string]$Platform = 'x64',
    [bool]$SelfContained = $true,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RuntimePlatform = $Platform.ToLowerInvariant()
$AppProject = Join-Path $RepositoryRoot 'MDReader.App\MDReader.App.csproj'
$TestProject = Join-Path $RepositoryRoot 'MDReader.Tests\MDReader.Tests.csproj'
$WixProject = Join-Path $RepositoryRoot 'MDReader.Installer\MDReader.Installer.wixproj'
$MsiOutputDirectory = Join-Path $RepositoryRoot 'MsiOutput'

if (-not (Test-Path $MsiOutputDirectory)) {
    New-Item -ItemType Directory -Path $MsiOutputDirectory | Out-Null
}

function Invoke-DotNetCommand($description, [string[]]$arguments) {
    Write-Host "`n== $description =="
    Write-Host "dotnet $($arguments -join ' ')"
    & dotnet @arguments
}

function Find-WixToolset() {
    $candidates = @(
        'C:\Program Files (x86)\WiX Toolset v3.14\bin\candle.exe',
        'C:\Program Files\WiX Toolset v3.14\bin\candle.exe'
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return Split-Path -Parent $candidate
        }
    }

    throw 'WiX Toolset not found. Install WiX Toolset from https://wixtoolset.org/releases/'
}

function Invoke-WixBuild($description, [string[]]$arguments) {
    $wixPath = Find-WixToolset
    $candle = Join-Path $wixPath 'candle.exe'
    $light = Join-Path $wixPath 'light.exe'

    Write-Host "`n== $description =="
    Write-Host "`"$candle`" $($arguments -join ' ')"
    & $candle @arguments

    if ($LASTEXITCODE -ne 0) {
        throw 'WiX candle failed.'
    }

    $wixobj = $arguments[-1] -replace '\.wxs$', '.wixobj'
    Write-Host "`"$light`" $wixobj -out $MsiOutputDirectory\MDReader.msi"
    & $light $wixobj -out "$MsiOutputDirectory\MDReader.msi"

    if ($LASTEXITCODE -ne 0) {
        throw 'WiX light failed.'
    }
}

# Build app and test projects
Invoke-DotNetCommand 'Publishing app project' @('publish', $AppProject, '-c', $Configuration, '-r', "win-$RuntimePlatform", '--self-contained', $SelfContained.ToString().ToLowerInvariant())
Invoke-DotNetCommand 'Building test project' @('build', $TestProject, '-c', $Configuration)

if (-not $SkipTests) {
    Invoke-DotNetCommand 'Running unit tests' @('test', $TestProject, '-c', $Configuration, '--no-build', '--no-restore')
}

# Build MSI using WiX
$wxsFile = Join-Path $RepositoryRoot 'MDReader.Installer\Product.wxs'
Invoke-WixBuild 'Building MSI package with WiX' @(
    $wxsFile,
    "-dConfiguration=$Configuration",
    "-dPlatform=$Platform",
    "-dSourceDir=$(Join-Path $RepositoryRoot 'MDReader.App\bin' $Platform $Configuration 'net10.0-windows10.0.22621.0' "win-$RuntimePlatform" 'publish')"
)

Write-Host "MSI package generated at $MsiOutputDirectory\MDReader.msi"