using UnityEngine;
using Ia.Gameplay.Actors;
using Ia.Core.Debugging;
using Ia.Core.Update; // Assuming this exists based on your snippet

namespace Ia.Systems.Interaction
{
    [DisallowMultipleComponent]
    public class IaDoorInteractable : IaBehaviour, IInteractable
    {
        [Header("Door Settings")]
        [SerializeField] private string interactionLabel = "Open";
        [SerializeField] private Transform pivot;
        
        [Tooltip("Axis: Positive Z is Forward. Door rotates around Y.")]
        [SerializeField] private Vector3 doorSize = new Vector3(0.1f, 2f, 1f); 
        
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float rotationSpeed = 90f; // Degrees per second
        [SerializeField] private bool startOpen;

        [Header("Safety Settings")]
        [SerializeField] private LayerMask obstructionMask; // Set this to Player Layer
        [SerializeField] private float obstructionCheckPadding = 0.05f; // Extra buffer so it stops slightly before touching

        private bool m_isOpen;
        private float m_targetAngle;
        private float m_currentAngle;
        
        // This is the offset from the pivot to the center of the door geometry
        private Vector3 m_centerOffset;

        public Transform Transform => transform;
        public int ExtraDistanceForInteraction => 0;
        public string InteractionLabel => m_isOpen ? "Close" : interactionLabel;

        protected override void Awake()
        {
            base.Awake();

            if (pivot == null) pivot = transform;

            // Assuming the pivot is at the edge of the door, 
            // the center is half the length forward and half the height up.
            // Adjust this if your pivot is in the center of the door mesh.
            m_centerOffset = new Vector3(0, doorSize.y / 2f, doorSize.z / 2f);

            m_isOpen = startOpen;
            m_currentAngle = m_isOpen ? openAngle : 0f;
            m_targetAngle = m_currentAngle;
            
            ApplyRotation();
        }

        private void Update()
        {
            if (Mathf.Approximately(m_currentAngle, m_targetAngle))
                return;

            MoveDoor(Time.deltaTime);
        }

        private void MoveDoor(float dt)
        {
            // Calculate where we WANT to be this frame
            float step = rotationSpeed * dt;
            float nextAngle = Mathf.MoveTowards(m_currentAngle, m_targetAngle, step);

            // 1. Calculate the Rotation and Position of the door at the "Next Angle"
            // Important: We must account for the Pivot's parent rotation + the local door rotation
            Quaternion currentPivotRot = pivot.parent != null ? pivot.parent.rotation : Quaternion.identity;
            // add 90 degrees because the door's "closed" position is along the local X axis (assuming forward is Z)
            currentPivotRot *= Quaternion.Euler(0f, 90f, 0f);
            
            Quaternion nextLocalRot = Quaternion.Euler(0f, nextAngle, 0f);
            Quaternion nextWorldRot = currentPivotRot * nextLocalRot; // The actual rotation in world space

            // 2. Find the center point of the door box in World Space at the new angle
            // We take the local offset (center of door) and rotate it by the Next Rotation
            Vector3 nextCenterPos = pivot.position + (nextWorldRot * m_centerOffset);

            // 3. Check for collisions
            // We use the doorSize minus a tiny bit for the box check, 
            // or plus padding if you want it to stop early.
            Vector3 checkSize = (doorSize * 0.5f) + (Vector3.one * obstructionCheckPadding);

            Collider[] results = new Collider[5]; // Buffer for results
            var size = Physics.OverlapBoxNonAlloc(nextCenterPos, checkSize, results, nextWorldRot, obstructionMask, QueryTriggerInteraction.Ignore);

            if (size > 0)
            {
                // OBSTRUCTION DETECTED
                // We do NOT update m_currentAngle. 
                // We do NOT change m_targetAngle (so it resumes automatically when player moves).
                
                #if UNITY_EDITOR
                IaLogger.Verbose(IaLogCategory.World, $"Door obstructed by {results[0].name}. Waiting...", this);
                DrawDebugBox(nextCenterPos, checkSize, nextWorldRot, Color.red);
                #endif
            }
            else
            {
                // PATH CLEAR
                m_currentAngle = nextAngle;
                ApplyRotation();
                
                #if UNITY_EDITOR
                DrawDebugBox(nextCenterPos, checkSize, nextWorldRot, Color.green);
                #endif
            }
        }

        private void ApplyRotation()
        {
            Vector3 euler = pivot.localEulerAngles;
            euler.y = m_currentAngle;
            pivot.localEulerAngles = euler;
        }

        public bool CanInteract(IaActor actor) => true;

        public void Interact(IaActor actor)
        {
            m_isOpen = !m_isOpen;
            m_targetAngle = m_isOpen ? openAngle : 0f;
        }

        public void Hover(IaActor actor)
        {
           
        }

        public void Unhover(IaActor actor)
        {
            
        }

        // --- Visualization ---

        private void OnDrawGizmos()
        {
            if (pivot == null) pivot = transform;
            
            // 1. Calculate the final world rotation of the door.
            // This combines the pivot's world rotation with the door's current swing angle.
            Quaternion doorWorldRotation = pivot.rotation * Quaternion.Euler(0f, m_currentAngle, 0f);
            // apply 90 degrees because the door's "closed" position is along the local X axis (assuming forward is Z)
            // based on the current angle we remove the current angle from the 90 degree offset
            doorWorldRotation *= Quaternion.Euler(0f, 90f - m_currentAngle, 0f);
            
            
            Gizmos.matrix = Matrix4x4.TRS(pivot.position, doorWorldRotation, Vector3.one);
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            
            // The center must be calculated based on the door's local space size *after* the pivot.
            // We assume the pivot is at the bottom center of the hinge edge (0, 0, 0)
            Vector3 center = new Vector3(0, doorSize.y / 2f, doorSize.z / 2f);
            
            // Draw the volume based on doorSize
            Gizmos.DrawCube(center, doorSize);
            Gizmos.DrawWireCube(center, doorSize);
            
            // Reset matrix
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void DrawDebugBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, Color color)
        {
            // Helper to visualize the OverlapBox in Scene view during play
            // Note: DrawLine is expensive, remove this in production or wrap in UNITY_EDITOR
            Color oldColor = Debug.unityLogger.logEnabled ? color : color; // Dummy usage
            // (You can use Debug.DrawLine here to reconstruct the box if you want extreme debug detail)
        }
    }
}