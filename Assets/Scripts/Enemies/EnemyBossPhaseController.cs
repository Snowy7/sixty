using Ia.Core.Update;
using Sixty.Combat;
using Sixty.Gameplay;
using UnityEngine;

namespace Sixty.Enemies
{
    [RequireComponent(typeof(Health))]
    public class EnemyBossPhaseController : IaBehaviour
    {
        [Header("Thresholds")]
        [SerializeField] [Range(0f, 1f)] private float phase2Threshold = 0.66f;
        [SerializeField] [Range(0f, 1f)] private float phase3Threshold = 0.33f;

        [Header("Phase Multipliers")]
        [SerializeField] private float phase1MoveMultiplier = 1f;
        [SerializeField] private float phase2MoveMultiplier = 1.2f;
        [SerializeField] private float phase3MoveMultiplier = 1.38f;
        [SerializeField] private float phase1FireRateMultiplier = 1f;
        [SerializeField] private float phase2FireRateMultiplier = 1.35f;
        [SerializeField] private float phase3FireRateMultiplier = 1.75f;

        [Header("Visual")]
        [SerializeField] private Color phase2FlashColor = new Color(1f, 0.6f, 0.25f, 1f);
        [SerializeField] private Color phase3FlashColor = new Color(1f, 0.25f, 0.18f, 1f);

        private Health health;
        private EnemyChaser chaser;
        private EnemyShooter shooter;
        private HitFlashSquash hitFlash;
        private int currentPhase = 1;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.AI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaAwake()
        {
            health = GetComponent<Health>();
            chaser = GetComponent<EnemyChaser>();
            shooter = GetComponent<EnemyShooter>();
            hitFlash = GetComponentInChildren<HitFlashSquash>();
        }

        protected override void OnIaStart()
        {
            ApplyPhase(1, true);
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (health == null || health.IsDead)
            {
                return;
            }

            float maxHealth = Mathf.Max(0.001f, health.MaxHealth);
            float normalized = Mathf.Clamp01(health.CurrentHealth / maxHealth);
            int targetPhase = normalized <= phase3Threshold ? 3 : (normalized <= phase2Threshold ? 2 : 1);

            if (targetPhase != currentPhase)
            {
                ApplyPhase(targetPhase, false);
            }
        }

        private void ApplyPhase(int phase, bool silent)
        {
            currentPhase = Mathf.Clamp(phase, 1, 3);

            if (chaser != null)
            {
                float moveMultiplier = currentPhase switch
                {
                    1 => phase1MoveMultiplier,
                    2 => phase2MoveMultiplier,
                    _ => phase3MoveMultiplier
                };
                chaser.SetExternalMoveSpeedMultiplier(moveMultiplier);
            }

            if (shooter != null)
            {
                float fireRateMultiplier = currentPhase switch
                {
                    1 => phase1FireRateMultiplier,
                    2 => phase2FireRateMultiplier,
                    _ => phase3FireRateMultiplier
                };
                shooter.SetExternalFireRateMultiplier(fireRateMultiplier);

                switch (currentPhase)
                {
                    case 1:
                        shooter.SetFireMode(EnemyShooter.FireMode.Single);
                        shooter.SetBurstSettings(1, 0.1f);
                        shooter.SetSpreadSettings(1, 0f);
                        break;

                    case 2:
                        shooter.SetFireMode(EnemyShooter.FireMode.Burst);
                        shooter.SetBurstSettings(3, 0.1f);
                        shooter.SetSpreadSettings(1, 0f);
                        break;

                    case 3:
                        shooter.SetFireMode(EnemyShooter.FireMode.BurstSpread);
                        shooter.SetBurstSettings(2, 0.14f);
                        shooter.SetSpreadSettings(5, 26f);
                        break;
                }
            }

            if (silent)
            {
                return;
            }

            if (hitFlash != null)
            {
                if (currentPhase == 2)
                {
                    hitFlash.SetFlashColor(phase2FlashColor);
                }
                else if (currentPhase == 3)
                {
                    hitFlash.SetFlashColor(phase3FlashColor);
                }

                hitFlash.PlayHitReaction(true);
            }

            GameFeelController.Instance?.OnBossPhaseShift(transform.position + (Vector3.up * 0.9f), currentPhase);
        }
    }
}
