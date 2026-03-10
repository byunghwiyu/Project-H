namespace ProjectH.Battle
{
    public enum BattleEventType
    {
        Spawned,
        TurnStarted,
        Damaged,
        Died,
        ActiveSkillUsed,
        Healed,
        Evaded,
        CounterAttacked,
        ThornsReflected,
    }

    public readonly struct BattleEvent
    {
        public BattleEvent(BattleEventType type, string sourceRuntimeId, string targetRuntimeId, int value)
        {
            Type = type;
            SourceRuntimeId = sourceRuntimeId;
            TargetRuntimeId = targetRuntimeId;
            Value = value;
        }

        public BattleEventType Type { get; }
        public string SourceRuntimeId { get; }
        public string TargetRuntimeId { get; }
        public int Value { get; }
    }
}
