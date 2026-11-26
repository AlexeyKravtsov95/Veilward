"""Проверка diff сохранённых Unity сцен относительно owner-merged ART.REWORK.0a."""
import re
import subprocess
from pathlib import Path

BASE = "ff1e129562879eb31a66820d52d428112d6e3d15"
ROOT = Path(__file__).resolve().parents[4]
SCENES = ("Assets/_Game/Scenes/MainMenu.unity", "Assets/_Game/Scenes/Battle.unity")
SCENE_RECORD = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\n", re.M)
LOCAL_REF = re.compile(r"\{fileID: (-?\d+)\}")


def records(content):
    parts = SCENE_RECORD.split(content)[1:]
    return {parts[i + 1]: (parts[i], parts[i + 2]) for i in range(0, len(parts), 3)}


def canvas_subtree(scene, name):
    (canvas_id,) = (key for key, (kind, data) in scene.items()
                    if kind == "1" and re.search(rf"^  m_Name: {re.escape(name)}$", data, re.M))
    transform_links = {}
    for key, (kind, data) in scene.items():
        if kind in ("4", "224"):
            game_object = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}$", data, re.M)
            parent = re.search(r"^  m_Father: \{fileID: (-?\d+)\}$", data, re.M)
            if game_object and parent:
                transform_links[key] = (game_object.group(1), parent.group(1))
    (canvas_transform,) = (key for key, (owner, _) in transform_links.items() if owner == canvas_id)
    descendants = {canvas_transform}
    while True:
        children = {key for key, (_, parent) in transform_links.items() if parent in descendants}
        if children <= descendants:
            break
        descendants.update(children)
    objects = {transform_links[key][0] for key in descendants}
    components = set()
    for key, (_, data) in scene.items():
        match = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}$", data, re.M)
        if match and match.group(1) in objects:
            components.add(key)
    return objects | components


def scene_scope(path):
    before = records(subprocess.check_output(["git", "show", f"{BASE}:{path}"], cwd=ROOT, text=True))
    after = records((ROOT / path).read_text())
    common = before.keys() & after.keys()
    old_ui = before.keys() - after.keys()
    new_ui = after.keys() - before.keys()
    canvas = "HUD" if path.endswith("/Battle.unity") else "Main Menu Canvas"
    assert old_ui <= canvas_subtree(before, canvas), (path, "Non-UI records removed")
    assert new_ui <= canvas_subtree(after, canvas), (path, "Non-UI records added")
    stable = 0
    rewritten = []
    for key in sorted(common):
        old_kind, old_data = before[key]
        new_kind, new_data = after[key]
        assert old_kind == new_kind, (path, key)
        if old_data == new_data:
            stable += 1
            continue

        assert old_kind in ("114", "1660057539"), (path, key, old_kind)
        # Увеличение Canvas не меняет старые world refs, данные компонентов и SceneRoots.
        normalize = lambda data, ui: LOCAL_REF.sub(
            lambda m: "{fileID: UI}" if m.group(1) in ui else m.group(), data
        )
        old_normalized = normalize(old_data, old_ui)
        new_normalized = normalize(new_data, new_ui)
        if old_kind == "1660057539":
            assert sorted(old_normalized.splitlines()) == sorted(new_normalized.splitlines()), (path, key)
        else:
            assert old_normalized == new_normalized, (path, key)
        rewritten.append(key)

    if path.endswith("/Battle.unity"):
        assert stable >= 380 and len(rewritten) == 9
    else:
        assert stable >= 14 and len(rewritten) == 2
    for key, (_, data) in after.items():
        dangling = set(LOCAL_REF.findall(data)) - after.keys() - {"0"}
        assert not dangling, (path, key, dangling)
    print(f"{Path(path).name}: {stable} identical records, {len(rewritten)} UI-reference-only records, "
          f"{len(old_ui)} replaced UI records, {len(new_ui)} new UI records, zero dangling local refs")


def main():
    for path in SCENES:
        scene_scope(path)


if __name__ == "__main__":
    main()
