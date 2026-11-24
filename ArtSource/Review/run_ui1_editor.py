"""Capture #48 through the resident Editor, preserving the open scene and GameView selection."""
import argparse
import json
from pathlib import Path
import shlex
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "TestResults/ui1/media"
RECORDER = "ArtSource/Review/CaptureProductionUi.cs"
MENU = "Assets/_Game/Scenes/MainMenu.unity"


def run(*args, report=None):
    with (OUT / "commands.txt").open("a") as journal:
        journal.write(shlex.join(args) + "\n")
    result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, timeout=55)
    if report:
        (OUT / (report + ".json")).write_text(result.stdout)
        if result.stderr:
            (OUT / (report + "-stderr.txt")).write_text(result.stderr)
    if result.returncode:
        raise RuntimeError(f"{shlex.join(args)}: {result.stdout}\n{result.stderr}")
    return json.loads(result.stdout)


def command(*args, **options):
    return run("unity", "command", "--caller", "plugin", "--skill", "unity-cli", "--format", "json", *args, **options)


def evaluate(code, **options):
    result = command("script", "eval", "--code", code, **options)
    if not result["success"]:
        raise RuntimeError(result)
    return result["result"]


def ready():
    return run("unity", "status", "--until-ready", "--project-path", str(ROOT), "--format", "json")


def open_scene(path):
    return evaluate('var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene(); '
                    'if (current.isDirty) throw new System.InvalidOperationException("The active scene has unsaved changes."); '
                    f'return UnityEditor.SceneManagement.EditorSceneManager.OpenScene({json.dumps(path)}).path;')


def entry(name, report):
    result = command("script", "run", "--file", RECORDER, "--entry", "CaptureProductionUi." + name, report=report)
    if not result["success"]:
        raise RuntimeError(result)
    return result["result"]


def capture(profile):
    cursor = command("console", "status")["cursor"]
    command("editor.playmode", "start")
    try:
        ready()
        wait = command("job", "wait", "--condition", '{"member":"UnityEngine.Time.frameCount","op":"changed"}',
                       "--timeout", "12", report=profile.lower() + "-ticking")
        if not wait.get("met"):
            raise RuntimeError(f"{profile} did not advance a production frame.")
        entry(profile, profile.lower() + "-launch")
        manifest = OUT / profile.lower() / "manifest.json"
        deadline = time.monotonic() + 120
        while not manifest.exists() and time.monotonic() < deadline:
            time.sleep(.5)
        if not manifest.exists():
            raise RuntimeError(f"{profile} did not produce a completion manifest.")
        result = json.loads(manifest.read_bytes())
        if not result["complete"]:
            raise RuntimeError(result["errors"])
        print(profile, len(result["shots"]), "PNG", len(result["frames"]), "video frames", flush=True)
    finally:
        command("console", "get", "--level", "error", "--since", str(cursor), report=profile.lower() + "-console")
        command("editor.playmode", "stop")
        ready()


def main(profiles):
    OUT.mkdir(parents=True, exist_ok=True)
    status = command("editor", "status", report="editor-before")
    if status["playMode"] != "stopped":
        raise RuntimeError("Existing Editor has an active Play Mode session.")
    baseline = evaluate(
        'var type=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand")).First(t=>t!=null); '
        'var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic; '
        'return new { scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, '
        'autoTick=(bool)type.GetProperty("IsEnabled",flags).GetValue(null), '
        'interval=(long)type.GetProperty("CurrentIntervalMs",flags).GetValue(null) };', report="baseline")
    prior_size = None
    try:
        open_scene(MENU)
        command("editor", "set_auto_tick", "--enabled", "true", "--interval-ms", "16")
        command("editor.playmode", "set_run_in_background", "--enabled", "true")
        for profile in profiles:
            if (OUT / profile.lower()).exists():
                raise FileExistsError(OUT / profile.lower())
            size = entry("SetWide" if profile == "Final" else "Set" + profile, profile.lower() + "-size")
            if prior_size is None:
                prior_size = size["previous"]
            capture(profile)
    finally:
        command("editor.playmode", "set_run_in_background", "--enabled", str(status["runInBackground"]).lower())
        command("editor", "set_auto_tick", "--enabled", str(baseline["autoTick"]).lower(),
                "--interval-ms", str(baseline["interval"]))
        if prior_size is not None:
            evaluate('var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"); '
                     'var window=UnityEditor.EditorWindow.GetWindow(type); '
                     'type.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|'
                     f'System.Reflection.BindingFlags.NonPublic).SetValue(window,{prior_size}); return true;')
        if baseline["scene"]:
            open_scene(baseline["scene"])
        command("editor", "status", report="editor-after")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("profile", nargs="+", choices=("Small", "Wide", "Tablet", "Final"))
    main(parser.parse_args().profile)
