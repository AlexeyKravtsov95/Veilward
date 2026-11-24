"""Read-only verification of the exact #93 Git delivery, tests and Editor artifacts."""
import argparse
import gzip
import json
from pathlib import Path
import re
import subprocess
import sys

sys.dont_write_bytecode = True
PACKET = Path(__file__).resolve().parent
ROOT = PACKET.parents[2]
sys.path.insert(0, str(ROOT / "ArtSource/Review"))
from pack_vfx1d_evidence import digest, probe_semantics, require, source_raster, stable_probe, verify_raster, visible

BASE = "913adb1d4d255fe4fc6683c18091b8379f38f917"
SOURCE = {
    "Assets/_Game/Scripts/Presentation/" + name for name in (
        "AbilityController.cs", "AbilityEffectView.cs", "AbilityPreviewView.cs", "BattleSceneController.cs",
        "BossCueView.cs", "BossCueView.cs.meta")
} | {
    "Assets/_Game/Tests/EditMode/AbilityPresentationTests.cs",
    "Assets/_Game/Tests/PlayMode/AbilityPlayModeTests.cs",
    "Assets/_Game/Tests/PlayMode/EnemyModelPlayModeTests.cs",
}
SUPPORT = {
    "README.md", "Docs/REGRESSION.md", "Docs/ROADMAP.md", "Docs/TECH_DESIGN.md", "Docs/VFX1D_RIFT_SEAL_BOSS.md",
    "ArtSource/Review/RecordRiftVfx.cs", "ArtSource/Review/run_vfx1d_editor.py",
    "ArtSource/Review/run_vfx1d_gates.py", "ArtSource/Review/pack_vfx1d_evidence.py",
}
MATERIAL_CARRY = {
    "Assets/Settings/Mobile_RPAsset.asset",
    *{"Assets/_Game/Art/Materials/Graybox" + suffix + ".mat" for suffix in ("Selection", "Tower", "TowerPreview")},
}
SUITES = {"EditMode": 1298, "PlayMode": 100}
POOLS = {"flame": 8, "basic": 12, "hybrid": 8, "reaction": 24, "ability": 32, "boss": 4,
         "flameLines": 104, "basicLines": 48, "hybridLines": 32, "reactionLines": 120, "abilityLines": 36, "bossLines": 4}
FAMILIES = {"flame", "tide", "gale", "stone", "steam", "storm"}
STATUSES = {"Burn", "Wet", "Gust", "Crack"}
CELLS = {(kind, trigger) for kind in ("Steam", "Storm") for trigger in ("Intrinsic", "StatusPair")}
BOUNDARIES = {
    "earned": {"earned-pause", "file-continue", "earned-impact", "boss-death"},
    "dense-all-open": {"invalid-preview", "valid-preview", "cancel", "dense-range", "dense-inspector", "four-status-inspector", "dense-impact",
                       "dense-combat", "boss-spawn", "boss-threat", "boss-leak"},
}


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def tree(ref):
    entries = {}
    for line in git("ls-tree", "-rz", ref).split(b"\0"):
        if line:
            attributes, path = line.split(b"\t", 1)
            entries[path.decode()] = attributes.decode()
    return entries


def packet_json(name):
    return json.loads((PACKET / name).read_bytes())


def packed_json(name):
    return json.loads(gzip.decompress((PACKET / name).read_bytes()))


def changed_paths(base, final, expected):
    changed = {path for path in base.keys() | final.keys() if base.get(path) != final.get(path)}
    require(changed == expected, "Protected Git paths changed.")
    return changed


def verify_git(manifest):
    base, final = tree(BASE), tree("HEAD")
    require(manifest["base"] == BASE, "Signed-off scope uses the specified main base.")
    require(set(manifest["changedSource"]) == SOURCE | SUPPORT, "Exact authored scope.")
    packet_path = PACKET.relative_to(ROOT).as_posix() + "/"
    actual_artifacts = {path[len(packet_path):] for path in final if path.startswith(packet_path)}
    require(actual_artifacts == set(manifest["artifacts"]) | {"manifest.json"}, "Exact Git artifact inventory.")
    changed = changed_paths(base, final, SOURCE | SUPPORT | {packet_path + item for item in actual_artifacts})
    local_delta = {path.decode() for path in git("diff", "--name-only", "-z", "HEAD").split(b"\0") if path}
    require(local_delta <= MATERIAL_CARRY, "Unexpected tracked local inputs differ.")
    protected = "\n".join(f"{path}\t{base[path]}" for path in sorted(base.keys() - changed)).encode()
    require(digest(protected) == manifest["protectedTreeSha256"], "Protected tree digest.")
    for name, sha in {**manifest["changedSource"], **{packet_path + k: v for k, v in manifest["artifacts"].items()}}.items():
        expected = git("show", "HEAD:" + name)
        require(digest(expected) == sha and expected == (ROOT / name).read_bytes(), "Tracked and checked-out identity " + name)
    for key in ("testedSource", "mediaSource"):
        ref = manifest[key]
        git("merge-base", "--is-ancestor", BASE, ref)
        git("merge-base", "--is-ancestor", ref, "HEAD")
        supplied = tree(ref)
        require(all(final.get(path) == supplied.get(path) for path in (SOURCE | {p for p in base if p.startswith(("Assets/", "Packages/", "ProjectSettings/"))})),
                key + " Unity input blobs equal current Git runtime.")
    require(tree(manifest["mediaSource"])["ArtSource/Review/RecordRiftVfx.cs"] == final["ArtSource/Review/RecordRiftVfx.cs"],
            "Media harness blob.")
    return len(base.keys() - changed), len(actual_artifacts)


def verify_suite(report, mode, total):
    if mode == "EditMode":
        require(report["Mode"] == mode and report["success"] is True and report["FilterApplied"] in (False, None)
                and report["result"] is None, "Unfiltered completed EditMode.")
        summary, results = report["Summary"], report["Results"]
    else:
        require(report["status"] == "completed" and not report.get("filter") and not report.get("FilterApplied"), "Unfiltered completed PlayMode.")
        summary, results = report["summary"], report["results"]
    counts = {key.lower(): value for key, value in summary.items()}
    names = [row["FullName"] for row in results]
    require(total > 0 and counts == dict(total=total, passed=total, failed=0, skipped=0, inconclusive=0) and
            all(type(v) is int for v in counts.values()) and total == len(names) == len(set(names)) and
            all(row["FullName"].strip() and row["Status"] == "Passed" for row in results), mode + " unique full PASS.")
    return names


def verify_gates(manifest):
    audit = packet_json("reports/gates-byte-audit.json")
    require(audit["testedSource"] == manifest["testedSource"] and audit["results"] == {"static": 0, "edit-mode": 0, "play-mode": 0}
            and audit["allOriginalInputsRestored"] and audit["editorInputsRefreshed"] and not audit["recoveryNeeded"]
            and not audit["unexpected"] and not audit["newInputs"] and not audit["commandDiagnostics"], "Full gate and restore receipt.")
    require(audit["carryInSha256"] == manifest["renderCarry"] and set(manifest["renderCarry"]) == MATERIAL_CARRY, "Carry-in identities.")
    static = (PACKET / "reports/static-command.txt").read_text()
    require(re.search(r"Ran 38 tests.*\n\nOK\nStatic Unity checks: PASS", static), "Static 38 and repository check.")
    compile_report = packet_json("reports/compile-final.json")
    require(compile_report["success"] is True and compile_report["data"]["summary"]["errors"] == 0, "Compile completed.")
    for mode, key in (("EditMode", "edit-mode"), ("PlayMode", "play-mode")):
        verify_suite(packed_json("reports/" + key + ".json.gz"), mode, SUITES[mode])


def exact_trace(start, before, after, report):
    require(before == after and digest(after) == report["traceSha256"] and len(after) == report["rawBytes"]
            and after.count(b"\n") == report["entries"], start + " exact full trace.")


def verify_replays():
    reports = packet_json("reports/replay-equality.json")
    for start, report in reports.items():
        before, after = (gzip.decompress((PACKET / "reports" / f"{phase}-{start}.trace.gz").read_bytes()) for phase in ("before", "after"))
        exact_trace(start, before, after, report)
        require(report["outcome"] == "Victory" and b"S|Victory|7|" in after and b"|D:" in after and b"|Cast:" in after
                and report["abilityKills"] > 0 and report["bossKills"] == 1
                and {(c["kind"], c["trigger"]) for c in report["cells"] if c["applied"] > 0} == CELLS, start + " REG214 coverage.")
    require(set(reports) == {"flame", "gale"}, "Both starting routes.")
    states = [(PACKET / "reports" / f"{name}-state.txt").read_bytes() for name in ("earned", "safe", "continued")]
    saves = [(PACKET / "reports" / f"{name}-file.json").read_bytes() for name in ("earned", "safe", "continued")]
    require(states[0] == states[1] == states[2] and states[0].startswith(b"S|Preparation|7|") and
            saves[0] == saves[1] == saves[2] and json.loads(saves[0])["state"]["waveIndex"] == 7,
            "Whole snapshot/Grid and exact durable Continue bytes.")


def verify_rows(scope, rows):
    require(len(rows) >= 100 and all(row["index"] == i and row["timeScale"] == 1 and row["captureFramerate"] == 0
                                    and re.fullmatch(r"[0-9a-f]{64}", row["sha256"]) for i, row in enumerate(rows)), "Realtime capture contract.")
    require(all(a["frame"] < b["frame"] and a["elapsed"] < b["elapsed"] for a, b in zip(rows, rows[1:])), "Raw frame order.")
    for row in rows:
        if row["phase"] is not None:
            require(row["productionUpdate"] and not row["genericBeams"] and max((item["slots"] for item in row["slots"]), default=0) <= 2,
                    "Production Update and two overhead slots.")
        casts = {(s["id"], s["tick"], s["sequence"]) for s in row["seal"] or [] if s["index"] == -1}
        for hit in (s for s in row["seal"] or [] if s["index"] >= 0):
            require(hit["loss"] > 0 and hit["enemy"] > 0 and hit["origin"] == "Ability" and
                    (hit["damageAbility"], hit["damageTick"], hit["damageCast"]) == (hit["id"], hit["tick"], hit["sequence"])
                    and (hit["id"], hit["tick"], hit["sequence"]) in casts, "Matching positive damage, attached to visible cast.")
        for cue in row["cues"] or []:
            if cue["kind"] in ("Spawn", "Threat"):
                require(row["boss"] and cue["enemy"] == row["boss"]["id"], "Live cue owner.")
            else:
                require(row["boss"] is None and any(p["IsTail"] and p["Pose"] == cue["kind"] for p in row["poses"]), "Terminal cue and ART5 tail.")
        for reaction in row["reactions"] or []:
            hits = reaction["hits"]
            require(len(hits) <= reaction["limit"] and len({h["index"] for h in hits}) == len(hits) and
                    (reaction["kind"] != "Storm" or all(1 <= h["index"] <= 2 for h in hits)) and
                    len(hits) == len({h["enemy"] for h in hits}), "Reaction hit limits and exact unique indices.")
    if scope == "earned":
        pause = [r for r in rows if r["stage"] == "pause"]
        resume = [r for r in rows if r["stage"] == "resume"]
        continues = [r for r in rows if r["stage"] == "file-continue"]
        require(len(pause) > 2 and all(r["paused"] and r["tick"] == pause[0]["tick"] and r["seal"] == pause[0]["seal"] for r in pause)
                and pause[0]["seal"] and resume and max(r["tick"] for r in resume) > pause[0]["tick"]
                and continues and all(r["phase"] == "Preparation" and not any(r[k] for k in ("shots", "seal", "cues", "slots", "reactions"))
                                      for r in continues), "Pause/freeze, Resume and clean file Continue.")
    else:
        require({s["kind"] for r in rows for s in r["shots"] or []} == FAMILIES and
                {status for r in rows for e in r["enemyStatuses"] or [] for status in e["statuses"]} == STATUSES and
                {(rx["kind"], rx["trigger"]) for r in rows for rx in r["reactions"] or []
                 if any(h["loss"] > 0 for h in rx["hits"])} == CELLS and
                any(r["boss"] and r["enemies"] >= 6 and r["seal"] and
                    any(h["loss"] > 0 for rx in r["reactions"] or [] for h in rx["hits"]) for r in rows),
                "Dense actual Update: family/status coverage and overlapping ability, boss and reaction.")
        require(all(not r["preview"] and not r["seal"] for r in rows if r["stage"] == "cancel"), "Cancel has no visuals or cast.")


def verify_pick(name, row, pick):
    path = PACKET / "playmode" / (name + ".png")
    require(row["index"] == pick["index"] and row["stage"] == pick["stage"] and row["tick"] == pick["tick"]
            and digest(path.read_bytes()) == row["sha256"] == pick["sourceSha256"], "Exact PNG frame " + name)
    allowed_stage = {"earned-pause": {"pause"}, "earned-impact": {"earned-combat", "resume"},
                     "dense-combat": {"dense-combat", "dense-live"}}.get(name, {name})
    require(name.startswith("boss-") or row["stage"] in allowed_stage, "Expected named phase " + name)
    lines = []
    if name.startswith("boss-"):
        kind = name.split("-", 1)[1].title()
        lines = [cue["lines"] for cue in row["cues"] if cue["kind"] == kind]
    elif name.endswith("-preview"):
        lines = [row["preview"]]
        marks = [line for line in row["preview"] if line["name"] == "Preview mark"]
        require(len(marks) == 1 and marks[0]["loop"] == (name == "valid-preview"), name + " validity geometry.")
    elif name.endswith("-impact"):
        lines = [hit["lines"] for hit in row["seal"] if hit["loss"] is not None and hit["loss"] > 0]
        if name == "earned-impact":
            require(any(hit["lethal"] for hit in row["seal"]), "Earned lethal attribution.")
    if name.startswith("boss-") or name.endswith(("-preview", "-impact")):
        require(lines and max(visible(path, stroke) for stroke in lines) == pick["palettePixels"] >= 6, "Visible strokes " + name)
    if name == "four-status-inspector":
        require(row["inspector"] and all(kind + ": power" in row["inspector"] for kind in STATUSES) and
                any(item["slots"] == 2 for item in row["slots"]), "Four inspector statuses with two overhead slots.")
    if name == "dense-range":
        require(row["selectedTower"] > 0 and row["boss"] and row["phase"] == "Combat", "Selected tower range in live boss Combat.")


def verify_video(manifest, scope):
    report = packet_json(f"reports/{scope}-video.json")
    completion = packet_json(f"reports/{scope}-completion.json")
    rows = packed_json(f"reports/{scope}-frames.json.gz")
    verify_rows(scope, rows)
    video = PACKET / (scope + ".mp4")
    stream, pts = stable_probe(video)
    require(report["source"] == manifest["mediaSource"] and report["sha256"] == digest(video.read_bytes()) and
            report["stream"] == stream == dict(width=1600, height=900, pix_fmt="yuv420p", codec_name="h264") and
            completion["ok"] is True and not completion["errors"] and len(pts) == len(rows) == completion["frames"] == report["frameCount"],
            "Exact MP4 identity and complete output.")
    require(pts == report["pts"] and all(a < b for a, b in zip(pts, pts[1:])) and
            max(abs(row["elapsed"] - rows[0]["elapsed"] - timestamp) for row, timestamp in zip(rows, pts)) < .003, "Stable FFprobe PTS/order.")
    raster = gzip.decompress((PACKET / "reports" / f"{scope}-source-raster.rgb.gz").read_bytes())
    verify_raster(video, raster, len(rows))
    require(set(report["picks"]) == BOUNDARIES[scope], "Exact PNG stage inventory.")
    for name, pick in report["picks"].items():
        index = pick["index"]
        require(0 <= index < len(rows) and pick["pts"] == pts[index], "PNG associates with video PTS.")
        verify_pick(name, rows[index], pick)
        downsample = source_raster([PACKET / "playmode" / (name + ".png")])
        require(raster[index * len(downsample):(index + 1) * len(downsample)] == downsample, "Exact pre-encode raster for " + name)
    return rows


def verify_render_inputs(manifest):
    for scope in (*BOUNDARIES, "profile-all-open"):
        console = packet_json(f"reports/{scope}-console.json")
        ticking = packet_json(f"reports/{scope}-ticking.json")
        require(not console["entries"] and console["returned"] == 0 and not console["dropped"] and
                not console["groundTruth"]["compilationFailed"] and ticking["met"] and ticking["framesObserved"] > 0,
                "New Editor errors and live-loop receipt " + scope)
    for scope in BOUNDARIES:
        data = packet_json(f"reports/{scope}-render-inputs.json")
        require(data["counts"]["pooled"] == POOLS and data["counts"]["genericBeams"] == 0 and data["counts"]["clean"] and
                data["unity"] == "6000.3.28f1" and data["quality"] == "Mobile" and data["graphics"] == "Metal",
                "Actual render environment, pool construction and absence of generic production beams.")
        matches = dict((row["path"], row["sha256"]) for row in data["dependencies"])
        require(len(matches) == len(data["dependencies"]) and set(manifest["renderCarry"]).issubset(matches) and
                all(matches[path] == sha for path, sha in manifest["renderCarry"].items()), "Actual dependency and carry-in inventory.")
        for name, expected in matches.items():
            path = ROOT / name
            if name.startswith("Packages/com."):
                _, package, relative = name.split("/", 2)
                paths = [p / relative for p in (ROOT / "Library/PackageCache").glob(package + "@*")]
                require(any(path.is_file() and digest(path.read_bytes()) == expected for path in paths),
                        "Actual package missing or differs " + name)
            else:
                require(path.is_file() and digest(path.read_bytes()) == expected, "Actual render input differs " + name)
        require(len(data["lineMaterials"]) == len({m["path"] for m in data["lineMaterials"]}) and
                all(m["path"] and m["shader"] for m in data["lineMaterials"]), "Actual shared line materials.")
        for package, version in {"pipeline": "0.10.0-exp.1", "render-pipelines.core": "17.3.0",
                                 "render-pipelines.universal": "17.3.0", "inputsystem": "1.20.1",
                                 "test-framework": "1.6.0"}.items():
            path = f"Packages/com.unity.{package}/package.json"
            require(any(p.is_file() and digest(p.read_bytes()) == matches[path] and
                        json.loads(p.read_bytes())["version"] == version
                        for p in (ROOT / "Library/PackageCache").glob(f"com.unity.{package}@*/package.json")),
                    "The matching installed package.json metadata for " + package)
    profile = packet_json("reports/profile.json")
    require(len(profile["passes"]) == 2, "Two profile repeats.")
    for run in profile["passes"]:
        require(run["before"] == run["restored"] and run["before"]["clean"] and run["endTick"] > run["startTick"] > 0 and run["seconds"] >= 6
                and len(run["rows"]) > 20 and all(run[phase]["pooled"] == POOLS and run[phase]["genericBeams"] == 0
                                                  for phase in ("before", "dense", "after", "restored")), "Scene counts, clocks and bounded pool repeats.")


def controls(earned, dense):
    from copy import deepcopy
    checks = []

    def reject(name, action):
        try:
            action()
        except (ValueError, KeyError, IndexError):
            checks.append(name)
            return
        raise ValueError("Negative control accepted " + name)

    edit = packed_json("reports/edit-mode.json.gz")
    duplicate = deepcopy(edit)
    duplicate["Results"][0] = duplicate["Results"][1]
    reject("duplicate suite case", lambda: verify_suite(duplicate, "EditMode", SUITES["EditMode"]))
    incomplete = packed_json("reports/play-mode.json.gz")
    incomplete["status"] = "running"
    reject("incomplete PlayMode", lambda: verify_suite(incomplete, "PlayMode", SUITES["PlayMode"]))
    jump = deepcopy(earned)
    next(r for r in jump if r["stage"] == "pause")["tick"] += 2
    reject("Pause tick drift", lambda: verify_rows("earned", jump))
    miss = deepcopy(dense)
    next(s for r in miss for s in r["seal"] or [] if s["index"] >= 0)["damageCast"] += 1
    reject("wrong ability cast identity", lambda: verify_rows("dense-all-open", miss))
    no_ward = deepcopy(dense)
    next(c for r in no_ward for c in r["cues"] or [] if c["kind"] == "Spawn")["enemy"] += 1
    reject("wrong Warden owner", lambda: verify_rows("dense-all-open", no_ward))
    pick = packet_json("reports/dense-all-open-video.json")["picks"]["dense-impact"]
    pick["sourceSha256"] = "0" * 64
    reject("wrong physical PNG", lambda: verify_pick("dense-impact", dense[pick["index"]], pick))
    base = {"protected.unity": "original", "allowed.cs": "original"}
    changed = {"protected.unity": "modified", "allowed.cs": "modified"}
    reject("modified protected Git path", lambda: changed_paths(base, changed, {"allowed.cs"}))
    trace = b"S|Victory|7|3805\n"
    receipt = dict(traceSha256=digest(trace), rawBytes=len(trace), entries=1)
    exact_trace("fixture", trace, trace, receipt)
    reject("changed replay byte", lambda: exact_trace("fixture", trace, trace[:-2] + b"6\n", receipt))
    basic_probe = {"streams": [{"width": 1600, "height": 900, "pix_fmt": "yuv420p", "codec_name": "h264"}],
                   "frames": [{"best_effort_timestamp_time": "0.000000"}, {"best_effort_timestamp_time": "0.034000"}]}
    alternate = deepcopy(basic_probe)
    alternate["frames"][0]["side_data_list"] = [{"side_data_type": "H.26[45] User Data Unregistered SEI message"}]
    alternate["streams"][0]["tags"] = {"encoder": "different ffprobe output"}
    require(probe_semantics(basic_probe) == probe_semantics(alternate), "Optional FFprobe metadata control.")
    del alternate["frames"][0]["best_effort_timestamp_time"]
    reject("missing required FFprobe PTS", lambda: probe_semantics(alternate))
    print("Negative controls:", ", ".join(checks))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--controls", action="store_true")
    args = parser.parse_args()
    manifest = packet_json("manifest.json")
    protected, artifacts = verify_git(manifest)
    verify_gates(manifest)
    verify_replays()
    earned = verify_video(manifest, "earned")
    dense = verify_video(manifest, "dense-all-open")
    verify_render_inputs(manifest)
    if args.controls:
        controls(earned, dense)
    print(f"PASS: {protected} protected Git paths, {artifacts} artifacts, complete suites, replay, Editor media and profile.")


if __name__ == "__main__":
    main()
