using System;

namespace Ia.Core.Update
{
    [Flags]
    public enum IaUpdatePhase
    {
        None = 0,
        Update = 1 << 0,
        LateUpdate = 1 << 1,
        FixedUpdate = 1 << 2,
        All = Update | LateUpdate | FixedUpdate
    }

    public enum IaUpdateGroup
    {
        Player = 0,
        AI = 1,
        World = 2,
        UI = 3,
        FX = 4,
        Custom1 = 5,
        Custom2 = 6
    }

    internal enum IaLifecycleEvent
    {
        Awake,
        Enable,
        Start,
        Disable,
        Destroy
    }
}
