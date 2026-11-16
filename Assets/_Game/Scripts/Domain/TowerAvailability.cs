using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    public sealed class TowerAvailability
    {
        internal TowerAvailability(PlaceTowerFailureReason reason, IReadOnlyList<ElementKind> missingElements = null)
        {
            FailureReason = reason;
            var copy = new ElementKind[missingElements?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++) copy[index] = missingElements[index];
            MissingElements = Array.AsReadOnly(copy);
        }
        public bool CanPlace => FailureReason == PlaceTowerFailureReason.None;
        public PlaceTowerFailureReason FailureReason { get; }
        public IReadOnlyList<ElementKind> MissingElements { get; }
    }
}
