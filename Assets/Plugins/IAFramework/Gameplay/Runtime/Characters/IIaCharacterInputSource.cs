namespace Ia.Gameplay.Characters
{
    /// <summary>
    /// Interface for any system that produces IaCharacterInput.
    /// Player input or AI logic can implement this.
    /// </summary>
    public interface IIaCharacterInputSource
    {
        IaCharacterInput GetInput();
    }
}