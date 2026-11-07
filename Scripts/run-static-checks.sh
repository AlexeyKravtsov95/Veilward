#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_PATH"

python3 Scripts/tests/test_quality_scripts.py
python3 Scripts/check_unity_project.py "$PROJECT_PATH"
