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
            IReadOnlyList<TowerDefinition> levels,
            IReadOnlyList<ElementKind> requiredElements = null)
            : this(id, showInCatalog, levels, requireThreeLevels: true, requiredElements)
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
            bool requireThreeLevels,
            IReadOnlyList<ElementKind> requiredElements = null)
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

                if (requireThreeLevels && (id.Value == "steam" || id.Value == "storm"))
                {
                    var requiredKind = id.Value == "steam" ? ReactionKind.Steam : ReactionKind.Storm;
                    if (level.StatusEffect != null || level.IntrinsicReaction == null ||
                        level.IntrinsicReaction.Kind != requiredKind)
                    {
                        throw new ArgumentException(
                            $"Hybrid archetype {id} requires a matching intrinsic reaction and no status on every level.",
                            nameof(levels));
                    }
                }

                copiedLevels[index] = level;
            }

            var elements = new ElementKind[requiredElements?.Count ?? 0];
            var uniqueElements = new HashSet<ElementKind>();
            for (var index = 0; index < elements.Length; index++)
            {
                var element = requiredElements[index];
                if (!Enum.IsDefined(typeof(ElementKind), element) || !uniqueElements.Add(element))
                    throw new ArgumentException("Required elements must be known and unique.", nameof(requiredElements));
                elements[index] = element;
            }
            Array.Sort(elements);
            RequiredElements = Array.AsReadOnly(elements);
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
        public IReadOnlyList<ElementKind> RequiredElements { get; }
        public bool ShowInCatalog { get; }

        internal static IReadOnlyList<ElementKind> NativeRequirements(TowerDefinitionId id)
        {
            switch (id.Value)
            {
                case "flame": return Array.AsReadOnly(new[] { ElementKind.Flame });
                case "tide": return Array.AsReadOnly(new[] { ElementKind.Tide });
                case "gale": return Array.AsReadOnly(new[] { ElementKind.Gale });
                case "stone": return Array.AsReadOnly(new[] { ElementKind.Stone });
                case "steam": return Array.AsReadOnly(new[] { ElementKind.Flame, ElementKind.Tide });
                case "storm": return Array.AsReadOnly(new[] { ElementKind.Tide, ElementKind.Gale });
                default: throw new ArgumentException("Unknown native tower archetype.", nameof(id));
            }
        }

        internal void ValidateNativeRequirements()
        {
            if (Id.Value == "test-tower") return;
            var expected = NativeRequirements(Id);
            if (RequiredElements.Count != expected.Count)
                throw new ArgumentException("Native tower requirements do not match the explicit element table.");
            for (var index = 0; index < expected.Count; index++)
                if (RequiredElements[index] != expected[index])
                    throw new ArgumentException("Native tower requirements do not match the explicit element table.");
        }
        public IReadOnlyList<TowerDefinition> Levels { get; }
    }
}
