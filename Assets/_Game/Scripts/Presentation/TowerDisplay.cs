using System;
using System.Globalization;
using Veilward.Domain;
using UnityEngine;

namespace Veilward.Presentation
{
    // Только отображение: числа поступают из validated Domain definitions.
    public static class TowerDisplay
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static string Name(TowerDefinitionId id) => id.Value == "test-tower" ? "Test Tower" : id.Value.ToUpperInvariant();

        public static string ArchetypeName(TowerDefinitionId id) => id.Value switch
        {
            "flame" => "Ember Lantern",
            "tide" => "Tide Bell",
            "gale" => "Wind Talisman",
            "stone" => "Shard Seal",
            "steam" => "Mist Fox",
            "storm" => "Thunder Carp",
            _ => Name(id)
        };

        public static string CoreStats(TowerDefinition definition, bool compact = false) =>
            compact
                ? FormattableString.Invariant($"Damage {definition.Damage} · Range {definition.Range:0.##} · {definition.FireRate:0.##}/s")
                : FormattableString.Invariant($"Damage: {definition.Damage} · Range: {definition.Range:0.##} · Rate: {definition.FireRate:0.##}/s");

        public static string Effect(TowerDefinition definition)
        {
            var intrinsic = definition.IntrinsicReaction;
            if (intrinsic != null)
                return intrinsic.Kind == ReactionKind.Steam ? "Innate Steam burst" : "Innate Storm chain";
            var status = definition.StatusEffect;
            if (status == null) return "Status: None";
            return status.Kind switch
            {
                StatusKind.Burn => $"Burn: {status.Power} damage / {Seconds(status.DamageIntervalTicks)} s",
                StatusKind.Wet => $"Wet: {status.Power}% slow",
                StatusKind.Gust => $"Gust: +{status.Power}% direct damage",
                _ => $"Crack: -{status.Power} armor"
            };
        }

        public static VeilwardGlyph Glyph(TowerDefinitionId id) => id.Value switch
        {
            "flame" => VeilwardGlyph.Flame,
            "tide" => VeilwardGlyph.Tide,
            "gale" => VeilwardGlyph.Gale,
            "stone" => VeilwardGlyph.Stone,
            "steam" => VeilwardGlyph.Steam,
            "storm" => VeilwardGlyph.Storm,
            _ => VeilwardGlyph.Seal
        };

        public static Color ElementTint(TowerDefinitionId id) => id.Value switch
        {
            "flame" => new Color32(244, 151, 131, 255),
            "tide" => new Color32(132, 195, 234, 255),
            "gale" => new Color32(139, 214, 186, 255),
            "steam" => new Color32(193, 218, 213, 255),
            "storm" => new Color32(189, 177, 240, 255),
            _ => new Color32(231, 194, 121, 255)
        };

        public static string Role(TowerDefinition definition)
        {
            if (definition.IntrinsicReaction != null)
                return definition.IntrinsicReaction.Kind == ReactionKind.Steam ? "Пар: встроенная AoE" : "Шторм: встроенная цепь";
            switch (definition.StatusEffect?.Kind)
            {
                case StatusKind.Burn: return "Sustained damage";
                case StatusKind.Wet: return "Movement control";
                case StatusKind.Gust: return "Rapid fire / damage support";
                case StatusKind.Crack: return "Armor piercing burst";
                default: return "Legacy defense";
            }
        }

        public static string Stats(TowerDefinition definition, ReactionCatalog catalog = null)
        {
            var stats = $"Damage: {definition.Damage}    Range: {definition.Range:0.##} units\n" +
                $"FireRate: {definition.FireRate:0.##}/s";
            var intrinsic = definition.IntrinsicReaction;
            if (intrinsic != null)
            {
                if (catalog == null || !catalog.TryGet(intrinsic.Kind, out var global))
                    throw new System.ArgumentException("Hybrid display requires the reaction catalog.", nameof(catalog));
                var cooldown = global.CooldownTicks;
                return stats + $"\n{intrinsic.Kind.ToString().ToUpperInvariant()}: встроенная {intrinsic.DamagePercent}%, " +
                    $"{(intrinsic.Kind == ReactionKind.Steam ? "радиус" : "hop")} {intrinsic.Range:0.##}, лимит {intrinsic.TargetLimit}" +
                    $"\nCD: {cooldown} ticks / {Seconds(cooldown)} s на враге" +
                    "\nНужна выжившая primary" + (intrinsic.Kind == ReactionKind.Storm ? "; нужен сосед" : "; лимит включает primary") +
                    "\nDamage — базовый direct; итог зависит от целей";
            }
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
                (definition.ArmorPenetration > 0 ? $"    Armor pierce: {definition.ArmorPenetration}" : "") + PairConditions(status.Kind, catalog);
        }

        private static string PairConditions(StatusKind status, ReactionCatalog catalog)
        {
            if (catalog == null || status == StatusKind.Crack) return string.Empty;
            var steam = status != StatusKind.Gust && catalog.TryGet(ReactionKind.Steam, out _);
            var storm = status != StatusKind.Burn && catalog.TryGet(ReactionKind.Storm, out _);
            if (!steam && !storm) return string.Empty;
            var pairs = (steam ? "Пламя + Прилив → Пар" : "") + (steam && storm ? "; " : "") +
                (storm ? "Прилив + Ветер → Шторм" : "");
            var cooldown = (steam ? $"Пар {catalog.Get(ReactionKind.Steam).CooldownTicks} ticks" : "") +
                (steam && storm ? "; " : "") + (storm ? $"Шторм {catalog.Get(ReactionKind.Storm).CooldownTicks} ticks" : "");
            if (steam && storm && catalog.Get(ReactionKind.Steam).CooldownTicks == catalog.Get(ReactionKind.Storm).CooldownTicks)
                cooldown = $"{catalog.Get(ReactionKind.Steam).CooldownTicks} ticks";
            return "\n" + pairs + "\nПосле нелетального direct по паре; CD врага: " + cooldown;
        }

        private static string Seconds(int ticks) => ((float)ticks / SimulationTiming.TicksPerSecond).ToString("0.##", CultureInfo.InvariantCulture);

        public static void ApplyVisual(Transform view, TowerDefinition definition, int level)
        {
            var model = view.GetComponent<FlameModelBinding>();
            if (model != null && model.Apply(definition.Id, level)) return;
            // PropertyBlock не создаёт копии material assets; footprint сохраняется при Upgrade.
            var color = new Color(0.95f, 0.7f, 0.2f);
            switch (definition.StatusEffect?.Kind)
            {
                case StatusKind.Burn: color = new Color(1f, 0.36f, 0.16f); break;
                case StatusKind.Wet: color = new Color(0.2f, 0.72f, 1f); break;
                case StatusKind.Gust: color = new Color(0.45f, 0.95f, 0.7f); break;
                case StatusKind.Crack: color = new Color(0.7f, 0.58f, 0.86f); break;
            }
            if (definition.IntrinsicReaction != null)
                color = definition.IntrinsicReaction.Kind == ReactionKind.Steam ? EnemyStatusView.SteamColor : EnemyStatusView.StormColor;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color * (1f + (level - 1) * 0.12f));
            foreach (var renderer in view.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(block);
        }
    }
}
