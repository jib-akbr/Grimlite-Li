param(
    [string]$Configuration = "Release",
    [string]$Solution = "Project Grimlite.sln"
)

function Find-MSBuild {
    $ms = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($ms) { return $ms.Path }

    $common = @(
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2022\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe"
    )
    foreach ($p in $common) { if (Test-Path $p) { return $p } }

    $vswhere = "C:\\Program Files (x86)\\Microsoft Visual Studio\\Installer\\vswhere.exe"
    if (Test-Path $vswhere) {
        $inst = & $vswhere -prerelease -latest -products * -requires Microsoft.Component.MSBuild -property installationPath 2>$null
        if ($inst) {
            $candidate = Join-Path $inst "MSBuild\\Current\\Bin\\MSBuild.exe"
            if (Test-Path $candidate) { return $candidate }
        }
    }

    return $null
}

Write-Host "Building solution: $Solution (Configuration=$Configuration)"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$slnPath = Join-Path $scriptDir $Solution
if (-not (Test-Path $slnPath)) {
    Write-Error "Solution file not found: $slnPath"
    exit 1
}

$msbuild = Find-MSBuild
if ($msbuild) {
    Write-Host "Using MSBuild: $msbuild"
    & "$msbuild" "$slnPath" /t:Rebuild /p:Configuration=$Configuration
    exit $LASTEXITCODE
}

Write-Host "MSBuild not found on PATH or common locations. Falling back to 'dotnet build' (may not work for older .NET Framework projects)."
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    dotnet build "$slnPath" -c $Configuration
    exit $LASTEXITCODE
}

Write-Error "Neither MSBuild nor dotnet CLI found. Install Visual Studio or the Visual Studio Build Tools with the .NET desktop build workload."
exit 2
