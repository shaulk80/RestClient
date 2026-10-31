# Coverage targets. Dot-sourced by build.ps1 after the tests have run.
# A missed target is a warning, never a failure: it must not block a build or a release. The tests themselves still
# have to pass. The reports are written by coverlet.msbuild (see Directory.Build.props) to artifacts/coverage/<project>.

function Write-CoverageWarning {
    param([string] $Message)

    Write-Warning $Message
    if ($env:GITHUB_ACTIONS -eq 'true') {
        # A workflow command: GitHub shows it as an annotation on the run.
        Write-Host "::warning title=Coverage::$Message"
    }
}

# $Targets maps a test project name to the minimum line coverage (percent) of the module it measures.
function Test-CoverageTargets {
    param(
        [string] $CoverageRoot,
        [hashtable] $Targets
    )

    foreach ($project in ($Targets.Keys | Sort-Object)) {
        $target = $Targets[$project]
        $reportPath = Join-Path (Join-Path $CoverageRoot $project) 'coverage.cobertura.xml'
        if (-not (Test-Path -LiteralPath $reportPath)) {
            Write-CoverageWarning "No coverage report for $project (expected $reportPath). Coverage was not checked."
            continue
        }

        [xml] $report = Get-Content -Raw -LiteralPath $reportPath
        $lineRate = [double]::Parse($report.coverage.'line-rate', [System.Globalization.CultureInfo]::InvariantCulture)
        $percent = [math]::Round($lineRate * 100, 2)
        if ($percent -lt $target) {
            Write-CoverageWarning "Line coverage of $project is $percent%, below the target of $target%. If the tests all passed and it reads 0%, coverlet may have collected no hits; run the build again."
        }
        else {
            Write-Host "Coverage OK: $project $percent% (target $target%)."
        }
    }
}
