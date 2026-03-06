using Ia.Gameplay.Actors;
using Ia.Gameplay.Combat;

namespace Ia.Core.Events
{
    public struct ActorDamagedEvent
    {
        public IaActor target;
        public IaDamageInfo damageInfo;

        public ActorDamagedEvent(IaActor target, IaDamageInfo info)
        {
            this.target = target;
            damageInfo = info;
        }
    }
}