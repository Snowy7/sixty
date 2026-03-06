using UnityEngine;
using Ia.Core.Update;

namespace Sixty.World
{
    public class SpinBob : IaBehaviour
    {
        [SerializeField] private float spinSpeed = 90f;
        [SerializeField] private float bobHeight = 0.25f;
        [SerializeField] private float bobFrequency = 2f;

        private Vector3 basePosition;
        private float bobTimer;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaStart()
        {
            basePosition = transform.position;
        }

        public override void OnIaUpdate(float deltaTime)
        {
            transform.Rotate(0f, spinSpeed * deltaTime, 0f, Space.World);

            bobTimer += deltaTime;
            float bobOffset = Mathf.Sin(bobTimer * bobFrequency) * bobHeight;
            transform.position = new Vector3(basePosition.x, basePosition.y + bobOffset, basePosition.z);
        }
    }
}
