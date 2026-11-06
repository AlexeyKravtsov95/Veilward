namespace Veilward.Domain
{
    public readonly struct AttackEvent
    {
        public AttackEvent(
            TowerId attackerTowerId,
            EnemyId targetEnemyId,
            PathPosition targetPosition,
            long tick,
            int damage,
            bool wasLethal)
        {
            AttackerTowerId = attackerTowerId;
            TargetEnemyId = targetEnemyId;
            TargetPosition = targetPosition;
            Tick = tick;
            Damage = damage;
            WasLethal = wasLethal;
        }

        public TowerId AttackerTowerId { get; }
        public EnemyId TargetEnemyId { get; }
        public PathPosition TargetPosition { get; }
        public long Tick { get; }
        public int Damage { get; }
        public bool WasLethal { get; }
    }
}
