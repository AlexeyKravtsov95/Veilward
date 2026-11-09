using System;

namespace Veilward.Domain
{
    public sealed class TowerOperationResult
    {
        private TowerOperationResult(
            bool success,
            TowerOperationFailureReason failureReason,
            TowerState tower,
            int refund)
        {
            Success = success;
            FailureReason = failureReason;
            Tower = tower;
            Refund = refund;
        }

        public bool Success { get; }
        public TowerOperationFailureReason FailureReason { get; }
        public TowerState Tower { get; }
        public int Refund { get; }

        public static TowerOperationResult Succeeded(TowerState tower, int refund = 0)
        {
            if (tower == null)
            {
                throw new ArgumentNullException(nameof(tower));
            }

            return new TowerOperationResult(true, TowerOperationFailureReason.None, tower, refund);
        }

        public static TowerOperationResult Failed(TowerOperationFailureReason reason)
        {
            if (reason == TowerOperationFailureReason.None)
            {
                throw new ArgumentException("A failed tower operation requires a reason.", nameof(reason));
            }

            return new TowerOperationResult(false, reason, null, 0);
        }
    }
}
