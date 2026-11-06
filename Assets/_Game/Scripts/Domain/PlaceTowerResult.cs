using System;

namespace Veilward.Domain
{
    public sealed class PlaceTowerResult
    {
        private PlaceTowerResult(bool success, PlaceTowerFailureReason failureReason, TowerState tower)
        {
            Success = success;
            FailureReason = failureReason;
            Tower = tower;
        }

        public bool Success { get; }
        public PlaceTowerFailureReason FailureReason { get; }
        public TowerState Tower { get; }

        public static PlaceTowerResult Succeeded(TowerState tower)
        {
            if (tower == null)
            {
                throw new ArgumentNullException(nameof(tower));
            }

            return new PlaceTowerResult(true, PlaceTowerFailureReason.None, tower);
        }

        public static PlaceTowerResult Failed(PlaceTowerFailureReason reason)
        {
            if (reason == PlaceTowerFailureReason.None || !Enum.IsDefined(typeof(PlaceTowerFailureReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }

            return new PlaceTowerResult(false, reason, null);
        }
    }
}
