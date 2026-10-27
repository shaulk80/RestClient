# SHA-256 checksums of the release zips. Dot-sourced by build.ps1 after the zips are written.
# The file format is the one `sha256sum -c` reads: lowercase hash, two spaces, file name.

function Write-Checksums {
    param(
        [string[]] $ZipPaths,
        [string] $ChecksumPath
    )

    $lines = foreach ($zipPath in ($ZipPaths | Sort-Object)) {
        $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $(Split-Path -Leaf $zipPath)"
    }

    # UTF-8 without a byte order mark and LF line ends, so sha256sum reads the file on any platform.
    $text = ($lines -join "`n") + "`n"
    [System.IO.File]::WriteAllText($ChecksumPath, $text, (New-Object System.Text.UTF8Encoding($false)))
}

# Returns the names of files whose hash differs from the checksum file, or that are missing. Empty means all match.
function Get-ChecksumMismatches {
    param([string] $ChecksumPath)

    $directory = Split-Path -Parent $ChecksumPath
    $mismatches = @()
    foreach ($line in (Get-Content -LiteralPath $ChecksumPath | Where-Object { $_.Trim() -ne '' })) {
        $expected, $name = $line -split '\s+', 2
        $filePath = Join-Path $directory $name
        if (-not (Test-Path -LiteralPath $filePath)) {
            $mismatches += $name
            continue
        }

        $actual = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $expected) {
            $mismatches += $name
        }
    }

    return $mismatches
}
