using System.Collections;
using System.Collections.Generic;
using Ia.Core.Events;
using Ia.Core.Update;
using Sixty.CameraSystem;
using Sixty.Core;
using Sixty.UI;
using UnityEngine;

namespace Sixty.Gameplay
{
    public class GameFeelController : IaBehaviour
    {
        [Header("References")]
        [SerializeField] private TopDownCameraFollow cameraFollow;
        [SerializeField] private ScreenFlashOverlay screenFlashOverlay;
        [SerializeField] private PostProcessFeedback postProcessFeedback;
        [SerializeField] private AudioSource sfxSource;

        [Header("Camera Shake")]
        [SerializeField] private float shootShake = 0.018f;
        [SerializeField] private float shootShakeCooldown = 0.05f;
        [SerializeField] private float enemyHitShake = 0.095f;
        [SerializeField] private float enemyKillShake = 0.18f;
        [SerializeField] private float playerDamageShake = 0.2f;
        [SerializeField] private float dashShake = 0.06f;
        [SerializeField] private float pickupShake = 0.045f;
        [SerializeField] private float roomClearShake = 0.11f;
        [SerializeField] private float bossPhaseShiftShake = 0.16f;

        [Header("Hit Stop")]
        [SerializeField] private bool enableHitStop = true;
        [SerializeField] private float hitStopOnHitSeconds = 0.018f;
        [SerializeField] private float hitStopOnKillSeconds = 0.04f;
        [SerializeField] private float hitStopOnPlayerDamageSeconds = 0.032f;

        [Header("Particle Colors")]
        [SerializeField] private Color shootColor = new Color(0.3f, 0.92f, 1f, 1f);
        [SerializeField] private Color enemyHitColor = new Color(1f, 0.55f, 0.15f, 1f);
        [SerializeField] private Color enemyKillColor = new Color(1f, 0.7f, 0.1f, 1f);
        [SerializeField] private Color playerDamageColor = new Color(1f, 0.2f, 0.12f, 1f);
        [SerializeField] private Color dashColor = new Color(0.2f, 0.88f, 1f, 1f);
        [SerializeField] private Color pickupColor = new Color(1f, 0.82f, 0.15f, 1f);
        [SerializeField] private Color roomClearColor = new Color(0.5f, 1f, 0.6f, 1f);

        [Header("Burst Pool")]
        [SerializeField] private int prewarmedBursts = 56;
        [SerializeField] private int maxBurstPoolSize = 96;
        
        [Header("Runtime Performance")]
        [SerializeField] private bool optimizeArenaSurfaces = true;
        [SerializeField] private string[] optimizedSurfaceObjectNames = { "BaseFloor", "ArenaPlate" };

        [Header("Audio")]
        [SerializeField] private AudioClip shootSfx;
        [SerializeField] private AudioClip enemyHitSfx;
        [SerializeField] private AudioClip enemyKillSfx;
        [SerializeField] private AudioClip playerHitSfx;
        [SerializeField] private AudioClip dashSfx;
        [SerializeField] private AudioClip pickupSfx;
        [SerializeField] private AudioClip roomClearSfx;
        [SerializeField] private AudioClip runWinSfx;

        [Header("Audio Pitch")]
        [SerializeField] private Vector2 shootPitchRange = new Vector2(0.96f, 1.06f);
        [SerializeField] private Vector2 enemyHitPitchRange = new Vector2(0.9f, 1.08f);
        [SerializeField] private Vector2 enemyKillPitchRange = new Vector2(0.8f, 0.95f);
        [SerializeField] private Vector2 playerHitPitchRange = new Vector2(0.8f, 0.95f);
        [SerializeField] private Vector2 dashPitchRange = new Vector2(0.95f, 1.05f);
        [SerializeField] private Vector2 pickupPitchRange = new Vector2(0.95f, 1.15f);
        [SerializeField] private Vector2 roomClearPitchRange = new Vector2(0.92f, 1.04f);
        [SerializeField] private Vector2 runWinPitchRange = new Vector2(0.9f, 1f);

        [Header("Audio Volume")]
        [SerializeField] [Range(0f, 1f)] private float shootVolume = 0.33f;
        [SerializeField] [Range(0f, 1f)] private float enemyHitVolume = 0.45f;
        [SerializeField] [Range(0f, 1f)] private float enemyKillVolume = 0.62f;
        [SerializeField] [Range(0f, 1f)] private float playerHitVolume = 0.6f;
        [SerializeField] [Range(0f, 1f)] private float dashVolume = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float pickupVolume = 0.55f;
        [SerializeField] [Range(0f, 1f)] private float roomClearVolume = 0.75f;
        [SerializeField] [Range(0f, 1f)] private float runWinVolume = 0.85f;

        public static GameFeelController Instance { get; private set; }

        private struct ActiveBurst
        {
            public ParticleSystem system;
            public float recycleAt;
        }

        private readonly List<ParticleSystem> burstPool = new List<ParticleSystem>(96);
        private readonly List<ActiveBurst> activeBursts = new List<ActiveBurst>(96);
        private Coroutine hitStopRoutine;
        private float defaultFixedDelta;
        private float lowTimePulseCooldown;
        private float nextShootShakeAt;
        private int burstRoundRobinCursor;
        private static Material cachedFxMaterial;
        private AudioClip fallbackShootSfx;
        private AudioClip fallbackEnemyHitSfx;
        private AudioClip fallbackEnemyKillSfx;
        private AudioClip fallbackPlayerHitSfx;
        private AudioClip fallbackDashSfx;
        private AudioClip fallbackPickupSfx;
        private AudioClip fallbackRoomClearSfx;
        private AudioClip fallbackRunWinSfx;
        private readonly ParticleSystem.Burst[] reusableBurst = new ParticleSystem.Burst[1];
        private static readonly Gradient SharedAlphaFadeGradient = CreateSharedAlphaFadeGradient();
        private static readonly Dictionary<int, Material> RuntimeSurfaceMaterialCache = new Dictionary<int, Material>(8);
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.FX;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaAwake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            defaultFixedDelta = Mathf.Max(0.005f, Time.fixedDeltaTime);
            if (defaultFixedDelta < 0.02f)
            {
                defaultFixedDelta = 0.02f;
            }
            EnsureAudioSource();
            GenerateFallbackClips();
            PrewarmBurstPool();
            OptimizeArenaSurfaceRendering();
        }

        protected override void OnIaEnable()
        {
            BindRuntimeReferences();
            IaEventBus.Subscribe<RoomClearedEvent>(HandleRoomClearedEvent);
            IaEventBus.Subscribe<RunWonEvent>(HandleRunWonEvent);
            IaEventBus.Subscribe<TimeChangedEvent>(HandleTimeChangedEvent);
        }

        protected override void OnIaDisable()
        {
            RestoreTimescaleState();
            IaEventBus.Unsubscribe<RoomClearedEvent>(HandleRoomClearedEvent);
            IaEventBus.Unsubscribe<RunWonEvent>(HandleRunWonEvent);
            IaEventBus.Unsubscribe<TimeChangedEvent>(HandleTimeChangedEvent);
            UnbindRuntimeReferences();
        }

        protected override void OnIaDestroy()
        {
            RestoreTimescaleState();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (cameraFollow == null || screenFlashOverlay == null || postProcessFeedback == null)
            {
                BindRuntimeReferences();
            }

            if (lowTimePulseCooldown > 0f)
            {
                lowTimePulseCooldown -= Time.unscaledDeltaTime;
            }

            TickBurstPool();
        }

        public void OnPlayerShot(Vector3 origin, Vector3 direction, Transform playerRoot = null)
        {
            if (Time.unscaledTime >= nextShootShakeAt)
            {
                AddShake(shootShake);
                nextShootShakeAt = Time.unscaledTime + shootShakeCooldown;
            }

            Vector3 normalized = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            SpawnBurst(origin + (normalized * 0.36f), normalized, shootColor, 18, 12f, 17f, 0.055f, 0.12f);
            SpawnBurst(origin, normalized, shootColor, 10, 7f, 11f, 0.075f, 0.15f);
            SpawnBurst(origin - (normalized * 0.16f), -normalized, shootColor, 7, 4f, 8f, 0.045f, 0.1f);
            TryPlayRecoil(playerRoot);
            PlaySfx(SfxEvent.Shoot);
            postProcessFeedback?.OnShot();
        }

        public void OnEnemyHit(Vector3 hitPosition, bool killed, Transform hitTarget = null)
        {
            AddShake(killed ? enemyKillShake : enemyHitShake);

            SpawnBurst(
                hitPosition,
                Vector3.up,
                killed ? enemyKillColor : enemyHitColor,
                killed ? 44 : 24,
                killed ? 11f : 7f,
                killed ? 19f : 12f,
                0.085f,
                killed ? 0.36f : 0.22f);

            SpawnBurst(hitPosition, Vector3.up, Color.white, killed ? 22 : 10, 4f, 10f, 0.045f, 0.13f);
            TryPlayHitReaction(hitTarget, killed);
            RequestHitStop(killed ? hitStopOnKillSeconds : hitStopOnHitSeconds);
            PlaySfx(killed ? SfxEvent.EnemyKill : SfxEvent.EnemyHit);
            postProcessFeedback?.OnEnemyHit(killed);
        }

        public void OnPlayerDamaged(Transform playerTransform)
        {
            AddShake(playerDamageShake);
            RequestHitStop(hitStopOnPlayerDamageSeconds);

            if (playerTransform != null)
            {
                Vector3 pos = playerTransform.position + (Vector3.up * 0.5f);
                SpawnBurst(pos, Vector3.up, playerDamageColor, 34, 9f, 16f, 0.085f, 0.28f);
                SpawnBurst(pos, Vector3.up, Color.white, 16, 5f, 11f, 0.06f, 0.14f);
                TryPlayHitReaction(playerTransform, true);
            }

            FlashScreen(playerDamageColor, 0.32f, 0.14f);
            PlaySfx(SfxEvent.PlayerHit);
            postProcessFeedback?.OnPlayerDamaged();
        }

        public void OnPlayerDash(Vector3 position, Vector3 direction)
        {
            AddShake(dashShake);
            Vector3 normalized = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            SpawnBurst(position, normalized, dashColor, 28, 8f, 15f, 0.065f, 0.2f);
            SpawnBurst(position - (normalized * 0.6f), -normalized, dashColor, 16, 5f, 10f, 0.06f, 0.18f);
            PlaySfx(SfxEvent.Dash);
            postProcessFeedback?.OnDash();
        }

        public void OnClockPickup(Vector3 position)
        {
            AddShake(pickupShake);
            SpawnBurst(position, Vector3.up, pickupColor, 34, 6f, 14f, 0.085f, 0.24f);
            FlashScreen(pickupColor, 0.16f, 0.12f);
            PlaySfx(SfxEvent.Pickup);
            postProcessFeedback?.OnPickup();
        }

        public void OnBossPhaseShift(Vector3 position, int phase)
        {
            Color phaseColor = phase switch
            {
                3 => new Color(1f, 0.22f, 0.18f, 1f),
                2 => new Color(1f, 0.55f, 0.24f, 1f),
                _ => roomClearColor
            };

            AddShake(bossPhaseShiftShake + (0.02f * Mathf.Clamp(phase, 1, 3)));
            SpawnBurst(position, Vector3.up, phaseColor, 64, 10f, 20f, 0.11f, 0.42f);
            SpawnBurst(position, Vector3.up, Color.white, 26, 6f, 13f, 0.07f, 0.22f);
            FlashScreen(phaseColor, 0.17f, 0.18f);
            postProcessFeedback?.OnBossPhaseShift(phase);
        }

        private void HandleRoomClearedEvent(RoomClearedEvent evt)
        {
            HandleRoomCleared(evt.Room, evt.TotalRooms);
        }

        private void HandleRunWonEvent(RunWonEvent evt)
        {
            HandleRunWon();
        }

        private void HandleTimeChangedEvent(TimeChangedEvent evt)
        {
            HandleTimeChanged(evt.Remaining, evt.Delta);
        }

        private void HandleRoomCleared(int room, int total)
        {
            AddShake(roomClearShake);
            Transform player = FindPlayerTransform();
            Vector3 position = player != null ? player.position : Vector3.zero;
            SpawnBurst(position, Vector3.up, roomClearColor, 48, 10f, 18f, 0.1f, 0.34f);
            FlashScreen(roomClearColor, 0.12f, 0.15f);
            PlaySfx(SfxEvent.RoomClear);
            postProcessFeedback?.OnRoomClear();
        }

        private void HandleRunWon()
        {
            Transform player = FindPlayerTransform();
            Vector3 position = player != null ? player.position : Vector3.zero;
            SpawnBurst(position, Vector3.up, roomClearColor, 78, 12f, 24f, 0.12f, 0.46f);
            AddShake(0.2f);
            FlashScreen(roomClearColor, 0.22f, 0.22f);
            PlaySfx(SfxEvent.RunWin);
            postProcessFeedback?.OnRunWin();
        }

        private void HandleTimeChanged(float remaining, float delta)
        {
            // Only react to significant time losses (damage), not normal clock ticks
            // Normal ticks are small (deltaTime ~0.016), damage events are >= 1 second
            if (remaining > 10f || lowTimePulseCooldown > 0f || delta >= -0.5f)
            {
                return;
            }

            lowTimePulseCooldown = 0.25f;
            AddShake(0.02f);
            FlashScreen(new Color(1f, 0.1f, 0.1f, 1f), 0.08f, 0.08f);
        }

        private void BindRuntimeReferences()
        {
            if (cameraFollow == null)
            {
                cameraFollow = FindFirstObjectByType<TopDownCameraFollow>();
            }

            if (screenFlashOverlay == null)
            {
                screenFlashOverlay = FindFirstObjectByType<ScreenFlashOverlay>();
                if (screenFlashOverlay == null)
                {
                    screenFlashOverlay = CreateRuntimeScreenOverlay();
                }
            }

            if (postProcessFeedback == null)
            {
                postProcessFeedback = FindFirstObjectByType<PostProcessFeedback>();
            }

            EnsureAudioSource();
        }

        private void UnbindRuntimeReferences()
        {
        }

        private void TryPlayHitReaction(Transform target, bool heavy)
        {
            if (target == null)
            {
                return;
            }

            Transform root = target.root != null ? target.root : target;
            HitFlashSquash reaction = root.GetComponentInChildren<HitFlashSquash>();
            if (reaction == null)
            {
                reaction = root.gameObject.AddComponent<HitFlashSquash>();
            }

            if (reaction != null)
            {
                reaction.SetFlashColor(heavy ? enemyKillColor : enemyHitColor);
                reaction.PlayHitReaction(heavy);
            }
        }

        private void TryPlayRecoil(Transform target)
        {
            if (target == null)
            {
                return;
            }

            Transform root = target.root != null ? target.root : target;
            HitFlashSquash reaction = root.GetComponentInChildren<HitFlashSquash>();
            if (reaction == null)
            {
                reaction = root.gameObject.AddComponent<HitFlashSquash>();
            }

            if (reaction != null)
            {
                reaction.PlayRecoil();
            }
        }

        private void FlashScreen(Color color, float alpha, float duration)
        {
            if (screenFlashOverlay != null)
            {
                screenFlashOverlay.Flash(color, alpha, duration);
            }
        }

        private void AddShake(float amount)
        {
            if (cameraFollow == null)
            {
                cameraFollow = FindFirstObjectByType<TopDownCameraFollow>();
            }

            if (cameraFollow != null)
            {
                cameraFollow.AddShake(amount);
            }
        }

        private void RequestHitStop(float duration)
        {
            if (!enableHitStop || duration <= 0f)
            {
                return;
            }

            if (hitStopRoutine != null)
            {
                StopCoroutine(hitStopRoutine);
                Time.timeScale = 1f;
                Time.fixedDeltaTime = defaultFixedDelta;
            }

            hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            Time.timeScale = 0.05f;
            Time.fixedDeltaTime = defaultFixedDelta * Time.timeScale;

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = defaultFixedDelta;
            hitStopRoutine = null;
        }

        private void RestoreTimescaleState()
        {
            if (hitStopRoutine != null)
            {
                StopCoroutine(hitStopRoutine);
                hitStopRoutine = null;
            }

            if (Time.timeScale != 1f)
            {
                Time.timeScale = 1f;
            }

            Time.fixedDeltaTime = defaultFixedDelta > 0.0001f ? defaultFixedDelta : 0.02f;
        }

        private void SpawnBurst(Vector3 position, Vector3 direction, Color color, int count, float speedMin, float speedMax, float startSize, float lifetime)
        {
            ParticleSystem particleSystem = AcquireBurstSystem();
            if (particleSystem == null)
            {
                return;
            }

            Vector3 lookDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.up;
            Transform burstTransform = particleSystem.transform;
            burstTransform.position = position;
            burstTransform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particleSystem.main;
            main.duration = lifetime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = startSize;
            main.startColor = color;

            var emission = particleSystem.emission;
            reusableBurst[0] = new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, short.MaxValue));
            emission.SetBursts(reusableBurst);

            particleSystem.gameObject.SetActive(true);
            particleSystem.Play(true);

            activeBursts.Add(new ActiveBurst
            {
                system = particleSystem,
                recycleAt = Time.time + lifetime + 0.12f
            });
        }

        private ParticleSystem AcquireBurstSystem()
        {
            for (int i = 0; i < burstPool.Count; i++)
            {
                ParticleSystem candidate = burstPool[i];
                if (candidate != null && !candidate.gameObject.activeSelf)
                {
                    return candidate;
                }
            }

            if (burstPool.Count < maxBurstPoolSize)
            {
                return CreateBurstSystem();
            }

            if (burstPool.Count == 0)
            {
                return null;
            }

            burstRoundRobinCursor = (burstRoundRobinCursor + 1) % burstPool.Count;
            ParticleSystem reused = burstPool[burstRoundRobinCursor];
            if (reused == null)
            {
                return null;
            }

            reused.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            reused.gameObject.SetActive(false);

            for (int i = activeBursts.Count - 1; i >= 0; i--)
            {
                if (activeBursts[i].system == reused)
                {
                    activeBursts.RemoveAt(i);
                }
            }

            return reused;
        }

        private void TickBurstPool()
        {
            for (int i = activeBursts.Count - 1; i >= 0; i--)
            {
                ActiveBurst active = activeBursts[i];
                if (active.system == null)
                {
                    activeBursts.RemoveAt(i);
                    continue;
                }

                if (Time.time < active.recycleAt || active.system.IsAlive(true))
                {
                    continue;
                }

                active.system.gameObject.SetActive(false);
                activeBursts.RemoveAt(i);
            }
        }

        private void PrewarmBurstPool()
        {
            int count = Mathf.Clamp(prewarmedBursts, 0, maxBurstPoolSize);
            for (int i = burstPool.Count; i < count; i++)
            {
                CreateBurstSystem();
            }
        }

        private ParticleSystem CreateBurstSystem()
        {
            GameObject fx = new GameObject($"BurstFx_{burstPool.Count:00}");
            fx.transform.SetParent(transform, false);
            fx.SetActive(false);

            ParticleSystem particleSystem = fx.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetFxMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            var main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;

            var emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.05f;

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(SharedAlphaFadeGradient);

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            burstPool.Add(particleSystem);
            return particleSystem;
        }

        private static Gradient CreateSharedAlphaFadeGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });

            return gradient;
        }

        private void OptimizeArenaSurfaceRendering()
        {
            if (!optimizeArenaSurfaces || optimizedSurfaceObjectNames == null || optimizedSurfaceObjectNames.Length == 0)
            {
                return;
            }

            for (int i = 0; i < optimizedSurfaceObjectNames.Length; i++)
            {
                string objectName = optimizedSurfaceObjectNames[i];
                if (string.IsNullOrWhiteSpace(objectName))
                {
                    continue;
                }

                GameObject surfaceObject = GameObject.Find(objectName);
                if (surfaceObject == null)
                {
                    continue;
                }

                Renderer renderer = surfaceObject.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.allowOcclusionWhenDynamic = false;

                Material optimizedMaterial = GetOrCreateOptimizedSurfaceMaterial(renderer.sharedMaterial);
                if (optimizedMaterial != null)
                {
                    renderer.sharedMaterial = optimizedMaterial;
                }
            }
        }

        private static Material GetOrCreateOptimizedSurfaceMaterial(Material sourceMaterial)
        {
            int key = sourceMaterial != null ? sourceMaterial.GetInstanceID() : 0;
            if (RuntimeSurfaceMaterialCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return sourceMaterial;
            }

            Material optimized = new Material(shader);
            optimized.name = sourceMaterial != null ? $"{sourceMaterial.name}_RuntimePerf" : "RuntimePerfSurface";

            Color color = Color.gray;
            Texture texture = null;
            if (sourceMaterial != null)
            {
                if (sourceMaterial.HasProperty("_BaseColor"))
                {
                    color = sourceMaterial.GetColor("_BaseColor");
                }
                else if (sourceMaterial.HasProperty("_Color"))
                {
                    color = sourceMaterial.GetColor("_Color");
                }

                if (sourceMaterial.HasProperty("_BaseMap"))
                {
                    texture = sourceMaterial.GetTexture("_BaseMap");
                }
                else if (sourceMaterial.HasProperty("_MainTex"))
                {
                    texture = sourceMaterial.GetTexture("_MainTex");
                }
            }

            if (optimized.HasProperty("_BaseColor"))
            {
                optimized.SetColor("_BaseColor", color);
            }

            if (optimized.HasProperty("_Color"))
            {
                optimized.SetColor("_Color", color);
            }

            if (optimized.HasProperty("_BaseMap"))
            {
                optimized.SetTexture("_BaseMap", texture);
            }

            if (optimized.HasProperty("_MainTex"))
            {
                optimized.SetTexture("_MainTex", texture);
            }

            if (optimized.HasProperty("_Smoothness"))
            {
                optimized.SetFloat("_Smoothness", 0f);
            }

            RuntimeSurfaceMaterialCache[key] = optimized;
            return optimized;
        }

        private void EnsureAudioSource()
        {
            if (sfxSource != null)
            {
                return;
            }

            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.loop = false;
            sfxSource.volume = 1f;
        }

        private enum SfxEvent
        {
            Shoot,
            EnemyHit,
            EnemyKill,
            PlayerHit,
            Dash,
            Pickup,
            RoomClear,
            RunWin
        }

        private void PlaySfx(SfxEvent eventType)
        {
            AudioClip clip = ResolveClip(eventType);
            switch (eventType)
            {
                case SfxEvent.Shoot:
                    PlayClip(clip, shootPitchRange, shootVolume);
                    break;
                case SfxEvent.EnemyHit:
                    PlayClip(clip, enemyHitPitchRange, enemyHitVolume);
                    break;
                case SfxEvent.EnemyKill:
                    PlayClip(clip, enemyKillPitchRange, enemyKillVolume);
                    break;
                case SfxEvent.PlayerHit:
                    PlayClip(clip, playerHitPitchRange, playerHitVolume);
                    break;
                case SfxEvent.Dash:
                    PlayClip(clip, dashPitchRange, dashVolume);
                    break;
                case SfxEvent.Pickup:
                    PlayClip(clip, pickupPitchRange, pickupVolume);
                    break;
                case SfxEvent.RoomClear:
                    PlayClip(clip, roomClearPitchRange, roomClearVolume);
                    break;
                case SfxEvent.RunWin:
                    PlayClip(clip, runWinPitchRange, runWinVolume);
                    break;
            }
        }

        private AudioClip ResolveClip(SfxEvent eventType)
        {
            switch (eventType)
            {
                case SfxEvent.Shoot:
                    return shootSfx != null ? shootSfx : fallbackShootSfx;
                case SfxEvent.EnemyHit:
                    return enemyHitSfx != null ? enemyHitSfx : fallbackEnemyHitSfx;
                case SfxEvent.EnemyKill:
                    return enemyKillSfx != null ? enemyKillSfx : fallbackEnemyKillSfx;
                case SfxEvent.PlayerHit:
                    return playerHitSfx != null ? playerHitSfx : fallbackPlayerHitSfx;
                case SfxEvent.Dash:
                    return dashSfx != null ? dashSfx : fallbackDashSfx;
                case SfxEvent.Pickup:
                    return pickupSfx != null ? pickupSfx : fallbackPickupSfx;
                case SfxEvent.RoomClear:
                    return roomClearSfx != null ? roomClearSfx : fallbackRoomClearSfx;
                case SfxEvent.RunWin:
                    return runWinSfx != null ? runWinSfx : fallbackRunWinSfx;
                default:
                    return null;
            }
        }

        private void PlayClip(AudioClip clip, Vector2 pitchRange, float volume)
        {
            if (clip == null)
            {
                return;
            }

            EnsureAudioSource();
            if (sfxSource == null)
            {
                return;
            }

            float clampedVolume = Mathf.Clamp01(volume);
            float pitch = Mathf.Max(0.05f, Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y)));
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, clampedVolume);
            sfxSource.pitch = 1f;
        }

        private void GenerateFallbackClips()
        {
            // Shoot: sharp digital blip - high freq, fast attack, FM modulation for metallic tone
            fallbackShootSfx = CreateProceduralClip("FallbackShoot", 0.05f,
                880f, 1760f, 0.08f, 0.55f, 3.2f, 2200f, 22f);

            // Enemy hit: punchy mid thud with harmonics
            fallbackEnemyHitSfx = CreateProceduralClip("FallbackEnemyHit", 0.06f,
                240f, 120f, 0.35f, 0.75f, 1.8f, 480f, 15f);

            // Enemy kill: deeper crunch with noise burst and sub
            fallbackEnemyKillSfx = CreateProceduralClip("FallbackEnemyKill", 0.11f,
                160f, 60f, 0.48f, 0.82f, 2.5f, 320f, 10f);

            // Player hit: harsh metallic alarm sweep
            fallbackPlayerHitSfx = CreateProceduralClip("FallbackPlayerHit", 0.09f,
                200f, 400f, 0.52f, 0.88f, 4.0f, 600f, 12f);

            // Dash: airy whoosh sweep upward
            fallbackDashSfx = CreateProceduralClip("FallbackDash", 0.08f,
                300f, 900f, 0.38f, 0.6f, 1.2f, 1200f, 14f);

            // Pickup: bright ascending chime with harmonics
            fallbackPickupSfx = CreateProceduralClip("FallbackPickup", 0.14f,
                660f, 1320f, 0.06f, 0.7f, 2.0f, 1980f, 7f);

            // Room clear: triumphant sweep with rich overtones
            fallbackRoomClearSfx = CreateProceduralClip("FallbackRoomClear", 0.22f,
                330f, 660f, 0.15f, 0.72f, 1.5f, 990f, 5f);

            // Run win: ascending chord with reverb tail
            fallbackRunWinSfx = CreateProceduralClip("FallbackRunWin", 0.35f,
                440f, 880f, 0.12f, 0.8f, 1.8f, 1320f, 4f);
        }

        private static AudioClip CreateProceduralClip(
            string clipName, float durationSeconds,
            float freqStart, float freqEnd,
            float noiseMix, float amplitude,
            float fmDepth, float fmFreq, float decayPower)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.Max(256, Mathf.CeilToInt(sampleRate * Mathf.Max(0.01f, durationSeconds)));
            float[] samples = new float[sampleCount];
            float phase = 0f;
            float fmPhase = 0f;

            float clampedNoiseMix = Mathf.Clamp01(noiseMix);
            float clampedAmplitude = Mathf.Clamp01(amplitude);
            float clampedDecay = Mathf.Max(1f, decayPower);

            // Simple seeded noise for determinism
            uint noiseSeed = (uint)(clipName.GetHashCode() & 0x7FFFFFFF);

            for (int i = 0; i < sampleCount; i++)
            {
                float life = 1f - (i / (float)(sampleCount - 1));

                // Shaped envelope: fast attack, exponential decay
                float attackT = Mathf.Clamp01(i / (sampleRate * 0.003f)); // 3ms attack
                float env = attackT * Mathf.Pow(life, clampedDecay);

                // Frequency sweep
                float freq = Mathf.Lerp(freqEnd, freqStart, life);

                // FM synthesis for metallic/digital character
                float fmEnv = Mathf.Pow(life, clampedDecay * 0.6f);
                fmPhase += (2f * Mathf.PI * fmFreq) / sampleRate;
                float fmMod = Mathf.Sin(fmPhase) * fmDepth * fmEnv;

                phase += (2f * Mathf.PI * (freq + fmMod)) / sampleRate;

                // Main tone with 2nd and 3rd harmonics
                float tone = Mathf.Sin(phase) * 0.6f
                           + Mathf.Sin(phase * 2f) * 0.25f
                           + Mathf.Sin(phase * 3f) * 0.15f;

                // Deterministic noise
                noiseSeed ^= noiseSeed << 13;
                noiseSeed ^= noiseSeed >> 17;
                noiseSeed ^= noiseSeed << 5;
                float noise = ((noiseSeed & 0xFFFF) / 32768f) - 1f;

                float sample = Mathf.Lerp(tone, noise, clampedNoiseMix * life) * env * clampedAmplitude;

                // Soft clip for warmth
                sample = Mathf.Clamp(sample, -1f, 1f);
                sample = sample - (sample * sample * sample * 0.15f);

                samples[i] = sample;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private Material GetFxMaterial()
        {
            if (cachedFxMaterial != null)
            {
                return cachedFxMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            cachedFxMaterial = new Material(shader);
            return cachedFxMaterial;
        }

        private Transform FindPlayerTransform()
        {
            Sixty.Player.PlayerController player = FindFirstObjectByType<Sixty.Player.PlayerController>();
            return player != null ? player.transform : null;
        }

        private ScreenFlashOverlay CreateRuntimeScreenOverlay()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("RuntimeFXCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            GameObject overlayGo = new GameObject("ScreenFlashOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(ScreenFlashOverlay));
            overlayGo.transform.SetParent(canvas.transform, false);

            RectTransform rect = overlayGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            UnityEngine.UI.Image image = overlayGo.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(1f, 0f, 0f, 0f);

            return overlayGo.GetComponent<ScreenFlashOverlay>();
        }
    }
}
