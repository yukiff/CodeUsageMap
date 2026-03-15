param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$SolutionPath = "",
    [switch]$LaunchExperimentalInstance
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($SolutionPath)) {
    $SolutionPath = Join-Path $RepoRoot "CodeUsageMap.sln"
}

$reportDir = Join-Path $RepoRoot "out\validation\windows"
$reportPath = Join-Path $reportDir "windows-ui-smoke.txt"
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null

Start-Transcript -Path $reportPath -Force | Out-Null
try {
    Write-Host "Windows UI smoke started at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Host "Solution: $SolutionPath"

    if ($LaunchExperimentalInstance) {
        $devenv = Get-Command devenv.exe -ErrorAction Stop
        Start-Process -FilePath $devenv.Source -ArgumentList "/RootSuffix Exp", $SolutionPath
        Write-Host "[PASS] Experimental Instance launch requested."
    }
    else {
        Write-Host "[INFO] Launch skipped. Use -LaunchExperimentalInstance to open the Experimental Instance."
    }

    Write-Host ""
    Write-Host "Manual smoke checklist:"
    Write-Host "1. Command appears in the editor context menu."
    Write-Host "2. Tool Window opens."
    Write-Host "3. Root preview renders before full graph."
    Write-Host "4. Refresh works."
    Write-Host "5. Cancel updates the status."
    Write-Host "6. Graph canvas supports zoom, pan, minimap, collapse, reroot, and display mode switching."
}
finally {
    Write-Host ""
    Write-Host "Windows UI smoke finished at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Host "Report written to: $reportPath"
    Stop-Transcript | Out-Null
}
