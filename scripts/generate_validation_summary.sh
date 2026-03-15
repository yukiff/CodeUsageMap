#!/bin/zsh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
MAC_REPORT="${1:-$REPO_DIR/out/validation/mac-validation-report.txt}"
WINDOWS_REPORT="${2:-$REPO_DIR/out/validation/windows/windows-validation-report.txt}"
OUTPUT_PATH="${3:-$REPO_DIR/out/validation/validation-summary.md}"

mkdir -p "${OUTPUT_PATH:h}"

summarize_report() {
  local label="$1"
  local path="$2"

  if [[ ! -f "$path" ]]; then
    echo "### $label"
    echo
    echo "- status: missing"
    echo "- path: $path"
    echo
    return
  fi

  local pass_count fail_count start_count
  pass_count=$(/usr/bin/grep -c "PASS:" "$path" || true)
  fail_count=$(/usr/bin/grep -Ec "FAIL:|ERROR|Unhandled exception" "$path" || true)
  start_count=$(/usr/bin/grep -c "START:" "$path" || true)

  echo "### $label"
  echo
  echo "- status: present"
  echo "- path: $path"
  echo "- steps started: $start_count"
  echo "- steps passed: $pass_count"
  echo "- failure markers: $fail_count"
  echo
  echo "Last lines:"
  echo
  echo '```text'
  /usr/bin/tail -n 8 "$path"
  echo '```'
  echo
}

{
  GIT_TOPLEVEL="$(git -C "$REPO_DIR" rev-parse --show-toplevel 2>/dev/null || true)"
  if [[ -n "$GIT_TOPLEVEL" && "$GIT_TOPLEVEL" == "$REPO_DIR" ]]; then
    BRANCH_NAME="$(git -C "$REPO_DIR" rev-parse --abbrev-ref HEAD 2>/dev/null || echo unknown)"
    GIT_STATUS_OUTPUT="$(git -C "$REPO_DIR" status --short 2>/dev/null || true)"
  else
    BRANCH_NAME="unavailable"
    GIT_STATUS_OUTPUT="git status unavailable for this workspace"
  fi

  echo "# Validation Summary"
  echo
  echo "- generatedAt: $(date '+%Y-%m-%d %H:%M:%S %Z')"
  echo "- repository: $REPO_DIR"
  echo "- branch: $BRANCH_NAME"
  echo
  echo "## Git Status"
  echo
  echo '```text'
  echo "$GIT_STATUS_OUTPUT"
  echo '```'
  echo

  echo "## Reports"
  echo
  summarize_report "macOS validation" "$MAC_REPORT"
  summarize_report "Windows validation" "$WINDOWS_REPORT"
} > "$OUTPUT_PATH"

echo "Validation summary written to: $OUTPUT_PATH"
