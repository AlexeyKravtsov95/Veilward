#!/usr/bin/env python3
"""Проверка доставки QA.1; device acceptance этим verifier не подтверждается."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

folder = Path(__file__).resolve().parent
root = folder.parents[2]


def git(*args):
    return subprocess.check_output(["git", "-C", str(root), *args], stderr=subprocess.PIPE)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def verify():
    manifest = json.loads((folder / "manifest.json").read_text())
    audit = json.loads((folder / "source-audit.json").read_text())
    source = audit["testedSourceSHA"]
    require(source == manifest["testedSourceSHA"], "source mismatch")
    require(manifest["deviceAcceptance"] == "BLOCKED", "partial QA.1 must not claim device PASS")
    git("merge-base", "--is-ancestor", source, "HEAD")
    for name, expected in manifest["artifacts"].items():
        path = folder / name
        require(path.resolve().is_relative_to(folder), "artifact path escapes evidence folder")
        require(path.is_file(), f"missing artifact: {name}")
        git("ls-files", "--error-unmatch", str(path.relative_to(root)))
        require(hashlib.sha256(path.read_bytes()).hexdigest() == expected, f"artifact hash mismatch: {name}")
    for path, expected in audit["sha256"].items():
        require(hashlib.sha256(git("show", f"{source}:{path}")).hexdigest() == expected, f"tested source mismatch: {path}")
        require(hashlib.sha256((root / path).read_bytes()).hexdigest() == expected, f"delivered source drift: {path}")
    for mode, total in (("EditMode", 1110), ("PlayMode", 60)):
        report = folder / f"reports/{mode}.xml"
        subprocess.run([sys.executable, str(root / "Scripts/verify_unity_test_results.py"), str(report)], check=True)
        summary = ET.parse(report).getroot()
        require(int(summary.attrib["total"]) == total and int(summary.attrib["passed"]) == total, f"unexpected {mode} count")
    # Старые файлы Assets/Packages/ProjectSettings/Scripts в этом PR не меняются.
    changed = git("diff", "--name-status", audit["baseSHA"], "HEAD", "--", "Assets", "Packages", "ProjectSettings", "Scripts").decode().splitlines()
    require(all(line.startswith("A\t") for line in changed), "existing source/config/package modified")
    print(f"QA.1 evidence PASS: {len(manifest['artifacts'])} tracked artifacts, {len(audit['sha256'])} source hashes, host Edit1110/Play60. Device acceptance BLOCKED.")


try:
    verify()
except (OSError, ValueError, KeyError, subprocess.SubprocessError, ET.ParseError) as error:
    print(f"QA.1 evidence FAIL: {error if isinstance(error, (ValueError, KeyError)) else type(error).__name__}", file=sys.stderr)
    sys.exit(1)
