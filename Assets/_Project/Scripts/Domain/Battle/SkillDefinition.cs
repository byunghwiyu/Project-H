namespace ProjectH.Battle
{
    public enum SkillKind
    {
        Passive,
        Active,
    }

    public readonly struct SkillDefinition
    {
        public SkillDefinition(string ownerId, string skillId, string skillName, SkillKind kind, string effectType, float value1)
        {
            OwnerId    = ownerId;
            SkillId    = skillId;
            SkillName  = skillName;
            Kind       = kind;
            EffectType = effectType;
            Value1     = value1;
        }

        public string    OwnerId    { get; }
        public string    SkillId    { get; }
        public string    SkillName  { get; }
        public SkillKind Kind       { get; }
        public string    EffectType { get; }
        public float     Value1     { get; }
    }
}
