[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [switch] $SkipSmokeTest
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'RestClient.sln'
$uiProject = Join-Path $root 'src/RestClient.UI/RestClient.UI.csproj'
$distRoot = Join-Path $root 'dist'
$runtimes = @('win-x64', 'linux-x64')

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
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            Compress-Archive -Path (Join-Path $SourceDir '*') -DestinationPath $ZipPath -Force
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

    $exe = Join-Path $PublishDir 'RestClient.exe'
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

if (Test-Path $distRoot) {
    Remove-Item $distRoot -Recurse -Force
}

Invoke-DotNet @('restore', $solution)
Invoke-DotNet @('build', $solution, '-c', $Configuration, '--no-restore')
Invoke-DotNet @('test', $solution, '-c', $Configuration, '--no-build')

foreach ($runtime in $runtimes) {
    $publishDir = Join-Path $distRoot $runtime
    Invoke-DotNet @(
        'publish', $uiProject,
        '-c', $Configuration,
        '-r', $runtime,
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-o', $publishDir
    )

    $zipPath = Join-Path $distRoot "RestClient-$runtime.zip"
    New-ReleaseZip -SourceDir $publishDir -ZipPath $zipPath

    $isHostWindows = $env:OS -eq 'Windows_NT'
    if (-not $SkipSmokeTest -and $isHostWindows -and $runtime -eq 'win-x64') {
        if (-not (Test-PublishedApp -PublishDir $publishDir)) {
            throw "Smoke test failed for ${runtime}: the app did not create data/app.db and a log file beside the executable."
        }
        Write-Host "Smoke test passed for $runtime."
    }
}

Write-Host "Build complete. Output in $distRoot."
