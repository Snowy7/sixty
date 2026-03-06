using Ia.Gameplay.Actors;

namespace Ia.Core.Events
{
    public struct ActorDiedEvent
    {
        public IaActor Actor;

        public ActorDiedEvent(IaActor actor)
        {
            Actor = actor;
        }
    }
}