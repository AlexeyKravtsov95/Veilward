using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BasicProjectileTests
    {
        [TestCase("tide")] [TestCase("gale")] [TestCase("stone")]
        public void Vfx1B01_BasicAttackNeverCreatesGenericBeam(string type)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/Battle.unity");
            try
            {
                var battle = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleSceneController>()).Single();
                Call(battle, "Start");
                var id = new TowerDefinitionId(type);
                var catalog = new TowerCatalog(new[] { battle.Simulation.TowerCatalog.Get(id) });
                var simulation = new BattleSimulation(new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1),
                    new PathDefinition(new[] { new GridCoordinate(-5, -2), new GridCoordinate(5, -2) }),
                    new[] { new WaveDefinition(new EnemyDefinition(new EnemyDefinitionId("shade"), 1000, 1, 1, 5), 1, 0, 30) }, catalog, 20, 150, 0, battle.Simulation.ReactionCatalog);
                typeof(BattleSceneController).GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battle, simulation);
                battle.ResetPresentation();
                Assert.That(simulation.PlaceTower(new PlaceTowerCommand(id, new GridCoordinate(-4, -1))).Success, Is.True);
                simulation.StartWave(); simulation.Step();
                Assert.That(simulation.AttackEvents.Count, Is.EqualTo(1));
                Call(battle, "ProcessAttackEvents");
                Assert.That(battle.GetComponentsInChildren<AttackBeamView>(), Is.Empty, "Basic type must have its own moving shape");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase("tide", 1)] [TestCase("tide", 2)] [TestCase("tide", 3)]
        [TestCase("gale", 1)] [TestCase("gale", 2)] [TestCase("gale", 3)]
        [TestCase("stone", 1)] [TestCase("stone", 2)] [TestCase("stone", 3)]
        public void Vfx1B01_CapturedAuthoritativeLevelSourceEndpointAndImmediateStatus(string type, int level)
        {
            WithBattle(battle =>
            {
                var id = new TowerDefinitionId(type);
                // Synthetic free levels preserve the actual status payload and authored model contract.
                var levels = battle.Simulation.TowerCatalog.Get(id).Levels.Select(l =>
                    new TowerDefinition(id, 0, 10, 30, 10, l.StatusEffect, l.ArmorPenetration)).ToArray();
                var s = Install(battle, new TowerCatalog(new[] { new TowerArchetypeDefinition(id, true, levels) }));
                s.PlaceTower(new PlaceTowerCommand(id, new GridCoordinate(-4, -1)));
                for (var i = 1; i < level; i++) Assert.That(s.UpgradeTower(s.State.Towers[0].Id).Success, Is.True);
                Call(battle, "SynchronizeViews");
                var binding = battle.GetComponentsInChildren<FlameModelBinding>().Single();
                var source = binding.ShotSource.position;
                s.StartWave(); s.Step(); var attack = s.AttackEvents.Single();
                Assert.That(s.State.Enemies[0].Statuses.Single().Kind, Is.EqualTo(levels[0].StatusEffect.Kind));
                Assert.That(s.State.Enemies[0].Health, Is.LessThan(1000));
                Call(battle, "ProcessAttackEvents"); Call(battle, "ProcessAttackEvents");
                var pool = battle.BasicProjectiles; var view = pool.GetView(0);
                Assert.That(view.Kind, Is.EqualTo(BasicProjectilePool.KindFor(id)));
                Assert.That(pool.GetLevel(0), Is.EqualTo(level)); Assert.That(pool.GetEvent(0), Is.EqualTo(attack));
                Assert.That(view.Source, Is.EqualTo(source));
                Assert.That(view.Target, Is.EqualTo(new Vector3(attack.TargetPosition.X, .6f, attack.TargetPosition.Y)));
                var hp = s.State.Enemies[0].Health; var statuses = s.State.Enemies[0].Statuses.Single().RemainingTicks;
                Object.DestroyImmediate(binding.gameObject);
                view.Advance(view.FlightDuration * .5f);
                Assert.That(view.HeadPosition, Is.Not.EqualTo(source).And.Not.EqualTo(view.Target));
                view.Advance(view.FlightDuration * .5f);
                Assert.That(view.ImpactVisible, Is.True); Assert.That(view.HeadPosition, Is.EqualTo(view.Target));
                Assert.That(s.State.Enemies[0].Health, Is.EqualTo(hp));
                Assert.That(s.State.Enemies[0].Statuses.Single().RemainingTicks, Is.EqualTo(statuses));
                Assert.That(pool.GetView(1).Remaining, Is.Zero);
                Assert.That(battle.GetComponentsInChildren<AttackBeamView>(), Is.Empty);
            });
        }

        [TestCase("tide")] [TestCase("gale")] [TestCase("stone")]
        public void Vfx1B02_LethalRemovalFallbackAndRepeatedConsumptionNeverRepeatRewards(string type)
        {
            WithBattle(battle =>
            {
                var id = new TowerDefinitionId(type);
                var s = Install(battle, new TowerCatalog(new[] { battle.Simulation.TowerCatalog.Get(id) }), 1);
                s.PlaceTower(new PlaceTowerCommand(id, new GridCoordinate(-4, -1)));
                s.StartWave(); s.Step(); var attack = s.AttackEvents.Single();
                Assert.That(attack.WasLethal, Is.True); Assert.That(s.State.Enemies, Is.Empty);
                var gold = s.State.Gold;
                Call(battle, "ProcessAttackEvents"); Call(battle, "ProcessAttackEvents");
                var view = battle.BasicProjectiles.GetView(0);
                Assert.That(Vector3.Distance(view.Source, new Vector3(-4, .89f, -1)), Is.LessThan(.0001f));
                Assert.That(battle.BasicProjectiles.GetEvent(0), Is.EqualTo(attack));
                battle.BasicProjectiles.Advance(.5f);
                Assert.That(s.State.Gold, Is.EqualTo(gold)); Assert.That(s.State.Kills, Is.EqualTo(1));
                Assert.That(battle.BasicProjectiles.GetView(1).Remaining, Is.Zero);
                Assert.That(s.AttackEvents, Is.Empty); Assert.That(s.DamageEvents, Is.Empty);
                Assert.That(s.ReactionEvents, Is.Empty); Assert.That(s.AbilityEvents, Is.Empty);
            });
        }

        [TestCase("tide")] [TestCase("gale")] [TestCase("stone")]
        public void Vfx1B01_OutOfBoundsAnchorFallBackToAuthoritativeCell(string type)
        {
            WithBattle(battle =>
            {
                var id = new TowerDefinitionId(type);
                var levels = battle.Simulation.TowerCatalog.Get(id).Levels.Select(l => new TowerDefinition(id, 0, 10, 30, 1, l.StatusEffect, l.ArmorPenetration)).ToArray();
                var s = Install(battle, new TowerCatalog(new[] { new TowerArchetypeDefinition(id, true, levels) }));
                s.PlaceTower(new PlaceTowerCommand(id, new GridCoordinate(-4, -1))); Call(battle, "SynchronizeViews");
                var binding = battle.GetComponentsInChildren<FlameModelBinding>().Single();
                s.StartWave();
                foreach (var bad in new[] { new Vector3(100,100,100), new Vector3(-100,0,0), new Vector3(0,100,0) })
                {
                    binding.ShotSource.position = bad;
                    s.Step(); Call(battle, "ProcessAttackEvents");
                }
                for (var i = 0; i < 3; i++)
                {
                    Assert.That(battle.BasicProjectiles.GetEvent(i).Tick, Is.EqualTo(i + 1));
                    Assert.That(Vector3.Distance(battle.BasicProjectiles.GetView(i).Source, new Vector3(-4,.89f,-1)), Is.LessThan(.0001f));
                }
            });
        }

        [Test]
        public void Vfx1B03_PrewarmSaturationCrossFamilyReuseAndResetHaveStableObjectAndMaterialIds()
        {
            WithBattle(battle =>
            {
                var pool = battle.BasicProjectiles;
                var objects = battle.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).ToArray();
                var renderers = battle.GetComponentsInChildren<LineRenderer>(true).Where(l => l.transform.parent.name.StartsWith("Basic Projectile Pool")).ToArray();
                Assert.That(renderers.Length, Is.EqualTo(48));
                var materials = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                Assert.That(materials.Length, Is.EqualTo(1));
                for (var i = 1; i <= 60; i++) pool.Play((BasicShotKind)(i % 3 + 1),
                    new AttackEvent(new TowerId(i), new EnemyId(i), new PathPosition(i,1), i,10,false), i % 3 + 1, Vector3.zero);
                CollectionAssert.AreEquivalent(Enumerable.Range(49,12), Enumerable.Range(0,12).Select(i => pool.GetEvent(i).Tick));
                CollectionAssert.AreEqual(objects, battle.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()));
                CollectionAssert.AreEquivalent(materials, renderers.SelectMany(r => r.sharedMaterials).Distinct());
                pool.Advance(.5f);
                Assert.That(renderers.All(l => !l.enabled && l.positionCount == 0), Is.True);
                for (var i = 0; i < 12; i++)
                {
                    Assert.That(pool.GetView(i).Kind, Is.EqualTo(BasicShotKind.None));
                    Assert.That(pool.GetEvent(i).TargetEnemyId.Value, Is.Zero); Assert.That(pool.GetLevel(i), Is.Zero);
                    Assert.That(pool.GetView(i).Source, Is.EqualTo(Vector3.zero));
                }
                pool.Play(BasicShotKind.Tide, new AttackEvent(new TowerId(1),new EnemyId(1),new PathPosition(1,1),1,10,false),1,Vector3.one);
                Assert.That(pool.GetView(0).Kind, Is.EqualTo(BasicShotKind.Tide)); Assert.That(pool.GetView(0).ImpactVisible, Is.False);
                var block = new MaterialPropertyBlock(); renderers[0].GetPropertyBlock(block);
                Assert.That(Vector4.Distance(block.GetColor("_BaseColor"), new Color(.25f,.82f,1)), Is.LessThan(.0001f));
                battle.ResetPresentation(); Assert.That(renderers.All(l => !l.enabled), Is.True);
                Call(battle,"Start"); Assert.That(battle.BasicProjectiles, Is.SameAs(pool));
                Assert.That(battle.GetComponentsInChildren<LineRenderer>(true).Count(l => l.transform.parent.name.StartsWith("Basic Projectile Pool")), Is.EqualTo(48));
            });
        }

        [Test]
        public void Vfx1B02_DamageWithoutAttackEventDoesNotCreateABasicShot()
        {
            WithBattle(battle =>
            {
                var s = battle.Simulation;
                s.State.AddEnemy(new EnemyState(new EnemyId(1), new EnemyDefinition(new EnemyDefinitionId("test"),100,1,1,0), 100,0));
                s.State.Enemies[0].ApplyStatus(new StatusEffectDefinition(StatusKind.Burn,1,30,1),new TowerId(99),0);
                s.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,"flame"));
                s.StartWave(); s.Step();
                Assert.That(s.DamageEvents.Any(d => d.Origin == DamageOrigin.Burn), Is.True);
                Assert.That(s.AttackEvents, Is.Empty);
                Call(battle,"ProcessAttackEvents");
                Assert.That(Enumerable.Range(0,12).All(i => battle.BasicProjectiles.GetView(i).Remaining == 0), Is.True);
            });
        }

        private static BattleSimulation Install(BattleSceneController battle, TowerCatalog catalog, int hp = 1000)
        {
            var s = new BattleSimulation(new GridMapDefinition(12,8,new GridCoordinate(-6,-4),1),
                new PathDefinition(new[]{new GridCoordinate(-5,-2),new GridCoordinate(5,-2)}),
                new[]{new WaveDefinition(new EnemyDefinition(new EnemyDefinitionId("shade"),hp,1,1,5),1,0,30)},catalog,20,150,0,battle.Simulation.ReactionCatalog);
            typeof(BattleSceneController).GetField("_simulation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(battle,s);
            battle.ResetPresentation(); return s;
        }
        private static void WithBattle(System.Action<BattleSceneController> action)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/Battle.unity");
            try
            {
                var battle = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattleSceneController>()).Single();
                Call(battle,"Start"); action(battle);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        internal static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
