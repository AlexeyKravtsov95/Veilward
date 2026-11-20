using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleTowerCatalogPresentationTests
    {
        private BattleSceneController _battle;
        private BattlePlacementController _placement;
        private static readonly GridCoordinate Cell = new GridCoordinate(-4, 0);

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            _battle = Object.FindFirstObjectByType<BattleSceneController>();
            _placement = Object.FindFirstObjectByType<BattlePlacementController>();
            Invoke(_battle, "Start");
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
            var historical = config.CreateHistoricalAllOpenSimulation();
            typeof(BattleSceneController).GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_battle, historical);
            _battle.GetComponent<BattleCheckpointController>().Configure(null, historical, "eight-wave-v1");
            Invoke(_placement, "Start");
        }

        [TearDown]
        public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        [Test]
        public void RegBattle116_FirstTap_OpensOnlyVisibleCatalogWithoutPurchase()
        {
            Assert.That(_placement.TrySelectCell(Cell), Is.True);
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(150));
            Assert.That(_battle.Simulation.State.Towers, Is.Empty);
            Assert.That(_placement.CatalogButtons.Select(b => b.GetComponentInChildren<Text>().text.Split('\n')[0]),
                Is.EqualTo(new[] { "FLAME", "TIDE", "GALE", "STONE", "STEAM", "STORM" }));
            Assert.That(Button("Confirm Button").interactable, Is.False);
            _placement.SelectTowerType(new TowerDefinitionId("test-tower"));
            _placement.ConfirmSelection();
            Assert.That(_battle.Simulation.State.Towers, Is.Empty);
        }

        [TestCase("flame", 70, 3f, "Burn", "3 damage / 0.5 s")]
        [TestCase("tide", 60, 3.5f, "Wet", "20% slow")]
        [TestCase("gale", 80, 3f, "Gust", "+20% direct damage")]
        [TestCase("stone", 90, 2.5f, "Crack", "-2 armor")]
        public void RegBattle117_EachType_PreviewsAndPurchasesExactlyOnce(
            string id, int price, float range, string status, string effect)
        {
            _placement.TrySelectCell(Cell);
            _placement.CatalogButtons.Single(b => b.GetComponentInChildren<Text>().text.Split('\n')[0] == id.ToUpperInvariant())
                .onClick.Invoke();
            Assert.That(_placement.SelectedTowerTypeId.Value, Is.EqualTo(id));
            var definition = _battle.Simulation.TowerCatalog.Get(new TowerDefinitionId(id)).Levels[0];
            Assert.That(_placement.BuildStatsText.text, Does.Contain($"Price: {price} gold"));
            Assert.That(_placement.BuildStatsText.text, Does.Contain($"Damage: {definition.Damage}"));
            Assert.That(_placement.BuildStatsText.text, Does.Contain("Range:"));
            Assert.That(_placement.BuildStatsText.text, Does.Contain("FireRate:"));
            Assert.That(_placement.BuildStatsText.text, Does.Contain(status).And.Contain(effect));
            var preview = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(r => r.name == "Range Preview");
            Assert.That(preview.GetPosition(0).magnitude, Is.EqualTo(range).Within(0.001));
            Assert.That(Button("Confirm Button").interactable, Is.True);
            var previewColor = ColorOf(GameObject.Find("Tower Preview"));
            Button("Confirm Button").onClick.Invoke();
            Button("Confirm Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.Towers.Single().Definition.Id.Value, Is.EqualTo(id));
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(150 - price));
            Assert.That(_placement.SelectedTowerTypeId.Value, Is.Null.Or.Empty);
            Assert.That(_placement.HasSelection, Is.False);
            Invoke(_battle, "LateUpdate");
            Assert.That(GameObject.Find("Tower 1"), Is.Not.Null);
            if (id == "flame" || id == "tide")
            {
                var expectedPreview = id == "flame" ? new Color(1f, .36f, .16f) : new Color(.2f, .72f, 1f);
                Assert.That(Vector4.Distance(previewColor, expectedPreview), Is.LessThan(.000001f));
                AssertProductionPalette(GameObject.Find("Tower 1"), id, 1);
            }
            else Assert.That(ColorOf(GameObject.Find("Tower 1")), Is.EqualTo(previewColor));
        }

        [Test]
        public void RegBattle118_CancelChangeCellInsufficientAndRepeat_DoNotPurchase()
        {
            _placement.TrySelectCell(Cell);
            _placement.SelectTowerType(new TowerDefinitionId("stone"));
            _placement.CancelSelection();
            _placement.ConfirmSelection();
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(150));
            _placement.TrySelectCell(Cell);
            _placement.SelectTowerType(new TowerDefinitionId("stone"));
            _placement.TrySelectCell(new GridCoordinate(-3, 0));
            Assert.That(_placement.SelectedTowerTypeId.Value, Is.Null.Or.Empty);
            Assert.That(Button("Confirm Button").interactable, Is.False);
            _placement.SelectTowerType(new TowerDefinitionId("stone"));
            _placement.ConfirmSelection();
            _placement.TrySelectCell(Cell);
            _placement.SelectTowerType(new TowerDefinitionId("stone"));
            Assert.That(Button("Confirm Button").interactable, Is.False);
            _placement.ConfirmSelection();
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(60));
            Assert.That(_battle.Simulation.State.Towers.Count, Is.EqualTo(1));
        }

        [TestCase("flame")]
        [TestCase("tide")]
        [TestCase("gale")]
        [TestCase("stone")]
        public void RegBattle119_PanelUpgradeMaxAndSell_UseSelectedArchetype(string id)
        {
            var checkpoint = _battle.Simulation.ExportCheckpoint();
            checkpoint.gold = 1000;
            _battle.Simulation.RestoreCheckpoint(checkpoint);
            _placement.TrySelectCell(Cell);
            _placement.SelectTowerType(new TowerDefinitionId(id));
            _placement.ConfirmSelection();
            _placement.TrySelectCell(Cell);
            var tower = _battle.Simulation.State.Towers.Single();
            var levels = _battle.Simulation.TowerCatalog.Get(tower.Definition.Id).Levels;
            Color? previousColor = null;
            Mesh previousModelMesh = null;
            for (var level = 1; level <= 3; level++)
            {
                Invoke(_battle, "LateUpdate");
                var view = GameObject.Find($"Tower {tower.Id.Value}");
                if (id == "flame" || id == "tide")
                {
                    var mesh = AssertProductionPalette(view, id, level);
                    if (previousModelMesh != null) Assert.That(mesh, Is.Not.SameAs(previousModelMesh), "level changes geometry, not brightness");
                    previousModelMesh = mesh;
                }
                else
                {
                    var color = ColorOf(view);
                    if (previousColor.HasValue) Assert.That(color, Is.Not.EqualTo(previousColor.Value));
                    previousColor = color;
                }
                var range = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Single(r => r.name == "Range Preview");
                Assert.That(range.GetPosition(0).magnitude, Is.EqualTo(levels[level - 1].Range).Within(0.001));
                Assert.That(_placement.TowerStatsText.preferredHeight,
                    Is.LessThanOrEqualTo(_placement.TowerStatsText.rectTransform.rect.height));
                Assert.That(_placement.TowerStatsText.text, Does.Contain(id.ToUpperInvariant())
                    .And.Contain($"Level: {level}").And.Contain($"Damage: {levels[level - 1].Damage}")
                    .And.Contain(levels[level - 1].StatusEffect.Kind.ToString())
                    .And.Contain($"Sell refund: {(long)tower.InvestedGold * 70 / 100} gold"));
                Assert.That(_placement.TowerStatsText.text, Does.Contain(level < 3
                    ? $"Upgrade: {levels[level].Price} gold" : "Upgrade: MAX"));
                if (level < 3) Assert.That(_placement.UpgradeSelectedTower().Success, Is.True);
            }
            Assert.That(_placement.UpgradeButton.interactable, Is.False);
            Assert.That(_placement.UpgradeSelectedTower().Success, Is.False);
            var gold = _battle.Simulation.State.Gold;
            var refund = (int)((long)tower.InvestedGold * 70 / 100);
            _placement.RequestSellSelectedTower();
            Assert.That(_placement.ConfirmSellSelectedTower().Success, Is.True);
            Assert.That(_placement.ConfirmSellSelectedTower().Success, Is.False);
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(gold + refund));
            Assert.That(_battle.Simulation.State.Towers, Is.Empty);
        }

        private static Mesh AssertProductionPalette(GameObject view, string id, int level)
        {
            var binding = view.GetComponent<FlameModelBinding>();
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.Level, Is.EqualTo(level));
            Assert.That(binding.Model, Is.Not.Null);
            Assert.That(binding.Model.name, Is.EqualTo(id.ToUpperInvariant() + " L" + level));
            Assert.That(binding.ShotSource, Is.Not.Null);
            var expectedPath = id == "flame" ? "Assets/_Game/Prefabs/ART3/P_Tower_FlameLanternL" + level + ".prefab"
                : "Assets/_Game/Prefabs/ART3/TIDE/P_Tower_TideBellL" + level + ".prefab";
            var expected = AssetDatabase.LoadAssetAtPath<GameObject>(expectedPath);
            Assert.That(expected, Is.Not.Null);
            var mesh = binding.Model.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(mesh, Is.SameAs(expected.GetComponentInChildren<MeshFilter>().sharedMesh));
            var renderer = binding.Model.GetComponentInChildren<MeshRenderer>();
            CollectionAssert.AreEqual(expected.GetComponentInChildren<MeshRenderer>().sharedMaterials, renderer.sharedMaterials);
            Assert.That(renderer.HasPropertyBlock(), Is.False, "authored palette replaces graybox type tint");
            Assert.That(binding.Model.GetComponentsInChildren<Renderer>().All(r => r.enabled && !r.HasPropertyBlock()), Is.True);
            Assert.That(view.transform.Find("Foundation").GetComponent<Renderer>().enabled, Is.False);
            Assert.That(view.transform.Find("Spirit").GetComponent<Renderer>().enabled, Is.False);
            Assert.That(view.transform.Cast<Transform>().Count(t => t.gameObject.activeSelf &&
                (t.name.StartsWith("FLAME L") || t.name.StartsWith("TIDE L"))), Is.EqualTo(1));
            return mesh;
        }

        [Test]
        public void RegBattle121_LegacyRestore_HasNoneStatusAndOwnEconomyButIsHidden()
        {
            Assert.That(_battle.PlaceTower(Cell).Success, Is.True);
            var saved = _battle.Simulation.ExportCheckpoint();
            _battle.Simulation.RestoreCheckpoint(saved);
            Invoke(_battle, "LateUpdate");
            _placement.TrySelectCell(Cell);
            Assert.That(_placement.TowerStatsText.text, Does.Contain("Test Tower").And.Contain("Status: None")
                .And.Contain("Upgrade: 50 gold").And.Contain("Sell refund: 42 gold"));
            Assert.That(_placement.UpgradeSelectedTower().Success, Is.True);
            Assert.That(_placement.TowerStatsText.text, Does.Contain("Damage: 35").And.Contain("Sell refund: 77 gold"));
            _placement.RequestSellSelectedTower();
            Assert.That(_placement.ConfirmSellSelectedTower().Refund, Is.EqualTo(77));
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(117));
        }

        [Test]
        public void RegBattle120_SellModal_RejectsPublicCellSelection()
        {
            _battle.PlaceTower(new TowerDefinitionId("tide"), Cell);
            var second = new GridCoordinate(-3, 0);
            _battle.PlaceTower(new TowerDefinitionId("tide"), second);
            _placement.TrySelectCell(Cell);
            var selected = _placement.SelectedTowerId;
            _placement.RequestSellSelectedTower();
            Assert.That(_placement.TrySelectCell(second), Is.False);
            Assert.That(_placement.TrySelectCell(new GridCoordinate(-2, 0)), Is.False);
            Assert.That(_placement.SelectedTowerId, Is.EqualTo(selected));
            Assert.That(_placement.IsSellConfirmationVisible, Is.True);
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(30));
            Assert.That(_battle.Simulation.State.Towers.Count, Is.EqualTo(2));
        }

        [Test]
        public void RegBattle122_LandscapeInsets_OffsetCatalogAndTowerPanelsInsideSafeArea()
        {
            var serialized = new SerializedObject(_placement);
            var panels = new[] { "confirmationPanel", "towerPanel" }.Select(name =>
                ((GameObject)serialized.FindProperty(name).objectReferenceValue).GetComponent<RectTransform>()).ToArray();
            var initial = panels.Select(panel => panel.anchoredPosition).ToArray();
            var method = typeof(BattlePlacementController).GetMethod("ApplySafeArea", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "New panels must account for landscape right/bottom unsafe insets.");
            method.Invoke(_placement, new object[] { new Rect(0f, 24f, 1468f, 876f), new Vector2(1600f, 900f), 0.8f });
            for (var index = 0; index < panels.Length; index++)
            {
                Assert.That(panels[index].anchoredPosition, Is.EqualTo(initial[index] + new Vector2(-165f, 30f)));
                Assert.That(panels[index].anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(panels[index].anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
            }
            method.Invoke(_placement, new object[] { new Rect(0f, 0f, 1600f, 900f), new Vector2(1600f, 900f), 0.8f });
            for (var index = 0; index < panels.Length; index++)
                Assert.That(panels[index].anchoredPosition, Is.EqualTo(initial[index]), "Insets must not accumulate or survive full-screen safe area.");
        }

        private static Color ColorOf(GameObject view)
        {
            var block = new MaterialPropertyBlock();
            view.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
            return block.GetColor(Shader.PropertyToID("_BaseColor"));
        }

        internal static void WireCatalog(SerializedObject serialized, Transform root)
        {
            var catalog = new GameObject("Catalog", typeof(RectTransform));
            catalog.transform.SetParent(root, false);
            var template = new GameObject("Template", typeof(RectTransform), typeof(Button));
            template.transform.SetParent(catalog.transform, false);
            new GameObject("Label", typeof(RectTransform), typeof(Text)).transform.SetParent(template.transform, false);
            var stats = new GameObject("Build Stats", typeof(RectTransform), typeof(Text));
            stats.transform.SetParent(root, false);
            var sellText = new GameObject("Sell Text", typeof(RectTransform), typeof(Text));
            sellText.transform.SetParent(root, false);
            serialized.FindProperty("catalogRoot").objectReferenceValue = catalog.GetComponent<RectTransform>();
            serialized.FindProperty("catalogButtonTemplate").objectReferenceValue = template.GetComponent<Button>();
            serialized.FindProperty("buildStatsText").objectReferenceValue = stats.GetComponent<Text>();
            serialized.FindProperty("sellConfirmationText").objectReferenceValue = sellText.GetComponent<Text>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button Button(string name) => Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
        private static void Invoke(object target, string method) => target.GetType().GetMethod(
            method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
