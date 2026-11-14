using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    public sealed class TowerCatalog
    {
        private readonly Dictionary<TowerDefinitionId, TowerArchetypeDefinition> _byId;

        public TowerCatalog(IReadOnlyList<TowerArchetypeDefinition> archetypes)
        {
            if (archetypes == null)
            {
                throw new ArgumentNullException(nameof(archetypes));
            }

            if (archetypes.Count == 0)
            {
                throw new ArgumentException("At least one tower archetype is required.", nameof(archetypes));
            }

            var copied = new TowerArchetypeDefinition[archetypes.Count];
            _byId = new Dictionary<TowerDefinitionId, TowerArchetypeDefinition>();
            for (var index = 0; index < archetypes.Count; index++)
            {
                var archetype = archetypes[index] ?? throw new ArgumentException(
                    "Tower catalog cannot contain null archetypes.", nameof(archetypes));
                if (_byId.ContainsKey(archetype.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate tower archetype ID: {archetype.Id}.", nameof(archetypes));
                }

                _byId.Add(archetype.Id, archetype);
                copied[index] = archetype;
            }

            Archetypes = Array.AsReadOnly(copied);
        }

        public IReadOnlyList<TowerArchetypeDefinition> Archetypes { get; }

        public TowerArchetypeDefinition Get(TowerDefinitionId id)
        {
            return _byId.TryGetValue(id, out var archetype)
                ? archetype
                : throw new KeyNotFoundException(id.Value);
        }

        public bool TryGet(TowerDefinitionId id, out TowerArchetypeDefinition archetype)
        {
            return _byId.TryGetValue(id, out archetype);
        }
    }
}
