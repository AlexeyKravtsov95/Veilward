#!/usr/bin/env python3
"""Строгая проверка revision2 delivery/source/device evidence, не QA.2/QA.3."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
import xml.etree.ElementTree as ET

folder = Path(__file__).resolve().parent
root = folder.parents[3]


def git(*args):
    return subprocess.check_output(["git", "-C", str(root), *args], stderr=subprocess.PIPE)


def require(ok, message):
    if not ok:
        raise ValueError(message)


def read(name):
    return json.loads((folder / name).read_text())


def verify_commit(source, hashes):
    # Batch читает настоящие blob bytes, включая бинарные assets, без checkout.
    with subprocess.Popen(["git", "-C", str(root), "cat-file", "--batch"],
                          stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE) as process:
        for name, expected in hashes.items():
            process.stdin.write(f"{source}:{name}\n".encode())
            process.stdin.flush()
            header = process.stdout.readline().split()
            require(len(header) == 3 and header[1] == b"blob", f"missing source blob: {name}")
            data = process.stdout.read(int(header[2]))
            require(process.stdout.read(1) == b"\n", "invalid git batch framing")
            require(hashlib.sha256(data).hexdigest() == expected, f"source hash mismatch: {name}")
        process.stdin.close()
        require(process.wait() == 0, "git source audit failed")


def verify():
    manifest = read("manifest.json")
    audit = read("source-audit.json")
    source = audit["testedSourceSHA"]
    require(source == manifest["testedSourceSHA"], "source attribution mismatch")
    git("merge-base", "--is-ancestor", source, "HEAD")
    for name, expected in manifest["artifacts"].items():
        path = folder / name
        require(path.resolve().is_relative_to(folder), "artifact escapes folder")
        git("ls-files", "--error-unmatch", str(path.relative_to(root)))
        require(hashlib.sha256(path.read_bytes()).hexdigest() == expected, f"artifact mismatch: {name}")
    old_folder = folder.parent
    old_manifest = json.loads((old_folder / "manifest.json").read_text())
    for name, expected in old_manifest["artifacts"].items():
        path = old_folder / name
        git("ls-files", "--error-unmatch", str(path.relative_to(root)))
        require(hashlib.sha256(path.read_bytes()).hexdigest() == expected, f"historical artifact modified: {name}")
    verify_commit(source, audit["sha256"])
    delivered_paths = set(git("ls-files", "-z", "Assets", "Packages", "ProjectSettings", "Scripts").decode().rstrip("\0").split("\0"))
    require(delivered_paths == set(audit["sha256"]), "source audit coverage gap")
    for name, expected in audit["sha256"].items():
        require(hashlib.sha256((root / name).read_bytes()).hexdigest() == expected, f"delivered source drift: {name}")
    whole = read("reports/export2-source-audit.json")
    require(whole == read("reports/export3-source-audit.json"), "two-export original source maps differ")
    verify_commit(source, whole)
    require(hashlib.sha256((old_folder / "manifest.json").read_bytes()).hexdigest() == whole["Docs/review/QA.1/manifest.json"], "historical manifest modified")
    comparison = read("unity-source-comparison.json")
    require(comparison["equal"] and not comparison["changed"] and comparison["newUnityRun"] is False, "incorrect host test attribution")
    host = audit["unityHostTestedSourceSHA"]
    paths = {p: h for p, h in audit["sha256"].items() if p.startswith(("Assets/", "Packages/", "ProjectSettings/"))}
    require(len(paths) == comparison["comparedFiles"], "incomplete Unity source comparison")
    verify_commit(host, paths)
    require(old_manifest["testedSourceSHA"] == host, "historical host source mismatch")
    for mode, total in (("EditMode", 1110), ("PlayMode", 60)):
        subprocess.run([sys.executable, str(root / "Scripts/verify_unity_test_results.py"), str(old_folder / f"reports/{mode}.xml")], check=True)
        summary = ET.parse(old_folder / f"reports/{mode}.xml").getroot().attrib
        require(int(summary["total"]) == total and int(summary["passed"]) == total, "historical host total mismatch")
    capability = read("capability.json")
    require(capability["success"] and capability["iosSupported"] and capability["selectedExecutableMatchesEditor"], "iOS capability not proven")
    receipt = read("reports/export2-receipt.json")
    require(receipt == read("reports/export3-receipt.json"), "export receipts differ")
    require(receipt["sourceSHA"] == source and receipt["unityVersion"] == "6000.3.28f1" and receipt["contentId"] == "eight-wave-ability-boss-v2" and receipt["version"] == "0.1.0" and receipt["buildNumber"] == "42", "wrong exported version/content")
    require(read("reports/export2-import-audit.json") == read("reports/export3-import-audit.json"), "derived import inputs differ")
    player = read("reports/player-metadata.json")
    require(player["sourceSHA"] == source and player["architectures"] == "arm64" and player["platform"] == "iphoneos" and player["codesignVerifyExitCode"] == 0, "native signed player not verified")
    require(player["version"] == receipt["version"] and player["build"] == receipt["buildNumber"] and player["bundleIdentifier"] == "com.veilward.spiritdefense", "wrong signed player identity/version")
    for name in ("xcode-signed-result", "install-result", "launch-result", "restart-result", "gale-restart-result"):
        result = read(f"reports/{name}.json")
        require(result["exitCode"] == 0 and result["sourceSHA"] == source, f"device/build step failed: {name}")
    for record in read("captures.json"):
        p = folder / "device" / record["name"]
        data = p.read_bytes()
        require(data[:8] == b"\x89PNG\r\n\x1a\n" and struct.unpack(">II", data[16:24]) == (2622, 1206), "invalid physical capture")
        require(record["sourceSHA"] == source and hashlib.sha256(data).hexdigest() == record["sha256"], "capture attribution/hash mismatch")
    require(len(read("captures.json")) == 21, "missing physical screenshots")
    smoke = read("smoke.json")
    require(smoke["sourceSHA"] == source and smoke["physicalDevice"]["simulator"] is False, "device smoke attribution mismatch")
    for start in ("FLAME", "GALE"):
        checkpoints = [read("device/" + name) for name in smoke[start]["checkpointFilesEqual"]]
        require(all(p == checkpoints[0] for p in checkpoints), f"checkpoint/repeated Continue differs: {start}")
        require(checkpoints[0]["schemaVersion"] == 4 and checkpoints[0]["scenarioContentId"] == receipt["contentId"], "wrong device checkpoint schema/content")
        expected = dict(phase=0, waveIndex=1, tick=399, gold=100, baseHealth=15, kills=0) if start == "FLAME" else dict(phase=4, waveIndex=0, tick=399, gold=95, baseHealth=16, kills=1)
        require(all(checkpoints[0]["state"].get(k) == v for k, v in expected.items()), "wrong restored physical checkpoint")
    require(read("reports/console-audit.json")["unexpectedGameplayExceptions"] == 0, "unexpected device exceptions")
    print(f"QA.1 revision2 PASS: {len(manifest['artifacts'])} tracked artifacts, {len(audit['sha256'])} current source hashes, {len(whole)} whole-repo export inputs, 21 physical PNG; host reports remain bound to {host[:7]}. QA.2/QA.3/ART/G2 not accepted.")


try:
    verify()
except (OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
    print(f"QA.1 revision2 FAIL: {error if isinstance(error, (ValueError, KeyError)) else type(error).__name__}", file=sys.stderr)
    sys.exit(1)
