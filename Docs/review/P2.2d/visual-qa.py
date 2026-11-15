"""Реальные Editor frames; временный schema 2 fixture, без изменения production data.
Запуск: python3 Docs/review/P2.2d/visual-qa.py из worktree с открытым Editor.
"""
import json
import pathlib
import subprocess
import time

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = ROOT / "Docs/review/P2.2d"
LOG = OUT / "qa.log"
PREFIX = """using UnityEngine; using UnityEngine.UI; using UnityEditor;
using Veilward.Application; using Veilward.Domain; using Veilward.Infrastructure;
using Veilward.Presentation; using System.Linq; using System.IO;
"""

def record(label, value):
    line = label + " " + json.dumps(value, ensure_ascii=False)
    print(line, flush=True)
    with LOG.open("a") as f:
        f.write(line + "\n")


def command(name, *args):
    result = subprocess.run(["unity", "command", name, "--caller", "plugin", "--skill", "unity-cli",
        "--project-path", str(ROOT), "--format", "json", "--", *args], capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    data = json.loads(result.stdout)
    if isinstance(data, dict) and data.get("success") is False:
        raise RuntimeError(result.stdout)
    return data


def evaluate(code):
    data = command("script eval", "--code", PREFIX + code)
    if isinstance(data, dict) and "result" in data:
        return data["result"]
    if isinstance(data, dict) and "data" in data:
        return data["data"].get("result", data["data"])
    return data


def capture(name):
    time.sleep(.3)
    result = command("capture game_view", "--source", "screen", "--width", "1600", "--height", "900",
        "--output-path", str(OUT / name))
    record("CAPTURE " + name, result)


def state():
    return evaluate("""var b=Object.FindFirstObjectByType<BattleSceneController>();
var s=b.Simulation;
return new { frame=Time.frameCount, tick=s.State.Tick, phase=s.State.Phase.ToString(), gold=s.State.Gold,
 hp=s.State.BaseHealth, kills=s.State.Kills, paused=Object.FindFirstObjectByType<BattlePauseController>().IsPaused,
 towers=s.State.Towers.Select(t=>new { id=t.Id.Value,type=t.Definition.Id.Value,level=t.Level,investment=t.InvestedGold,next=t.NextAttackTick }).ToArray(),
 enemies=s.State.Enemies.Select(e=>new { id=e.Id.Value,hp=e.Health,steam=e.LastSteamTick,storm=e.LastStormTick }).ToArray() };""")


LOG.write_text("P2.2d — реальный Editor Play Mode flow. Synthetic validated Preparation fixture: 1000 Gold; production factory default remains 150.\n")
command("console clear")
evaluate('Application.runInBackground=true; EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity"); return true;'.replace('EditorSceneManager.', 'UnityEditor.SceneManagement.EditorSceneManager.'))
command("editor set_auto_tick")
command("editor.playmode start")
time.sleep(3)
command("editor focus")
record("FIXTURE", evaluate("""var config=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
var s=config.CreateSimulation(); var originalGold=s.State.Gold;
var cp=s.ExportCheckpoint(); cp.gold=1000;
var root=Path.GetFullPath("TestResults/P22dVisual"); Directory.CreateDirectory(root);
var store=new WaveCheckpointStore(root);
if(!store.TrySave(new WaveCheckpointEnvelope { schemaVersion=2,scenarioContentId=config.ScenarioContentId,state=cp },s.TryValidateCheckpoint,out var error)) throw new System.Exception(error);
MainMenuController.CheckpointStoreFactory=()=>new WaveCheckpointStore(root);
var menu=Object.FindFirstObjectByType<MainMenuController>(); menu.ConfigureStore(store);
if(!menu.ContinueButton.interactable) throw new System.Exception("Continue unavailable");
menu.ContinueButton.onClick.Invoke(); return new {originalGold,fixtureGold=cp.gold,unity=Application.unityVersion,platform=Application.platform.ToString(),target=EditorUserBuildSettings.activeBuildTarget.ToString()};"""))
time.sleep(2)
record("INITIAL", state())
record("CATALOG", evaluate("""var p=Object.FindFirstObjectByType<BattlePlacementController>();
p.TrySelectCell(new GridCoordinate(-4,-1));
return p.CatalogButtons.Select(b=>b.GetComponentInChildren<Text>().text).ToArray();"""))
capture("01-catalog.png")
evaluate('Object.FindFirstObjectByType<BattlePlacementController>().SelectTowerType(new TowerDefinitionId("steam")); return true;')
capture("02-steam-preview.png")
evaluate('var p=Object.FindFirstObjectByType<BattlePlacementController>(); p.ConfirmSelection(); p.ConfirmSelection(); p.TrySelectCell(new GridCoordinate(-2,-1)); p.SelectTowerType(new TowerDefinitionId("storm")); return true;')
capture("03-storm-preview.png")
evaluate('var p=Object.FindFirstObjectByType<BattlePlacementController>(); p.ConfirmSelection(); return true;')
record("BUILT", state())
evaluate('var p=Object.FindFirstObjectByType<BattlePlacementController>(); p.TrySelectCell(new GridCoordinate(-4,-1)); if(!p.UpgradeSelectedTower().Success) throw new System.Exception("Steam upgrade failed"); return p.TowerStatsText.text;')
capture("04-steam-upgraded.png")
evaluate('var p=Object.FindFirstObjectByType<BattlePlacementController>(); p.TrySelectCell(new GridCoordinate(-2,-1)); if(!p.UpgradeSelectedTower().Success) throw new System.Exception("Storm upgrade failed"); return p.TowerStatsText.text;')
capture("05-storm-upgraded.png")
record("UPGRADED", state())
evaluate('Object.FindFirstObjectByType<BattlePlacementController>().StartWave(); return true;')
time.sleep(3)
a=state(); record("COMBAT_A", a)
time.sleep(.3)
b=state(); record("COMBAT_B", b)
assert b["frame"] > a["frame"] and b["tick"] > a["tick"], (a,b)
capture("06-hybrid-combat.png")
deadline=time.monotonic()+40
while state()["phase"] == "Combat" and time.monotonic()<deadline:
    time.sleep(1)
record("WAVE_COMPLETE", state())
evaluate('UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu"); return true;')
time.sleep(1)
record("MENU", evaluate('var m=Object.FindFirstObjectByType<MainMenuController>(); if(!m.ContinueButton.interactable) throw new System.Exception("Continue unavailable"); return m.StatusText.text;'))
capture("07-continue-menu.png")
evaluate('Object.FindFirstObjectByType<MainMenuController>().ContinueButton.onClick.Invoke(); return true;')
time.sleep(1)
record("CONTINUED", state())
capture("08-continued-hybrids.png")
evaluate('var p=Object.FindFirstObjectByType<BattlePlacementController>(); p.ContinueToNextWave(); p.TrySelectCell(new GridCoordinate(-4,-1)); p.RequestSellSelectedTower(); return true;')
capture("09-sell-confirmation.png")
record("SOLD", evaluate('var p=Object.FindFirstObjectByType<BattlePlacementController>(); var first=p.ConfirmSellSelectedTower(); var repeated=p.ConfirmSellSelectedTower(); if(!first.Success || repeated.Success) throw new System.Exception("Sell invalid"); return new {refund=first.Refund,repeated=repeated.Success};'))
record("AFTER_SELL", state())
capture("10-after-sell.png")
record("CONSOLE_STATUS", command("console status"))
record("CONSOLE_WARNINGS_ERRORS", command("console get", "--level", "warn", "--tail", "100"))
command("editor.playmode stop")
record("PASS", "Live frame/tick advancement; Button/Presentation APIs only after fixture Load; no manual simulation Step, no changes to assets/waves/prices/rewards.")
