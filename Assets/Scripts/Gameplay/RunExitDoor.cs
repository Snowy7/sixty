using System;
using Ia.Core.Update;
using Sixty.Player;
using UnityEngine;

namespace Sixty.Gameplay
{
    public enum RunExitDoorDirection
    {
        North = 0,
        South = 1,
        East = 2,
        West = 3
    }

    public class RunExitDoor : IaBehaviour
    {
        [SerializeField] private RunExitDoorDirection direction = RunExitDoorDirection.North;
        [SerializeField] private Transform doorVisual;
        [SerializeField] private Collider entryTrigger;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Color lockedColor = new Color(0.86f, 0.24f, 0.2f, 1f);
        [SerializeField] private Color unlockedColor = new Color(0.28f, 1f, 0.72f, 1f);
        [SerializeField] private float openHeight = 3.6f;
        [SerializeField] private float animationSpeed = 8.5f;

        private bool isUnlocked;
        private bool consumed;
        private Vector3 closedLocalPos;
        private Vector3 openLocalPos;
        private MaterialPropertyBlock propertyBlock;
        private bool initialized;

        public event Action<RunExitDoor, PlayerController> OnPlayerEntered;
        public RunExitDoorDirection Direction => direction;
        public bool IsUnlocked => isUnlocked;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaAwake()
        {
            EnsureInitialized();
            SetLocked(true, true);
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (doorVisual == null)
            {
                return;
            }

            Vector3 target = isUnlocked ? openLocalPos : closedLocalPos;
            doorVisual.localPosition = Vector3.Lerp(
                doorVisual.localPosition,
                target,
                Mathf.Clamp01(animationSpeed * deltaTime));
        }

        public void SetLocked(bool locked, bool instant = false)
        {
            EnsureInitialized();
            isUnlocked = !locked;
            consumed = false;
            UpdateDoorVisuals();

            if (instant && doorVisual != null)
            {
                doorVisual.localPosition = isUnlocked ? openLocalPos : closedLocalPos;
            }
        }

        public void SetRuntimeRefs(
            RunExitDoorDirection dir,
            Transform visual,
            Collider trigger,
            Renderer[] renderers,
            float height,
            float speed,
            Color locked,
            Color unlocked)
        {
            direction = dir;
            doorVisual = visual;
            entryTrigger = trigger;
            targetRenderers = renderers;
            openHeight = height;
            animationSpeed = speed;
            lockedColor = locked;
            unlockedColor = unlocked;
        }

        public void Open(bool instant = false)
        {
            SetLocked(false, instant);
        }

        public void Close(bool instant = false)
        {
            SetLocked(true, instant);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isUnlocked || consumed)
            {
                return;
            }

            Transform root = other.transform.root;
            if (root == null)
            {
                return;
            }

            PlayerController player = root.GetComponent<PlayerController>();
            if (player == null)
            {
                return;
            }

            consumed = true;
            OnPlayerEntered?.Invoke(this, player);
        }

        private void UpdateDoorVisuals()
        {
            EnsureInitialized();
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                return;
            }

            Color color = isUnlocked ? unlockedColor : lockedColor;
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (propertyBlock == null)
                {
                    propertyBlock = new MaterialPropertyBlock();
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_EmissionColor", color * (isUnlocked ? 2.8f : 1.4f));
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            if (doorVisual == null)
            {
                doorVisual = transform;
            }

            if (entryTrigger == null)
            {
                entryTrigger = GetComponent<Collider>();
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            closedLocalPos = doorVisual != null ? doorVisual.localPosition : Vector3.zero;
            openLocalPos = closedLocalPos + (Vector3.up * Mathf.Max(0.5f, openHeight));
            initialized = true;
        }
    }
}
