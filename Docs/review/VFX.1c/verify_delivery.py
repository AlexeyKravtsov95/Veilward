"""Read-only Git, Unity suite, replay, dependency, and Editor media audit for #92."""
import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
import sys

sys.dont_write_bytecode = True
PACKET = Path(__file__).resolve().parent
ROOT = PACKET.parents[2]
sys.path.insert(0, str(ROOT / "ArtSource/Review"))
from encode_hybrid_video import (KINDS, PHASES, STAGES, attack_phase, digest, probe, select_event, visibility)

BASE = "4dac946a142e281adc739aeb05ffb9aba136f402"
PREFIX = "Docs/review/VFX.1c/"
RUNTIME = ("Assets/", "Packages/", "ProjectSettings/", "Scripts/")
SOURCE = RUNTIME + ("ArtSource/",)
MANIFEST = PREFIX + "manifest.json"
DOCS = {"Docs/VFX1C_HYBRID_REACTIONS.md", "Docs/REGRESSION.md", "Docs/TECH_DESIGN.md", "Docs/ROADMAP.md", "README.md"}
CHANGED = DOCS - {"Docs/VFX1C_HYBRID_REACTIONS.md"} | {
    "Assets/_Game/Scripts/Presentation/BattleSceneController.cs",
    "Assets/_Game/Scripts/Presentation/ReactionEffectView.cs",
    "Assets/_Game/Tests/EditMode/BattleReactionPresentationTests.cs",
    "Assets/_Game/Tests/EditMode/ProductionFlameProjectileTests.cs",
    "Assets/_Game/Tests/PlayMode/BattleReactionPlayModeTests.cs",
}
HARNESS = {
    "ArtSource/Review/CaptureHybridReplays.cs", "ArtSource/Review/RecordHybridVfx.cs",
    "ArtSource/Review/CaptureHybridScenes.cs", "ArtSource/Review/encode_hybrid_video.py",
    "ArtSource/Review/run_vfx1c_gates.py",
}
NEW_ASSETS = {
    stem + suffix for stem in (
        "Assets/_Game/Scripts/Presentation/HybridProjectilePool",
        "Assets/_Game/Tests/EditMode/HybridProjectileTests",
        "Assets/_Game/Tests/PlayMode/HybridProjectilePlayModeTests",
    ) for suffix in (".cs", ".cs.meta")
}
CARRY = {
    "Assets/Settings/Mobile_RPAsset.asset": "2d5182d2b8884ac88bc6ed033a0a722c23cfb15a7bc8b7daa38db37227bafd09",
    "Assets/_Game/Art/Materials/GrayboxSelection.mat": "6107f7e1b6deb79ceb4489599637fdb16c5e01870013e0cc3a71d2c50b35ad65",
    "Assets/_Game/Art/Materials/GrayboxTower.mat": "71ced636160a68dca7cf26fd2338913b0d0620aeb891c4ea106d7cacba94f275",
    "Assets/_Game/Art/Materials/GrayboxTowerPreview.mat": "1b49d268ffb66f7e898a81d0a4a7b02e404d14dc7d884cae98cac05bdfa4ce70",
}
CELLS = {(kind, trigger) for kind in KINDS for trigger in ("Intrinsic", "StatusPair")}
REACTION_PNGS = {
    "steam-intrinsic": ("Steam", "Intrinsic", "impacts"),
    "steam-statuspair": ("Steam", "StatusPair", "impacts"),
    "storm-intrinsic-hop1": ("Storm", "Intrinsic", "hop1"),
    "storm-intrinsic-hop2": ("Storm", "Intrinsic", "hop2"),
    "storm-statuspair-hop1": ("Storm", "StatusPair", "hop1"),
    "storm-statuspair-hop2": ("Storm", "StatusPair", "hop2"),
}
L3_PNGS = {f"{kind.lower()}-l3-{phase}" for kind in KINDS for phase in PHASES}
DENSE_PNGS = {"synthetic-dense-six-boss", "synthetic-all-four-inspector"}
REALTIME_PNGS = {f"realtime-{kind.lower()}-{phase}" for kind in KINDS for phase in PHASES} | {f"realtime-{p}" for p in STAGES}


def require(value, message):
    if not value:
        raise ValueError(message)


def strict_json(raw):
    def object_pairs(pairs):
        result = dict(pairs)
        require(len(result) == len(pairs), "duplicate JSON key")
        return result
    def invalid(value):
        raise ValueError("non-finite JSON " + value)
    return json.loads(raw, object_pairs_hook=object_pairs, parse_constant=invalid)


def read(path):
    data = (PACKET / path).read_bytes()
    return strict_json(gzip.decompress(data) if path.endswith(".gz") else data)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT, stderr=subprocess.PIPE)


def git_blob(data):
    return hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()


def tree(ref):
    if ref != "HEAD":
        require(type(ref) is str and re.fullmatch("[0-9a-f]{40}", ref) and git("cat-file", "-t", ref) == b"commit\n", "exact commit ID")
    result = {}
    for row in filter(None, git("ls-tree", "-r", "-z", ref).split(b"\0")):
        meta, path = row.split(b"\t", 1)
        mode, kind, digest_value = meta.split()
        require(kind == b"blob" and mode in (b"100644", b"100755"), "unexpected Git tree entry")
        result[path.decode()] = digest_value.decode()
    return result


def ancestry(before, after):
    return subprocess.run(["git", "merge-base", "--is-ancestor", before, after], cwd=ROOT, check=False).returncode == 0


def artifact_keys(paths):
    return {p for p in paths if (p.startswith(PREFIX) or p in DOCS) and p != MANIFEST}


def inventory(manifest):
    base = tree(BASE)
    current = tree("HEAD")
    before = tree(manifest["beforeSource"])
    tested = tree(manifest["testedSource"])
    media = tree(manifest["mediaSource"])
    anchor = tree(manifest["evidenceSource"])
    require(manifest["baseline"] == BASE and set(manifest["changedExisting"]) == CHANGED, "unexpected baseline/scope")
    refs = [BASE, manifest["beforeSource"], manifest["testedSource"], manifest["mediaSource"], manifest["evidenceSource"], "HEAD"]
    require(all(ancestry(a, b) for a, b in zip(refs, refs[1:])), "tested/media/evidence ancestry")
    require(set(current) == set(manifest["trackedInventory"]), "incomplete or stale Git tree inventory")
    require({p for p in base if current.get(p) != base[p]} == CHANGED, "protected Git source changed")
    require({p for p in current.keys() - base.keys() if not p.startswith(PREFIX)} ==
            NEW_ASSETS | HARNESS | {"Docs/VFX1C_HYBRID_REACTIONS.md"}, "unexpected additions outside evidence")
    source = {p for p in current if p.startswith(SOURCE)}
    artifacts = artifact_keys(current)
    require(set(manifest["source"]) == source and set(manifest["artifacts"]) == artifacts, "incomplete source/artifact inventory")
    require(set(manifest["renderCarry"]) == set(CARRY) and manifest["renderCarry"] == CARRY, "unexpected rendering exceptions")
    require(git_blob((ROOT / MANIFEST).read_bytes()) == current[MANIFEST], "manifest differs from reviewed Git")
    for path in current:
        if path == MANIFEST:
            continue
        data = (ROOT / path).read_bytes()
        exact = git_blob(data) == current[path]
        require(exact or (path in CARRY and sha(data) == CARRY[path]), "checkout blob mismatch " + path)
        if path in source:
            require(sha(data) == manifest["source"][path] or
                    (path in CARRY and sha(git("show", f"HEAD:{path}")) == manifest["source"][path]),
                    "source SHA mismatch " + path)
        if path in artifacts:
            require(current[path] == anchor.get(path) and sha(data) == manifest["artifacts"][path], "artifact anchor/SHA " + path)
        if path.startswith(RUNTIME):
            require(tested.get(path) == media.get(path) == current[path], "tested/media runtime blob mismatch " + path)
    require({p: h for p, h in before.items() if p.startswith(RUNTIME)} ==
            {p: h for p, h in base.items() if p.startswith(RUNTIME)}, "before replay/runtime baseline mismatch")
    require({p: h for p, h in tested.items() if p.startswith(RUNTIME)} ==
            {p: h for p, h in current.items() if p.startswith(RUNTIME)}, "tested runtime inventory drift")
    for path in HARNESS:
        commit = manifest["mediaSource"]
        if path.endswith("run_vfx1c_gates.py"):
            commit = manifest["testedSource"]
        require(digest(ROOT / path) == manifest["harnesses"][path] and git_blob((ROOT / path).read_bytes()) ==
                (tested if commit == manifest["testedSource"] else media).get(path),
                "capture/encoder source blob " + path)
    require(set(manifest["harnesses"]) == HARNESS, "harness inventory")
    for row in filter(None, git("status", "--porcelain=v1", "-z", "--untracked-files=all").split(b"\0")):
        path = row[3:].decode()
        require(row[:2] == b"??" and not path.startswith((*SOURCE, PREFIX)) or
                row[:2] == b"??" and path.startswith("Scripts/__pycache__/") or
                row[:2] == b" M" and path in CARRY, "unexpected checkout status " + path)
    return len(source), len(base) - len(CHANGED), len(artifacts)


def positive_suite(report, mode, expected):
    if mode == "EditMode":
        require(report["Mode"] == mode and report["success"] is True and report["result"] is None and
                report["FilterApplied"] is False, "EditMode must be complete and unfiltered")
        summary, rows = report["Summary"], report["Results"]
    else:
        require(report["status"] == "completed" and not report.get("filter"), "PlayMode must be complete and unfiltered")
        summary, rows = report["summary"], report["results"]
    summary = {key.lower(): value for key, value in summary.items()}
    require(type(expected) is int and expected > 0 and summary == dict(total=expected, passed=expected, failed=0, skipped=0, inconclusive=0)
            and all(type(v) is int for v in summary.values()), "nonempty zero-skip full suite")
    names = [row["FullName"] for row in rows]
    require(len(rows) == expected == len(set(names)) and all(type(name) is str and name.strip() for name in names) and
            all(row["Status"] == "Passed" for row in rows), "unique full per-case PASS")
    return names


def verify_gates(manifest):
    report = read("reports/gates-byte-audit.json")
    require(report["testedSource"] == manifest["testedSource"] and report["results"] ==
            {"static": 0, "edit-mode": 0, "play-mode": 0}, "full gate command exit codes")
    require(report["allOriginalInputsRestored"] is True and report["editorSceneDetached"] is True and
            report["editorInputsRefreshed"] is True and report["recoveryNeeded"] is False and
            report["newInputs"] == report["unexpected"] == report["commandDiagnostics"] == [], "exact restoration and refresh")
    require(report["carryInSha256"] == read("reports/initial-audit.json")["renderCarry"] == CARRY, "carry-in SHA")
    static = (PACKET / "reports/static-command.txt").read_text()
    require(static.startswith("./Scripts/run-static-checks.sh\n") and "Ran 38 tests in " in static and
            "Static Unity checks: PASS" in static, "static checks raw report")
    edit = positive_suite(read("reports/edit-mode.json.gz"), "EditMode", manifest["totals"]["EditMode"])
    play = positive_suite(read("reports/play-mode.json.gz"), "PlayMode", manifest["totals"]["PlayMode"])
    require(any("BuildTwice_PreservesSavedConfig" in name for name in edit) and
            sum("HybridProjectileTests.Vfx1C" in n for n in edit) == 16 and
            sum("HybridProjectilePlayModeTests.Vfx1C" in n for n in play) == 1, "builder and new regressions ran")
    compiler = read("reports/compile-full.json")
    require(compiler["success"] is True and compiler["data"]["compilationFailed"] is False and
            compiler["data"]["summary"]["errors"] == 0, "compiler report")


def verify_replay(start, first, second, raw_before, raw_after):
    require(first == second and raw_before == raw_after and len(raw_before) > 100_000, "whole replay mismatch " + start)
    require(first["start"] == start and first["outcome"] == "Victory" and first["entries"] == len(raw_before.splitlines())
            and first["traceSha256"] == sha(raw_before) and first["rawBytes"] == len(raw_before), "replay identity/rawBytes/SHA " + start)
    waves = first["waves"]
    require([w["wave"] for w in waves] == list(range(1, 9)) and waves[-1]["phase"] == "Victory", "whole Victory8 " + start)
    require(first["bossKills"] == 1 and first["bossLeaks"] == 0 and first["abilityKills"] > 0 and
            len(first["cells"]) == 4 and {(c["kind"], c["trigger"]) for c in first["cells"]} == CELLS and
            all(c["procs"] > 0 and c["applied"] > 0 for c in first["cells"]), "same-run REG214 " + start)
    require(raw_before.startswith(b"C|NewGame\n") and b"|150|0|IDs:" in raw_before.splitlines()[1], "production NewGame " + start)


def verify_replays():
    for start in ("flame", "gale"):
        base = "reports/{}-" + start
        before, after = (read(base.format(phase) + ".json") for phase in ("before", "after"))
        inputs = [gzip.decompress((PACKET / (base.format(phase) + ".trace.gz")).read_bytes()) for phase in ("before", "after")]
        verify_replay(start, before, after, *inputs)


def verify_rendering(actual):
    require(actual["actualPlayMode"] is True and actual["unity"] == "6000.3.28f1" and actual["graphics"] == "Metal" and
            actual["quality"] == "Mobile" and actual["camera"]["orthographicSize"] > 1 and
            abs(actual["camera"]["aspect"] - 16 / 9) < .001, "actual production rendering context")
    require(actual["hybridViews"] == 8 and actual["hybridRenderers"] == actual["hybridSlots"] == 32 and
            actual["reactionViews"] == 24 and actual["reactionRenderers"] == actual["reactionSlots"] == 120 and
            actual["basicRenderers"] == 48 and actual["flameRenderers"] == 104, "actual bounded materials/renderers")
    material = actual["material"]
    require(material["path"] == "Assets/_Game/Art/Materials/ReactionEffect.mat" and
            material["name"] == "Universal Render Pipeline/Unlit" and material["passes"] > 0, "actual material and shader")
    rows = actual["dependencies"]
    inputs = {row["path"]: row["sha256"] for row in rows}
    minimum = {"Assets/_Game/Scenes/Battle.unity", material["path"], material["shader"],
               "Packages/packages-lock.json", "Packages/com.unity.pipeline/Editor/Commands/Capture/CaptureCommands.cs",
               "Packages/com.unity.render-pipelines.universal/Shaders/UnlitForwardPass.hlsl"} | set(CARRY)
    require(len(inputs) == len(rows) and minimum <= set(inputs), "required actual rendering inputs")
    lock = strict_json((ROOT / "Packages/packages-lock.json").read_bytes())["dependencies"]
    for name, checksum in inputs.items():
        require(not name.startswith("/") and ".." not in Path(name).parts and name.startswith(("Assets/", "Packages/")),
                "invalid dependency path " + name)
        if name in CARRY:
            require(checksum == CARRY[name], "exact capture carry-in " + name)
            continue
        path = ROOT / name
        if not path.is_file() and name.startswith("Packages/com."):
            package, relative = name[len("Packages/"):].split("/", 1)
            versions = [p for p in (ROOT / "Library/PackageCache").glob(package + "@*")
                        if strict_json((p / "package.json").read_bytes())["version"] == lock[package]["version"]]
            require(len(versions) == 1, "exact PackageCache prerequisite " + package)
            path = versions[0] / relative
        require(path.is_file() and digest(path) == checksum, "actual dependency SHA " + name)
    return len(inputs)


def checked_stage(row, target):
    return row["stage"] == target and row["productionUpdate"] is True and row["wave"] == 8


def verify_video(rows, receipt, decoded, proof):
    require(type(rows) is list and receipt["ok"] is True and receipt["realTime"] is True and
            receipt["productionUpdate"] is True and receipt["liveResourceMutation"] is False and
            receipt["frames"] == proof["frames"] == len(rows) >= 100 and proof["speed"] == 1 and proof["splices"] == 0,
            "complete unspliced production Update video")
    require(decoded == proof["decoded"], "fresh decoded metadata matches source-index receipt")
    stream, = decoded["streams"]
    require((stream["codec_name"], stream["pix_fmt"], stream["width"], stream["height"]) ==
            ("h264", "yuv420p", 1600, 900), "decodable original GameView dimensions and format")
    pts = [float(f["best_effort_timestamp_time"]) for f in decoded["frames"]]
    require(len(pts) == len(rows) and pts[0] == 0 and all(b > a for a, b in zip(pts, pts[1:])), "decoded PTS order")
    require(all(type(r["frame"]) is int and r["frame"] > 0 and type(r["elapsed"]) in (int, float) and
                math.isfinite(r["elapsed"]) and r["elapsed"] >= 0 and r["index"] == i and
                re.fullmatch("[a-f0-9]{64}", r["sha256"]) for i, r in enumerate(rows)), "source frame identity")
    require(all(a["frame"] < b["frame"] and a["elapsed"] < b["elapsed"] for a, b in zip(rows, rows[1:])), "ordered, unique source runtime frame IDs")
    error = max(abs(t - (r["elapsed"] - rows[0]["elapsed"])) for t, r in zip(pts, rows))
    require(error <= .002 and abs(proof["maxPtsError"] - error) < 1e-8 and proof["lastPts"] == pts[-1] > 10,
            "realtime PTS deviation")
    require(all(r["timeScale"] == 1 and r["captureFramerate"] == 0 for r in rows if r["shots"] is not None) and
            all(r["productionUpdate"] is True for r in rows if r["shots"] is not None), "production realtime clock")
    frozen = [r for r in rows if checked_stage(r, STAGES["pause"])]
    require(len(frozen) > 2 and frozen[0]["shots"] and all(
        r["paused"] and all(r[k] == frozen[0][k] for k in ("tick", "shots", "reactions", "cues")) for r in frozen), "paused geometry")
    clean = [r for r in rows if checked_stage(r, STAGES["continue"])]
    require(clean and all(not r["shots"] and not r["reactions"] and not r["cues"] for r in clean), "Continue cleanup")
    live = [r for r in rows if checked_stage(r, "Combat8 / production Update resumed") and not r["paused"]]
    require(len(live) > 60 and live[-1]["tick"] > live[0]["tick"] + 200 and rows[-1]["index"] > frozen[-1]["index"], "sequential combat cadence")
    for kind in KINDS:
        events = {tuple(select_event(s).values()) for r in live for s in r["shots"] if s["kind"] == kind}
        require(len(events) > 1 and any(v["kind"] == kind and v["hits"] for r in live for v in r["reactions"]), "realtime direct/reaction cadence " + kind)
    states = {kind for r in live for c in r["cues"] for kind in (c["first"], c["second"]) if kind is not None}
    require(len(states) >= 2 and all(c["slots"] <= 2 for r in live for c in r["cues"]) and
            len({tuple((c["owner"], c["first"], c["second"]) for c in r["cues"]) for r in live}) > 2,
            "live status transitions and two slots")
    require(set(proof["picks"]) == REALTIME_PNGS, "all realtime PNG stages")
    return len(pts)


def exact_picks(rows, picks):
    for key, pick in picks.items():
        index = pick["index"]
        require(type(index) is int and 0 <= index < len(rows), "PNG index out of bounds")
        row = rows[index]
        png = PACKET / "playmode" / (key + ".png")
        require(pick["sha256"] == row["sha256"] == digest(png), "PNG exact source SHA " + key)
        if key in {f"realtime-{p}" for p in STAGES}:
            require(checked_stage(row, STAGES[key.removeprefix("realtime-")]), "PNG runtime stage " + key)
            continue
        shot, = (s for s in row["shots"] if select_event(s) == pick["event"])
        require(not row["paused"] and not row["stage"].startswith("Continue") and attack_phase(shot) == pick["phase"],
                "PNG event phase " + key)
        measured = visibility(png, shot["lines"])
        require(measured == pick["pixels"] and sum(measured.values()) >= 4, "visible same-event raster pixels " + key)
    for kind in KINDS:
        frames = [picks[f"realtime-{kind.lower()}-{p}"] for p in PHASES]
        require([f["phase"] for f in frames] == list(PHASES) and frames[0]["index"] < frames[1]["index"] < frames[2]["index"] and
                all(f["event"] == frames[0]["event"] and f["event"]["kind"] == kind and f["event"]["level"] >= 1 for f in frames),
                "same captured attack/level/target across ordered stages " + kind)


def replay_reactions(raw):
    events = {}
    for line in raw.decode().splitlines():
        if line.startswith("S|") and "|R:" in line:
            chunks = line.split("|")
            damage = [c[2:].split(",") for c in chunks if c.startswith("D:")]
            for r in (c[2:].split(",") for c in chunks if c.startswith("R:")):
                tick, sequence, kind, trigger, tower, primary = r[:6]
                key = (int(tick), int(sequence), kind, trigger, int(tower), int(primary))
                hits = [dict(index=int(h[-1]), enemy=int(h[6]), from_=[float(v) for v in h[7:9]],
                             to=[float(v) for v in h[9:11]], loss=int(h[14]), lethal=h[17] == "True")
                        for h in damage if h[0] == tick and h[2] == "Reaction" and h[3] == tower and h[-3] == kind and h[-2] == sequence]
                for hit in hits:
                    hit["from"] = hit.pop("from_")
                events[key] = sorted(hits, key=lambda h: h["index"])
    return events


def supplemental(reports, actual_events):
    require(reports["ok"] is True, "supplement capture completed")
    rows = reports["captures"]
    by_name = {r["name"]: r for r in rows}
    require(len(by_name) == len(rows) and set(by_name) == set(REACTION_PNGS) | L3_PNGS | DENSE_PNGS and
            all(a["frame"] < b["frame"] for a, b in zip(rows, rows[1:])), "complete ordered unique supplemental PNG sets")
    for name, row in by_name.items():
        require(row["scope"] == ("synthetic-test-clock" if name in DENSE_PNGS else "earned-test-clock"), "explicit media scope " + name)
        png = PACKET / "playmode" / (name + ".png")
        require(digest(png) == row["sha256"], "actual supplemental source file " + name)
        data = row["data"]
        if name not in DENSE_PNGS:
            measured = visibility(png, data["lines"])
            require(sum(measured.values()) >= 4, "visible supplemental pixels " + name)
        if name in REACTION_PNGS:
            kind, trigger, stage = REACTION_PNGS[name]
            key = (data["Tick"], data["sequence"], kind, trigger, data["tower"], data["primary"])
            hits = data["hits"]
            require((data["kind"], data["trigger"], data["stage"]) == (kind, trigger, stage) and
                    key in actual_events and len(hits) == len(actual_events[key]) == 2, "earned reaction exact key and hits " + name)
            for hit, raw_hit in zip(hits, actual_events[key]):
                require(all(hit[k] == raw_hit[k] for k in ("index", "enemy", "loss", "lethal")) and
                        all(math.dist(hit[k], raw_hit[k]) < .00001 for k in ("from", "to")), "earned raw hit attribution " + name)
            require([h["index"] for h in hits] == ([0, 1] if kind == "Steam" else [1, 2]) and all(h["loss"] > 0 for h in hits),
                    "captured hit limit/order/applied HP loss " + name)
            lines = {line["name"]: line for line in data["lines"]}
            if kind == "Steam":
                require(hits[0]["enemy"] == data["primary"] and hits[1]["enemy"] != data["primary"] and data["limit"] <= 4 and
                        all(measured.get(f"Reaction stroke {i}", 0) >= 2 for i in (1, 2)), "visible Steam primary and secondary impacts " + name)
            else:
                require(all(h["enemy"] != data["primary"] for h in hits) and data["limit"] == 2, "Storm additional-only targets")
                for i in range(1, 3):
                    stroke = f"Reaction stroke {i}"
                    if i == 2 and stage == "hop1":
                        require(stroke not in lines, "second hop not present before its time")
                        continue
                    points = lines[stroke]["world"]
                    require(math.dist([points[0][0], points[0][2]], hits[i - 1]["from"]) < .00001 and
                            math.dist([points[-1][0], points[-1][2]], hits[i - 1]["to"]) < .00001, "captured hop endpoints " + name)
                require(measured.get("Reaction stroke " + ("1" if stage == "hop1" else "2"), 0) >= 3, "visible Storm hop " + name)
    for kind in KINDS:
        series = [by_name[f"{kind.lower()}-l3-{phase}"] for phase in PHASES]
        data = [r["data"] for r in series]
        require(all(select_event(d) == select_event(data[0]) and d["level"] == 3 and d["kind"] == kind for d in data) and
                [d["IsFlying"] for d in data] == [True, True, False] and
                series[0]["frame"] < series[1]["frame"] < series[2]["frame"], "same-event earned L3 sequence")
    dense = by_name["synthetic-dense-six-boss"]["data"]
    require(set(dense["seen"]) == {"flame", "tide", "gale", "stone", "steam", "storm"} and
            dense["count"] >= 6 and dense["boss"] > 0, "dense six-family/boss scenario")
    status = by_name["synthetic-all-four-inspector"]["data"]
    require(set(status["statuses"]) == {"Burn", "Wet", "Gust", "Crack"} and status["slots"] == 2 and
            all(name in status["details"] for name in status["statuses"]), "four live statuses in two slots and inspector")


def verify_media():
    rows = read("reports/video-frames.json.gz")
    proof = read("reports/video-proof.json")
    clip = PACKET / "editor-flow.mp4"
    require(digest(clip) == proof["sha256"] and re.fullmatch("[a-f0-9]{64}", proof["executableSha256"]) and
            len(proof["versions"]) == 2, "video and encoder receipts")
    count = verify_video(rows, read("reports/completion.json"), probe(clip), proof)
    concat = (PACKET / "reports/source.ffconcat").read_text().splitlines()
    require(concat[0] == "ffconcat version 1.0" and
            [c for c in concat if c.startswith("file ")] == [f"file frames/{i:05d}.png" for i in range(count)] and
            [c for c in concat if c.startswith("option ")] == ["option framerate 1000"] * count and
            len(concat) == 3 * count, "exact encoder frame order and count")
    durations = [float(c.split()[1]) for c in concat if c.startswith("duration ")]
    require(len(durations) == count - 1 and all(abs(d - (b["elapsed"] - a["elapsed"])) < 1e-8
            for d, a, b in zip(durations, rows, rows[1:])), "source concat durations")
    check = subprocess.run(["ffmpeg", "-v", "error", "-xerror", "-i", str(clip), "-map", "0:v:0",
                            "-fps_mode", "passthrough", "-f", "null", "-"], stderr=subprocess.PIPE, check=False)
    require(check.returncode == 0 and not check.stderr, "whole video decode")
    exact_picks(rows, proof["picks"])
    reactions = replay_reactions(gzip.decompress((PACKET / "reports/before-flame.trace.gz").read_bytes()))
    supplemental(read("reports/supplement.json"), reactions)
    pngs = {p.stem for p in (PACKET / "playmode").glob("*.png")}
    require(pngs == REALTIME_PNGS | set(REACTION_PNGS) | L3_PNGS | DENSE_PNGS, "complete artifact PNG inventory")
    require((PACKET / "reports/earned-state.txt").read_bytes() == (PACKET / "reports/continued-state.txt").read_bytes() and
            (PACKET / "reports/combat-safe-file.json").read_bytes() == (PACKET / "reports/continued-file.json").read_bytes(),
            "actual file and whole state Continue equality")
    return count


def main(check_preservation):
    manifest = read("manifest.json")
    source, protected, artifacts = inventory(manifest)
    verify_gates(manifest)
    verify_replays()
    deps = verify_rendering(read("reports/render-inputs.json"))
    count = verify_media()
    if check_preservation:
        local = strict_json((ROOT / "TestResults/vfx1c/initial-local-audit.json").read_bytes())
        require(local["renderCarry"] == CARRY and all(digest(ROOT / path) == value
                for path, value in (local["renderCarry"] | local["untracked"]).items()), "local preservation")
    print(json.dumps(dict(status="PASS", source=source, protected=protected, artifacts=artifacts, actualDependencies=deps,
                          videoFrames=count, totals=manifest["totals"], testedSource=manifest["testedSource"],
                          mediaSource=manifest["mediaSource"], evidenceSource=manifest["evidenceSource"],
                          localPreservation="PASS" if check_preservation else "not requested")))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--check-local-preservation", action="store_true")
    arguments = parser.parse_args()
    try:
        main(arguments.check_local_preservation)
    except (ValueError, TypeError, KeyError, IndexError, OSError, subprocess.CalledProcessError) as error:
        print("FAIL:", error, file=sys.stderr)
        sys.exit(1)
