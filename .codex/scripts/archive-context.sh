#!/usr/bin/env bash
set -euo pipefail
STATE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../state" && pwd)"
mkdir -p "$STATE_DIR/history"
stamp="$(date +%Y%m%d-%H%M%S)"
out="$STATE_DIR/history/${stamp}-context.md"
{
  echo "# Archived Context $stamp"
  echo
  echo "## Current Task"
  cat "$STATE_DIR/CURRENT_TASK.md" 2>/dev/null || true
  echo
  echo "## Verification"
  cat "$STATE_DIR/VERIFICATION.md" 2>/dev/null || true
} > "$out"
echo "Archived context to $out"
