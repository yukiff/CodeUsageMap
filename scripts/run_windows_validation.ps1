param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$SolutionPath = "",
    [string]$VsixProjectPath = "",
    [string]$RepresentativeSampleSolutionPath = "",
    [string]$Net48SolutionPath = "",
    [switch]$LaunchExperimentalInstance
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($SolutionPath)) {
    $SolutionPath = Join-Path $RepoRoot "CodeUsageMap.sln"
}

if ([string]::IsNullOrWhiteSpace($VsixProjectPath)) {
    $VsixProjectPath = Join-Path $RepoRoot "src\CodeUsageMap.Vsix\CodeUsageMap.Vsix.csproj"
}

if ([string]::IsNullOrWhiteSpace($RepresentativeSampleSolutionPath)) {
    $RepresentativeSampleSolutionPath = Join-Path $RepoRoot "samples\RepresentativeSample\RepresentativeSample.sln"
}

$reportDir = Join-Path $RepoRoot "out\validation\windows"
$reportPath = Join-Path $reportDir "windows-validation-report.txt"
$smokeOutputPath = Join-Path $reportDir "representative-smoke.json"

New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
Start-Transcript -Path $reportPath -Force | Out-Null

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] START: $Name"
    & $Action
    Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] PASS: $Name"
}

function Assert-PathExists {
    param([string]$PathValue)
    if (-not (Test-Path $PathValue)) {
        throw "Path not found: $PathValue"
    }
}

Write-Host "Windows validation started at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host "Repository: $RepoRoot"
Write-Host "Report: $reportPath"

Assert-PathExists $SolutionPath
Assert-PathExists $VsixProjectPath
Assert-PathExists $RepresentativeSampleSolutionPath

Push-Location $RepoRoot
try {
    Invoke-Step "Build CLI" {
        dotnet build "src\CodeUsageMap.Cli\CodeUsageMap.Cli.csproj" --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal
    }

    Invoke-Step "Build Representative Sample" {
        dotnet build $RepresentativeSampleSolutionPath --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal
    }

    Invoke-Step "Build VSIX" {
        dotnet build $VsixProjectPath --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal
    }

    Invoke-Step "Representative Sample Smoke (MSBuildWorkspace)" {
        dotnet "src\CodeUsageMap.Cli\bin\Debug\net9.0\CodeUsageMap.Cli.dll" analyze `
            --solution $RepresentativeSampleSolutionPath `
            --symbol "M:Representative.Core.IWorkflow.ExecuteAsync" `
            --format json `
            --output $smokeOutputPath `
            --workspace-loader msbuild `
            --depth 2
    }

    if (-not [string]::IsNullOrWhiteSpace($Net48SolutionPath) -and (Test-Path $Net48SolutionPath)) {
        Invoke-Step ".NET Framework 4.8 Solution Build" {
            dotnet build $Net48SolutionPath --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal
        }
    }
    else {
        Write-Host "[INFO] .NET Framework 4.8 sample solution was not provided."
    }

    if ($LaunchExperimentalInstance) {
        Invoke-Step "Launch Experimental Instance" {
            $devenv = Get-Command devenv.exe -ErrorAction Stop
            Start-Process -FilePath $devenv.Source -ArgumentList "/RootSuffix Exp", $SolutionPath
        }
    }
    else {
        Write-Host "[INFO] Experimental Instance launch was skipped. Use -LaunchExperimentalInstance to enable it."
    }
}
finally {
    Pop-Location
    Write-Host ""
    Write-Host "Windows validation finished at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Host "Smoke output: $smokeOutputPath"
    Write-Host "Report written to: $reportPath"
    Stop-Transcript | Out-Null
}
