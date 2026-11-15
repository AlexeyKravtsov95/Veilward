using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    public sealed class ReactionCatalog
    {
        private readonly Dictionary<ReactionKind, ReactionDefinition> _byKind;

        public static ReactionCatalog Empty { get; } = new ReactionCatalog(Array.Empty<ReactionDefinition>());

        public ReactionCatalog(IReadOnlyList<ReactionDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var copied = new ReactionDefinition[definitions.Count];
            _byKind = new Dictionary<ReactionKind, ReactionDefinition>();
            var priorities = new HashSet<int>();
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index] ?? throw new ArgumentException(
                    "Reaction catalog cannot contain null definitions.", nameof(definitions));
                if (_byKind.ContainsKey(definition.Payload.Kind))
                {
                    throw new ArgumentException(
                        $"Duplicate reaction kind: {definition.Payload.Kind}.", nameof(definitions));
                }

                if (!priorities.Add(definition.Priority))
                {
                    throw new ArgumentException(
                        $"Duplicate reaction priority: {definition.Priority}.", nameof(definitions));
                }

                _byKind.Add(definition.Payload.Kind, definition);
                copied[index] = definition;
            }

            Definitions = Array.AsReadOnly(copied);
        }

        public IReadOnlyList<ReactionDefinition> Definitions { get; }

        public ReactionDefinition Get(ReactionKind kind)
        {
            return _byKind.TryGetValue(kind, out var definition)
                ? definition
                : throw new KeyNotFoundException(kind.ToString());
        }

        public bool TryGet(ReactionKind kind, out ReactionDefinition definition)
        {
            return _byKind.TryGetValue(kind, out definition);
        }
    }
}
