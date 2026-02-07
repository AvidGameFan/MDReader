param(
    [string]$Configuration = 'Release',
    [ValidateSet('x64','ARM64')]
    [string]$Platform = 'x64',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$AppProject = Join-Path $RepositoryRoot 'MDReader.App\MDReader.App.csproj'
$TestProject = Join-Path $RepositoryRoot 'MDReader.Tests\MDReader.Tests.csproj'
$WapProject = Join-Path $RepositoryRoot 'WapProject\WapProject.wapproj'
$WapPackageDirectory = Join-Path $RepositoryRoot 'WapProject\AppPackages'

if (-not (Test-Path $WapPackageDirectory)) {
    New-Item -ItemType Directory -Path $WapPackageDirectory | Out-Null
}

function Invoke-DotNetCommand($description, [string[]]$arguments) {
    Write-Host "`n== $description =="
    Write-Host "dotnet $($arguments -join ' ')"
    & dotnet @arguments
}

function Find-VSMSBuild() {
    $candidates = @(
        'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw 'Visual Studio MSBuild.exe not found. Install Visual Studio with Desktop development for the packaging project.'
}

function Invoke-MSBuild($description, [string[]]$arguments) {
    $msbuild = Find-VSMSBuild
    Write-Host "`n== $description =="
    Write-Host "`"$msbuild`" $($arguments -join ' ')"
    & $msbuild @arguments
}

Invoke-DotNetCommand 'Building app project' @('build', $AppProject, '-c', $Configuration)
Invoke-DotNetCommand 'Building test project' @('build', $TestProject, '-c', $Configuration)

if (-not $SkipTests) {
    Invoke-DotNetCommand 'Running unit tests' @('test', $TestProject, '-c', $Configuration, '--no-build', '--no-restore')
}

Invoke-MSBuild 'Building MSIX package (WAP project)' @(
    $WapProject,
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform"
)
if ($LASTEXITCODE -ne 0) {
    throw 'MSBuild failed while producing the MSIX package.'
}

$msixCandidates = Get-ChildItem -Path $WapPackageDirectory -Filter '*.msix' -Recurse
if (-not $msixCandidates) {
    throw 'MSIX package not found under WapProject\AppPackages. Verify the packaging build output.'
}

$latestPackage = $msixCandidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "MSIX bundle generated at $($latestPackage.FullName)"
