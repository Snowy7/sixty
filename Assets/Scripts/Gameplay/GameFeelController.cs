using System.Collections;
using System.Collections.Generic;
using Sixty.CameraSystem;
using Sixty.Core;
using Sixty.UI;
using UnityEngine;

namespace Sixty.Gameplay
{
    public class GameFeelController : MonoBehaviour
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

        [Header("Hit Stop")]
        [SerializeField] private bool enableHitStop = true;
        [SerializeField] private float hitStopOnHitSeconds = 0.018f;
        [SerializeField] private float hitStopOnKillSeconds = 0.04f;
        [SerializeField] private float hitStopOnPlayerDamageSeconds = 0.032f;

        [Header("Particle Colors")]
        [SerializeField] private Color shootColor = new Color(0.65f, 0.95f, 1f, 1f);
        [SerializeField] private Color enemyHitColor = new Color(1f, 0.5f, 0.32f, 1f);
        [SerializeField] private Color enemyKillColor = new Color(1f, 0.76f, 0.22f, 1f);
        [SerializeField] private Color playerDamageColor = new Color(1f, 0.27f, 0.2f, 1f);
        [SerializeField] private Color dashColor = new Color(0.45f, 0.9f, 1f, 1f);
        [SerializeField] private Color pickupColor = new Color(1f, 0.88f, 0.28f, 1f);
        [SerializeField] private Color roomClearColor = new Color(0.7f, 1f, 0.7f, 1f);

        [Header("Burst Pool")]
        [SerializeField] private int prewarmedBursts = 56;
        [SerializeField] private int maxBurstPoolSize = 96;

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
        private RunDirector runDirector;
        private TimeManager timeManager;
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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            defaultFixedDelta = Time.fixedDeltaTime;
            EnsureAudioSource();
            GenerateFallbackClips();
            PrewarmBurstPool();
        }

        private void OnEnable()
        {
            BindRuntimeReferences();
        }

        private void OnDisable()
        {
            UnbindRuntimeReferences();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (cameraFollow == null || screenFlashOverlay == null || runDirector == null || timeManager == null || postProcessFeedback == null)
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
            if (remaining > 10f || lowTimePulseCooldown > 0f || delta >= 0f)
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

            if (runDirector == null)
            {
                runDirector = FindFirstObjectByType<RunDirector>();
                if (runDirector != null)
                {
                    runDirector.OnRoomCleared += HandleRoomCleared;
                    runDirector.OnRunWon += HandleRunWon;
                }
            }

            if (timeManager == null)
            {
                timeManager = TimeManager.Instance;
                if (timeManager != null)
                {
                    timeManager.OnTimeChanged += HandleTimeChanged;
                }
            }

            EnsureAudioSource();
        }

        private void UnbindRuntimeReferences()
        {
            if (runDirector != null)
            {
                runDirector.OnRoomCleared -= HandleRoomCleared;
                runDirector.OnRunWon -= HandleRunWon;
                runDirector = null;
            }

            if (timeManager != null)
            {
                timeManager.OnTimeChanged -= HandleTimeChanged;
                timeManager = null;
            }
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

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetFxMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            var main = particleSystem.main;
            main.playOnAwake = false;
            main.duration = lifetime;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = startSize;
            main.startColor = color;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(count * 2, 64);

            var emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, short.MaxValue)) });

            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.05f;

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

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
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            burstPool.Add(particleSystem);
            return particleSystem;
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
            fallbackShootSfx = CreateProceduralClip("FallbackShoot", 0.045f, 850f, 0.18f, 18f, 0.55f);
            fallbackEnemyHitSfx = CreateProceduralClip("FallbackEnemyHit", 0.05f, 220f, 0.5f, 13f, 0.75f);
            fallbackEnemyKillSfx = CreateProceduralClip("FallbackEnemyKill", 0.09f, 140f, 0.58f, 9f, 0.8f);
            fallbackPlayerHitSfx = CreateProceduralClip("FallbackPlayerHit", 0.08f, 170f, 0.66f, 10f, 0.88f);
            fallbackDashSfx = CreateProceduralClip("FallbackDash", 0.07f, 500f, 0.42f, 12f, 0.65f);
            fallbackPickupSfx = CreateProceduralClip("FallbackPickup", 0.1f, 980f, 0.1f, 6f, 0.7f);
            fallbackRoomClearSfx = CreateProceduralClip("FallbackRoomClear", 0.16f, 420f, 0.32f, 5f, 0.72f);
            fallbackRunWinSfx = CreateProceduralClip("FallbackRunWin", 0.2f, 620f, 0.28f, 4f, 0.8f);
        }

        private static AudioClip CreateProceduralClip(string clipName, float durationSeconds, float baseFrequency, float noiseMix, float decayPower, float amplitude)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.Max(128, Mathf.CeilToInt(sampleRate * Mathf.Max(0.01f, durationSeconds)));
            float[] samples = new float[sampleCount];
            float phase = 0f;

            float clampedNoiseMix = Mathf.Clamp01(noiseMix);
            float clampedAmplitude = Mathf.Clamp01(amplitude);
            float clampedDecay = Mathf.Max(1f, decayPower);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float life = 1f - (i / (float)(sampleCount - 1));
                float env = Mathf.Pow(life, clampedDecay);

                float freqSweep = baseFrequency * (1f + (0.35f * life));
                phase += (2f * Mathf.PI * freqSweep) / sampleRate;

                float tone = Mathf.Sin(phase);
                float noise = (Random.value * 2f) - 1f;
                float sample = Mathf.Lerp(tone, noise, clampedNoiseMix) * env * clampedAmplitude;
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

    public class PostProcessFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UnityEngine.Rendering.Volume volume;

        [Header("Low Time")]
        [SerializeField] private float lowTimeThreshold = 10f;
        [SerializeField] private float lowTimeVignetteBoost = 0.22f;
        [SerializeField] private float lowTimeSaturationPenalty = 26f;
        [SerializeField] private float lowTimeSmoothing = 4.5f;

        [Header("Pulse Decay")]
        [SerializeField] private float chromaticDecayPerSecond = 2.6f;
        [SerializeField] private float lensDecayPerSecond = 3.2f;
        [SerializeField] private float vignetteDecayPerSecond = 3.3f;
        [SerializeField] private float flashDecayPerSecond = 5.2f;

        private UnityEngine.Rendering.Universal.Vignette vignette;
        private UnityEngine.Rendering.Universal.ChromaticAberration chromatic;
        private UnityEngine.Rendering.Universal.LensDistortion lens;
        private UnityEngine.Rendering.Universal.ColorAdjustments colorAdjustments;

        private float baseVignette;
        private float baseSaturation;
        private float baseChromatic;
        private float baseLens;
        private Color baseColorFilter = Color.white;

        private float chromaticPulse;
        private float vignettePulse;
        private float lensPulse;
        private float flashStrength;
        private Color flashColor = Color.white;

        private void Awake()
        {
            ResolveOverrides();
        }

        private void Update()
        {
            if (volume == null || vignette == null || chromatic == null || lens == null || colorAdjustments == null)
            {
                ResolveOverrides();
            }

            float lowTimeFactor = 0f;
            if (Sixty.Core.TimeManager.Instance != null && lowTimeThreshold > 0.01f)
            {
                lowTimeFactor = Mathf.Clamp01(1f - (Sixty.Core.TimeManager.Instance.TimeRemaining / lowTimeThreshold));
            }

            if (vignette != null)
            {
                float targetVignette = baseVignette + (lowTimeFactor * lowTimeVignetteBoost) + vignettePulse;
                vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, Mathf.Clamp01(targetVignette), lowTimeSmoothing * Time.deltaTime);
            }

            if (colorAdjustments != null)
            {
                float targetSaturation = baseSaturation - (lowTimeFactor * lowTimeSaturationPenalty);
                colorAdjustments.saturation.value = Mathf.Lerp(colorAdjustments.saturation.value, targetSaturation, lowTimeSmoothing * Time.deltaTime);
                colorAdjustments.colorFilter.value = Color.Lerp(baseColorFilter, flashColor, Mathf.Clamp01(flashStrength));
            }

            if (chromatic != null)
            {
                chromatic.intensity.value = Mathf.Lerp(chromatic.intensity.value, Mathf.Clamp01(baseChromatic + chromaticPulse), lowTimeSmoothing * Time.deltaTime);
            }

            if (lens != null)
            {
                lens.intensity.value = Mathf.Lerp(lens.intensity.value, Mathf.Clamp(baseLens + lensPulse, -1f, 1f), lowTimeSmoothing * Time.deltaTime);
            }

            chromaticPulse = Mathf.MoveTowards(chromaticPulse, 0f, chromaticDecayPerSecond * Time.deltaTime);
            vignettePulse = Mathf.MoveTowards(vignettePulse, 0f, vignetteDecayPerSecond * Time.deltaTime);
            lensPulse = Mathf.MoveTowards(lensPulse, 0f, lensDecayPerSecond * Time.deltaTime);
            flashStrength = Mathf.MoveTowards(flashStrength, 0f, flashDecayPerSecond * Time.deltaTime);
        }

        public void OnShot()
        {
            chromaticPulse = Mathf.Max(chromaticPulse, 0.03f);
        }

        public void OnEnemyHit(bool killed)
        {
            chromaticPulse = Mathf.Max(chromaticPulse, killed ? 0.18f : 0.1f);
            vignettePulse = Mathf.Max(vignettePulse, killed ? 0.08f : 0.04f);
        }

        public void OnPlayerDamaged()
        {
            chromaticPulse = Mathf.Max(chromaticPulse, 0.24f);
            vignettePulse = Mathf.Max(vignettePulse, 0.2f);
            TriggerFlash(new Color(1f, 0.24f, 0.2f, 1f), 0.55f);
        }

        public void OnDash()
        {
            lensPulse = Mathf.Min(lensPulse, -0.2f);
            chromaticPulse = Mathf.Max(chromaticPulse, 0.08f);
        }

        public void OnPickup()
        {
            chromaticPulse = Mathf.Max(chromaticPulse, 0.06f);
            vignettePulse = Mathf.Max(vignettePulse, 0.05f);
            TriggerFlash(new Color(1f, 0.9f, 0.45f, 1f), 0.22f);
        }

        public void OnRoomClear()
        {
            chromaticPulse = Mathf.Max(chromaticPulse, 0.12f);
            vignettePulse = Mathf.Max(vignettePulse, 0.09f);
            TriggerFlash(new Color(0.68f, 1f, 0.74f, 1f), 0.16f);
        }

        public void OnRunWin()
        {
            chromaticPulse = Mathf.Max(chromaticPulse, 0.28f);
            vignettePulse = Mathf.Max(vignettePulse, 0.2f);
            lensPulse = Mathf.Min(lensPulse, -0.18f);
            TriggerFlash(new Color(0.75f, 1f, 0.82f, 1f), 0.26f);
        }

        private void TriggerFlash(Color color, float intensity)
        {
            flashColor = color;
            flashStrength = Mathf.Max(flashStrength, intensity);
        }

        private void ResolveOverrides()
        {
            if (volume == null)
            {
                volume = FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            }

            if (volume == null || volume.profile == null)
            {
                return;
            }

            volume.profile.TryGet(out vignette);
            volume.profile.TryGet(out chromatic);
            volume.profile.TryGet(out lens);
            volume.profile.TryGet(out colorAdjustments);

            if (vignette != null)
            {
                baseVignette = vignette.intensity.value;
            }

            if (chromatic != null)
            {
                baseChromatic = chromatic.intensity.value;
            }

            if (lens != null)
            {
                baseLens = lens.intensity.value;
            }

            if (colorAdjustments != null)
            {
                baseSaturation = colorAdjustments.saturation.value;
                baseColorFilter = colorAdjustments.colorFilter.value;
            }
        }
    }
}
