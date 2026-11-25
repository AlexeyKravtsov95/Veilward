"""Run UI.1 gates on the resident Editor and preserve local serialized inputs."""
import gzip
import json
import os
from pathlib import Path
import subprocess
import sys

sys.dont_write_bytecode = True
import run_vfx1b_gates as audit

ROOT = Path(__file__).resolve().parents[2]
RESULTS = ROOT / "TestResults"
COMMANDS = (
    ("static", "./Scripts/run-static-checks.sh", None),
    ("edit-mode", "./Scripts/run-edit-mode-tests.sh", "EditMode"),
    ("play-mode", "./Scripts/run-play-mode-tests.sh", "PlayMode"),
)


def main(attempt):
    if not attempt.replace("-", "").isalnum():
        raise ValueError("Expected a new attempt name")
    output = RESULTS / "ui1" / attempt
    output.mkdir(parents=True)
    audit.OUT = output
    historical = json.loads((ROOT / "Docs/review/VFX.1c/reports/gates-byte-audit.json").read_bytes())
    carry = set(historical["carryInSha256"])
    known = {row["path"] for row in historical["changed"]} | carry
    saved = {path: (ROOT / path).read_bytes() for path in known}
    existing_results = {
        path: path.read_bytes() if path.exists() else None
        for mode in ("EditMode", "PlayMode") for suffix in (".json", ".xml", ".log")
        for path in [RESULTS / (mode + suffix)]
    }
    before = audit.inputs()
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    status = {}
    try:
        for label, command, mode in COMMANDS:
            print(command, flush=True)
            with (output / (label + "-command.txt")).open("w") as log:
                log.write(command + "\n")
                log.flush()
                status[label] = subprocess.run(command.split(), cwd=ROOT, stdout=log, stderr=subprocess.STDOUT,
                                               env=dict(os.environ, PYTHONDONTWRITEBYTECODE="1")).returncode
            if mode is not None:
                for suffix in (".json", ".xml", ".log"):
                    raw = RESULTS / (mode + suffix)
                    if raw.exists():
                        (output / (label + suffix + ".gz")).write_bytes(gzip.compress(raw.read_bytes(), mtime=0))
            print(label, status[label], flush=True)
    finally:
        for path, content in existing_results.items():
            if content is None:
                path.unlink(missing_ok=True)
            else:
                path.write_bytes(content)
        report = audit.restore_known_inputs(saved, before, audit.inputs())
        final = audit.inputs()
        command = ["git", "diff", "--check"]
        with (output / "diff-command.txt").open("w") as log:
            log.write("git diff --check\n")
            log.flush()
            status["diff"] = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT).returncode
        report.update(testedSource=head, results=status, carryInSha256={path: final[path] for path in sorted(carry)},
                      previousTestReportsRestored=all(
                          (path.read_bytes() if path.exists() else None) == content for path, content in existing_results.items()))
        (output / "byte-audit.json").write_text(json.dumps(report, indent=2) + "\n")
    if set(status.values()) != {0} or not report["allOriginalInputsRestored"] or report["recoveryNeeded"]:
        raise SystemExit("A gate or exact restoration failed")


if __name__ == "__main__":
    main(sys.argv[1])
