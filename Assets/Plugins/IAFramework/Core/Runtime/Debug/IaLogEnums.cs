namespace Ia.Core.Debugging
{
    public enum IaLogCategory
    {
        Core,
        Update,
        Gameplay,
        AI,
        Combat,
        UI,
        World,
        Audio,
        Network,
        Custom1,
        Custom2
    }

    public enum IaLogLevel
    {
        Error = 0,
        Warning = 1,
        Info = 2,
        Verbose = 3
    }
}