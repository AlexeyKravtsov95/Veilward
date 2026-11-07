#!/usr/bin/env python3
"""Reject empty, failed, inconclusive, or skipped Unity test reports."""
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def integer(attributes, name, default=0):
    try:
        value = int(attributes.get(name, default))
    except (TypeError, ValueError):
        raise ValueError(f"{name} must be a non-negative integer")
    if value < 0:
        raise ValueError(f"{name} must be a non-negative integer")
    return value


def summary_for(root):
    if root.tag == "test-run":
        total = integer(root.attrib, "total", integer(root.attrib, "testcasecount"))
        return {
            "Total": total,
            "Passed": integer(root.attrib, "passed"),
            "Failed": integer(root.attrib, "failed") + integer(root.attrib, "errors"),
            "Inconclusive": integer(root.attrib, "inconclusive"),
            "Skipped": integer(root.attrib, "skipped"),
        }

    if root.tag == "testsuite":
        suites = [root]
        attributes = root.attrib
    elif root.tag == "testsuites":
        suites = [element for element in root if element.tag == "testsuite"]
        attributes = root.attrib
    else:
        raise ValueError(f"unsupported report root <{root.tag}>")

    if "tests" in attributes:
        total = integer(attributes, "tests")
        failed = integer(attributes, "failures") + integer(attributes, "errors")
        skipped = integer(attributes, "skipped")
        inconclusive = integer(attributes, "inconclusive")
    else:
        total = sum(integer(suite.attrib, "tests") for suite in suites)
        failed = sum(
            integer(suite.attrib, "failures") + integer(suite.attrib, "errors")
            for suite in suites
        )
        skipped = sum(integer(suite.attrib, "skipped") for suite in suites)
        inconclusive = sum(integer(suite.attrib, "inconclusive") for suite in suites)
    return {
        "Total": total,
        "Passed": total - failed - skipped - inconclusive,
        "Failed": failed,
        "Inconclusive": inconclusive,
        "Skipped": skipped,
    }


def verify_report(path):
    try:
        root = ET.parse(path).getroot()
        summary = summary_for(root)
    except (ET.ParseError, OSError) as error:
        return [f"{path.name}: malformed XML ({error})"]
    except ValueError as error:
        return [f"{path.name}: invalid summary ({error})"]

    errors = []
    for field in ("Total", "Passed", "Failed", "Inconclusive", "Skipped"):
        value = summary[field]
        if value < 0:
            errors.append(f"{path.name}: {field} must be non-negative")
    if summary["Total"] == 0:
        errors.append(f"{path.name}: Total must be greater than zero")
    if summary["Passed"] != summary["Total"]:
        errors.append(f"{path.name}: Passed must equal Total")
    for field in ("Failed", "Inconclusive", "Skipped"):
        if summary[field] != 0:
            errors.append(f"{path.name}: {field} must be zero")
    return errors


def verify_path(path):
    path = Path(path)
    reports = [path] if path.is_file() else sorted(path.rglob("*.xml"), key=str) if path.is_dir() else []
    if not reports:
        return [f"No XML test reports found: {path}"]
    errors = []
    for report in reports:
        errors.extend(verify_report(report))
    return errors


def main():
    if len(sys.argv) != 2:
        print("Usage: verify_unity_test_results.py <xml-or-directory>", file=sys.stderr)
        return 2
    errors = verify_path(sys.argv[1])
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        print("Unity test report: FAIL", file=sys.stderr)
        return 1
    print("Unity test report: PASS (complete, non-empty, zero skipped)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
