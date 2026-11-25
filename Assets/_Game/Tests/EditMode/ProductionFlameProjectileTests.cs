using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class ProductionFlameProjectileTests
    {
        private Scene _scene;
        private BattleSceneController _battle;

        [SetUp]
        public void LoadProduction()
        {
            _scene = EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/Battle.unity");
            _battle = _scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleSceneController>()).Single();
            Invoke(_battle, "Start");
            var choice = _battle.Simulation.State.ElementProgression.PendingChoiceId;
            Assert.That(_battle.ChooseElement(new ChooseElementCommand(choice, "flame")).Success, Is.True);
        }

        [TearDown]
        public void CloseProduction() { if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); }

        [Test]
        public void Vfx1A01_FlameAttackUsesPrewarmedMovingProjectileInsteadOfGenericBeam()
        {
            Assert.That(_battle.PlaceTower(new TowerDefinitionId("flame"), new GridCoordinate(-4, -1)).Success, Is.True);
            Invoke(_battle, "LateUpdate");
            Assert.That(_battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            _battle.Simulation.Step();
            Assert.That(_battle.Simulation.AttackEvents.Count, Is.EqualTo(1));
            Invoke(_battle, "ProcessAttackEvents");
            Assert.That(_battle.GetComponentsInChildren<AttackBeamView>(), Is.Empty, "Production FLAME must route to a prewarmed fireball.");
            Assert.That(_battle.GetComponentsInChildren<LineRenderer>().Count(r => r.enabled && r.name == "Fire head layer"), Is.EqualTo(3));
            Assert.That(_battle.Simulation.AttackEvents, Is.Empty);
        }

        private BattleSimulation InstallFixture(string type, int hp = 1000, int count = 1)
        {
            // Явный synthetic unit fixture; earned production replay проверяется отдельно.
            var id = new TowerDefinitionId(type);
            var levels = _battle.Simulation.TowerCatalog.Get(id).Levels
                .Select(l => new TowerDefinition(id, 0, 10, 30, 10, l.StatusEffect, l.ArmorPenetration, l.IntrinsicReaction)).ToArray();
            var catalog = new TowerCatalog(new[] { new TowerArchetypeDefinition(id, true, levels) });
            var simulation = new BattleSimulation(new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1),
                new PathDefinition(new[] { new GridCoordinate(-5, -2), new GridCoordinate(5, -2) }),
                new[] { new WaveDefinition(new EnemyDefinition(new EnemyDefinitionId("shade"), hp, 1, 1, 5), count, 0, 30) }, catalog, 20, 150, 0, _battle.Simulation.ReactionCatalog);
            typeof(BattleSceneController).GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_battle, simulation);
            _battle.ResetPresentation();
            return simulation;
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Vfx1A01_LevelAndAuthoredSourceAreCapturedAndStayFixedAfterModelRemoval(int level)
        {
            var s = InstallFixture("flame");
            var placed = s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), new GridCoordinate(-4, -1)));
            for (var i = 1; i < level; i++) Assert.That(s.UpgradeTower(s.State.Towers[0].Id).Success, Is.True);
            Invoke(_battle, "SynchronizeViews");
            var binding = _battle.GetComponentsInChildren<FlameModelBinding>().Single();
            Assert.That(binding.Level, Is.EqualTo(level));
            s.StartWave(); s.Step(); var attack = s.AttackEvents.Single(); Invoke(_battle, "ProcessAttackEvents");
            var source = binding.ShotSource.position;
            var pool = _battle.FlameProjectiles; var view = pool.GetView(0);
            Assert.That(pool.GetLevel(0), Is.EqualTo(level));
            Assert.That(pool.GetEvent(0).AttackerTowerId, Is.EqualTo(s.State.Towers[0].Id));
            Assert.That(pool.GetEvent(0).Tick, Is.EqualTo(attack.Tick));
            Assert.That(view.Source, Is.EqualTo(source));
            Object.DestroyImmediate(binding.gameObject);
            pool.Advance(.1f);
            Assert.That(view.Source, Is.EqualTo(source));
            Assert.That(view.HeadPosition, Is.Not.EqualTo(source));
            Assert.That(Vector3.Distance(view.HeadPosition, view.TailEnd), Is.LessThanOrEqualTo(.481f));
            Assert.That(view.HeadPosition, Is.Not.EqualTo(view.Target));
        }

        [Test]
        public void Vfx1A02_LethalTargetRemovedBeforeConsumptionKeepsCapturedEndpointAndRewardOnce()
        {
            var s = InstallFixture("flame", hp: 10);
            s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), new GridCoordinate(-4, -1)));
            s.StartWave(); s.Step(); var attack = s.AttackEvents.Single();
            Assert.That(s.State.Enemies, Is.Empty); Assert.That(attack.WasLethal, Is.True);
            var gold = s.State.Gold; var kills = s.State.Kills; var tick = s.State.Tick;
            Invoke(_battle, "ProcessAttackEvents"); Invoke(_battle, "ProcessAttackEvents");
            var view = _battle.FlameProjectiles.GetView(0);
            Assert.That(view.Target, Is.EqualTo(new Vector3(attack.TargetPosition.X, .6f, attack.TargetPosition.Y)));
            Assert.That(view.TargetEnemyId, Is.EqualTo(attack.TargetEnemyId)); Assert.That(view.WasLethal, Is.True);
            Assert.That(Vector3.Distance(view.Source, new Vector3(-4f, .89f, -1f)), Is.LessThan(.0001f), "No rendered tower: bounded Domain center fallback");
            _battle.FlameProjectiles.Advance(.2f);
            Assert.That(view.ImpactVisible, Is.True); Assert.That(view.HeadPosition, Is.EqualTo(view.Target));
            Assert.That(s.State.Gold, Is.EqualTo(gold)); Assert.That(s.State.Kills, Is.EqualTo(kills)); Assert.That(s.State.Tick, Is.EqualTo(tick));
            Assert.That(_battle.FlameProjectiles.GetView(1).Remaining, Is.Zero);
        }

        [Test]
        public void Vfx1A01_InvalidAuthoredAnchorFallsBackToBoundedDomainCell()
        {
            var s = InstallFixture("flame");
            s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), new GridCoordinate(-4, -1)));
            Invoke(_battle, "SynchronizeViews");
            var binding = _battle.GetComponentsInChildren<FlameModelBinding>().Single();
            binding.ShotSource.position = new Vector3(100, 100, 100);
            s.StartWave(); s.Step(); Invoke(_battle, "ProcessAttackEvents");
            Assert.That(Vector3.Distance(_battle.FlameProjectiles.GetView(0).Source, new Vector3(-4, .89f, -1)), Is.LessThan(.0001f));
        }

        [TestCase("test-tower")]
        public void Vfx1A01_OtherDefinitionsKeepGenericBeam(string type)
        {
            var s = InstallFixture(type);
            s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(type), new GridCoordinate(-4, -1)));
            s.StartWave(); s.Step(); Invoke(_battle, "ProcessAttackEvents");
            Assert.That(_battle.GetComponentsInChildren<AttackBeamView>().Length, Is.EqualTo(1));
            Assert.That(Enumerable.Range(0, 8).All(i => _battle.FlameProjectiles.GetView(i).Remaining == 0), Is.True);
        }

        [Test]
        public void Vfx1A03_MultipleTicksAndRepeatedConsumptionPreserveEachEventWithoutDuplicateShots()
        {
            var s = InstallFixture("flame");
            s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), new GridCoordinate(-4, -1)));
            s.StartWave();
            for (var tick = 1; tick <= 3; tick++)
            {
                s.Step(); var attack = s.AttackEvents.Single();
                Invoke(_battle, "ProcessAttackEvents"); Invoke(_battle, "ProcessAttackEvents");
                var shot = _battle.FlameProjectiles.GetEvent(tick - 1);
                Assert.That(shot.Tick, Is.EqualTo(tick)); Assert.That(shot.TargetPosition, Is.EqualTo(attack.TargetPosition));
                Assert.That(s.AttackEvents, Is.Empty); Assert.That(s.DamageEvents, Is.Empty);
                Assert.That(s.ReactionEvents, Is.Empty); Assert.That(s.AbilityEvents, Is.Empty);
            }
            Assert.That(_battle.FlameProjectiles.GetView(3).Remaining, Is.Zero);
        }

        [Test]
        public void Vfx1A04_SaturationReplacesOldestIncludingSameFrameTiesWithoutCreatingObjectsOrMaterials()
        {
            var pool = _battle.FlameProjectiles;
            var objects = _battle.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).ToArray();
            var materials = _battle.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
            for (var i = 1; i <= 18; i++) pool.Play(new AttackEvent(new TowerId(i), new EnemyId(i), new PathPosition(i, 0), i, 10, i % 2 == 0), i % 3 + 1, Vector3.zero);
            CollectionAssert.AreEquivalent(Enumerable.Range(11, 8), Enumerable.Range(0, 8).Select(i => pool.GetEvent(i).TargetEnemyId.Value));
            CollectionAssert.AreEqual(objects, _battle.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()));
            CollectionAssert.AreEqual(materials, _battle.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials));
            pool.Advance(.21f); Assert.That(pool.GetView(0).ImpactVisible, Is.True);
            pool.Clear();
            for (var i = 0; i < 8; i++)
            {
                Assert.That(pool.GetView(i).Remaining, Is.Zero); Assert.That(pool.GetView(i).ImpactVisible, Is.False);
                Assert.That(pool.GetView(i).TrailVisible, Is.False); Assert.That(pool.GetView(i).Source, Is.EqualTo(Vector3.zero));
                Assert.That(pool.GetEvent(i).TargetEnemyId.Value, Is.Zero); Assert.That(pool.GetLevel(i), Is.Zero);
            }
            pool.Play(new AttackEvent(new TowerId(20), new EnemyId(21), new PathPosition(2, 3), 99, 10, false), 2, Vector3.one);
            Assert.That(pool.GetView(0).ImpactVisible, Is.False); Assert.That(pool.GetView(0).HeadPosition, Is.EqualTo(Vector3.one));
            pool.Advance(.5f); Assert.That(pool.GetEvent(0).TargetEnemyId.Value, Is.Zero);
        }

        [Test]
        public void Vfx1A04_RepeatedStartKeepsOneFlamePoolAndResetClearsActiveGeometry()
        {
            var pool = _battle.FlameProjectiles;
            Invoke(_battle, "Start");
            Assert.That(_battle.FlameProjectiles, Is.SameAs(pool));
            Assert.That(_battle.GetComponentsInChildren<LineRenderer>(true).Count(l => l.name == "Fire head layer"), Is.EqualTo(24));
            pool.Play(new AttackEvent(new TowerId(1), new EnemyId(1), new PathPosition(1, 0), 1, 10, false), 1, Vector3.zero);
            _battle.ResetPresentation();
            Assert.That(pool.GetView(0).Remaining, Is.Zero); Assert.That(pool.GetView(0).ImpactVisible, Is.False);
        }

        internal static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
