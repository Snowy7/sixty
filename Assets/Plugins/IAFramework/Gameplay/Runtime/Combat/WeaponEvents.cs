using Ia.Gameplay.Actors;
using UnityEngine;

namespace Ia.Core.Events
{
    public struct WeaponFiredEvent
    {
        public IaActor Shooter;
        public Vector3 MuzzlePos;
        public Vector3 EndPos;
        public bool DidHit;

        public WeaponFiredEvent(IaActor shooter, Vector3 muzzlePos, Vector3 endPos, bool didHit)
        {
            Shooter = shooter;
            MuzzlePos = muzzlePos;
            EndPos = endPos;
            DidHit = didHit;
        }
    }

    public struct WeaponReloadStartedEvent
    {
        public IaActor Shooter;

        public WeaponReloadStartedEvent(IaActor shooter)
        {
            Shooter = shooter;
        }
    }

    public struct WeaponReloadFinishedEvent
    {
        public IaActor Shooter;

        public WeaponReloadFinishedEvent(IaActor shooter)
        {
            Shooter = shooter;
        }
    }
}