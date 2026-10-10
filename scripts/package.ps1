[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')]
    [string]$Version,
    [string]$IsccPath,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$releaseDirectory = Join-Path $artifactsRoot $Version
$workRoot = Join-Path $artifactsRoot '.staging'
$workDirectory = Join-Path $workRoot ([Guid]::NewGuid().ToString('N'))
$publishDirectory = Join-Path $workDirectory 'publish'
$packageDirectory = Join-Path $workDirectory 'packages'
$portableDirectory = Join-Path $workDirectory 'DevServerManager'
$numericVersion = ($Version -split '-')[0]
foreach ($part in $numericVersion.Split('.')) {
    if ([long]$part -gt 65534) { throw 'Version components must be at most 65534 for Windows file version metadata.' }
}
if (Test-Path -LiteralPath $releaseDirectory) {
    throw "Output already exists: $releaseDirectory. Use a new version or move the existing output before rebuilding."
}

if (-not $SkipInstaller) {
    if (-not $IsccPath) {
        $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($compiler) { $IsccPath = $compiler.Source }
        else {
            foreach ($programDirectory in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
                if ($programDirectory) {
                    $candidate = Join-Path $programDirectory 'Inno Setup 6\ISCC.exe'
                    if (Test-Path -LiteralPath $candidate) { $IsccPath = $candidate; break }
                }
            }
            if (-not $IsccPath) {
                $projectCompiler = Join-Path $artifactsRoot 'tooling\InnoSetup\ISCC.exe'
                if (Test-Path -LiteralPath $projectCompiler) { $IsccPath = $projectCompiler }
            }
        }
    }
    if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) {
        throw 'Inno Setup 6.3+ is required. Install it or pass -IsccPath; use -SkipInstaller to build only the ZIP.'
    }
}

try {
    New-Item -ItemType Directory -Path $publishDirectory, $packageDirectory, $portableDirectory -Force | Out-Null
    & dotnet run --project (Join-Path $repositoryRoot 'tests\PackagingChecks\PackagingChecks.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Storage verification failed.' }

    # Keep native WPF/SQLite dependencies in the application folder; no extraction cache is needed.
    & dotnet publish (Join-Path $repositoryRoot 'DevServerManager\DevServerManager.csproj') `
        --configuration Release --runtime win-x64 --self-contained true --output $publishDirectory `
        "-p:Version=$Version" "-p:FileVersion=$numericVersion.0" "-p:AssemblyVersion=$numericVersion.0" `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
    foreach ($required in @('DevServerManager.exe', 'DevServerManager.dll', 'coreclr.dll', 'hostfxr.dll', 'e_sqlite3.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory $required))) {
            throw "Self-contained publish is missing $required."
        }
    }
    $stateFiles = Get-ChildItem -LiteralPath $publishDirectory -Recurse -File | Where-Object {
        $_.Name -like '*.db' -or $_.Name -like '*.db-*' -or $_.Name -eq 'portable.flag'
    }
    if ($stateFiles) { throw 'Published output unexpectedly contains user state or a portable marker.' }

    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $publishDirectory
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'DevServerManager\README.md') -Destination (Join-Path $publishDirectory 'USER-GUIDE.md')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\releases.md') -Destination (Join-Path $publishDirectory 'RELEASE-GUIDE.md')
    Get-ChildItem -LiteralPath $publishDirectory | Copy-Item -Destination $portableDirectory -Recurse
    Set-Content -LiteralPath (Join-Path $portableDirectory 'portable.flag') -Value 'Keep this file beside DevServerManager.exe to store settings in this folder.' -Encoding utf8
    $zipPath = Join-Path $packageDirectory "DevServerManager-$Version-Portable-x64.zip"
    Compress-Archive -LiteralPath $portableDirectory -DestinationPath $zipPath -CompressionLevel Optimal

    # Inspect the actual ZIP, not just the staging folder.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($required in @('DevServerManager.exe', 'portable.flag', 'coreclr.dll', 'e_sqlite3.dll')) {
            if (-not ($archive.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq "DevServerManager/$required" })) {
                throw "Portable ZIP is missing $required."
            }
        }
        if ($archive.Entries | Where-Object { $_.Name -like '*.db' -or $_.Name -like '*.db-*' }) {
            throw 'Portable ZIP contains user state.'
        }
    }
    finally { $archive.Dispose() }

    if (-not $SkipInstaller) {
        & $IsccPath /Q "/DAppVersion=$Version" "/DPublishDir=$publishDirectory" "/DOutputDir=$packageDirectory" `
            (Join-Path $repositoryRoot 'installer\DevServerManager.iss')
        if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
        if (-not (Test-Path -LiteralPath (Join-Path $packageDirectory "DevServerManager-$Version-Setup-x64.exe"))) {
            throw 'Installer compiler did not create the expected EXE.'
        }
    }
    $checksums = Get-ChildItem -LiteralPath $packageDirectory -File | Sort-Object Name | ForEach-Object {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
    }
    Set-Content -LiteralPath (Join-Path $packageDirectory 'SHA256SUMS.txt') -Value $checksums -Encoding ascii
    New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
    Move-Item -LiteralPath $packageDirectory -Destination $releaseDirectory
    Write-Host "Packages ready: $releaseDirectory"
}
finally {
    # Delete only this invocation's GUID directory, after verifying its absolute parent.
    $resolvedWork = [IO.Path]::GetFullPath($workDirectory)
    $resolvedRoot = [IO.Path]::GetFullPath($workRoot)
    if ([IO.Path]::GetDirectoryName($resolvedWork) -ne $resolvedRoot) {
        throw 'Refusing to clean a staging directory outside artifacts/.staging.'
    }
    if (Test-Path -LiteralPath $resolvedWork) { Remove-Item -LiteralPath $resolvedWork -Recurse -Force }
}
