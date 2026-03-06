using UnityEngine;
using Ia.Core.Update;

namespace Ia.Samples
{
    public class IaTestRotator : IaBehaviour
    {
        [SerializeField] private float speed = 90f;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        
        
        public override void OnIaUpdate(float deltaTime)
        {
            transform.Rotate(Vector3.up, speed * deltaTime, Space.World);
        }
    }
}