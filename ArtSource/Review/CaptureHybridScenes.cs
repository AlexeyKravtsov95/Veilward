// Additional Play Mode PNGs. Earned windows and synthetic stress use an explicit test clock.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Veilward.Tests.Shared;
using Object = UnityEngine.Object;

public sealed class CaptureHybridScenes : MonoBehaviour
{
    private const string Root = "TestResults/vfx1c/supplement/";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private readonly List<object> _captures = new List<object>();
    private BattleSceneController _battle;
    private BattleScenarioConfig _config;
    private Text _label;
    private MethodInfo _encode;
    private MethodInfo _prepare;

    public static string Capture()
    {
        Require(EditorApplication.isPlaying, "Play Mode required.");
        Require(!Directory.Exists(Root), "Use a fresh supplement directory.");
        Directory.CreateDirectory(Root);
        var gameObject = new GameObject("VFX1c supplementary capture");
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<CaptureHybridScenes>().StartCoroutine("Flow");
        return "Started supplementary captures.";
    }

    private IEnumerator Flow()
    {
        var capture = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t => t != null);
        _prepare = capture.GetMethod("PrepareGameView", PrivateStatic);
        _encode = capture.GetMethod("EncodeScreenToPng", PrivateStatic);
        _config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        BattleSessionContext.Clear();
        yield return SceneManager.LoadSceneAsync("Battle");
        var ready = Time.realtimeSinceStartup + 5;
        do
        {
            yield return null;
            _battle = Object.FindFirstObjectByType<BattleSceneController>();
        } while ((_battle == null || _battle.Simulation == null) && Time.realtimeSinceStartup < ready);
        Require(_battle != null && _battle.Simulation != null, "Battle loaded.");
        _battle.enabled = false;
        BuildLabel();
        var script = AbilityBossTrace.Load("flame");
        var run = new AbilityBossTrace.Run(_config.CreateSimulation(), script);
        var earned = new Dictionary<int, WaveCheckpointEnvelope>();
        run.stable = (r, stage) =>
        {
            if (stage == "preparation") earned.Add(r.sim.State.WaveIndex + 1, AbilityBossTrace.Envelope(r.sim));
        };
        run.Play();
        Require(run.sim.State.Phase == BattlePhase.Victory, "Earned checkpoints backed by Victory8.");

        yield return Reaction(earned[4], 4, script, ReactionKind.Steam, ReactionTriggerKind.StatusPair);
        yield return Reaction(earned[7], 7, script, ReactionKind.Storm, ReactionTriggerKind.StatusPair);
        yield return Reaction(earned[7], 7, script, ReactionKind.Steam, ReactionTriggerKind.Intrinsic);
        yield return Reaction(earned[8], 8, script, ReactionKind.Storm, ReactionTriggerKind.Intrinsic);
        yield return LevelThree("steam", "ART.3e", 3);
        yield return LevelThree("storm", "ART.3f", 2);
        yield return DenseFixture();
        File.WriteAllText(Root + "completion.json", JsonConvert.SerializeObject(new { ok = true, captures = _captures }, Formatting.Indented) + "\n");
    }

    private void BuildLabel()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var label = new GameObject("Supplement scope label", typeof(Text));
        label.transform.SetParent(transform, false);
        _label = label.GetComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _label.fontSize = 17;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.raycastTarget = false;
        _label.rectTransform.anchorMin = new Vector2(0, 0);
        _label.rectTransform.anchorMax = new Vector2(1, 0);
        _label.rectTransform.pivot = new Vector2(.5f, 0);
        _label.rectTransform.sizeDelta = new Vector2(0, 45);
    }

    private void Restore(WaveCheckpointEnvelope checkpoint)
    {
        _battle.GetComponent<BattleCheckpointController>().RestoreCheckpoint(checkpoint);
        Call("LateUpdate");
    }

    private IEnumerator Reaction(WaveCheckpointEnvelope checkpoint, int wave, BossMissionScript script, ReactionKind kind, ReactionTriggerKind trigger)
    {
        Restore(checkpoint);
        _label.text = "EARNED TEST CLOCK | production wave " + wave + " | " + kind + " / " + trigger;
        Require(_battle.StartWave() == WaveTransitionResult.Started, "Earned Start Wave.");
        var simulation = _battle.Simulation;
        var ability = _battle.GetComponent<AbilityController>();
        for (var i = 0; i < 1400 && simulation.State.Phase == BattlePhase.Combat; i++)
        {
            if (script.TryCenter(simulation, out var center))
            {
                Require(ability.BeginPreview() && ability.SelectTarget(center) && ability.ConfirmPreview(), "Earned public ability.");
                Call("LateUpdate");
                Require(ability.LastCast.HasValue && ability.LastCast.Value.Succeeded, "Ability dispatch.");
            }
            Advance(1f / 30);
            simulation.Step();
            var match = simulation.ReactionEvents.Where(e => e.Kind == kind && e.TriggerKind == trigger)
                .FirstOrDefault(e => simulation.DamageEvents.Count(h => ReactionPresentationState.Matches(e, h)) == 2);
            var hits = simulation.DamageEvents.Where(h => ReactionPresentationState.Matches(match, h)).OrderBy(h => h.HitIndex).ToArray();
            Call("ProcessAttackEvents");
            if (hits.Length != 2 || match.Tick == 0) continue;
            var effect = _battle.GetComponentsInChildren<ReactionEffectView>().Single(v => v.Event.Equals(match));
            Require(effect.HitCount == hits.Length && hits.Select((hit, index) => hit.Equals(effect.GetHit(index))).All(x => x), "Captured actual hits.");
            Call("LateUpdate");
            var key = kind.ToString().ToLowerInvariant() + "-" + trigger.ToString().ToLowerInvariant();
            if (kind == ReactionKind.Storm)
            {
                effect.Advance(.055f);
                yield return Save(key + "-hop1", "earned-test-clock", Effect(match, hits, effect, "hop1"));
                effect.Advance(.085f);
                yield return Save(key + "-hop2", "earned-test-clock", Effect(match, hits, effect, "hop2"));
            }
            else
            {
                effect.Advance(.12f);
                yield return Save(key, "earned-test-clock", Effect(match, hits, effect, "impacts"));
            }
            yield break;
        }
        throw new InvalidOperationException("No two-hit earned reaction for " + kind + " / " + trigger);
    }

    private object Effect(ReactionEvent reaction, DamageResolvedEvent[] hits, ReactionEffectView effect, string stage) =>
        new
        {
            kind = reaction.Kind.ToString(), trigger = reaction.TriggerKind.ToString(), reaction.Tick,
            sequence = reaction.SequenceInTick, tower = reaction.SourceTowerId.Value, primary = reaction.PrimaryEnemyId.Value,
            center = new[] { reaction.PrimaryPosition.X, reaction.PrimaryPosition.Y }, range = reaction.RadiusOrHopRange,
            limit = reaction.TargetLimit, stage, hits = hits.Select(h => new
            {
                index = h.HitIndex, enemy = h.TargetEnemyId.Value, from = new[] { h.FromPosition.X, h.FromPosition.Y },
                to = new[] { h.TargetPosition.X, h.TargetPosition.Y }, loss = h.AppliedHpLoss, lethal = h.WasLethal,
            }), lines = VisibleLines(effect.transform),
        };

    private IEnumerator LevelThree(string type, string route, int gold)
    {
        var commands = "Docs/review/" + route + "/inputs/earned.commands.json";
        var script = JsonUtility.FromJson<BossMissionScript>(File.ReadAllText(commands));
        var run = new AbilityBossTrace.Run(_config.CreateSimulation(), script);
        WaveCheckpointEnvelope checkpoint = null;
        run.stable = (r, stage) =>
        {
            if (stage == "preparation" && r.sim.State.WaveIndex == 7) checkpoint = AbilityBossTrace.Envelope(r.sim);
        };
        run.Play();
        Require(checkpoint != null, "Earned tier 3 Preparation8 from " + route);
        Restore(checkpoint);
        var simulation = _battle.Simulation;
        var keeper = simulation.State.Towers.Single(t => t.Definition.Id.Value == type);
        Require(keeper.Level == 3 && keeper.InvestedGold == 495 && simulation.State.Gold == gold,
            "Production route earned and paid for both hybrid upgrades.");
        File.WriteAllText(Root + type + "-l3-earned-state.txt", AbilityBossTrace.Snapshot(simulation));
        AbilityBossTrace.Write(Root + type + "-l3-earned-replay", run);
        _label.text = "EARNED TEST CLOCK | " + route + " public-command replay | " + type.ToUpperInvariant() + " L3";
        Require(_battle.StartWave() == WaveTransitionResult.Started, "Earned L3 Start Wave.");
        for (var step = 0; step < 900 && simulation.State.Phase == BattlePhase.Combat; step++)
        {
            simulation.Step();
            var events = simulation.AttackEvents.ToArray();
            Call("ProcessAttackEvents");
            if (events.Length == 0) continue;
            Call("LateUpdate");
            var shot = _battle.HybridProjectiles.GetView(0);
            var snapshot = AbilityBossTrace.Snapshot(simulation);
            Require(shot.Level == 3 && shot.Event.Equals(events.Single()), "Exact level 3 AttackEvent.");
            _battle.HybridProjectiles.Advance(.025f);
            yield return Save(type + "-l3-launch", "earned-test-clock", Attack(shot, "launch", commands, gold));
            _battle.HybridProjectiles.Advance(shot.FlightDuration * .47f);
            yield return Save(type + "-l3-flight", "earned-test-clock", Attack(shot, "intermediate", commands, gold));
            _battle.HybridProjectiles.Advance(shot.FlightDuration * .56f);
            Require(!shot.IsFlying && snapshot == AbilityBossTrace.Snapshot(simulation), "Cosmetic arrival leaves whole simulation untouched.");
            yield return Save(type + "-l3-impact", "earned-test-clock", Attack(shot, "impact", commands, gold));
            yield break;
        }
        throw new InvalidOperationException("Earned level 3 attack missing for " + type);
    }

    private object Attack(HybridStrikeView shot, string stage, string commands, int gold) => new
    {
        kind = shot.Kind.ToString(), tick = shot.Event.Tick, tower = shot.Event.AttackerTowerId.Value,
        enemy = shot.Event.TargetEnemyId.Value, level = shot.Level, stage, shot.IsFlying, commands, gold,
        source = Vector(shot.Source), target = Vector(shot.Target), head = Vector(shot.HeadPosition),
        lines = VisibleLines(_battle.transform.Find("Hybrid Projectile Pool 0")),
    };

    private IEnumerator DenseFixture()
    {
        var production = _battle.Simulation;
        var shade = new EnemyDefinition(new EnemyDefinitionId("shade"), 1000, 1.1f, 1, 0, 5);
        var boss = new EnemyDefinition(new EnemyDefinitionId("rift-warden"), 600, .8f, 20, 0, 5);
        var wave = new WaveDefinition(new[] { new WaveSpawnGroup(shade, 7, 0, 8), new WaveSpawnGroup(boss, 1, 28, 1) });
        var fixture = new BattleSimulation(production.Grid.Map, production.Path, new[] { wave },
            production.TowerCatalog, 20, 3000, 0, production.ReactionCatalog);
        typeof(BattleSceneController).GetField("_simulation", PrivateInstance).SetValue(_battle, fixture);
        typeof(BattleSceneController).GetField("checkpointController", PrivateInstance).SetValue(_battle, null);
        _battle.ResetPresentation();
        var types = new[] { "flame", "tide", "gale", "stone", "steam", "storm" };
        var cells = new[] { new GridCoordinate(-5, -1), new GridCoordinate(-4, -1), new GridCoordinate(-3, -1),
            new GridCoordinate(-2, -1), new GridCoordinate(-5, 0), new GridCoordinate(-4, 0) };
        for (var i = 0; i < types.Length; i++)
            Require(_battle.PlaceTower(new TowerDefinitionId(types[i]), cells[i]).Success, "Synthetic all-open placement.");
        Call("LateUpdate");
        Require(_battle.StartWave() == WaveTransitionResult.Started, "Synthetic Start Wave.");
        var seen = new HashSet<string>();
        var inspector = _battle.GetComponent<EnemyInspectorController>();
        var capturedAllFour = false;
        for (var i = 0; i < 170; i++)
        {
            Advance(1f / 30);
            fixture.Step();
            foreach (var attack in fixture.AttackEvents)
                seen.Add(fixture.State.Towers.Single(t => t.Id == attack.AttackerTowerId).Definition.Id.Value);
            Call("ProcessAttackEvents");
            var owner = fixture.State.Enemies.FirstOrDefault(e => e.Statuses.Count == 4);
            var warden = fixture.State.Enemies.FirstOrDefault(e => e.Definition.Id.Value == "rift-warden");
            if (fixture.State.Enemies.Count < 6 || warden == null || seen.Count != types.Length) continue;
            if (!capturedAllFour && owner != null)
            {
                Call("LateUpdate");
                Require(inspector.Select(owner.Id), "Actual four-status owner inspector.");
                Call("LateUpdate");
                _label.text = "SYNTHETIC TEST CLOCK | four applied statuses | two overhead slots and inspector";
                yield return Save("synthetic-all-four-inspector", "synthetic-test-clock",
                    new { enemy = owner.Id.Value, tick = fixture.State.Tick, details = inspector.Details,
                        statuses = owner.Statuses.Select(s => s.Kind.ToString()).ToArray(), slots = EnemyStatusView.CueCapacity });
                inspector.Close();
                capturedAllFour = true;
            }

            if (!capturedAllFour || warden.DistanceAlongPath < 2.1f) continue;
            Call("LateUpdate");
            _label.text = "SYNTHETIC TEST CLOCK | six tower families and Rift Warden | production camera";
            yield return Save("synthetic-dense-six-boss", "synthetic-test-clock",
                new { seen, count = fixture.State.Enemies.Count, tick = fixture.State.Tick,
                    boss = warden.Id.Value, distance = warden.DistanceAlongPath });
            Require(inspector.Select(warden.Id), "Live Rift Warden inspector.");
            Call("LateUpdate");
            _label.text = "SYNTHETIC TEST CLOCK | Rift Warden selection with six tower families";
            yield return Save("synthetic-dense-six-boss-inspector", "synthetic-test-clock",
                new { boss = warden.Id.Value, hp = warden.Health, tick = fixture.State.Tick, details = inspector.Details });
            yield break;
        }
        throw new InvalidOperationException("Dense synthetic six-family and four-status frame not found.");
    }

    private static object[] VisibleLines(Transform root)
    {
        var block = new MaterialPropertyBlock();
        return root.GetComponentsInChildren<LineRenderer>().Where(line => line.enabled).Select(line =>
        {
            line.GetPropertyBlock(block);
            var tint = block.GetColor("_BaseColor");
            return (object)new
            {
                line.name, line.startWidth, line.endWidth, tint = new[] { tint.r, tint.g, tint.b },
                screen = Enumerable.Range(0, line.positionCount).Select(i => Camera.main.WorldToViewportPoint(line.GetPosition(i)))
                    .Select(p => new[] { p.x, p.y }).ToArray(),
                world = Enumerable.Range(0, line.positionCount).Select(i => Vector(line.GetPosition(i))).ToArray(),
            };
        }).ToArray();
    }

    private IEnumerator Save(string name, string scope, object data)
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        _prepare.Invoke(null, null);
        var png = (byte[])_encode.Invoke(null, new object[] { 1600, 900 });
        File.WriteAllBytes(Root + name + ".png", png);
        using (var sha = SHA256.Create())
        {
            var digest = BitConverter.ToString(sha.ComputeHash(png)).Replace("-", "").ToLowerInvariant();
            _captures.Add(new { name, sha256 = digest, frame = Time.frameCount, scope, data });
        }
        File.WriteAllText(Root + "attempt-progress.json", JsonConvert.SerializeObject(_captures, Formatting.Indented) + "\n");
    }

    private void Advance(float delta)
    {
        _battle.Presentation.Advance(delta);
        _battle.HybridProjectiles.Advance(delta);
        _battle.BasicProjectiles.Advance(delta);
        _battle.FlameProjectiles.Advance(delta);
        foreach (var reaction in _battle.GetComponentsInChildren<ReactionEffectView>()) reaction.Advance(delta);
    }

    private void Call(string method) => typeof(BattleSceneController).GetMethod(method, PrivateInstance).Invoke(_battle, null);
    private static float[] Vector(Vector3 v) => new[] { v.x, v.y, v.z };
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
