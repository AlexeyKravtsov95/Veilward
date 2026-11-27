"""Run the AUDIO.1 recorder in the resident Unity Editor and encode its master mix."""
import json
from pathlib import Path
import subprocess
import sys
import time

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "Review"))
import run_ui1_editor as editor

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "TestResults/audio1"
TAKE = OUT / "capture"


def main():
    if TAKE.exists():
        raise FileExistsError(TAKE)
    editor.OUT = OUT / "capture-commands"
    editor.OUT.mkdir(parents=True, exist_ok=True)
    status = editor.command("editor", "status", report="before")
    baseline = editor.evaluate(
        'var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene(); '
        'if (scene.isDirty) throw new System.InvalidOperationException("Unsaved editor scene"); '
        'var type=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand")).First(t=>t!=null); '
        'var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic; '
        'return new { scene=scene.path, autoTick=(bool)type.GetProperty("IsEnabled",flags).GetValue(null), '
        'interval=(long)type.GetProperty("CurrentIntervalMs",flags).GetValue(null) };', report="baseline")
    if status["playMode"] != "stopped":
        raise RuntimeError("Existing Play Mode session")
    size = editor.entry("SetWide", "size")
    cursor = editor.command("console", "status")["cursor"]
    try:
        editor.command("editor", "set_auto_tick", "--enabled", "true", "--interval-ms", "16")
        editor.command("editor.playmode", "set_run_in_background", "--enabled", "true")
        editor.command("editor.playmode", "start")
        editor.ready()
        editor.command("script", "run", "--file", "ArtSource/Audio/RecordEditorAudio.cs", "--entry",
                       "RecordEditorAudio.Begin", report="capture-start")
        deadline = time.monotonic() + 320
        manifest = TAKE / "manifest.json"
        while not manifest.exists():
            if time.monotonic() >= deadline:
                raise RuntimeError("Audio capture did not finish")
            time.sleep(.75)
        data = json.loads(manifest.read_text())
        if data["errors"] or data["peak"] < .005:
            raise RuntimeError(data)
        channels = round(data["samples"] / data["frames"] * data["frameRate"] / data["sampleRate"])
        if channels != 2:
            raise RuntimeError("Expected stereo engine capture")
    finally:
        editor.command("console", "get", "--level", "error", "--since", str(cursor), report="console")
        editor.command("editor.playmode", "stop")
        editor.ready()
        editor.command("editor.playmode", "set_run_in_background", "--enabled", str(status["runInBackground"]).lower())
        editor.command("editor", "set_auto_tick", "--enabled", str(baseline["autoTick"]).lower(),
                       "--interval-ms", str(baseline["interval"]))
        editor.evaluate('var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"); '
                        'var window=UnityEditor.EditorWindow.GetWindow(type); '
                        'type.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|'
                        f'System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).SetValue(window,{size["previous"]}); '
                        'return true;')
        editor.open_scene(baseline["scene"])
        editor.command("editor", "status", report="after")
    command = [
        "ffmpeg", "-hide_banner", "-loglevel", "error", "-framerate", str(data["frameRate"]),
        "-i", str(TAKE / "frames/%05d.png"), "-f", "f32le", "-ar", str(data["sampleRate"]), "-ac", str(channels),
        "-i", str(TAKE / "mix.f32"), "-c:v", "libx264", "-crf", "23", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "144k", "-movflags", "+faststart", "-shortest", str(TAKE / "editor-game-audio.mp4"),
    ]
    subprocess.run(command, check=True, cwd=ROOT)
    (TAKE / "encode-command.json").write_text(json.dumps(command, indent=2) + "\n")


if __name__ == "__main__":
    main()
