using UnityEngine;

namespace Ia.Core.Update
{
    /// <summary>
    /// Optional bootstrap that ensures IaUpdateManager exists early in the scene.
    /// </summary>
    public class IaBootstrap : MonoBehaviour
    {
        [Tooltip("If true, this object will be destroyed after ensuring the manager exists.")]
        [SerializeField]
        private bool destroyAfterInit = true;

        private void Awake()
        {
            _ = IaUpdateManager.Instance; // Force creation if missing

            if (destroyAfterInit)
            {
                Destroy(gameObject);
            }
        }
    }
}