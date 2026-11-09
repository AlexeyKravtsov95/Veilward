using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleTowerLevelConfigTests
    {
        [Test]
        public void SavedScenario_HasThreeConfiguredLevelsAndPreservesLegacyBaseFields()
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(
                "Assets/_Game/Configs/TestWave.asset");
            Assert.That(config, Is.Not.Null);
            var serialized = new SerializedObject(config);
            Assert.That(serialized.FindProperty("towerPrice").intValue, Is.EqualTo(60));
            Assert.That(serialized.FindProperty("towerRange").floatValue, Is.EqualTo(3f));
            Assert.That(serialized.FindProperty("towerFireRate").floatValue, Is.EqualTo(2f));
            Assert.That(serialized.FindProperty("towerDamage").intValue, Is.EqualTo(25));
            var upgrades = serialized.FindProperty("towerUpgrades");
            Assert.That(upgrades, Is.Not.Null);
            Assert.That(upgrades.arraySize, Is.EqualTo(2));
            AssertLevel(upgrades.GetArrayElementAtIndex(0), 50, 3f, 2f, 35);
            AssertLevel(upgrades.GetArrayElementAtIndex(1), 80, 3f, 2f, 50);

            var simulation = config.CreateSimulation();

            Assert.That(simulation.TowerLevels.Count, Is.EqualTo(3));
            Assert.That(simulation.TowerLevels[0].Price, Is.EqualTo(60));
            Assert.That(simulation.TowerLevels[1].Price, Is.EqualTo(50));
            Assert.That(simulation.TowerLevels[2].Price, Is.EqualTo(80));
            Assert.That(simulation.TowerLevels[2].Damage, Is.EqualTo(50));
        }

        [Test]
        public void CreateSimulation_UsesSerializedUpgradeValues()
        {
            var config = BattleScenarioConfig.CreateDefault();
            var serialized = new SerializedObject(config);
            var upgrades = serialized.FindProperty("towerUpgrades");
            upgrades.arraySize = 2;
            SetLevel(upgrades.GetArrayElementAtIndex(0), 51, 4f, 3f, 36);
            SetLevel(upgrades.GetArrayElementAtIndex(1), 81, 5f, 4f, 51);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var simulation = config.CreateSimulation();

            Assert.That(simulation.TowerLevels[1].Price, Is.EqualTo(51));
            Assert.That(simulation.TowerLevels[1].Range, Is.EqualTo(4f));
            Assert.That(simulation.TowerLevels[1].FireRate, Is.EqualTo(3f));
            Assert.That(simulation.TowerLevels[1].Damage, Is.EqualTo(36));
            Assert.That(simulation.TowerLevels[2].Price, Is.EqualTo(81));
            Assert.That(simulation.TowerLevels[2].Range, Is.EqualTo(5f));
            Assert.That(simulation.TowerLevels[2].FireRate, Is.EqualTo(4f));
            Assert.That(simulation.TowerLevels[2].Damage, Is.EqualTo(51));
            Object.DestroyImmediate(config);
        }

        private static void AssertLevel(SerializedProperty property, int price, float range, float fireRate, int damage)
        {
            Assert.That(property.FindPropertyRelative("price").intValue, Is.EqualTo(price));
            Assert.That(property.FindPropertyRelative("range").floatValue, Is.EqualTo(range));
            Assert.That(property.FindPropertyRelative("fireRate").floatValue, Is.EqualTo(fireRate));
            Assert.That(property.FindPropertyRelative("damage").intValue, Is.EqualTo(damage));
        }

        private static void SetLevel(SerializedProperty property, int price, float range, float fireRate, int damage)
        {
            property.FindPropertyRelative("price").intValue = price;
            property.FindPropertyRelative("range").floatValue = range;
            property.FindPropertyRelative("fireRate").floatValue = fireRate;
            property.FindPropertyRelative("damage").intValue = damage;
        }
    }
}
