"""Run full #92 gates in the existing Editor and restore only known builder outputs."""
import base64
import gzip
import json
import os
import subprocess
import sys
from pathlib import Path

sys.dont_write_bytecode = True
import run_vfx1b_gates as shared

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Docs/review/VFX.1c/reports"
shared.OUT = OUT


def main():
    prior = json.loads((ROOT / "Docs/review/VFX.1b/reports/gates-byte-audit.json").read_text())
    initial = json.loads((OUT / "initial-audit.json").read_text())
    paths = {row["path"] for row in prior["changed"]} | set(initial["renderCarry"])
    saved = {path: (ROOT / path).read_bytes() for path in sorted(paths)}
    backup = ROOT / "TestResults/vfx1c/gate-serialized-before.json"
    backup.write_text(json.dumps({path: base64.b64encode(data).decode() for path, data in saved.items()}))
    before = shared.inputs()
    tested_source = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    results = {}
    environment = dict(os.environ, PYTHONDONTWRITEBYTECODE="1")
    try:
        for name, command, raw in (
            ("static", "./Scripts/run-static-checks.sh", None),
            ("edit-mode", "./Scripts/run-edit-mode-tests.sh", "EditMode.json"),
            ("play-mode", "./Scripts/run-play-mode-tests.sh", "PlayMode.json"),
        ):
            print(command, flush=True)
            raw_path = ROOT / "TestResults" / raw if raw else None
            if raw_path and raw_path.exists():
                previous = raw_path.stat().st_mtime_ns
            else:
                previous = None
            with (OUT / (name + "-command.txt")).open("w") as log:
                log.write(command + "\n")
                log.flush()
                results[name] = subprocess.run([command], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, env=environment).returncode
            if raw_path and raw_path.exists() and raw_path.stat().st_mtime_ns != previous:
                with (OUT / (name + ".json.gz")).open("wb") as output:
                    output.write(gzip.compress(raw_path.read_bytes(), mtime=0))
            print(name, results[name], flush=True)
    finally:
        report = shared.restore_known_inputs(saved, before, shared.inputs())
        final = shared.inputs()
        report.update(
            testedSource=tested_source, results=results,
            carryInSha256={path: final.get(path) for path in initial["renderCarry"]},
        )
        (OUT / "gates-byte-audit.json").write_text(json.dumps(report, indent=2) + "\n")
    if (set(results.values()) != {0} or report["unexpected"] or report["newInputs"] or
        not report["allOriginalInputsRestored"] or report["recoveryNeeded"] or
        report["carryInSha256"] != initial["renderCarry"]):
        raise SystemExit("Gates or byte restoration failed. See reports.")


if __name__ == "__main__":
    main()
