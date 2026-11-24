"""Small positive and mutated-evidence controls; memory only and no Unity project copy."""
from copy import deepcopy
import gzip
import sys

sys.dont_write_bytecode = True
import verify_delivery as audit


def reject(label, reason, check):
    try:
        check()
    except (ValueError, TypeError, KeyError) as error:
        if reason not in str(error):
            raise AssertionError(f"{label} rejected by the wrong guard: {error}") from error
        print("REJECT", label, "-", reason)
    else:
        raise AssertionError(f"{label} unexpectedly passed")


def mutated(value, change):
    copy = deepcopy(value)
    change(copy)
    return copy


def main():
    edit = audit.read("reports/edit-mode.json.gz")
    play = audit.read("reports/play-mode.json.gz")
    et = edit["Summary"]["Total"]
    pt = play["summary"]["total"]
    audit.positive_suite(edit, "EditMode", et)
    audit.positive_suite(play, "PlayMode", pt)
    for label, base, mode, total, reason, change in [
        ("filtered EditMode", edit, "EditMode", et, "unfiltered", lambda r: r.update(FilterApplied="single")),
        ("empty EditMode", edit, "EditMode", 0, "nonempty", lambda r: r["Summary"].update(Total=0, Passed=0)),
        ("duplicate Unity test", edit, "EditMode", et, "unique", lambda r: r["Results"].__setitem__(1, r["Results"][0])),
        ("unfinished PlayMode", play, "PlayMode", pt, "complete", lambda r: r.update(status="running")),
        ("skipped PlayMode", play, "PlayMode", pt, "zero-skip", lambda r: r["summary"].update(skipped=1)),
        ("substituted PlayMode status", play, "PlayMode", pt, "per-case", lambda r: r["results"][0].update(Status="Failed")),
    ]:
        reject(label, reason, lambda: audit.positive_suite(mutated(base, change), mode, total))

    baseline = audit.read("reports/before-flame.json")
    trace = gzip.decompress((audit.PACKET / "reports/before-flame.trace.gz").read_bytes())
    audit.verify_replay("flame", baseline, baseline, trace, trace)
    reject("same report for wrong start", "replay identity", lambda: audit.verify_replay("gale", baseline, baseline, trace, trace))
    replay_mutations = (
        ("fake raw byte count", "replay identity", lambda r: r.update(rawBytes=44)),
        ("missing coverage cell", "same-run REG214", lambda r: r["cells"].pop()),
        ("truncated wave history", "whole Victory8", lambda r: r["waves"].pop()),
    )
    for label, reason, change in replay_mutations:
        report = mutated(baseline, change)
        reject(label, reason, lambda: audit.verify_replay("flame", report, report, trace, trace))
    reject("swapped trace bytes", "whole replay", lambda: audit.verify_replay("flame", baseline, baseline, trace, trace[:-1]))
    reject("duplicate JSON key", "duplicate JSON key", lambda: audit.strict_json(b'{"frames":1,"frames":100}'))
    reject("non-finite JSON", "non-finite", lambda: audit.strict_json(b'{"tick":NaN}'))

    rows = audit.read("reports/video-frames.json.gz")
    proof = audit.read("reports/video-proof.json")
    receipt = audit.read("reports/completion.json")
    decoded = proof["decoded"]
    audit.verify_video(rows, receipt, decoded, proof)
    audit.exact_picks(rows, proof["picks"])
    video_mutations = (
        ("duplicate source runtime frame", "unique source", lambda r: r[1].update(frame=r[0]["frame"])),
        ("timing changed", "PTS deviation", lambda r: r[1].update(elapsed=(r[0]["elapsed"] + r[1]["elapsed"]) / 2)),
        ("test clock video", "realtime clock", lambda r: next(x for x in r if x["shots"] is not None).update(captureFramerate=30)),
        ("Continue with ghost shot", "Continue cleanup", lambda r: next(x for x in r if x["stage"] == audit.STAGES["continue"]).update(shots=[{}])),
    )
    for label, reason, change in video_mutations:
        reject(label, reason, lambda: audit.verify_video(mutated(rows, change), receipt, decoded, proof))
    reject("missing pick set", "all realtime PNG", lambda: audit.verify_video(
        rows, receipt, decoded, mutated(proof, lambda p: p["picks"].pop("realtime-storm-impact"))))
    reject("substituted PNG SHA", "source SHA", lambda: audit.exact_picks(
        rows, mutated(proof["picks"], lambda p: p["realtime-steam-impact"].update(sha256="0" * 64))))
    reject("invented impact visibility", "raster pixels", lambda: audit.exact_picks(
        rows, mutated(proof["picks"], lambda p: p["realtime-storm-impact"].update(pixels={"line": 999}))))
    reject("nonexistent encoded frame", "decoded metadata", lambda: audit.verify_video(
        rows, receipt, mutated(decoded, lambda d: d["frames"].pop()), proof))

    supplemental = audit.read("reports/supplement.json")
    events = audit.replay_reactions(trace)
    audit.supplemental(supplemental, events)
    reject("duplicate supplementary PNG", "unique supplemental", lambda: audit.supplemental(
        mutated(supplemental, lambda r: r["captures"].append(r["captures"][0])), events))
    def fake_target(report):
        item = next(r for r in report["captures"] if r["name"] == "storm-intrinsic-hop2")
        item["data"]["hits"][1]["enemy"] += 1000
    reject("phantom Storm hop", "raw hit attribution", lambda: audit.supplemental(mutated(supplemental, fake_target), events))
    def swapped_endpoints(report):
        item = next(r for r in report["captures"] if r["name"] == "storm-intrinsic-hop2")
        line = next(l for l in item["data"]["lines"] if l["name"] == "Reaction stroke 2")
        line["world"][0], line["world"][-1] = line["world"][-1], line["world"][0]
    reject("reversed Storm hop", "captured hop endpoints", lambda: audit.supplemental(mutated(supplemental, swapped_endpoints), events))
    print("PASS positive suite/replay/media inputs and 24 negative controls")


if __name__ == "__main__":
    main()
