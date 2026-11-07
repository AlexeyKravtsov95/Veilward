#!/usr/bin/env python3
"""Dependency-free static checks for the VEILWARD Unity project."""
import json
from pathlib import Path
import re
import sys

GUID_PATTERN = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)
REQUIRED_ASSEMBLIES = {
    "Veilward",
    "Veilward.Domain",
    "Veilward.Editor",
    "Veilward.Tests.EditMode",
}


def load_json(path):
    try:
        with path.open(encoding="utf-8") as stream:
            return json.load(stream), None
    except (OSError, json.JSONDecodeError) as error:
        return None, f"{path}: invalid JSON ({error})"


def check_version_and_packages(root, baseline_path):
    errors = []
    baseline, error = load_json(baseline_path)
    if error:
        return [error]

    version_path = root / "ProjectSettings/ProjectVersion.txt"
    try:
        version_text = version_path.read_text(encoding="utf-8")
    except OSError as read_error:
        return [f"{version_path}: {read_error}"]
    version_match = re.search(r"^m_EditorVersion:\s*(\S+)\s*$", version_text, re.MULTILINE)
    revision_match = re.search(
        r"^m_EditorVersionWithRevision:\s*\S+\s+\(([^)]+)\)\s*$",
        version_text,
        re.MULTILINE,
    )
    if not version_match or version_match.group(1) != baseline["unityVersion"]:
        actual = version_match.group(1) if version_match else "missing"
        errors.append(f"Unity version mismatch: expected {baseline['unityVersion']}, got {actual}")
    if not revision_match or revision_match.group(1) != baseline["unityRevision"]:
        actual = revision_match.group(1) if revision_match else "missing"
        errors.append(f"Unity revision mismatch: expected {baseline['unityRevision']}, got {actual}")

    manifest, error = load_json(root / "Packages/manifest.json")
    if error:
        errors.append(error)
        return errors
    lock, error = load_json(root / "Packages/packages-lock.json")
    if error:
        errors.append(error)
        return errors

    packages = manifest.get("dependencies")
    if packages != baseline["packages"]:
        errors.append("Direct package baseline mismatch; approved baseline is Scripts/unity-project-baseline.json")
        return errors
    lock_dependencies = lock.get("dependencies", {})
    for name, version in packages.items():
        entry = lock_dependencies.get(name)
        if not isinstance(entry, dict):
            errors.append(f"Missing lock entry for direct package {name}")
            continue
        if entry.get("version") != version:
            errors.append(
                f"Direct package lock version mismatch for {name}: "
                f"manifest {version}, lock {entry.get('version')}"
            )
        if entry.get("depth") != 0:
            errors.append(f"Direct package {name} must have lock depth 0")
    return errors


def ignored_by_unity(path):
    return path.name.startswith(".") or path.name.endswith("~")


def check_meta_files(assets):
    errors = []
    if not assets.is_dir():
        return [f"Assets directory is missing: {assets}"]
    guid_owners = {}
    paths = sorted((path for path in assets.rglob("*") if not ignored_by_unity(path)), key=str)
    for path in paths:
        if path.name.endswith(".meta"):
            continue
        if not path.with_name(path.name + ".meta").is_file():
            errors.append(f"{path}: missing .meta")
    for meta in (path for path in paths if path.is_file() and path.name.endswith(".meta")):
        source = meta.with_name(meta.name[:-5])
        if not source.exists():
            errors.append(f"{meta}: orphan .meta")
        try:
            text = meta.read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError) as error:
            errors.append(f"{meta}: unreadable ({error})")
            continue
        match = GUID_PATTERN.search(text)
        if not match:
            errors.append(f"{meta}: missing or invalid 32-hex GUID")
            continue
        guid = match.group(1).lower()
        if guid in guid_owners:
            errors.append(f"{meta}: duplicate GUID also used by {guid_owners[guid]}")
        else:
            guid_owners[guid] = meta
    return errors


def check_assembly_definitions(assets):
    errors = []
    definitions = {}
    for path in sorted(assets.rglob("*.asmdef"), key=str):
        payload, error = load_json(path)
        if error:
            errors.append(error)
            continue
        name = payload.get("name")
        if not isinstance(name, str) or not name:
            errors.append(f"{path}: assembly name must be a non-empty string")
            continue
        if name in definitions:
            errors.append(f"{path}: duplicate assembly name {name}")
            continue
        definitions[name] = (path, payload)

    missing = REQUIRED_ASSEMBLIES.difference(definitions)
    for name in sorted(missing):
        errors.append(f"Missing required Assembly Definition: {name}")

    for name, (path, payload) in definitions.items():
        references = payload.get("references", [])
        if not isinstance(references, list):
            errors.append(f"{path}: references must be an array")
            continue
        for reference in references:
            if isinstance(reference, str) and reference.startswith("Veilward") and reference not in definitions:
                errors.append(f"{path}: unknown local assembly reference {reference}")

    domain = definitions.get("Veilward.Domain")
    if domain and domain[1].get("noEngineReferences") is not True:
        errors.append("Veilward.Domain must have noEngineReferences: true")
    for name in ("Veilward.Editor", "Veilward.Tests.EditMode"):
        definition = definitions.get(name)
        if definition and definition[1].get("includePlatforms") != ["Editor"]:
            errors.append(f"{name} must be Editor-only")
    return errors


def check_project(root, baseline_path=None):
    root = Path(root).resolve()
    baseline = Path(baseline_path) if baseline_path else Path(__file__).with_name("unity-project-baseline.json")
    errors = check_version_and_packages(root, baseline)
    assets = root / "Assets"
    errors.extend(check_meta_files(assets))
    errors.extend(check_assembly_definitions(assets))
    return errors


def main():
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]
    errors = check_project(root)
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        print(f"Static Unity checks: FAIL ({len(errors)} errors)", file=sys.stderr)
        return 1
    print("Static Unity checks: PASS (version, packages, .meta, GUID, Assembly Definitions)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
