using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    public sealed class TowerArchetypeDefinition
    {
        public const int RequiredLevelCount = 3;

        public TowerArchetypeDefinition(
            TowerDefinitionId id,
            bool showInCatalog,
            IReadOnlyList<TowerDefinition> levels)
            : this(id, showInCatalog, levels, requireThreeLevels: true)
        {
        }

        internal static TowerArchetypeDefinition CreateCompatibility(
            IReadOnlyList<TowerDefinition> levels)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            if (levels.Count == 0)
            {
                throw new ArgumentException("At least one compatibility tower level is required.", nameof(levels));
            }

            var first = levels[0] ?? throw new ArgumentException(
                "Compatibility tower levels cannot contain null entries.", nameof(levels));
            return new TowerArchetypeDefinition(first.Id, showInCatalog: true, levels, requireThreeLevels: false);
        }

        private TowerArchetypeDefinition(
            TowerDefinitionId id,
            bool showInCatalog,
            IReadOnlyList<TowerDefinition> levels,
            bool requireThreeLevels)
        {
            if (string.IsNullOrWhiteSpace(id.Value))
            {
                throw new ArgumentException("Archetype ID cannot be empty.", nameof(id));
            }

            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            if (requireThreeLevels && levels.Count != RequiredLevelCount)
            {
                throw new ArgumentException(
                    $"Archetype {id} must contain exactly {RequiredLevelCount} levels.", nameof(levels));
            }

            var copiedLevels = new TowerDefinition[levels.Count];
            for (var index = 0; index < levels.Count; index++)
            {
                var level = levels[index] ?? throw new ArgumentException(
                    "Archetype levels cannot contain null entries.", nameof(levels));
                if (level.Id != id)
                {
                    throw new ArgumentException(
                        "Every archetype level must use the archetype ID.", nameof(levels));
                }

                if (requireThreeLevels && IsElemental(id) && level.StatusEffect == null)
                {
                    throw new ArgumentException(
                        $"Elemental archetype {id} requires a status on every level.", nameof(levels));
                }

                copiedLevels[index] = level;
            }

            Id = id;
            ShowInCatalog = showInCatalog;
            Levels = Array.AsReadOnly(copiedLevels);
        }

        private static bool IsElemental(TowerDefinitionId id)
        {
            return id.Value == "flame" || id.Value == "tide" ||
                   id.Value == "gale" || id.Value == "stone";
        }

        public TowerDefinitionId Id { get; }
        public bool ShowInCatalog { get; }
        public IReadOnlyList<TowerDefinition> Levels { get; }
    }
}
