"""Run the full gates sequentially in the existing Editor; restore known generated outputs."""
import gzip
import json
import os
from pathlib import Path
import subprocess
import sys

sys.dont_write_bytecode = True
import run_vfx1b_gates as shared

ROOT = Path(__file__).resolve().parents[2]
PRIVATE_OUTPUT = ROOT / "TestResults/vfx1d"
PRIOR = ROOT / "Docs/review/VFX.1c/reports"
COMMANDS = (
    ("static", "./Scripts/run-static-checks.sh", None),
    ("edit-mode", "./Scripts/run-edit-mode-tests.sh", "EditMode.json"),
    ("play-mode", "./Scripts/run-play-mode-tests.sh", "PlayMode.json"),
)


def main():
    name = sys.argv[1] if len(sys.argv) == 2 else "gates-final"
    if not name.replace("-", "").isalnum():
        raise ValueError("Pass an unused attempt name.")
    output = PRIVATE_OUTPUT / name
    output.mkdir(parents=True)
    shared.OUT = output
    historical = json.loads((PRIOR / "gates-byte-audit.json").read_bytes())
    carry = set(historical["carryInSha256"])
    known = {row["path"] for row in historical["changed"]} | carry
    saved = {path: (ROOT / path).read_bytes() for path in known}
    before = shared.inputs()
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    statuses = {}
    try:
        for key, command, raw in COMMANDS:
            print(command, flush=True)
            raw_path = ROOT / "TestResults" / raw if raw else None
            previous = raw_path.stat().st_mtime_ns if raw_path and raw_path.exists() else None
            with (output / (key + "-command.txt")).open("w") as log:
                log.write(command + "\n")
                log.flush()
                statuses[key] = subprocess.run([command], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT,
                                               env=dict(os.environ, PYTHONDONTWRITEBYTECODE="1")).returncode
            if raw_path and raw_path.exists() and raw_path.stat().st_mtime_ns != previous:
                (output / (key + ".json.gz")).write_bytes(gzip.compress(raw_path.read_bytes(), mtime=0))
            print(key, statuses[key], flush=True)
    finally:
        report = shared.restore_known_inputs(saved, before, shared.inputs())
        final = shared.inputs()
        report.update(testedSource=head, results=statuses, carryInSha256={path: final[path] for path in sorted(carry)})
        (output / "gates-byte-audit.json").write_text(json.dumps(report, indent=2) + "\n")
    if set(statuses.values()) != {0} or not report["allOriginalInputsRestored"] or report["recoveryNeeded"]:
        raise SystemExit("A gate or exact restoration failed.")


if __name__ == "__main__":
    main()
