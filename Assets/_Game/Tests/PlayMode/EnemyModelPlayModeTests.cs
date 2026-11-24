using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Domain;
using Veilward.Presentation;
using Veilward.Tests.Shared;
using Object = UnityEngine.Object;
namespace Veilward.Tests.PlayMode
{
    public sealed class EnemyModelPlayModeTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        BattleSceneController Battle => Object.FindFirstObjectByType<BattleSceneController>();
        static void Call(object o, string method) => o.GetType().GetMethod(method, Flags).Invoke(o, null);
        EnemyModelView[] Visuals => Battle.GetComponentsInChildren<EnemyModelView>();
        static Matrix4x4[] Poses(EnemyModelView visual) => visual.Model.GetComponentsInChildren<MeshFilter>().Where(m => !m.name.StartsWith("Body occlusion cue")).Select(m => m.transform.localToWorldMatrix).ToArray();
        static void Changed(Matrix4x4[] a, Matrix4x4[] b) => Assert.That(a.Where((v, i) => v != b[i]).Any(), Is.True);
        BattleSimulation Fixture(string source, int count = 1, float speed = 1)
        {
            var reaction = source == "Steam" || source == "Storm"; var dual = source == "Dual";
            var tower = new TowerDefinition(new TowerDefinitionId("fixture"), 0, 20, 30, source == "Hit" || dual ? 1 : 20,
                null, 0, reaction ? new ReactionPayload(source == "Steam" ? ReactionKind.Steam : ReactionKind.Storm, 100, 2, 2) : null);
            var catalog = new TowerCatalog(new[] { new TowerArchetypeDefinition(tower.Id, true, new[] { tower, tower, tower }) });
            var enemy = new EnemyDefinition(new EnemyDefinitionId("shade"), dual ? 100 : source == "Hit" || reaction ? 1000 : 10, speed, 1, 5);
            var groups = reaction || dual ? new[] { new WaveSpawnGroup(enemy, 1, 0, 30), new WaveSpawnGroup(new EnemyDefinition(new EnemyDefinitionId("rift-warden"), dual ? 100 : 10, speed, 20, 5), 1, 0, 30) } : new[] { new WaveSpawnGroup(enemy, count, 0, 1) };
            var s = new BattleSimulation(new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1), new PathDefinition(new[] { new GridCoordinate(0, 0), new GridCoordinate(5, 0) }),
                new[] { new WaveDefinition(groups) }, catalog, 20, 150, 20, Battle.Simulation.ReactionCatalog, null, AbilityDefinition.RiftSeal);
            return s;
        }
        void Install(BattleSimulation s)
        {
            Battle.enabled = false;
            typeof(BattleSceneController).GetField("_simulation", Flags).SetValue(Battle, s);
            Battle.ResetPresentation();
        }
        static string DomainState(BattleSimulation s) => $"{s.State.Tick}|{s.State.Phase}|{s.State.Gold}|{s.State.BaseHealth}|{s.State.Kills}|" + string.Join(";", s.State.Enemies.Select(e => $"{e.Id}:{e.Health}:{e.DistanceAlongPath}"));
        [UnityTearDown] public IEnumerator Cleanup() { yield return SceneManager.LoadSceneAsync("Battle"); yield return null; }

        [UnityTest] public IEnumerator Art504_AllRealDamageOriginsCreateOneBoundedDeathAndNoPresentationRewards()
        {
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            foreach (var source in new[] { "Direct", "Burn", "Steam", "Storm", "Ability" })
            {
                var s = Fixture(source); Install(s);
                if (source == "Direct" || source == "Steam" || source == "Storm") Assert.That(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("fixture"), new GridCoordinate(0, 1))).Success, Is.True);
                s.StartWave(); Call(Battle, "SynchronizeViews");
                var victim = s.State.Enemies.Last(); var victimId = victim.Id;
                if (source == "Burn") typeof(EnemyState).GetMethod("ApplyStatus", Flags).Invoke(victim, new object[] { new StatusEffectDefinition(StatusKind.Burn, 10, 30, 1), new TowerId(1), s.State.Tick });
                if (source == "Ability") Assert.That(s.CastAbility(new CastAbilityCommand(AbilityDefinitionId.RiftSeal, new PathPosition(0, 0), 0, 0, s.AbilityState.NextCastSequence)).Succeeded, Is.True);
                else s.Step();
                var lethal = s.DamageEvents.Single(d => d.TargetEnemyId == victimId && d.WasLethal);
                Assert.That(lethal.Origin.ToString(), Is.EqualTo(source == "Steam" || source == "Storm" ? "Reaction" : source));
                if (source == "Steam" || source == "Storm") Assert.That(lethal.ReactionKind.ToString(), Is.EqualTo(source));
                var snapshot = DomainState(s); Call(Battle, "ProcessAttackEvents"); Call(Battle, "ProcessAttackEvents");
                var tail = Visuals.Single(v => v.IsTail); Assert.That(tail.Pose, Is.EqualTo("Death"));
                Assert.That(Visuals.Count(v => !v.IsTail), Is.EqualTo(s.State.Enemies.Count));
                Assert.That(tail.transform.position.x, Is.EqualTo(lethal.TargetPosition.X).Within(.0001));
                Assert.That(tail.transform.position.z, Is.EqualTo(lethal.TargetPosition.Y).Within(.0001));
                Assert.That(DomainState(s), Is.EqualTo(snapshot)); Assert.That(s.State.Kills, Is.EqualTo(1));
                var start = Poses(tail); tail.Advance(.3f); Changed(start, Poses(tail));
                Assert.That(tail.Advance(.3f), Is.True); Battle.ResetPresentation();
                Assert.That(Visuals.Any(v => v.IsTail), Is.False);
                yield return null;
            }
        }

        [UnityTest] public IEnumerator Art505_RealPauseFreezesAnimatedPartsAndTailsRestoreClearsSynchronously()
        {
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            var s = Fixture("Hit"); Install(s); s.StartWave(); Call(Battle, "SynchronizeViews");
            var view = Visuals.Single(); var idle = Poses(view); view.Synchronize(Vector3.forward, false); view.Advance(.2f); Changed(idle, Poses(view));
            var moving = Poses(view); view.Synchronize(Vector3.right, true); view.Advance(.2f); Changed(moving, Poses(view));
            view.Hit(); view.Advance(.15f); var hit = Poses(view); Assert.That(view.Pose, Is.EqualTo("Hit"));
            var pause = Battle.GetComponent<BattlePauseController>(); pause.Pause();
            Battle.enabled = true; var clock = view.Clock;
            for (var i = 0; i < 5; i++) yield return null;
            Assert.That(view.Clock, Is.EqualTo(clock)); CollectionAssert.AreEqual(hit, Poses(view));
            pause.OnApplicationFocus(true); Assert.That(pause.Resume(), Is.True);
            for (var i = 0; i < 5; i++) yield return null;
            Assert.That(view.Clock, Is.GreaterThan(clock));
            Battle.enabled = false; while (s.State.Enemies.Count > 0) s.Step(); Call(Battle, "ProcessAttackEvents");
            var tail = Visuals.Single(v => v.IsTail); Assert.That(tail.Pose, Is.EqualTo("Leak"));
            Assert.That(tail.transform.position.x, Is.EqualTo(5).Within(.0001));
            var pose = Poses(tail); tail.Advance(.25f); Changed(pose, Poses(tail));
            pause.Pause(); Battle.enabled = true; pose = Poses(tail); clock = tail.TailClock;
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(tail.TailClock, Is.EqualTo(clock)); CollectionAssert.AreEqual(pose, Poses(tail));
            Battle.ResetPresentation(); Assert.That(Visuals.Length, Is.Zero, "inactive deferred objects are detached from live model ownership");
        }

        [UnityTest] public IEnumerator Art504_MultiHitOneOwnerBothTypesDeathWinsAndProductionUpdateExpiresTails()
        {
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            var s = Fixture("Dual"); Install(s);
            foreach (var x in new[] { 0, 1 }) Assert.That(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("fixture"), new GridCoordinate(x, 1))).Success, Is.True);
            s.StartWave(); Call(Battle, "SynchronizeViews"); s.Step();
            Assert.That(s.DamageEvents.Count(d => d.Origin == DamageOrigin.Direct), Is.EqualTo(2));
            Call(Battle, "ProcessAttackEvents");
            Assert.That(Visuals.Count(v => !v.IsTail), Is.EqualTo(2)); Assert.That(Visuals.Count(v => v.Pose == "Hit"), Is.EqualTo(1));
            Assert.That(s.CastAbility(new CastAbilityCommand(AbilityDefinitionId.RiftSeal, new PathPosition(0, 0), 0, s.State.Tick, s.AbilityState.NextCastSequence)).Succeeded, Is.True);
            Call(Battle, "ProcessAttackEvents");
            Assert.That(Visuals.Count(v => v.Pose == "Death"), Is.EqualTo(2));
            foreach (var tail in Visuals) { var pose = Poses(tail); tail.Advance(.2f); Changed(pose, Poses(tail)); }
            Battle.enabled = true; var started = Battle.Presentation.ActiveTime; var deadline = Time.realtimeSinceStartup + 3f;
            while (Visuals.Length > 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); Assert.That(Visuals, Is.Empty);
            Assert.That(Battle.Presentation.ActiveTime - started, Is.LessThan(.85f)); Assert.That(s.State.Kills, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator Art507_ProductionCheckpointRestoreClearsLiveAndDeathVisualOwnership()
        {
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null; Battle.enabled = false;
            Assert.That(Battle.ChooseElement(new ChooseElementCommand("start-resonance", "flame")).Success, Is.True);
            var safe = AbilityBossTrace.Envelope(Battle.Simulation); var wire = AbilityBossTrace.Snapshot(Battle.Simulation);
            Assert.That(Battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            var ability = Battle.GetComponent<AbilityController>(); var target = Battle.Simulation.Path.GetPosition(Battle.Simulation.State.Enemies[0].DistanceAlongPath);
            Assert.That(ability.BeginPreview() && ability.SelectTarget(target) && ability.ConfirmPreview(), Is.True); Call(Battle, "LateUpdate");
            Assert.That(Visuals.Count(v => v.Pose == "Death"), Is.EqualTo(1));
            Battle.GetComponent<BattleCheckpointController>().RestoreCheckpoint(safe);
            Assert.That(AbilityBossTrace.Snapshot(Battle.Simulation), Is.EqualTo(wire)); Assert.That(Visuals, Is.Empty);
            Assert.That(Battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started)); Assert.That(Visuals.Count(v => !v.IsTail), Is.EqualTo(1));
            yield return null;
        }

        [UnityTest] public IEnumerator Art505_TailCapEvictsSynchronouslyAndSpawnKillNeverCreatesGhost()
        {
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            var s = Fixture("Direct", 24); Install(s); s.StartWave();
            for (var i = 0; i < 24; i++) { s.Step(); Call(Battle, "ProcessAttackEvents"); }
            Assert.That(s.State.Enemies.Count, Is.EqualTo(24));
            Assert.That(s.CastAbility(new CastAbilityCommand(AbilityDefinitionId.RiftSeal, new PathPosition(.4f, 0), 0, s.State.Tick, s.AbilityState.NextCastSequence)).Succeeded, Is.True);
            Call(Battle, "ProcessAttackEvents");
            Assert.That(Visuals.Count(v => v.IsTail && v.gameObject.activeInHierarchy), Is.EqualTo(16));
            Assert.That(s.State.Kills, Is.EqualTo(24)); Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Victory));
            Battle.ResetPresentation(); Assert.That(Visuals.Count(v => v.gameObject.activeInHierarchy), Is.Zero);
            s = Fixture("Direct"); Install(s); s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("fixture"), new GridCoordinate(0, 1))); s.StartWave(); s.Step(); Call(Battle, "ProcessAttackEvents");
            Assert.That(s.State.Kills, Is.EqualTo(1)); Assert.That(Visuals.Count(v => v.gameObject.activeInHierarchy), Is.Zero, "spawn→kill without earlier view creates neither alive nor leak ghost");
            yield return null;
        }

        [UnityTest] public IEnumerator Vfx1d_WardenCuesFollowTheRealOwnerAndKeepArt5Tails()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;
            var battle = Battle;
            var pool = battle.GetComponentsInChildren<BossCueView>(true);
            Assert.That(pool.Length, Is.EqualTo(4));
            Assert.That(pool.Select(cue => cue.GetComponentInChildren<LineRenderer>(true).sharedMaterial).Distinct().Count(), Is.EqualTo(1));
            foreach (var lethal in new[] { true, false })
            {
                var s = Fixture("Dual");
                Install(s);
                s.StartWave();
                var boss = s.State.Enemies.Single(enemy => enemy.Definition.Id.Value == BossHudController.BossId);
                Call(battle, "ProcessAttackEvents");
                var spawn = pool.Single(cue => cue.Kind == BossCueKind.Spawn);
                Assert.That(spawn.EnemyId, Is.EqualTo(boss.Id));
                Assert.That(spawn.Position, Is.EqualTo(s.Path.GetPosition(boss.DistanceAlongPath)));
                Call(battle, "SynchronizeViews");
                Assert.That(pool.Count(cue => cue.Kind == BossCueKind.Spawn), Is.EqualTo(1));

                while (s.Path.TotalLength - boss.DistanceAlongPath > 2.25f) s.Step();
                Call(battle, "ProcessAttackEvents");
                Assert.That(pool.Any(cue => cue.Kind == BossCueKind.Threat), Is.False);
                while (s.Path.TotalLength - boss.DistanceAlongPath > 2.2f) s.Step();
                Call(battle, "ProcessAttackEvents");
                var threat = pool.Single(cue => cue.Kind == BossCueKind.Threat);
                var threatPosition = threat.Position;
                Assert.That(threat.EnemyId, Is.EqualTo(boss.Id));
                Assert.That(threat.Position, Is.EqualTo(s.Path.GetPosition(boss.DistanceAlongPath)));
                Call(battle, "SynchronizeViews");
                Assert.That(pool.Count(cue => cue.Kind == BossCueKind.Threat), Is.EqualTo(1));

                if (lethal)
                    Assert.That(s.CastAbility(new CastAbilityCommand(AbilityDefinitionId.RiftSeal, threat.Position,
                        0, s.State.Tick, s.AbilityState.NextCastSequence)).Succeeded, Is.True);
                else
                    while (s.State.Phase == BattlePhase.Combat) s.Step();
                Call(battle, "ProcessAttackEvents");
                Call(battle, "ProcessAttackEvents");
                var terminal = pool.Single(cue => cue.Kind != BossCueKind.None);
                Assert.That(terminal.Kind, Is.EqualTo(lethal ? BossCueKind.Death : BossCueKind.Leak));
                Assert.That(terminal.EnemyId, Is.EqualTo(boss.Id));
                Assert.That(terminal.Position, Is.EqualTo(lethal ? threatPosition : s.Path.GetPosition(s.Path.TotalLength)));
                Assert.That(Visuals.Any(view => view.IsTail && view.Pose == terminal.Kind.ToString()), Is.True);
                Assert.That(Visuals.Any(view => !view.IsTail), Is.False);
                if (lethal)
                {
                    var hit = battle.GetComponentsInChildren<AbilityEffectView>().Single(view => view.Hit?.TargetEnemyId == boss.Id);
                    Assert.That(hit.Hit.Value.WasLethal, Is.True);
                    Assert.That(hit.CapturedPosition, Is.EqualTo(terminal.Position));
                }

                battle.ResetPresentation();
                CollectionAssert.AreEquivalent(pool, battle.GetComponentsInChildren<BossCueView>(true));
                Assert.That(pool.All(cue => cue.Kind == BossCueKind.None && cue.EnemyId == default &&
                    cue.GetComponentInChildren<LineRenderer>(true).positionCount == 0), Is.True);
            }

            var unseen = Fixture("Steam");
            Install(unseen);
            unseen.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("fixture"), new GridCoordinate(0, 1)));
            unseen.StartWave();
            unseen.Step();
            Assert.That(unseen.State.Enemies.All(enemy => enemy.Definition.Id.Value != BossHudController.BossId), Is.True);
            Call(battle, "ProcessAttackEvents");
            Assert.That(pool.All(cue => cue.Kind == BossCueKind.None), Is.True);
            Assert.That(Visuals.Any(view => view.IsTail), Is.False);

        }
    }
}
