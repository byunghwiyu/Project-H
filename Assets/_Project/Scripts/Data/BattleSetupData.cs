using UnityEngine;

namespace ProjectH.Data
{
    [CreateAssetMenu(menuName = "ProjectH/Battle Setup Data", fileName = "BattleSetupData")]
    public sealed class BattleSetupData : ScriptableObject
    {
        public BattleUnitDefinition[] allies;
        public BattleUnitDefinition[] enemies;
    }
}
