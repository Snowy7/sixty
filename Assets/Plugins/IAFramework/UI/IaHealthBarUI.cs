using UnityEngine;
using UnityEngine.UI;
using Ia.Gameplay.Actors;

namespace Ia.UI
{
    [DisallowMultipleComponent]
    public class IaHealthBarUI : MonoBehaviour
    {
        [SerializeField] IaActor actor;
        [SerializeField] Slider slider; // or Image fill

        void Awake()
        {
            if (actor == null)
                actor = FindAnyObjectByType<IaActor>();

            if (slider == null)
                slider = GetComponentInChildren<Slider>();

            if (actor != null && slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = actor.MaxHealth;
                slider.value = actor.CurrentHealth;
            }
        }

        void OnEnable()
        {
            if (actor != null)
                actor.HealthChanged += OnHealthChanged;
        }

        void OnDisable()
        {
            if (actor != null)
                actor.HealthChanged -= OnHealthChanged;
        }

        void OnHealthChanged(IaActor actorRef, float oldValue, float newValue)
        {
            if (slider == null)
                return;

            slider.maxValue = actorRef.MaxHealth;
            slider.value = newValue;
        }
    }
}