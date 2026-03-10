using System;

namespace ProjectH.Battle
{
    public sealed class BattleEventBus
    {
        public event Action<BattleEvent> OnPublished;

        public void Publish(BattleEvent battleEvent)
        {
            OnPublished?.Invoke(battleEvent);
        }
    }
}
