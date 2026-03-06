using UnityEngine;
using UnityEngine.UI;
using Ia.Core.Events;
using TMPro;

namespace Ia.UI
{
    [DisallowMultipleComponent]
    public class IaInteractionPromptUI : MonoBehaviour
    {
        [SerializeField] TMP_Text promptText;

        void Awake()
        {
            if (promptText == null)
                promptText = GetComponentInChildren<TMP_Text>();

            if (promptText != null)
                promptText.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            IaEventBus.Subscribe<InteractionFocusChangedEvent>(OnFocusChanged);
            IaEventBus.Subscribe<InteractionPromptUpdatedEvent>(OnPromptUpdated);
        }

        void OnDisable()
        {
            IaEventBus.Unsubscribe<InteractionFocusChangedEvent>(OnFocusChanged);
            IaEventBus.Unsubscribe<InteractionPromptUpdatedEvent>(OnPromptUpdated);
        }

        void OnFocusChanged(InteractionFocusChangedEvent evt)
        {
            if (promptText == null)
                return;

            if (evt.Interactable == null)
            {
                promptText.gameObject.SetActive(false);
            }
            else
            {
                promptText.text = $"[E] {evt.Interactable.InteractionLabel}";
                promptText.gameObject.SetActive(true);
            }
        }
        
        void OnPromptUpdated(InteractionPromptUpdatedEvent evt)
        {
            if (promptText == null || evt.Interactable == null)
                return;

            promptText.text = $"[E] {evt.Interactable.InteractionLabel}";
        }
    }
}