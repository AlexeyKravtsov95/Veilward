using Veilward.Domain;
using UnityEngine;

namespace Veilward.Presentation
{
    // Только отображение: числа поступают из validated Domain definitions.
    public static class TowerDisplay
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static string Name(TowerDefinitionId id) => id.Value == "test-tower" ? "Test Tower" : id.Value.ToUpperInvariant();

        public static string Role(TowerDefinition definition)
        {
            switch (definition.StatusEffect?.Kind)
            {
                case StatusKind.Burn: return "Sustained damage";
                case StatusKind.Wet: return "Movement control";
                case StatusKind.Gust: return "Rapid fire / damage support";
                case StatusKind.Crack: return "Armor piercing burst";
                default: return "Legacy defense";
            }
        }

        public static string Stats(TowerDefinition definition)
        {
            var stats = $"Damage: {definition.Damage}    Range: {definition.Range:0.##} units\n" +
                $"FireRate: {definition.FireRate:0.##}/s";
            var status = definition.StatusEffect;
            if (status == null) return stats + "\nStatus: None";
            string effect;
            switch (status.Kind)
            {
                case StatusKind.Burn:
                    effect = $"{status.Power} damage / {Seconds(status.DamageIntervalTicks)} s";
                    break;
                case StatusKind.Wet: effect = $"{status.Power}% slow"; break;
                case StatusKind.Gust: effect = $"+{status.Power}% direct damage"; break;
                default: effect = $"-{status.Power} armor"; break;
            }
            return stats + $"\nStatus: {status.Kind} — Power: {effect}\nDuration: {Seconds(status.DurationTicks)} s" +
                (definition.ArmorPenetration > 0 ? $"    Armor pierce: {definition.ArmorPenetration}" : "");
        }

        private static string Seconds(int ticks) => ((float)ticks / SimulationTiming.TicksPerSecond).ToString("0.##");

        public static void ApplyVisual(Transform view, TowerDefinition definition, int level)
        {
            // PropertyBlock не создаёт копии material assets; footprint сохраняется при Upgrade.
            var color = new Color(0.95f, 0.7f, 0.2f);
            switch (definition.StatusEffect?.Kind)
            {
                case StatusKind.Burn: color = new Color(1f, 0.36f, 0.16f); break;
                case StatusKind.Wet: color = new Color(0.2f, 0.72f, 1f); break;
                case StatusKind.Gust: color = new Color(0.45f, 0.95f, 0.7f); break;
                case StatusKind.Crack: color = new Color(0.7f, 0.58f, 0.86f); break;
            }
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color * (1f + (level - 1) * 0.12f));
            foreach (var renderer in view.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(block);
        }
    }
}
