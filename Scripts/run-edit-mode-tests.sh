#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_VERSION="$(awk '/m_EditorVersion:/ { print $2; exit }' "$PROJECT_PATH/ProjectSettings/ProjectVersion.txt")"

if unity status --project-path "$PROJECT_PATH" --format json >/dev/null 2>&1; then
  unity command "test run" \
    --project-path "$PROJECT_PATH" \
    --mode editor \
    --timeout 600 \
    --format json |
    python3 -c '
import json
import sys

payload = json.load(sys.stdin)
print(json.dumps(payload, ensure_ascii=False))
summary = payload.get("Summary")
if not isinstance(summary, dict):
    print("EditMode test summary is missing.", file=sys.stderr)
    sys.exit(1)

total = summary.get("Total", 0)
passed = summary.get("Passed", 0)
failed = summary.get("Failed", -1)
inconclusive = summary.get("Inconclusive", -1)
print(
    f"EditMode: total={total}, passed={passed}, failed={failed}, inconclusive={inconclusive}",
    file=sys.stderr,
)
sys.exit(1 if not total or failed or inconclusive else 0)
'
else
  mkdir -p "$PROJECT_PATH/TestResults"
  unity test "$PROJECT_PATH" \
    --editor-version "$UNITY_VERSION" \
    --mode EditMode \
    --report-format junit \
    --output "$PROJECT_PATH/TestResults/EditMode.xml" \
    --timeout 600 \
    -- -buildTarget iOS
fi
