#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_VERSION="$(awk '/m_EditorVersion:/ { print $2; exit }' "$PROJECT_PATH/ProjectSettings/ProjectVersion.txt")"

if unity status --project-path "$PROJECT_PATH" --format json >/dev/null 2>&1; then
  RESULT=""
  UNITY_STATUS=0
  RESULT="$(unity command "test run" \
    --project-path "$PROJECT_PATH" \
    --mode editor \
    --timeout 600 \
    --format json)" || UNITY_STATUS=$?

  if [ "$UNITY_STATUS" -ne 0 ]; then
    printf '%s\n' "$RESULT"
    exit "$UNITY_STATUS"
  fi

  printf '%s\n' "$RESULT" | python3 -c '
import json
import sys


def fail(message):
    print(f"Invalid EditMode result: {message}", file=sys.stderr)
    sys.exit(1)


try:
    payload = json.load(sys.stdin)
except (json.JSONDecodeError, UnicodeDecodeError) as error:
    fail(f"malformed JSON ({error})")

print(json.dumps(payload, ensure_ascii=False))
if not isinstance(payload, dict) or payload.get("success") is not True:
    fail("missing successful Unity CLI status")

summary = payload.get("Summary")
if summary is None:
    data = payload.get("data")
    result = data.get("result") if isinstance(data, dict) else None
    summary = result.get("Summary") if isinstance(result, dict) else None
if not isinstance(summary, dict):
    fail("Summary is missing or is not an object")

fields = ("Total", "Passed", "Failed", "Inconclusive", "Skipped")
values = {}
for field in fields:
    value = summary.get(field)
    if type(value) is not int or value < 0:
        fail(f"Summary.{field} must be a non-negative integer")
    values[field] = value

total = values["Total"]
passed = values["Passed"]
failed = values["Failed"]
inconclusive = values["Inconclusive"]
skipped = values["Skipped"]
print(
    f"EditMode: total={total}, passed={passed}, failed={failed}, "
    f"inconclusive={inconclusive}, skipped={skipped}",
    file=sys.stderr,
)

if total == 0:
    fail("Summary.Total must be greater than zero")
if passed + failed + inconclusive + skipped != total:
    fail("Summary counts do not add up to Total")
if passed != total or failed != 0 or inconclusive != 0 or skipped != 0:
    sys.exit(1)
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
