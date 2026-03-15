#!/bin/zsh

set -euo pipefail

SCRIPT_DIR=${0:A:h}
REPO_DIR=${SCRIPT_DIR:h}
REPORT_DIR="$REPO_DIR/out/validation"
REPORT_PATH="$REPORT_DIR/mac-validation-report.txt"
SMOKE_OUTPUT_PATH="$REPORT_DIR/iusageanalyzer-smoke.json"

mkdir -p "$REPORT_DIR"
exec > >(tee "$REPORT_PATH") 2>&1

timestamp() {
  date "+%Y-%m-%d %H:%M:%S"
}

run_step() {
  local name="$1"
  shift
  echo
  echo "[$(timestamp)] START: $name"
  "$@"
  echo "[$(timestamp)] PASS: $name"
}

cd "$REPO_DIR"

echo "Mac validation started at $(timestamp)"
echo "Repository: $REPO_DIR"
echo "Report: $REPORT_PATH"

run_step \
  "Build CLI" \
  dotnet build src/CodeUsageMap.Cli/CodeUsageMap.Cli.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal

run_step \
  "Build Integration Tests" \
  dotnet build tests/CodeUsageMap.Integration.Tests/CodeUsageMap.Integration.Tests.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal

run_step \
  "Build Representative Sample" \
  sh -c 'cd samples/RepresentativeSample && dotnet build RepresentativeSample.sln --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal'

run_step \
  "Build Metadata Normalization Probe" \
  dotnet build tools/CodeUsageMap.MetadataNormalizationProbe/CodeUsageMap.MetadataNormalizationProbe.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal

run_step \
  "Run Metadata Normalization Probe" \
  dotnet tools/CodeUsageMap.MetadataNormalizationProbe/bin/Debug/net9.0/CodeUsageMap.MetadataNormalizationProbe.dll

run_step \
  "Build Serialization Probe" \
  dotnet build tools/CodeUsageMap.SerializationProbe/CodeUsageMap.SerializationProbe.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal

run_step \
  "Run Serialization Probe" \
  dotnet tools/CodeUsageMap.SerializationProbe/bin/Debug/net9.0/CodeUsageMap.SerializationProbe.dll

run_step \
  "Build Snapshot Regression Probe" \
  dotnet build tools/CodeUsageMap.SnapshotRegressionProbe/CodeUsageMap.SnapshotRegressionProbe.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal

run_step \
  "Run Snapshot Regression Probe" \
  dotnet tools/CodeUsageMap.SnapshotRegressionProbe/bin/Debug/net9.0/CodeUsageMap.SnapshotRegressionProbe.dll

run_step \
  "Build Graph Canvas Probe" \
  dotnet build tools/CodeUsageMap.GraphCanvasProbe/CodeUsageMap.GraphCanvasProbe.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal

run_step \
  "Run Graph Canvas Probe" \
  dotnet tools/CodeUsageMap.GraphCanvasProbe/bin/Debug/net9.0/CodeUsageMap.GraphCanvasProbe.dll

run_step \
  "Build DI Probe" \
  dotnet build tools/CodeUsageMap.DiProbe/CodeUsageMap.DiProbe.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal

run_step \
  "Run DI Probe" \
  dotnet tools/CodeUsageMap.DiProbe/bin/Debug/net9.0/CodeUsageMap.DiProbe.dll

run_step \
  "Build Node Assessment Probe" \
  dotnet build tools/CodeUsageMap.NodeAssessmentProbe/CodeUsageMap.NodeAssessmentProbe.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal

run_step \
  "Run Node Assessment Probe" \
  dotnet tools/CodeUsageMap.NodeAssessmentProbe/bin/Debug/net9.0/CodeUsageMap.NodeAssessmentProbe.dll

run_step \
  "Build Representative Sample Probe" \
  dotnet build tools/CodeUsageMap.RepresentativeSampleProbe/CodeUsageMap.RepresentativeSampleProbe.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal

run_step \
  "Run Representative Sample Probe" \
  dotnet tools/CodeUsageMap.RepresentativeSampleProbe/bin/Debug/net9.0/CodeUsageMap.RepresentativeSampleProbe.dll

run_step \
  "Build Edge Kind Probe" \
  dotnet build tools/CodeUsageMap.EdgeKindProbe/CodeUsageMap.EdgeKindProbe.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal

run_step \
  "Run Edge Kind Probe" \
  dotnet tools/CodeUsageMap.EdgeKindProbe/bin/Debug/net9.0/CodeUsageMap.EdgeKindProbe.dll

run_step \
  "Build Presentation Consistency Probe" \
  dotnet build tools/CodeUsageMap.PresentationConsistencyProbe/CodeUsageMap.PresentationConsistencyProbe.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false -v minimal

run_step \
  "Run Presentation Consistency Probe" \
  dotnet tools/CodeUsageMap.PresentationConsistencyProbe/bin/Debug/net9.0/CodeUsageMap.PresentationConsistencyProbe.dll

run_step \
  "CLI Smoke" \
  dotnet src/CodeUsageMap.Cli/bin/Debug/net9.0/CodeUsageMap.Cli.dll analyze \
    --solution "$REPO_DIR/CodeUsageMap.sln" \
    --symbol CodeUsageMap.Contracts.Analysis.IUsageAnalyzer \
    --format json \
    --output "$SMOKE_OUTPUT_PATH" \
    --workspace-loader adhoc \
    --depth 2

echo
echo "Mac validation finished at $(timestamp)"
echo "Smoke output: $SMOKE_OUTPUT_PATH"
echo "Report written to: $REPORT_PATH"
