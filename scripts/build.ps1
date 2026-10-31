[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    # win-x64 builds on any host. linux-x64 builds only on a Linux host (the CI Linux job), where it produces a .tar.gz that
    # keeps the executable permission; a zip made on Windows cannot. On Windows, linux-x64 is skipped with a warning.
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

function Invoke-Native {
    param([string] $FilePath, [string[]] $Arguments)

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
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

function New-ReleaseTarGz {
    param(
        [string] $SourceDir,
        [string] $TarGzPath
    )

    # Directories 755, files 644, the program 755: the package carries the executable bit that a Windows-made zip lacks.
    # GNU tar: sorted entries and numeric owner 0 keep the content independent of the build user.
    Invoke-Native -FilePath 'chmod' -Arguments @('-R', 'u=rwX,go=rX', $SourceDir)
    Invoke-Native -FilePath 'chmod' -Arguments @('755', (Join-Path $SourceDir 'RestDraft'))
    Invoke-Native -FilePath 'tar' -Arguments @('--sort=name', '--owner=0', '--group=0', '--numeric-owner', '-czf', $TarGzPath, '-C', $SourceDir, '.')
}

function Test-PublishedApp {
    param(
        [string] $PublishDir,
        [string] $ExeName = 'RestDraft.exe'
    )

    $exe = Join-Path $PublishDir $ExeName
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

function Test-PublishedAppOnLinux {
    param([string] $PublishDir)

    # The app needs an X display. Xvfb provides a virtual one, so the smoke test also runs on a machine without a screen.
    if (-not (Get-Command Xvfb -ErrorAction SilentlyContinue)) {
        throw 'The Linux smoke test needs Xvfb (sudo apt-get install xvfb). Pass -SkipSmokeTest to build without it.'
    }

    $display = ':99'
    $xvfb = Start-Process -FilePath 'Xvfb' -ArgumentList @($display, '-screen', '0', '1280x720x24', '-nolisten', 'tcp') -PassThru
    $previousDisplay = $env:DISPLAY
    $env:DISPLAY = $display
    try {
        Start-Sleep -Seconds 1
        return (Test-PublishedApp -PublishDir $PublishDir -ExeName 'RestDraft')
    }
    finally {
        $env:DISPLAY = $previousDisplay
        if (-not $xvfb.HasExited) {
            Stop-Process -Id $xvfb.Id -Force
        }
    }
}

# Extracts the package like a user would, starts the program from it, and returns true when data/app.db and a log appear.
function Test-ReleasePackage {
    param(
        [string] $Runtime,
        [string] $PackagePath,
        [string] $SmokeDir
    )

    if ($Runtime -like 'linux-*') {
        New-Item -ItemType Directory -Path $SmokeDir -Force | Out-Null
        Invoke-Native -FilePath 'tar' -Arguments @('-xzf', $PackagePath, '-C', $SmokeDir)
        return (Test-PublishedAppOnLinux -PublishDir $SmokeDir)
    }

    Expand-Archive -Path $PackagePath -DestinationPath $SmokeDir -Force
    return (Test-PublishedApp -PublishDir $SmokeDir)
}

Invoke-DotNet @('restore', $solution)
Test-DependencyLicenses -Root $root -AllowedLicenses $allowedLicenses -ReviewedLicenses $reviewedLicenses
Invoke-DotNet @('build', $solution, '-c', $Configuration, '--no-restore')
Invoke-DotNet @('test', $solution, '-c', $Configuration, '--no-build')

$isHostWindows = $env:OS -eq 'Windows_NT'
$packagePaths = @()
foreach ($runtime in $Runtimes) {
    $isLinuxRuntime = $runtime -like 'linux-*'
    if ($isLinuxRuntime -and $isHostWindows) {
        Write-Warning "Skipping ${runtime}: the Linux package is built on a Linux host, because a package made on Windows cannot keep the executable permission."
        continue
    }

    # Clean only this runtime, so a package for another runtime survives.
    $publishDir = Join-Path $distRoot $runtime
    $packageExtension = '.zip'
    if ($isLinuxRuntime) {
        $packageExtension = '.tar.gz'
    }
    $packagePath = Join-Path $distRoot "RestDraft-$runtime$packageExtension"
    foreach ($stale in @($publishDir, $packagePath)) {
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

    if ($isLinuxRuntime) {
        New-ReleaseTarGz -SourceDir $publishDir -TarGzPath $packagePath
    }
    else {
        New-ReleaseZip -SourceDir $publishDir -ZipPath $packagePath
    }
    $packagePaths += $packagePath

    $canSmokeTest = $isLinuxRuntime -or $isHostWindows
    if (-not $SkipSmokeTest -and $canSmokeTest) {
        # Smoke-test the extracted package, the same artifact a user receives.
        $smokeDir = Join-Path $distRoot "smoke-$runtime"
        try {
            if (-not (Test-ReleasePackage -Runtime $runtime -PackagePath $packagePath -SmokeDir $smokeDir)) {
                throw "Smoke test failed for ${runtime}: the app did not create data/app.db and a log file beside the executable."
            }
        }
        finally {
            Remove-Item $smokeDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        Write-Host "Smoke test passed for $runtime."
    }
}

if ($packagePaths.Count -eq 0) {
    Write-Warning 'No package was built, so SHA256SUMS.txt was not written.'
    return
}

# One checksum file covers every package built in this run, and is checked right after it is written.
$checksumPath = Join-Path $distRoot 'SHA256SUMS.txt'
Write-Checksums -PackagePaths $packagePaths -ChecksumPath $checksumPath
$mismatches = @(Get-ChecksumMismatches -ChecksumPath $checksumPath)
if ($mismatches.Count -gt 0) {
    throw "Checksum verification failed for: $($mismatches -join ', ')"
}
Get-Content -LiteralPath $checksumPath | ForEach-Object { Write-Host $_ }

Write-Host "Build complete. Output in $distRoot."
