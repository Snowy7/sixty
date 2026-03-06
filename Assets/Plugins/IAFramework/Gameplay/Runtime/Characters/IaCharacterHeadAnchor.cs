using UnityEngine;

namespace Ia.Gameplay.Characters
{
    /// <summary>
    /// Keeps a head/camera anchor at the correct height based on CharacterController.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class IaCharacterHeadAnchor : MonoBehaviour
    {
        [SerializeField] private CharacterController controller;
        [SerializeField] private float heightOffset = 0f;
        [SerializeField] private bool alignInLocalSpace = true;

        private void OnValidate()
        {
            if (controller == null)
                controller = GetComponentInParent<CharacterController>();
        }

        private void LateUpdate()
        {
            if (controller == null)
                return;

            float targetY = (controller.height / 2) + heightOffset;

            if (alignInLocalSpace)
            {
                Vector3 local = transform.localPosition;
                local.y = targetY;
                transform.localPosition = local;
            }
            else
            {
                Vector3 pos = transform.position;
                pos.y = controller.transform.position.y + targetY;
                transform.position = pos;
            }
        }
    }
}