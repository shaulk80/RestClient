# Dependency license check. Dot-sourced by build.ps1 after restore.
# Reads the packages in every project.assets.json under src and tests, and their nuspec files in the NuGet package folder.

function Get-RestoredPackages {
    param([string] $Root)

    $assetsFiles = @(Get-ChildItem -Path (Join-Path $Root 'src'), (Join-Path $Root 'tests') -Recurse -Filter project.assets.json |
        Where-Object { $_.FullName -match '[\\/]obj[\\/]' })
    if ($assetsFiles.Count -eq 0) {
        throw 'No project.assets.json found under src or tests. Run restore first.'
    }

    $assetsList = @($assetsFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json })
    $packageFolder = @($assetsList[0].packageFolders.PSObject.Properties)[0].Name

    $packageKeys = @{}
    foreach ($assets in $assetsList) {
        foreach ($library in $assets.libraries.PSObject.Properties) {
            if ($library.Value.type -eq 'package') {
                $packageKeys[$library.Name] = $true
            }
        }
    }

    foreach ($key in ($packageKeys.Keys | Sort-Object)) {
        $id, $version = $key -split '/', 2
        $folderName = $id.ToLowerInvariant()
        $nuspecPath = Join-Path $packageFolder (Join-Path $folderName (Join-Path $version.ToLowerInvariant() "$folderName.nuspec"))
        [pscustomobject]@{ Id = $id; Version = $version; NuspecPath = $nuspecPath }
    }
}

function Get-NuspecLicense {
    param([string] $NuspecPath)

    [xml] $nuspec = Get-Content -Raw -LiteralPath $NuspecPath
    $metadata = '/*[local-name()="package"]/*[local-name()="metadata"]'

    $license = $nuspec.SelectSingleNode("$metadata/*[local-name()='license']")
    if ($license) {
        return [pscustomobject]@{ Type = $license.type; Value = $license.InnerText }
    }

    $licenseUrl = $nuspec.SelectSingleNode("$metadata/*[local-name()='licenseUrl']")
    if ($licenseUrl) {
        return [pscustomobject]@{ Type = 'url'; Value = $licenseUrl.InnerText }
    }

    return [pscustomobject]@{ Type = 'none'; Value = '' }
}

function Test-LicenseExpression {
    param([string] $Expression, [string[]] $AllowedLicenses)

    # "A AND B" needs every term allowed. "A OR B" needs one option allowed.
    foreach ($term in $Expression -split '\s+AND\s+') {
        $options = $term -split '\s+OR\s+'
        if (-not ($options | Where-Object { $AllowedLicenses -contains $_ })) {
            return $false
        }
    }

    return $true
}

function Get-LicenseStatus {
    param([pscustomobject] $Package, [string[]] $AllowedLicenses, [hashtable] $ReviewedLicenses)

    $key = "$($Package.Id)/$($Package.Version)"
    if ($ReviewedLicenses.ContainsKey($key)) {
        return [pscustomobject]@{ License = "$($ReviewedLicenses[$key]) (reviewed)"; Allowed = $true }
    }

    $license = Get-NuspecLicense -NuspecPath $Package.NuspecPath
    switch ($license.Type) {
        'expression' {
            $allowed = Test-LicenseExpression -Expression $license.Value -AllowedLicenses $AllowedLicenses
            return [pscustomobject]@{ License = $license.Value; Allowed = $allowed }
        }
        'file' {
            return [pscustomobject]@{ License = 'license file, not reviewed'; Allowed = $false }
        }
        'url' {
            return [pscustomobject]@{ License = "license URL only: $($license.Value)"; Allowed = $false }
        }
        default {
            return [pscustomobject]@{ License = 'no license declared'; Allowed = $false }
        }
    }
}

function Test-DependencyLicenses {
    param([string] $Root, [string[]] $AllowedLicenses, [hashtable] $ReviewedLicenses)

    $packages = @(Get-RestoredPackages -Root $Root)
    $results = @(foreach ($package in $packages) {
        $status = Get-LicenseStatus -Package $package -AllowedLicenses $AllowedLicenses -ReviewedLicenses $ReviewedLicenses
        [pscustomobject]@{
            Package = "$($package.Id) $($package.Version)"
            License = $status.License
            Allowed = $status.Allowed
        }
    })

    foreach ($result in $results) {
        $state = if ($result.Allowed) { 'OK' } else { 'FAIL' }
        Write-Host ("{0,-5} {1,-65} {2}" -f $state, $result.Package, $result.License)
    }

    $failures = @($results | Where-Object { -not $_.Allowed })
    if ($failures.Count -gt 0) {
        $names = ($failures | ForEach-Object { $_.Package }) -join ', '
        throw "License check failed for $($failures.Count) package(s): $names. Review the license and add it to the reviewed list, or change the dependency."
    }

    Write-Host "License check passed: $($results.Count) packages."
}
