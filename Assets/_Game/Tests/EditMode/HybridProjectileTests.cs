using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class HybridProjectileTests
    {
        private static readonly GridCoordinate Cell = new GridCoordinate(-4, -1);
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase("steam", 1)] [TestCase("steam", 2)] [TestCase("steam", 3)]
        [TestCase("storm", 1)] [TestCase("storm", 2)] [TestCase("storm", 3)]
        public void Vfx1C01_UsesAuthoritativeLevelAndAnchorWithoutChangingCombatEconomy(string type, int level)
        {
            WithBattle(type, 1000, (battle, s, id) =>
            {
                var tower = s.State.Towers.Single();
                for (var i = 1; i < level; i++) Assert.That(battle.UpgradeTower(tower.Id).Success, Is.True);
                BasicProjectileTests.Call(battle, "SynchronizeViews");
                var binding = battle.GetComponentsInChildren<FlameModelBinding>().Single();
                Assert.That(binding.Level, Is.EqualTo(level));
                var source = binding.ShotSource.position;
                battle.StartWave();
                s.Step();
                var hit = s.AttackEvents.Single();
                Assert.That(s.State.Enemies.Single().Statuses, Is.Empty);
                Assert.That(s.State.Enemies.Single().Health, Is.LessThan(1000));
                Consume(battle);
                Consume(battle);
                var pool = battle.HybridProjectiles;
                var shot = pool.GetView(0);
                Assert.That(shot.Kind, Is.EqualTo(HybridProjectilePool.KindFor(id)));
                Assert.That(shot.Event, Is.EqualTo(hit));
                Assert.That(shot.Level, Is.EqualTo(level));
                Assert.That(shot.Source, Is.EqualTo(source));
                Assert.That(shot.Target, Is.EqualTo(new Vector3(hit.TargetPosition.X, .6f, hit.TargetPosition.Y)));
                Assert.That(pool.GetView(1).Remaining, Is.Zero);
                Assert.That(battle.GetComponentsInChildren<AttackBeamView>(), Is.Empty);
                Assert.That(battle.UpgradeTower(tower.Id).Success, Is.False, "Combat economy rules remain unchanged.");
                var hp = s.State.Enemies.Single().Health;
                pool.Advance(shot.FlightDuration * .5f);
                Assert.That(shot.HeadPosition, Is.Not.EqualTo(source).And.Not.EqualTo(shot.Target));
                pool.Advance(shot.FlightDuration * .51f);
                Assert.That(shot.IsFlying, Is.False);
                Assert.That(Vector3.Distance(shot.HeadPosition, shot.Target), Is.LessThan(.0001f));
                Assert.That(shot.Level, Is.EqualTo(level));
                Assert.That(shot.Event, Is.EqualTo(hit));
                Assert.That(s.State.Enemies.Single().Health, Is.EqualTo(hp));
            });
        }

        [TestCase("steam")] [TestCase("storm")]
        public void Vfx1C01_LethalAttackSurvivesRemovalAndRejectsOutOfBoundsAnchor(string type)
        {
            WithBattle(type, 1, (battle, s, id) =>
            {
                battle.GetComponentsInChildren<FlameModelBinding>().Single().ShotSource.position = Vector3.one * 100;
                battle.StartWave();
                s.Step();
                var hit = s.AttackEvents.Single();
                Assert.That(hit.WasLethal, Is.True);
                Assert.That(s.State.Enemies, Is.Empty);
                Assert.That(s.ReactionEvents, Is.Empty);
                var gold = s.State.Gold;
                Consume(battle);
                var shot = battle.HybridProjectiles.GetView(0);
                Assert.That(shot.Event, Is.EqualTo(hit));
                Assert.That(Vector3.Distance(shot.Source, new Vector3(-4, .89f, -1)), Is.LessThan(.0001f));
                Assert.That(s.State.Gold, Is.EqualTo(gold));
                Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
                var source = s.State.Towers.Single();
                if (type == "steam")
                {
                    Assert.That(battle.UpgradeTower(source.Id).Success, Is.True);
                    BasicProjectileTests.Call(battle, "SynchronizeViews");
                    Assert.That(shot.Level, Is.EqualTo(1));
                }
                else Assert.That(battle.SellTower(source.Id).Success, Is.True);
                var goldAfterEconomy = s.State.Gold;
                battle.HybridProjectiles.Advance(shot.FlightDuration + .01f);
                Assert.That(Vector3.Distance(shot.HeadPosition, shot.Target), Is.LessThan(.0001f));
                Assert.That(shot.Event, Is.EqualTo(hit));
                Assert.That(s.State.Gold, Is.EqualTo(goldAfterEconomy));
                Assert.That(s.State.Kills, Is.EqualTo(1));
                Assert.That(s.DamageEvents, Is.Empty);
                Assert.That(s.AttackEvents, Is.Empty);
            });
        }

        [TestCase("steam", ReactionKind.Steam)] [TestCase("storm", ReactionKind.Storm)]
        public void Vfx1C01_CooldownDoesNotSuppressDirectAndStormNeedsActualNeighbors(string type, ReactionKind kind)
        {
            WithBattle(type, 1000, (battle, s, id) =>
            {
                battle.StartWave();
                // Синтетическая фиксация cooldown только для этого теста presentation.
                var enemy = s.State.Enemies.Single();
                typeof(EnemyState).GetMethod("ReserveReaction", PrivateInstance).Invoke(enemy, new object[] { kind, 0L });
                for (var step = 0; step < 2; step++)
                {
                    s.Step();
                    Assert.That(s.AttackEvents.Count, Is.EqualTo(1));
                    Assert.That(s.ReactionEvents, Is.Empty);
                    Consume(battle);
                    Assert.That(battle.HybridProjectiles.GetView(step).Event.Tick, Is.EqualTo(step + 1));
                    Assert.That(s.AttackEvents, Is.Empty);
                }
                Assert.That(battle.GetComponentsInChildren<ReactionEffectView>(), Is.Empty);
                Assert.That(battle.GetComponentsInChildren<AttackBeamView>(), Is.Empty);
            });
        }

        [TestCase(ReactionKind.Steam, ReactionTriggerKind.Intrinsic)]
        [TestCase(ReactionKind.Steam, ReactionTriggerKind.StatusPair)]
        [TestCase(ReactionKind.Storm, ReactionTriggerKind.Intrinsic)]
        [TestCase(ReactionKind.Storm, ReactionTriggerKind.StatusPair)]
        public void Vfx1C02_ConsumerIsolatesKeysSortsHitsAndRetainsOnlyValidRendererSlots(ReactionKind kind, ReactionTriggerKind trigger)
        {
            WithBattle("steam", 1000, (battle, s, _) =>
            {
                var first = Reaction(kind, trigger, 10, 4, 20);
                var second = Reaction(kind, trigger, 10, 7, 21);
                var otherTick = Reaction(kind, trigger, 11, 4, 20);
                var steam = kind == ReactionKind.Steam;
                var firstIndex = steam ? 0 : 1;
                var lastIndex = steam ? 3 : 2;
                var ordered = Enumerable.Range(firstIndex, lastIndex - firstIndex + 1)
                    .Select(i => Hit(first, i, i == 0 ? 10 : 40 + i, i == firstIndex ? 2 : 17)).ToArray();
                var secondHit = Hit(second, firstIndex, steam ? 10 : 50, 3);
                var ignored = new[]
                {
                    Hit(Reaction(kind, trigger, 10, 4, 21), firstIndex, steam ? 10 : 51, 80),
                    Hit(Reaction(kind, trigger, 10, 5, 20), firstIndex, steam ? 10 : 52, 80),
                    Hit(Reaction(steam ? ReactionKind.Storm : ReactionKind.Steam, trigger, 10, 4, 20), firstIndex, 53, 80),
                    Hit(first, firstIndex, steam ? 10 : 54, 80, DamageOrigin.Direct),
                    Hit(otherTick, firstIndex, steam ? 10 : 55, 80)
                };
                Stream<ReactionEvent>(s, "_reactionEvents").AddRange(new[] { first, second });
                Stream<DamageResolvedEvent>(s, "_damageEvents").AddRange(ignored.Concat(ordered.Reverse()).Append(secondHit));
                Consume(battle);
                var effects = battle.GetComponentsInChildren<ReactionEffectView>();
                Assert.That(effects.Length, Is.EqualTo(2));
                var one = effects.Single(e => e.Event.Equals(first));
                var two = effects.Single(e => e.Event.Equals(second));
                CollectionAssert.AreEqual(ordered, Enumerable.Range(0, one.HitCount).Select(one.GetHit));
                CollectionAssert.AreEqual(new[] { secondHit }, Enumerable.Range(0, two.HitCount).Select(two.GetHit));
                Assert.That(battle.Presentation.LastSummary, Does.EndWith("1 целей, 3 HP"));
                Assert.That(one.GetHit(0).WasLethal, Is.True);
                Assert.That(s.DamageEvents, Is.Empty);
                Assert.That(s.ReactionEvents, Is.Empty);
                Consume(battle);
                Assert.That(battle.GetComponentsInChildren<ReactionEffectView>().Length, Is.EqualTo(2));
                Stream<ReactionEvent>(s, "_reactionEvents").Add(otherTick);
                Stream<DamageResolvedEvent>(s, "_damageEvents").Add(ignored.Last());
                Consume(battle);
                Assert.That(battle.GetComponentsInChildren<ReactionEffectView>().Count(e => e.Event.Equals(otherTick)), Is.EqualTo(1));
                Assert.That(battle.HybridProjectiles.GetView(0).Remaining, Is.Zero);
            });
        }

        [Test]
        public void Vfx1C02_StormUsesCapturedFromPositionsAndNeverFlashesPrimaryOrZeroHpLoss()
        {
            WithBattle("storm", 1000, (battle, s, _) =>
            {
                var reaction = Reaction(ReactionKind.Storm, ReactionTriggerKind.StatusPair, 10, 4, 1);
                var captured = Hit(reaction, 2, 20, 0);
                Stream<ReactionEvent>(s, "_reactionEvents").Add(reaction);
                Stream<DamageResolvedEvent>(s, "_damageEvents").AddRange(new[]
                {
                    Hit(reaction, 0, 10, 5), Hit(reaction, 1, 10, 5), captured, Hit(reaction, 3, 25, 5)
                });
                Consume(battle);
                var effect = battle.GetComponentsInChildren<ReactionEffectView>().Single();
                var lines = effect.GetComponentsInChildren<LineRenderer>();
                Assert.That(effect.HitCount, Is.EqualTo(1));
                Assert.That(lines.Count(l => l.enabled), Is.EqualTo(1), "Only primary cue before hop 2.");
                effect.Advance(.13f);
                Assert.That(lines[1].GetPosition(0), Is.EqualTo(new Vector3(captured.FromPosition.X, .57f, captured.FromPosition.Y)));
                Assert.That(lines[1].GetPosition(8), Is.EqualTo(new Vector3(captured.TargetPosition.X, .57f, captured.TargetPosition.Y)));
                Assert.That(lines.Skip(2).All(l => !l.enabled), Is.True);
                effect.Play(reaction, Array.Empty<DamageResolvedEvent>());
                Assert.That(effect.HitCount, Is.Zero);
                Assert.That(lines.Skip(1).All(l => !l.enabled && l.positionCount == 0), Is.True);
                effect.Advance(.5f);
                Assert.That(effect.Event, Is.EqualTo(default(ReactionEvent)));
                Assert.That(lines.All(l => !l.enabled && l.positionCount == 0), Is.True);
            });
        }

        [Test]
        public void Vfx1C03_PrewarmOverflowReusesObjectsAndRetainsTrueAgeAcrossMixedExpiry()
        {
            WithBattle("steam", 1000, (battle, s, _) =>
            {
                var pool = battle.HybridProjectiles;
                var renderers = battle.GetComponentsInChildren<LineRenderer>(true)
                    .Where(l => l.transform.parent.name.StartsWith("Hybrid Projectile Pool", StringComparison.Ordinal)).ToArray();
                Assert.That(renderers.Length, Is.EqualTo(32));
                var ids = renderers.Select(l => l.GetInstanceID()).ToArray();
                var material = renderers[0].sharedMaterial;
                for (var tick = 1; tick <= 7; tick++) Play(pool, HybridShotKind.Steam, tick);
                Play(pool, HybridShotKind.Storm, 8);
                pool.Advance(.32f);
                Play(pool, HybridShotKind.Steam, 9);
                Play(pool, HybridShotKind.Storm, 10);
                var events = Enumerable.Range(0, HybridProjectilePool.Capacity).Select(i => pool.GetView(i).Event.Tick);
                CollectionAssert.AreEquivalent(new long[] { 2, 3, 4, 5, 6, 7, 9, 10 }, events);
                Assert.That(renderers.All(l => l.sharedMaterials.Length == 1 && l.sharedMaterial == material), Is.True);
                Assert.That(renderers.Select(l => l.GetInstanceID()), Is.EqualTo(ids));
                pool.Advance(.5f);
                Assert.That(renderers.All(l => !l.enabled && l.positionCount == 0), Is.True);
                Assert.That(events.All(t => t == 0), Is.True);
                Assert.That(pool.GetView(0).Level, Is.Zero);
                Assert.That(pool.GetView(0).Source, Is.EqualTo(Vector3.zero));
                Play(pool, HybridShotKind.Storm, 11);
                battle.ResetPresentation();
                Assert.That(pool.GetView(0).Kind, Is.EqualTo(HybridShotKind.None));
                Assert.That(renderers.All(l => !l.enabled && l.positionCount == 0), Is.True);
            });
        }

        private static void Play(HybridProjectilePool pool, HybridShotKind kind, int tick) =>
            pool.Play(kind, new AttackEvent(new TowerId(1), new EnemyId(tick), new PathPosition(3, 1), tick, 10, false), 2, Vector3.one);

        private static ReactionEvent Reaction(ReactionKind kind, ReactionTriggerKind trigger, int tick, int sequence, int tower) =>
            new ReactionEvent(tick, sequence, kind, trigger, new TowerId(tower), new EnemyId(10), new PathPosition(1, 2),
                kind == ReactionKind.Steam ? StatusKind.Burn : StatusKind.Wet,
                kind == ReactionKind.Steam ? StatusKind.Wet : StatusKind.Gust, 1.25f, kind == ReactionKind.Steam ? 4 : 2);

        private static DamageResolvedEvent Hit(ReactionEvent reaction, int index, int enemy, int loss, DamageOrigin origin = DamageOrigin.Reaction) =>
            new DamageResolvedEvent(reaction.Tick, reaction.SequenceInTick + 1 + index, origin, reaction.SourceTowerId, new EnemyId(enemy),
                new PathPosition(index + .2f, 3), new PathPosition(index + 1.1f, 4), 17, 17, 17, loss,
                loss == 2 ? 0 : 20, loss == 2, reaction.Kind, reaction.SequenceInTick, index);

        private static List<T> Stream<T>(BattleSimulation s, string field) =>
            (List<T>)typeof(BattleSimulation).GetField(field, PrivateInstance).GetValue(s);

        private static void Consume(BattleSceneController battle) => BasicProjectileTests.Call(battle, "ProcessAttackEvents");

        private static void WithBattle(string type, int health, Action<BattleSceneController, BattleSimulation, TowerDefinitionId> test)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/Battle.unity");
            try
            {
                var battle = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleSceneController>()).Single();
                BasicProjectileTests.Call(battle, "Start");
                var id = new TowerDefinitionId(type);
                var levels = battle.Simulation.TowerCatalog.Get(id).Levels.Select(l =>
                    new TowerDefinition(id, 0, 10, 30, 1, intrinsicReaction: l.IntrinsicReaction)).ToArray();
                var catalog = new TowerCatalog(new[] { new TowerArchetypeDefinition(id, true, levels) });
                var wave = new WaveDefinition(new EnemyDefinition(new EnemyDefinitionId("shade"), health, 1, 1, 5), 1, 0, 30);
                var simulation = new BattleSimulation(new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1),
                    new PathDefinition(new[] { new GridCoordinate(-5, -2), new GridCoordinate(5, -2) }),
                    new[] { wave, wave },
                    catalog, 20, 150, 0, battle.Simulation.ReactionCatalog);
                typeof(BattleSceneController).GetField("_simulation", PrivateInstance).SetValue(battle, simulation);
                typeof(BattleSceneController).GetField("checkpointController", PrivateInstance).SetValue(battle, null);
                battle.ResetPresentation();
                Assert.That(battle.PlaceTower(id, Cell).Success, Is.True);
                BasicProjectileTests.Call(battle, "SynchronizeViews");
                test(battle, simulation, id);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
