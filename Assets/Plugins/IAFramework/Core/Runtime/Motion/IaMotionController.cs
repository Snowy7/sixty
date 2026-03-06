using UnityEngine;
using Ia.Core.Update;

namespace Ia.Core.Motion
{
    /// <summary>
    /// Controls a motion target and a set of motion layers.
    /// Resets accumulation and applies the final transform each frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class IaMotionController : IaBehaviour
    {
        [SerializeField] IaMotionTarget motionTarget;
        [SerializeField] IaMotionLayer[] layers;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.LateUpdate;

        protected override void OnIaAwake()
        {
            if (motionTarget == null)
                motionTarget = GetComponent<IaMotionTarget>();
        }

        public override void OnIaLateUpdate(float deltaTime)
        {
            if (motionTarget == null)
                return;

            motionTarget.ResetAccumulated();

            if (layers != null)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    IaMotionLayer layer = layers[i];
                    if (layer == null)
                        continue;

                    // Each layer adds offsets to motionTarget
                    layer.OnTick(deltaTime);
                }
            }

            motionTarget.ApplyAccumulated();
        }

        public void SetLayers(IaMotionLayer[] newLayers)
        {
            layers = newLayers;
        }
    }
}