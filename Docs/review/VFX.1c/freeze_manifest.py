"""Freeze tracked source/protected/artifact inventories after the evidence commit."""
import argparse
import json
import sys

sys.dont_write_bytecode = True
from verify_delivery import BASE, CARRY, CHANGED, HARNESS, MANIFEST, PACKET, ROOT, SOURCE, artifact_keys, digest, git, read, sha, tree


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--before-source", required=True)
    parser.add_argument("--media-source", required=True)
    args = parser.parse_args()
    anchor = git("rev-parse", "HEAD").decode().strip()
    files = tree(anchor)
    source = {p: sha(git("show", f"{anchor}:{p}")) if p in CARRY else digest(ROOT / p)
              for p in sorted(files) if p.startswith(SOURCE)}
    totals = {"EditMode": read("reports/edit-mode.json.gz")["Summary"]["Total"],
              "PlayMode": read("reports/play-mode.json.gz")["summary"]["total"]}
    manifest = dict(baseline=BASE, beforeSource=args.before_source, testedSource=read("reports/gates-byte-audit.json")["testedSource"],
                    mediaSource=args.media_source, evidenceSource=anchor, trackedInventory=sorted(set(files) | {MANIFEST}),
                    changedExisting=sorted(CHANGED), renderCarry=CARRY, source=source, totals=totals,
                    harnesses={p: digest(ROOT / p) for p in sorted(HARNESS)},
                    artifacts={p: digest(ROOT / p) for p in sorted(artifact_keys(files))})
    (PACKET / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")
    print(anchor)


if __name__ == "__main__":
    main()
