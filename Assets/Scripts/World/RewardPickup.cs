using System;
using Ia.Core.Update;
using Sixty.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sixty.World
{
    [RequireComponent(typeof(Collider))]
    public class RewardPickup : IaBehaviour
    {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private float interactGraceSeconds = 0.35f;
        [SerializeField] private bool requireFreshPress = true;

        private Action<RewardPickup> onCollected;
        private bool playerInRange;
        private bool collected;
        private float canCollectAt;
        private bool hasObservedReleasedInteract;

        public string Label { get; private set; } = string.Empty;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        protected override void OnIaAwake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(true);
            }

            canCollectAt = Time.unscaledTime + Mathf.Max(0f, interactGraceSeconds);
            hasObservedReleasedInteract = !IsInteractHeld();
        }

        public void Configure(string label, Color color, Action<RewardPickup> collectedCallback)
        {
            Label = label ?? string.Empty;
            onCollected = collectedCallback;

            if (labelText != null)
            {
                labelText.text = $"{Label}\nPress [E] / [A]";
                labelText.color = Color.white;
            }

            if (targetRenderers == null)
            {
                return;
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material material = renderer.material;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", color);
                }
            }
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (collected || !playerInRange || Time.unscaledTime < canCollectAt)
            {
                return;
            }

            if (requireFreshPress && !hasObservedReleasedInteract)
            {
                if (!IsInteractHeld())
                {
                    hasObservedReleasedInteract = true;
                }

                return;
            }

            bool interactPressedThisFrame = IsInteractPressedThisFrame();
            if (!interactPressedThisFrame)
            {
                return;
            }

            collected = true;
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.enabled = false;
            }

            onCollected?.Invoke(this);
        }

        private static bool IsInteractPressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            bool keyboardInteract = keyboard != null && keyboard.eKey.wasPressedThisFrame;
            Gamepad gamepad = Gamepad.current;
            bool gamepadInteract = gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
            return keyboardInteract || gamepadInteract;
        }

        private static bool IsInteractHeld()
        {
            Keyboard keyboard = Keyboard.current;
            bool keyboardInteract = keyboard != null && keyboard.eKey.isPressed;
            Gamepad gamepad = Gamepad.current;
            bool gamepadInteract = gamepad != null && gamepad.buttonSouth.isPressed;
            return keyboardInteract || gamepadInteract;
        }

        private void OnTriggerEnter(Collider other)
        {
            Transform otherRoot = other.transform.root;
            if (otherRoot == null)
            {
                return;
            }

            PlayerController player = otherRoot.GetComponent<PlayerController>();
            if (player == null)
            {
                return;
            }

            playerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            Transform otherRoot = other.transform.root;
            if (otherRoot == null)
            {
                return;
            }

            PlayerController player = otherRoot.GetComponent<PlayerController>();
            if (player == null)
            {
                return;
            }

            playerInRange = false;
        }
    }
}
