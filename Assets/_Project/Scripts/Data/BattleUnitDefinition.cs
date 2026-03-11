using System;
using ProjectH.Battle;
using UnityEngine;

namespace ProjectH.Data
{
    [Serializable]
    public struct BattleUnitDefinition
    {
        public string mercenaryId;
        public string templateId;
        public string talentTag;
        public string displayName;
        public BattleStatBlock statBlock;
        public string prefabResourcePath;
        public float spawnX;
        public float spawnY;
    }
}
