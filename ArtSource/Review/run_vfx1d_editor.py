"""Run the #93 replay, GameView captures and non-capture profile in the existing Editor."""
import argparse
import json
from pathlib import Path
import shlex
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "TestResults/vfx1d"
RUNS = {"Earned": "earned", "Dense": "dense-all-open", "Profile": "profile-all-open"}
SCRIPT = "ArtSource/Review/RecordRiftVfx.cs"
FLAGS = ("--caller", "plugin", "--skill", "unity-cli", "--format", "json")


def run(*command, name=None, timeout=60):
    with (OUT / "media-commands.txt").open("a") as journal:
        journal.write(shlex.join(command) + "\n")
    result = subprocess.run(command, cwd=ROOT, text=True, capture_output=True, timeout=timeout)
    if name:
        (OUT / (name + ".json")).write_text(result.stdout)
        if result.stderr:
            (OUT / (name + "-stderr.txt")).write_text(result.stderr)
    if result.returncode:
        raise RuntimeError(f"{shlex.join(command)}: {result.stderr}\n{result.stdout}")
    return json.loads(result.stdout)


def unity(*command, **options):
    return run("unity", "command", *FLAGS, *command, **options)


def ready():
    return run("unity", "status", "--until-ready", "--project-path", str(ROOT), "--format", "json")


def replay(phase):
    original = ROOT / "ArtSource/Review/CaptureHybridReplays.cs"
    script = OUT / "CaptureHybridReplays.cs"
    script.write_bytes(original.read_bytes().replace(b"TestResults/vfx1c/replay", b"TestResults/vfx1d/replay"))
    return unity("script", "run", "--file", str(script.relative_to(ROOT)),
                 "--entry", "CaptureHybridReplays." + phase.capitalize(), name="replay-" + phase)


def scene(path):
    unity("script", "eval", "--code", 'var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene(); '
          'if(active.isDirty) throw new System.InvalidOperationException("Expected a saved Editor scene."); '
          f'return UnityEditor.SceneManagement.EditorSceneManager.OpenScene("{path}").path;')


def capture(entries):
    status = unity("editor", "status", name="editor-before")
    if status["playMode"] != "stopped":
        raise RuntimeError("Existing Editor is already in a Play Mode session.")
    state = unity("script", "eval", "--code",
                  'var type=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand")).First(t=>t!=null); '
                  'var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic; '
                  'return new { active=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, '
                  'enabled=(bool)type.GetProperty("IsEnabled",flags).GetValue(null), '
                  'interval=(long)type.GetProperty("CurrentIntervalMs",flags).GetValue(null) };',
                  name="editor-settings-before")["result"]
    try:
        scene("Assets/_Game/Scenes/MainMenu.unity")
        unity("script", "run", "--file", "ArtSource/Review/CaptureArt5.cs", "--entry", "CaptureArt5.ConfigureView", name="game-view")
        unity("editor", "set_auto_tick", "--enabled", "true", "--interval-ms", "16")
        unity("editor.playmode", "set_run_in_background", "--enabled", "true")
        for entry in entries:
            run_dir = OUT / RUNS[entry]
            if run_dir.exists():
                raise FileExistsError(run_dir)
            unity("editor.playmode", "start")
            try:
                ready()
                cursor = unity("console", "status")["cursor"]
                wait = unity("job", "wait", "--condition", '{"member":"UnityEngine.Time.frameCount","op":"changed"}', "--timeout", "12",
                             name=RUNS[entry] + "-ticking")
                if wait.get("met") is not True:
                    raise RuntimeError(f"Editor did not advance before {entry}: {wait}")
                unity("script", "run", "--file", SCRIPT, "--entry", "RiftVfxRecording." + entry,
                      name=RUNS[entry] + "-start")
                finish = run_dir / "completion.json"
                deadline = time.monotonic() + 110
                while not finish.exists() and time.monotonic() < deadline:
                    time.sleep(.5)
                if not finish.exists():
                    raise RuntimeError(f"{entry} did not produce a completion report.")
                report = json.loads(finish.read_bytes())
                unity("console", "get", "--since", str(cursor), "--level", "error", name=RUNS[entry] + "-console")
                if not report["ok"]:
                    raise RuntimeError(f"{entry}: {report['errors']}")
                print(entry, report, flush=True)
            finally:
                unity("editor.playmode", "stop")
                ready()
    finally:
        unity("editor.playmode", "set_run_in_background", "--enabled", str(status["runInBackground"]).lower())
        unity("editor", "set_auto_tick", "--enabled", str(state["enabled"]).lower(),
              "--interval-ms", str(state["interval"]))
        if state["active"]:
            scene(state["active"])
        unity("editor", "status", name="editor-after")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("steps", nargs="+", choices=tuple(RUNS) + ("Before", "After"))
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    media = []
    for step in args.steps:
        if step in RUNS:
            media.append(step)
        else:
            replay(step.lower())
    if media:
        head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True)
        (OUT / "media-head.txt").write_text(head)
        capture(media)


if __name__ == "__main__":
    main()
