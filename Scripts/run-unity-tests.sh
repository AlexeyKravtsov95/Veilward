#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ] || { [ "$1" != "EditMode" ] && [ "$1" != "PlayMode" ]; }; then
  echo "Usage: $0 EditMode|PlayMode" >&2
  exit 2
fi

TEST_MODE="$1"
PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_VERSION="$(awk '/m_EditorVersion:/ { print $2; exit }' "$PROJECT_PATH/ProjectSettings/ProjectVersion.txt")"
RESULT_DIR="$PROJECT_PATH/TestResults"
RESULT_PATH="$RESULT_DIR/$TEST_MODE.xml"
LOG_PATH="$RESULT_DIR/$TEST_MODE.log"
mkdir -p "$RESULT_DIR"
rm -f "$RESULT_PATH"

if unity status --project-path "$PROJECT_PATH" --format json >/dev/null 2>&1; then
  if [ "$TEST_MODE" = "PlayMode" ]; then
    CLI_MODE="playmode"
  else
    CLI_MODE="editor"
  fi

  RESULT=""
  UNITY_STATUS=0
  RUN_ARGS=(command "test run" --project-path "$PROJECT_PATH" --mode "$CLI_MODE" --timeout 600 --format json)
  if [ "$TEST_MODE" = "PlayMode" ]; then
    RUN_ARGS+=(--async-tests)
  fi
  RESULT="$(unity "${RUN_ARGS[@]}")" || UNITY_STATUS=$?
  if [ "$UNITY_STATUS" -ne 0 ]; then
    printf '%s\n' "$RESULT"
    exit "$UNITY_STATUS"
  fi

  if [ "$TEST_MODE" = "PlayMode" ]; then
    DEADLINE=$((SECONDS + 600))
    COMPLETED=0
    while [ "$SECONDS" -lt "$DEADLINE" ]; do
      UNITY_STATUS=0
      RESULT="$(unity command "test status" --project-path "$PROJECT_PATH" --format json)" || UNITY_STATUS=$?
      if [ "$UNITY_STATUS" -ne 0 ]; then
        printf '%s\n' "$RESULT"
        exit "$UNITY_STATUS"
      fi

      RUN_STATUS="$(printf '%s\n' "$RESULT" | python3 -c '
import json
import sys
payload = json.load(sys.stdin)
if not isinstance(payload, dict) or payload.get("success") is not True:
    raise SystemExit(2)
result = payload.get("result")
print(result.get("status", "running") if isinstance(result, dict) else "running")
')"
      if [ "$RUN_STATUS" = "completed" ]; then
        COMPLETED=1
        break
      fi
      if [ "$RUN_STATUS" = "failed" ] || [ "$RUN_STATUS" = "error" ]; then
        printf '%s\n' "$RESULT"
        exit 1
      fi
      sleep 2
    done
    if [ "$COMPLETED" -ne 1 ]; then
      echo "PlayMode tests did not complete within 600 seconds." >&2
      exit 1
    fi
  fi

  printf '%s\n' "$RESULT" | tee "$RESULT_DIR/$TEST_MODE.json"
  printf '%s\n' "$RESULT" | TEST_MODE="$TEST_MODE" python3 -c '
import json
import os
import sys

MODE = os.environ["TEST_MODE"]


def fail(message):
    print(f"Invalid {MODE} result: {message}", file=sys.stderr)
    sys.exit(1)


try:
    payload = json.load(sys.stdin)
except (json.JSONDecodeError, UnicodeDecodeError) as error:
    fail(f"malformed JSON ({error})")

if not isinstance(payload, dict) or payload.get("success") is not True:
    fail("missing successful Unity CLI status")
summary = payload.get("Summary")
result = None
if summary is None:
    data = payload.get("data")
    result = data.get("result") if isinstance(data, dict) else None
    summary = result.get("Summary") if isinstance(result, dict) else None
if not isinstance(summary, dict):
    result = payload.get("result")
    if isinstance(result, dict):
        summary = result.get("Summary") or result.get("summary")
        if MODE == "PlayMode" and result.get("status") != "completed":
            fail("asynchronous PlayMode run is not completed")
if not isinstance(summary, dict):
    fail("Summary is missing or is not an object")

normalized = {str(key).lower(): value for key, value in summary.items()}
fields = ("Total", "Passed", "Failed", "Inconclusive", "Skipped")
values = {}
for field in fields:
    value = normalized.get(field.lower())
    if type(value) is not int or value < 0:
        fail(f"Summary.{field} must be a non-negative integer")
    values[field] = value

print(f"{MODE}: {values}", file=sys.stderr)
if values["Total"] == 0:
    fail("Summary.Total must be greater than zero")
if values["Passed"] + values["Failed"] + values["Inconclusive"] + values["Skipped"] != values["Total"]:
    fail("Summary counts do not add up to Total")
if values["Passed"] != values["Total"] or any(values[field] for field in ("Failed", "Inconclusive", "Skipped")):
    sys.exit(1)
'
  exit "$?"
fi

if [ -f "$PROJECT_PATH/Library/Pipeline/.unity-pipeline-port" ]; then
  echo "Unity CLI is unavailable but this project has a live Editor descriptor." >&2
  echo "Install/use Unity CLI to drive that Editor; a second Editor will not be started." >&2
  exit 2
fi

UNITY_BIN="${UNITY_EDITOR_PATH:-}"
if [ -z "$UNITY_BIN" ] && [ "$(uname -s)" = "Darwin" ]; then
  UNITY_BIN="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
fi
if [ -z "$UNITY_BIN" ] || [ ! -x "$UNITY_BIN" ]; then
  echo "BLOCKED: Unity $UNITY_VERSION executable not found. Set UNITY_EDITOR_PATH." >&2
  exit 2
fi

UNITY_STATUS=0
"$UNITY_BIN" \
  -batchmode \
  -nographics \
  -projectPath "$PROJECT_PATH" \
  -buildTarget iOS \
  -runTests \
  -testPlatform "$TEST_MODE" \
  -testResults "$RESULT_PATH" \
  -logFile "$LOG_PATH" \
  -quit || UNITY_STATUS=$?

python3 "$PROJECT_PATH/Scripts/verify_unity_test_results.py" "$RESULT_PATH"
if [ "$UNITY_STATUS" -ne 0 ]; then
  exit "$UNITY_STATUS"
fi
