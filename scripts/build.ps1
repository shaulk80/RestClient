[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    # linux-x64 is deferred to a later version (plan section 8.1). Pass -Runtimes win-x64,linux-x64 once that stage starts.
    [string[]] $Runtimes = @('win-x64'),
    [switch] $SkipSmokeTest
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'RestDraft.slnx'
$uiProject = Join-Path $root 'src/RestDraft.UI/RestDraft.UI.csproj'
$distRoot = Join-Path $root 'dist'

# Licenses accepted without review, as SPDX identifiers. "Public domain" from plan section 8 maps to Unlicense and CC0-1.0.
$allowedLicenses = @('MIT', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause', 'ISC', 'Unlicense', 'CC0-1.0')

# Packages whose nuspec names a license file instead of an SPDX expression. Each entry was checked by reading the file.
# Keyed by id and version, so an upgrade needs a new review.
$reviewedLicenses = @{
    'Avalonia.Angle.Windows.Natives/2.1.27548.20260419' = 'BSD-3-Clause'
}

. (Join-Path $PSScriptRoot 'LicenseCheck.ps1')
. (Join-Path $PSScriptRoot 'Checksums.ps1')

function Invoke-DotNet {
    param([string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function New-ReleaseZip {
    param(
        [string] $SourceDir,
        [string] $ZipPath,
        [int] $MaxAttempts = 5
    )

    # Antivirus or indexing can briefly lock a freshly published executable, so retry before failing.
    # Entry names are written with forward slashes. Windows PowerShell 5.1 (.NET Framework) and
    # Compress-Archive use backslashes, which Linux unzip tools extract as literal file names.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $sourceRoot = (Resolve-Path $SourceDir).Path.TrimEnd('\')
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            if (Test-Path $ZipPath) {
                Remove-Item $ZipPath -Force
            }
            $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, 'Create')
            try {
                Get-ChildItem $sourceRoot -Recurse -File | ForEach-Object {
                    $entryName = $_.FullName.Substring($sourceRoot.Length + 1).Replace('\', '/')
                    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entryName) | Out-Null
                }
            }
            finally {
                $zip.Dispose()
            }
            return
        }
        catch {
            if ($attempt -eq $MaxAttempts) {
                throw
            }
            Start-Sleep -Seconds 2
        }
    }
}

function Test-PublishedApp {
    param([string] $PublishDir)

    $exe = Join-Path $PublishDir 'RestDraft.exe'
    $databaseFile = Join-Path $PublishDir 'data/app.db'
    $logDir = Join-Path $PublishDir 'data/logs'

    $process = Start-Process -FilePath $exe -WorkingDirectory $PublishDir -PassThru
    try {
        $deadline = (Get-Date).AddSeconds(20)
        while (-not (Test-Path $databaseFile) -and -not $process.HasExited -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 250
        }

        $logWritten = (Test-Path $logDir) -and ((Get-ChildItem $logDir -File).Count -gt 0)
        $started = (Test-Path $databaseFile) -and $logWritten -and -not $process.HasExited
        return $started
    }
    finally {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }
}

Invoke-DotNet @('restore', $solution)
Test-DependencyLicenses -Root $root -AllowedLicenses $allowedLicenses -ReviewedLicenses $reviewedLicenses
Invoke-DotNet @('build', $solution, '-c', $Configuration, '--no-restore')
Invoke-DotNet @('test', $solution, '-c', $Configuration, '--no-build')

$zipPaths = @()
foreach ($runtime in $Runtimes) {
    # Clean only this runtime, so a release zip for another runtime survives.
    $publishDir = Join-Path $distRoot $runtime
    $zipPath = Join-Path $distRoot "RestDraft-$runtime.zip"
    foreach ($stale in @($publishDir, $zipPath)) {
        if (Test-Path $stale) {
            Remove-Item $stale -Recurse -Force
        }
    }

    Invoke-DotNet @(
        'publish', $uiProject,
        '-c', $Configuration,
        '-r', $runtime,
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        # Debug symbols are about 100 MB uncompressed and are not needed by users.
        '-p:DebugType=none',
        '-p:DebugSymbols=false',
        '-o', $publishDir
    )

    # Native packages (SkiaSharp, HarfBuzz) bring their own .pdb files, which DebugType=none does not remove.
    Get-ChildItem -LiteralPath $publishDir -Recurse -Filter '*.pdb' | Remove-Item -Force

    New-ReleaseZip -SourceDir $publishDir -ZipPath $zipPath
    $zipPaths += $zipPath

    $isHostWindows = $env:OS -eq 'Windows_NT'
    if (-not $SkipSmokeTest -and $isHostWindows -and $runtime -eq 'win-x64') {
        # Smoke-test the extracted zip, the same artifact a user receives.
        $smokeDir = Join-Path $distRoot "smoke-$runtime"
        Expand-Archive -Path $zipPath -DestinationPath $smokeDir -Force
        try {
            if (-not (Test-PublishedApp -PublishDir $smokeDir)) {
                throw "Smoke test failed for ${runtime}: the app did not create data/app.db and a log file beside the executable."
            }
        }
        finally {
            Remove-Item $smokeDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        Write-Host "Smoke test passed for $runtime."
    }
}

# One checksum file covers every zip built in this run, and is checked right after it is written.
$checksumPath = Join-Path $distRoot 'SHA256SUMS.txt'
Write-Checksums -ZipPaths $zipPaths -ChecksumPath $checksumPath
$mismatches = @(Get-ChecksumMismatches -ChecksumPath $checksumPath)
if ($mismatches.Count -gt 0) {
    throw "Checksum verification failed for: $($mismatches -join ', ')"
}
Get-Content -LiteralPath $checksumPath | ForEach-Object { Write-Host $_ }

Write-Host "Build complete. Output in $distRoot."
