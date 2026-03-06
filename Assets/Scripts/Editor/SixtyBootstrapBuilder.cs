#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Sixty.CameraSystem;
using Sixty.Combat;
using Sixty.Core;
using Sixty.Enemies;
using Sixty.Gameplay;
using Sixty.Player;
using Sixty.UI;
using Sixty.World;
using Ia.Core.Update;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Sixty.EditorTools
{
    public static class SixtyBootstrapBuilder
    {
        private const string GeneratedRoot = "Assets/SixtyGenerated";
        private const string MaterialsFolder = GeneratedRoot + "/Materials";
        private const string PrefabsFolder = GeneratedRoot + "/Prefabs";
        private const string ScriptableFolder = GeneratedRoot + "/ScriptableObjects";
        private const string ScenesFolder = GeneratedRoot + "/Scenes";
        private const string ScenePath = ScenesFolder + "/Sixty_Playable.unity";
        private const float EnemyHoverHeight = 1.4f;
        private const string DecimateMaterialsFolder = "Assets/Plugins/Decimate/Grid Master/URP/Materials";
        private const string ScalableMaterialsFolder = "Assets/Plugins/Scalable Grid Prototype Materials/Materials";
        private const string ScalableGroundMaterialsFolder = "Assets/Plugins/Scalable Grid Prototype Materials/Materials/Ground";

        private sealed class BootstrapAssets
        {
            public InputActionAsset inputActions;

            public Material floorMaterial;
            public Material floorTrimMaterial;
            public Material floorPerformanceMaterial;
            public Material floorTrimPerformanceMaterial;
            public Material wallMaterial;
            public Material coverMaterial;
            public Material accentMaterial;
            public Material playerMaterial;
            public Material playerProjectileMaterial;
            public Material enemyProjectileMaterial;
            public Material pickupMaterial;
            public Material droneMaterial;
            public Material turretMaterial;
            public Material hunterMaterial;
            public Material tankMaterial;
            public Material bossMaterial;
            public VolumeProfile gameplayVolumeProfile;

            public GameObject playerProjectilePrefab;
            public GameObject enemyProjectilePrefab;
            public ClockPickup clockPickupPrefab;

            public WeaponDefinition pulseRifle;
            public WeaponDefinition shotgun;
            public WeaponDefinition chargeBeam;

            public GameObject playerPrefab;
            public GameObject dronePrefab;
            public GameObject turretPrefab;
            public GameObject hunterPrefab;
            public GameObject tankPrefab;
            public GameObject bossPrefab;
        }

        [MenuItem("Tools/Sixty/Build Playable Scene")]
        public static void BuildPlayableScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureGeneratedFolders();
            BootstrapAssets assets = CreateOrUpdateAssets();
            if (assets == null)
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildScene(scene, assets);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Selection.activeObject = sceneAsset;

            EditorUtility.DisplayDialog(
                "SIXTY Setup Complete",
                "Created Assets/SixtyGenerated/Scenes/Sixty_Playable.unity and all generated prefabs/assets.\nOpen the scene and press Play.",
                "OK");
        }

        [MenuItem("Tools/Sixty/Repair Missing Scripts (Generated)")]
        public static void RepairMissingScriptsGenerated()
        {
            int removedCount = 0;
            int addedCount = 0;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    continue;
                }

                int before = CountMissingScriptsRecursive(root);
                if (before > 0)
                {
                    RemoveMissingScriptsRecursive(root);
                    removedCount += before;
                }

                if (EnsureEnemyPrefabComponents(root))
                {
                    addedCount++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                GameObject[] roots = activeScene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    GameObject root = roots[i];
                    int before = CountMissingScriptsRecursive(root);
                    if (before > 0)
                    {
                        RemoveMissingScriptsRecursive(root);
                        removedCount += before;
                    }

                    if (EnsureEnemyPrefabComponents(root))
                    {
                        addedCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Missing Script Repair",
                $"Removed approximately {removedCount} missing component references.\nReattached/ensured enemy impact components on {addedCount} root objects.",
                "OK");
        }

        private static BootstrapAssets CreateOrUpdateAssets()
        {
            InputActionAsset inputActions = ResolveInputActionsAsset();
            if (inputActions == null)
            {
                Debug.LogError("Input actions asset not found. Expected an InputActionAsset named 'InputSystem_Actions'.");
                return null;
            }

            BootstrapAssets assets = new BootstrapAssets
            {
                inputActions = inputActions,
                floorMaterial = LoadMaterialFromPaths(
                    new[]
                    {
                        $"{ScalableGroundMaterialsFolder}/DarkGray_Ground_Prototype.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-03-URP.mat"
                    },
                    $"{MaterialsFolder}/M_Floor.mat",
                    new Color(0.18f, 0.2f, 0.24f)),
                floorTrimMaterial = LoadMaterialFromPaths(
                    new[]
                    {
                        $"{DecimateMaterialsFolder}/GM-Grid-02-URP.mat",
                        $"{ScalableGroundMaterialsFolder}/Gray_Ground_Prototype.mat"
                    },
                    $"{MaterialsFolder}/M_FloorTrim.mat",
                    new Color(0.24f, 0.27f, 0.32f)),
                wallMaterial = LoadMaterialFromPaths(
                    new[]
                    {
                        $"{DecimateMaterialsFolder}/GM-Grid-11-URP.mat",
                        $"{ScalableMaterialsFolder}/Dark_Gray_Prototype.mat"
                    },
                    $"{MaterialsFolder}/M_Wall.mat",
                    new Color(0.1f, 0.12f, 0.16f)),
                coverMaterial = LoadMaterialFromPaths(
                    new[]
                    {
                        $"{DecimateMaterialsFolder}/GM-Grid-07-URP.mat",
                        $"{ScalableMaterialsFolder}/Gray_Prototype.mat"
                    },
                    $"{MaterialsFolder}/M_Cover.mat",
                    new Color(0.13f, 0.15f, 0.19f)),
                accentMaterial = LoadMaterialFromPaths(
                    new[]
                    {
                        $"{ScalableMaterialsFolder}/Blue_Sapphire_Prototype.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-20-URP.mat"
                    },
                    $"{MaterialsFolder}/M_Accent.mat",
                    new Color(0.19f, 0.55f, 0.68f)),
                // Use generated standard URP materials for gameplay actors/projectiles so hit flash tinting is always visible.
                playerMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Player.mat", new Color(0.35f, 0.78f, 1f)),
                playerProjectileMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_PlayerProjectile.mat", new Color(0.62f, 0.9f, 1f)),
                enemyProjectileMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_EnemyProjectile.mat", new Color(1f, 0.45f, 0.35f)),
                pickupMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_ClockPickup.mat", new Color(1f, 0.85f, 0.25f)),
                droneMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Drone.mat", new Color(1f, 0.57f, 0.32f)),
                turretMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Turret.mat", new Color(1f, 0.72f, 0.28f)),
                hunterMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Hunter.mat", new Color(1f, 0.46f, 0.22f)),
                tankMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Tank.mat", new Color(0.82f, 0.36f, 0.14f)),
                bossMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Boss.mat", new Color(0.78f, 0.18f, 0.12f))
            };

            assets.floorPerformanceMaterial = CreateOrUpdateCheapSurfaceMaterial(
                $"{MaterialsFolder}/M_Floor_Perf.mat",
                assets.floorMaterial,
                new Color(0.18f, 0.2f, 0.24f));

            assets.floorTrimPerformanceMaterial = CreateOrUpdateCheapSurfaceMaterial(
                $"{MaterialsFolder}/M_FloorTrim_Perf.mat",
                assets.floorTrimMaterial,
                new Color(0.24f, 0.27f, 0.32f));

            assets.gameplayVolumeProfile = CreateOrUpdateGameplayVolumeProfile($"{ScriptableFolder}/VP_GameplayFeel.asset");

            assets.playerProjectilePrefab = CreatePlayerProjectilePrefab(assets);
            assets.enemyProjectilePrefab = CreateEnemyProjectilePrefab(assets);
            assets.clockPickupPrefab = CreateClockPickupPrefab(assets);

            assets.pulseRifle = CreateOrUpdateWeaponDefinition(
                $"{ScriptableFolder}/W_PulseRifle.asset",
                "Pulse Rifle",
                8f,
                12f,
                assets.playerProjectilePrefab,
                45f,
                2f);

            assets.shotgun = CreateOrUpdateWeaponDefinition(
                $"{ScriptableFolder}/W_Shotgun.asset",
                "Shotgun",
                1.5f,
                8f,
                assets.playerProjectilePrefab,
                40f,
                0.8f);

            assets.chargeBeam = CreateOrUpdateWeaponDefinition(
                $"{ScriptableFolder}/W_ChargeBeam.asset",
                "Charge Beam",
                1f,
                80f,
                assets.playerProjectilePrefab,
                60f,
                1.25f);

            assets.playerPrefab = CreatePlayerPrefab(assets);
            assets.dronePrefab = CreateEnemyPrefab(new EnemyPrefabBuildParams
            {
                path = $"{PrefabsFolder}/P_Enemy_Drone.prefab",
                name = "Enemy_Drone",
                primitiveType = PrimitiveType.Sphere,
                scale = new Vector3(1.1f, 1.1f, 1.1f),
                material = assets.droneMaterial,
                maxHealth = 30f,
                destroyOnDeath = true,
                addChaser = true,
                moveSpeed = 4.9f,
                stoppingDistance = 0.95f,
                chaserMoveMode = EnemyChaser.MovementMode.ChargeBurst,
                chaserChargeSpeedMultiplier = 3.3f,
                chaserChargeApproachSpeedMultiplier = 1.05f,
                chaserChargeDuration = 0.24f,
                chaserChargeCooldown = 0.95f,
                chaserChargeMinRange = 1.4f,
                chaserChargeMaxRange = 16f,
                chaserChargeTurnMultiplier = 0.5f,
                addContactDamage = true,
                contactDamage = 2f,
                contactCooldown = 0.35f,
                addShooter = false,
                enemyProjectilePrefab = assets.enemyProjectilePrefab,
                impactKnockback = 2.6f,
                impactKillKnockback = 4.1f,
                impactStunDuration = 0.1f
            });

            assets.turretPrefab = CreateEnemyPrefab(new EnemyPrefabBuildParams
            {
                path = $"{PrefabsFolder}/P_Enemy_Turret.prefab",
                name = "Enemy_Turret",
                primitiveType = PrimitiveType.Cube,
                scale = new Vector3(1.6f, 1.6f, 1.6f),
                material = assets.turretMaterial,
                maxHealth = 50f,
                destroyOnDeath = true,
                addChaser = false,
                addContactDamage = false,
                addShooter = true,
                enemyProjectilePrefab = assets.enemyProjectilePrefab,
                shooterRate = 0.95f,
                shooterRange = 20f,
                shooterDamageAsTimeLoss = 2f,
                shooterProjectileSpeed = 15f,
                shooterFireMode = EnemyShooter.FireMode.Burst,
                shooterBurstCount = 2,
                shooterBurstInterval = 0.12f,
                shooterWindupSeconds = 0.24f,
                impactKnockback = 2f,
                impactKillKnockback = 3.2f,
                impactStunDuration = 0.08f
            });

            assets.hunterPrefab = CreateEnemyPrefab(new EnemyPrefabBuildParams
            {
                path = $"{PrefabsFolder}/P_Enemy_Hunter.prefab",
                name = "Enemy_Hunter",
                primitiveType = PrimitiveType.Capsule,
                scale = new Vector3(1f, 1.15f, 1f),
                material = assets.hunterMaterial,
                maxHealth = 40f,
                destroyOnDeath = true,
                addChaser = true,
                moveSpeed = 4.6f,
                stoppingDistance = 4.9f,
                chaserMoveMode = EnemyChaser.MovementMode.OrbitStrafe,
                chaserOrbitPreferredDistance = 6.4f,
                chaserOrbitStrafeWeight = 1.2f,
                chaserOrbitApproachWeight = 0.8f,
                chaserOrbitDirectionFlipInterval = 1.35f,
                chaserOrbitSpeedMultiplier = 1.05f,
                addContactDamage = true,
                contactDamage = 2f,
                contactCooldown = 0.45f,
                addShooter = true,
                enemyProjectilePrefab = assets.enemyProjectilePrefab,
                shooterRate = 1.55f,
                shooterRange = 18f,
                shooterMinimumRange = 3.2f,
                shooterDamageAsTimeLoss = 2f,
                shooterProjectileSpeed = 14f,
                shooterFireMode = EnemyShooter.FireMode.Burst,
                shooterBurstCount = 3,
                shooterBurstInterval = 0.09f,
                shooterWindupSeconds = 0.08f,
                shooterUsePredictiveAim = true,
                shooterPredictiveLeadSeconds = 0.16f,
                impactKnockback = 2.35f,
                impactKillKnockback = 3.8f,
                impactStunDuration = 0.09f
            });

            assets.tankPrefab = CreateEnemyPrefab(new EnemyPrefabBuildParams
            {
                path = $"{PrefabsFolder}/P_Enemy_Tank.prefab",
                name = "Enemy_Tank",
                primitiveType = PrimitiveType.Cube,
                scale = new Vector3(2.1f, 2.1f, 2.1f),
                material = assets.tankMaterial,
                maxHealth = 120f,
                destroyOnDeath = true,
                addChaser = true,
                moveSpeed = 2.2f,
                stoppingDistance = 1.2f,
                chaserMoveMode = EnemyChaser.MovementMode.HeavyTank,
                chaserHeavySpeedMultiplier = 0.8f,
                chaserHeavyTurnMultiplier = 0.5f,
                addContactDamage = true,
                contactDamage = 3.2f,
                contactCooldown = 0.45f,
                addShooter = false,
                enemyProjectilePrefab = assets.enemyProjectilePrefab,
                impactKnockback = 1.7f,
                impactKillKnockback = 2.8f,
                impactStunDuration = 0.06f
            });

            assets.bossPrefab = CreateEnemyPrefab(new EnemyPrefabBuildParams
            {
                path = $"{PrefabsFolder}/P_Enemy_Boss.prefab",
                name = "Enemy_Boss",
                primitiveType = PrimitiveType.Cylinder,
                scale = new Vector3(3.4f, 2.5f, 3.4f),
                material = assets.bossMaterial,
                maxHealth = 800f,
                destroyOnDeath = true,
                addChaser = true,
                moveSpeed = 2.6f,
                stoppingDistance = 7.2f,
                chaserMoveMode = EnemyChaser.MovementMode.HeavyTank,
                chaserHeavySpeedMultiplier = 0.9f,
                chaserHeavyTurnMultiplier = 0.72f,
                addContactDamage = true,
                contactDamage = 4f,
                contactCooldown = 0.35f,
                addShooter = true,
                enemyProjectilePrefab = assets.enemyProjectilePrefab,
                shooterRate = 1.45f,
                shooterRange = 22f,
                shooterMinimumRange = 4.5f,
                shooterDamageAsTimeLoss = 3f,
                shooterProjectileSpeed = 17f,
                shooterFireMode = EnemyShooter.FireMode.Single,
                shooterWindupSeconds = 0.12f,
                shooterUsePredictiveAim = true,
                shooterPredictiveLeadSeconds = 0.14f,
                addBossPhaseController = true,
                bossPhase2MoveMultiplier = 1.2f,
                bossPhase3MoveMultiplier = 1.38f,
                bossPhase2FireRateMultiplier = 1.35f,
                bossPhase3FireRateMultiplier = 1.75f,
                impactKnockback = 1.3f,
                impactKillKnockback = 2.3f,
                impactStunDuration = 0.05f
            });

            return assets;
        }

        private static void BuildScene(Scene scene, BootstrapAssets assets)
        {
            GameObject world = new GameObject("World");
            BuildArena(world.transform, assets);
            Transform[] spawnPoints = BuildSpawnPoints(world.transform);

            GameObject gameplayRoot = new GameObject("Gameplay");
            GameObject iaBootstrapGo = new GameObject("IAFrameworkBootstrap");
            iaBootstrapGo.transform.SetParent(gameplayRoot.transform);
            iaBootstrapGo.AddComponent<IaBootstrap>();

            GameObject timeManagerGo = new GameObject("TimeManager");
            timeManagerGo.transform.SetParent(gameplayRoot.transform);
            timeManagerGo.AddComponent<TimeManager>();

            GameObject runDirectorGo = new GameObject("RunDirector");
            runDirectorGo.transform.SetParent(gameplayRoot.transform);
            RunDirector runDirector = runDirectorGo.AddComponent<RunDirector>();
            ConfigureRunDirector(runDirector, assets, spawnPoints);

            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(assets.playerPrefab);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 0.95f, 0f);

            Camera mainCamera = BuildMainCamera(player.transform);
            BindPlayerCamera(player, mainCamera);
            Volume gameplayVolume = BuildPostProcessingVolume(gameplayRoot.transform, assets.gameplayVolumeProfile);

            GameObject gameFeelGo = new GameObject("GameFeel");
            gameFeelGo.transform.SetParent(gameplayRoot.transform);
            GameFeelController gameFeel = gameFeelGo.AddComponent<GameFeelController>();
            PostProcessFeedback postProcessFeedback = gameFeelGo.AddComponent<PostProcessFeedback>();
            AudioSource feelAudio = gameFeelGo.AddComponent<AudioSource>();
            feelAudio.playOnAwake = false;
            feelAudio.loop = false;
            feelAudio.spatialBlend = 0f;
            TopDownCameraFollow follow = mainCamera.GetComponent<TopDownCameraFollow>();
            ScreenFlashOverlay overlay = BuildHud(runDirector, assets);
            ConfigureSerialized(gameFeel, so =>
            {
                so.FindProperty("cameraFollow").objectReferenceValue = follow;
                so.FindProperty("screenFlashOverlay").objectReferenceValue = overlay;
                so.FindProperty("postProcessFeedback").objectReferenceValue = postProcessFeedback;
                so.FindProperty("sfxSource").objectReferenceValue = feelAudio;
            });
            ConfigureSerialized(postProcessFeedback, so =>
            {
                so.FindProperty("volume").objectReferenceValue = gameplayVolume;
            });

            BuildLighting();
            EnsureEventSystem();

            SceneManager.SetActiveScene(scene);
        }

        private static void BuildArena(Transform parent, BootstrapAssets assets)
        {
            GameObject baseFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            baseFloor.name = "BaseFloor";
            baseFloor.transform.SetParent(parent);
            baseFloor.transform.position = Vector3.zero;
            baseFloor.transform.localScale = new Vector3(5.6f, 1f, 5.6f);
            Renderer baseFloorRenderer = baseFloor.GetComponent<Renderer>();
            baseFloorRenderer.sharedMaterial = assets.floorPerformanceMaterial != null ? assets.floorPerformanceMaterial : assets.floorMaterial;
            ConfigureEnvironmentRenderer(baseFloorRenderer, true);

            GameObject arenaPlate = CreateBlock(parent, assets.floorTrimPerformanceMaterial != null ? assets.floorTrimPerformanceMaterial : assets.floorTrimMaterial, new Vector3(0f, 0.06f, 0f), new Vector3(44f, 0.12f, 44f), "ArenaPlate", false);
            ConfigureEnvironmentRenderer(arenaPlate.GetComponent<Renderer>(), true);
            CreateBlock(parent, assets.accentMaterial, new Vector3(0f, 0.11f, 0f), new Vector3(2.2f, 0.025f, 26f), "Guide_NorthSouth", false);
            CreateBlock(parent, assets.accentMaterial, new Vector3(0f, 0.11f, 0f), new Vector3(26f, 0.025f, 2.2f), "Guide_EastWest", false);

            CreateWall(parent, assets.wallMaterial, new Vector3(0f, 1.6f, 24f), new Vector3(48f, 3.2f, 1f), "Wall_North");
            CreateWall(parent, assets.wallMaterial, new Vector3(0f, 1.6f, -24f), new Vector3(48f, 3.2f, 1f), "Wall_South");
            CreateWall(parent, assets.wallMaterial, new Vector3(24f, 1.6f, 0f), new Vector3(1f, 3.2f, 48f), "Wall_East");
            CreateWall(parent, assets.wallMaterial, new Vector3(-24f, 1.6f, 0f), new Vector3(1f, 3.2f, 48f), "Wall_West");

            Vector3[] coverPositions =
            {
                new Vector3(-10f, 0.9f, -10f),
                new Vector3(10f, 0.9f, -10f),
                new Vector3(-10f, 0.9f, 10f),
                new Vector3(10f, 0.9f, 10f),
                new Vector3(0f, 0.9f, -12f),
                new Vector3(0f, 0.9f, 12f),
                new Vector3(-12f, 0.9f, 0f),
                new Vector3(12f, 0.9f, 0f)
            };

            Vector3[] coverScales =
            {
                new Vector3(3f, 1.8f, 1.2f),
                new Vector3(3f, 1.8f, 1.2f),
                new Vector3(3f, 1.8f, 1.2f),
                new Vector3(3f, 1.8f, 1.2f),
                new Vector3(4.2f, 1.8f, 1.2f),
                new Vector3(4.2f, 1.8f, 1.2f),
                new Vector3(1.2f, 1.8f, 4.2f),
                new Vector3(1.2f, 1.8f, 4.2f)
            };

            for (int i = 0; i < coverPositions.Length; i++)
            {
                CreateBlock(parent, assets.coverMaterial, coverPositions[i], coverScales[i], $"Cover_{i + 1:00}");
            }

            CreateDoorFrame(parent, assets.accentMaterial, new Vector3(0f, 0f, 20.5f), Quaternion.Euler(0f, 0f, 0f), "DoorFrame_North");
            CreateDoorFrame(parent, assets.accentMaterial, new Vector3(0f, 0f, -20.5f), Quaternion.Euler(0f, 180f, 0f), "DoorFrame_South");
            CreateDoorFrame(parent, assets.accentMaterial, new Vector3(20.5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), "DoorFrame_East");
            CreateDoorFrame(parent, assets.accentMaterial, new Vector3(-20.5f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), "DoorFrame_West");
        }

        private static Transform[] BuildSpawnPoints(Transform parent)
        {
            GameObject spawnRoot = new GameObject("SpawnPoints");
            spawnRoot.transform.SetParent(parent);
            spawnRoot.transform.position = Vector3.zero;

            Vector3[] positions =
            {
                new Vector3(0f, EnemyHoverHeight, 20f),
                new Vector3(0f, EnemyHoverHeight, -20f),
                new Vector3(20f, EnemyHoverHeight, 0f),
                new Vector3(-20f, EnemyHoverHeight, 0f),
                new Vector3(15f, EnemyHoverHeight, 15f),
                new Vector3(-15f, EnemyHoverHeight, 15f),
                new Vector3(15f, EnemyHoverHeight, -15f),
                new Vector3(-15f, EnemyHoverHeight, -15f),
                new Vector3(9f, EnemyHoverHeight, 19f),
                new Vector3(-9f, EnemyHoverHeight, 19f),
                new Vector3(9f, EnemyHoverHeight, -19f),
                new Vector3(-9f, EnemyHoverHeight, -19f),
                new Vector3(19f, EnemyHoverHeight, 9f),
                new Vector3(19f, EnemyHoverHeight, -9f),
                new Vector3(-19f, EnemyHoverHeight, 9f),
                new Vector3(-19f, EnemyHoverHeight, -9f)
            };

            Transform[] points = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject point = new GameObject($"SpawnPoint_{i + 1:00}");
                point.transform.SetParent(spawnRoot.transform);
                point.transform.position = positions[i];
                points[i] = point.transform;
            }

            return points;
        }

        private static Camera BuildMainCamera(Transform target)
        {
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";

            Camera cam = cameraGo.AddComponent<Camera>();
            cam.enabled = true;
            cam.transform.position = new Vector3(0f, 22f, -12f);
            cam.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.06f, 0.08f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 90f;

            cameraGo.AddComponent<AudioListener>();
            UniversalAdditionalCameraData cameraData = cameraGo.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            }

            cameraData.renderPostProcessing = true;
            cameraData.renderShadows = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            TopDownCameraFollow follow = cameraGo.AddComponent<TopDownCameraFollow>();
            follow.SetTarget(target);

            return cam;
        }

        private static void BuildLighting()
        {
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.08f;
            light.color = new Color(0.92f, 0.96f, 1f);
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Vector3[] lightPositions =
            {
                new Vector3(18f, 5f, 18f),
                new Vector3(-18f, 5f, -18f)
            };

            for (int i = 0; i < lightPositions.Length; i++)
            {
                GameObject fill = new GameObject($"FillLight_{i + 1:00}");
                Light fillLight = fill.AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.range = 17f;
                fillLight.intensity = 0.85f;
                fillLight.color = new Color(0.25f, 0.45f, 0.58f);
                fillLight.shadows = LightShadows.None;
                fill.transform.position = lightPositions[i];
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.23f, 0.27f);
        }

        private static Volume BuildPostProcessingVolume(Transform parent, VolumeProfile profile)
        {
            GameObject volumeGo = new GameObject("PostProcessVolume");
            volumeGo.transform.SetParent(parent);
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.profile = profile;
            return volume;
        }

        private static ScreenFlashOverlay BuildHud(RunDirector runDirector, BootstrapAssets assets)
        {
            GameObject canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            TMP_FontAsset fontAsset = ResolveLiberationSansFontAsset();
            TextMeshProUGUI timeText = CreateHudText(canvas.transform, "TimeLabel", fontAsset, new Vector2(20f, -20f), 48, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI roomText = CreateHudText(canvas.transform, "RoomLabel", fontAsset, new Vector2(20f, -78f), 26, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI enemiesText = CreateHudText(canvas.transform, "EnemiesLabel", fontAsset, new Vector2(20f, -112f), 26, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI deathText = CreateHudText(canvas.transform, "DeathsLabel", fontAsset, new Vector2(20f, -146f), 26, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI weaponText = CreateHudText(canvas.transform, "WeaponLabel", fontAsset, new Vector2(20f, -180f), 26, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI statusText = CreateHudText(canvas.transform, "StatusLabel", fontAsset, new Vector2(0f, -20f), 34, TextAlignmentOptions.Top);
            TextMeshProUGUI rewardText = CreateHudText(canvas.transform, "RewardLabel", fontAsset, new Vector2(0f, -84f), 30, TextAlignmentOptions.Top);

            RectTransform statusRect = statusText.rectTransform;
            statusRect.anchorMin = new Vector2(0.5f, 1f);
            statusRect.anchorMax = new Vector2(0.5f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -20f);

            RectTransform rewardRect = rewardText.rectTransform;
            rewardRect.anchorMin = new Vector2(0.5f, 1f);
            rewardRect.anchorMax = new Vector2(0.5f, 1f);
            rewardRect.pivot = new Vector2(0.5f, 1f);
            rewardRect.sizeDelta = new Vector2(1200f, 220f);
            rewardRect.anchoredPosition = new Vector2(0f, -84f);

            GameObject overlayGo = new GameObject("ScreenFlashOverlay", typeof(RectTransform), typeof(Image), typeof(ScreenFlashOverlay));
            overlayGo.transform.SetParent(canvas.transform, false);
            RectTransform overlayRect = overlayGo.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            Image overlayImage = overlayGo.GetComponent<Image>();
            overlayImage.color = new Color(1f, 0f, 0f, 0f);
            overlayImage.raycastTarget = false;

            RunHudView hud = canvasGo.AddComponent<RunHudView>();
            ConfigureSerialized(hud, so =>
            {
                so.FindProperty("runDirector").objectReferenceValue = runDirector;
                so.FindProperty("timeText").objectReferenceValue = timeText;
                so.FindProperty("deathText").objectReferenceValue = deathText;
                so.FindProperty("roomText").objectReferenceValue = roomText;
                so.FindProperty("enemiesText").objectReferenceValue = enemiesText;
                so.FindProperty("statusText").objectReferenceValue = statusText;
                so.FindProperty("weaponText").objectReferenceValue = weaponText;
                so.FindProperty("rewardText").objectReferenceValue = rewardText;
                so.FindProperty("shotgunWeapon").objectReferenceValue = assets.shotgun;
                so.FindProperty("chargeBeamWeapon").objectReferenceValue = assets.chargeBeam;
            });

            return overlayGo.GetComponent<ScreenFlashOverlay>();
        }

        private static TextMeshProUGUI CreateHudText(Transform parent, string name, TMP_FontAsset fontAsset, Vector2 anchoredPosition, int fontSize, TextAlignmentOptions alignment)
        {
            GameObject textGo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(parent, false);

            TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
            text.font = fontAsset;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            RectTransform rect = text.rectTransform;
            if (alignment == TextAlignmentOptions.Top || alignment == TextAlignmentOptions.Center)
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(1000f, 60f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(900f, 60f);
            }

            rect.anchoredPosition = anchoredPosition;
            return text;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static void ConfigureRunDirector(RunDirector director, BootstrapAssets assets, Transform[] spawnPoints)
        {
            ConfigureSerialized(director, so =>
            {
                SerializedProperty spawnPointsProperty = so.FindProperty("spawnPoints");
                spawnPointsProperty.arraySize = spawnPoints.Length;
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    spawnPointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
                }

                so.FindProperty("bossPrefab").objectReferenceValue = assets.bossPrefab;
                so.FindProperty("clockPickupPrefab").objectReferenceValue = assets.clockPickupPrefab;
                so.FindProperty("rewardRoomChance").floatValue = 0.2f;
                so.FindProperty("riskRoomChance").floatValue = 0.1f;
                so.FindProperty("guaranteedCombatRooms").intValue = 2;
                so.FindProperty("rewardClockPickups").intValue = 2;
                so.FindProperty("riskGuaranteedClockPickups").intValue = 1;
                so.FindProperty("riskEnemyMultiplier").floatValue = 1.35f;
                so.FindProperty("roomEnemyScalePerRoom").floatValue = 0.45f;
                so.FindProperty("lowTimePressureThreshold").floatValue = 20f;
                so.FindProperty("lowTimeEnemyBonusMultiplier").floatValue = 0.55f;
                so.FindProperty("maxAdditionalEnemiesFromPressure").intValue = 3;

                SerializedProperty enemyList = so.FindProperty("enemyPrefabs");
                enemyList.arraySize = 0;
                AddEnemySpawnEntry(enemyList, "Drone", assets.dronePrefab, 1, 2.4f);
                AddEnemySpawnEntry(enemyList, "Turret", assets.turretPrefab, 2, 1.5f);
                AddEnemySpawnEntry(enemyList, "Hunter", assets.hunterPrefab, 4, 1.8f);
                AddEnemySpawnEntry(enemyList, "Tank", assets.tankPrefab, 6, 1.1f);
            });
        }

        private static void AddEnemySpawnEntry(SerializedProperty listProperty, string label, GameObject prefab, int unlockRoom, float weight)
        {
            int index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);
            SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("label").stringValue = label;
            element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            element.FindPropertyRelative("unlockRoom").intValue = unlockRoom;
            element.FindPropertyRelative("weight").floatValue = weight;
        }

        private static void BindPlayerCamera(GameObject player, Camera sceneCamera)
        {
            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null)
            {
                return;
            }

            ConfigureSerialized(controller, so =>
            {
                so.FindProperty("aimCamera").objectReferenceValue = sceneCamera;
            });
        }

        private static void CreateWall(Transform parent, Material material, Vector3 position, Vector3 scale, string name)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static GameObject CreateBlock(Transform parent, Material material, Vector3 position, Vector3 scale, string name, bool withCollider = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.localScale = scale;
            Renderer renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            ConfigureEnvironmentRenderer(renderer, false);
            if (!withCollider)
            {
                Collider collider = block.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }

            return block;
        }

        private static void CreateDoorFrame(Transform parent, Material material, Vector3 position, Quaternion rotation, string name)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = position;
            root.transform.rotation = rotation;

            CreateBlockLocal(root.transform, material, new Vector3(-2.8f, 1.6f, 0f), new Vector3(0.7f, 3.2f, 0.7f), "LeftPillar");
            CreateBlockLocal(root.transform, material, new Vector3(2.8f, 1.6f, 0f), new Vector3(0.7f, 3.2f, 0.7f), "RightPillar");
            CreateBlockLocal(root.transform, material, new Vector3(0f, 3f, 0f), new Vector3(6f, 0.5f, 0.7f), "TopBeam");
            CreateBlockLocal(root.transform, material, new Vector3(0f, 0.15f, 0f), new Vector3(6f, 0.08f, 0.7f), "FloorStrip");
        }

        private static GameObject CreateBlockLocal(Transform parent, Material material, Vector3 localPosition, Vector3 localScale, string name, bool withCollider = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.localPosition = localPosition;
            block.transform.localScale = localScale;
            Renderer renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            ConfigureEnvironmentRenderer(renderer, false);
            if (!withCollider)
            {
                Collider collider = block.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }

            return block;
        }

        private static GameObject CreatePlayerProjectilePrefab(BootstrapAssets assets)
        {
            string path = $"{PrefabsFolder}/P_Projectile_Player.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Projectile_Player";
            root.transform.localScale = Vector3.one * 0.22f;
            root.GetComponent<Renderer>().sharedMaterial = assets.playerProjectileMaterial;
            root.GetComponent<Collider>().isTrigger = true;
            root.AddComponent<Projectile>();

            return SaveAsPrefab(path, root);
        }

        private static GameObject CreateEnemyProjectilePrefab(BootstrapAssets assets)
        {
            string path = $"{PrefabsFolder}/P_Projectile_Enemy.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Projectile_Enemy";
            root.transform.localScale = Vector3.one * 0.25f;
            root.GetComponent<Renderer>().sharedMaterial = assets.enemyProjectileMaterial;
            root.GetComponent<Collider>().isTrigger = true;
            root.AddComponent<EnemyProjectile>();

            return SaveAsPrefab(path, root);
        }

        private static ClockPickup CreateClockPickupPrefab(BootstrapAssets assets)
        {
            string path = $"{PrefabsFolder}/P_ClockPickup.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "ClockPickup";
            root.transform.localScale = new Vector3(0.65f, 0.2f, 0.65f);
            root.GetComponent<Renderer>().sharedMaterial = assets.pickupMaterial;
            root.GetComponent<Collider>().isTrigger = true;
            root.AddComponent<SpinBob>();
            root.AddComponent<ClockPickup>();

            GameObject prefab = SaveAsPrefab(path, root);
            return prefab.GetComponent<ClockPickup>();
        }

        private struct EnemyPrefabBuildParams
        {
            public string path;
            public string name;
            public PrimitiveType primitiveType;
            public Vector3 scale;
            public Material material;
            public float maxHealth;
            public bool destroyOnDeath;
            public bool addChaser;
            public float moveSpeed;
            public float stoppingDistance;
            public EnemyChaser.MovementMode chaserMoveMode;
            public float chaserOrbitPreferredDistance;
            public float chaserOrbitStrafeWeight;
            public float chaserOrbitApproachWeight;
            public float chaserOrbitDirectionFlipInterval;
            public float chaserOrbitSpeedMultiplier;
            public float chaserChargeSpeedMultiplier;
            public float chaserChargeApproachSpeedMultiplier;
            public float chaserChargeDuration;
            public float chaserChargeCooldown;
            public float chaserChargeMinRange;
            public float chaserChargeMaxRange;
            public float chaserChargeTurnMultiplier;
            public float chaserHeavySpeedMultiplier;
            public float chaserHeavyTurnMultiplier;
            public bool addContactDamage;
            public float contactDamage;
            public float contactCooldown;
            public bool addShooter;
            public GameObject enemyProjectilePrefab;
            public float shooterRate;
            public float shooterProjectileSpeed;
            public float shooterDamageAsTimeLoss;
            public float shooterRange;
            public float shooterMinimumRange;
            public bool shooterUsePredictiveAim;
            public float shooterPredictiveLeadSeconds;
            public float shooterWindupSeconds;
            public EnemyShooter.FireMode shooterFireMode;
            public int shooterBurstCount;
            public float shooterBurstInterval;
            public int shooterSpreadProjectiles;
            public float shooterSpreadAngle;
            public bool addBossPhaseController;
            public float bossPhase2Threshold;
            public float bossPhase3Threshold;
            public float bossPhase2MoveMultiplier;
            public float bossPhase3MoveMultiplier;
            public float bossPhase2FireRateMultiplier;
            public float bossPhase3FireRateMultiplier;
            public float impactKnockback;
            public float impactKillKnockback;
            public float impactStunDuration;
        }

        private static GameObject CreateEnemyPrefab(EnemyPrefabBuildParams parameters)
        {
            GameObject root = GameObject.CreatePrimitive(parameters.primitiveType);
            root.name = parameters.name;
            root.transform.localScale = parameters.scale;
            root.GetComponent<Renderer>().sharedMaterial = parameters.material;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

            Health health = root.AddComponent<Health>();
            ConfigureSerialized(health, so =>
            {
                so.FindProperty("maxHealth").floatValue = parameters.maxHealth;
                so.FindProperty("destroyOnDeath").boolValue = parameters.destroyOnDeath;
            });

            HitFlashSquash hitFlash = root.AddComponent<HitFlashSquash>();
            ConfigureSerialized(hitFlash, so =>
            {
                so.FindProperty("flashColor").colorValue = Color.white;
            });

            EnemyImpactResponse impactResponse = root.AddComponent<EnemyImpactResponse>();
            ConfigureSerialized(impactResponse, so =>
            {
                so.FindProperty("knockbackStrength").floatValue = parameters.impactKnockback > 0f ? parameters.impactKnockback : 2.4f;
                so.FindProperty("killKnockbackStrength").floatValue = parameters.impactKillKnockback > 0f ? parameters.impactKillKnockback : 3.8f;
                so.FindProperty("stunDuration").floatValue = parameters.impactStunDuration > 0f ? parameters.impactStunDuration : 0.08f;
                so.FindProperty("velocityDamping").floatValue = 0.36f;
                so.FindProperty("flattenToMovementPlane").boolValue = true;
            });

            if (parameters.addContactDamage)
            {
                GameObject damageZone = new GameObject("DamageZone");
                damageZone.transform.SetParent(root.transform);
                damageZone.transform.localPosition = Vector3.zero;
                damageZone.transform.localRotation = Quaternion.identity;
                damageZone.transform.localScale = Vector3.one;

                SphereCollider trigger = damageZone.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = Mathf.Max(parameters.scale.x, parameters.scale.z) * 0.42f;

                ContactTimeDamage contactDamage = damageZone.AddComponent<ContactTimeDamage>();
                ConfigureSerialized(contactDamage, so =>
                {
                    so.FindProperty("timeDamage").floatValue = parameters.contactDamage;
                    so.FindProperty("hitCooldown").floatValue = parameters.contactCooldown;
                });
            }

            if (parameters.addChaser)
            {
                EnemyChaser chaser = root.AddComponent<EnemyChaser>();
                ConfigureSerialized(chaser, so =>
                {
                    so.FindProperty("moveSpeed").floatValue = parameters.moveSpeed > 0f ? parameters.moveSpeed : 4f;
                    so.FindProperty("stoppingDistance").floatValue = parameters.stoppingDistance > 0f ? parameters.stoppingDistance : 1.2f;
                    so.FindProperty("moveMode").enumValueIndex = (int)parameters.chaserMoveMode;

                    if (parameters.chaserOrbitPreferredDistance > 0f)
                    {
                        so.FindProperty("orbitPreferredDistance").floatValue = parameters.chaserOrbitPreferredDistance;
                    }

                    if (parameters.chaserOrbitStrafeWeight > 0f)
                    {
                        so.FindProperty("orbitStrafeWeight").floatValue = parameters.chaserOrbitStrafeWeight;
                    }

                    if (parameters.chaserOrbitApproachWeight > 0f)
                    {
                        so.FindProperty("orbitApproachWeight").floatValue = parameters.chaserOrbitApproachWeight;
                    }

                    if (parameters.chaserOrbitDirectionFlipInterval > 0f)
                    {
                        so.FindProperty("orbitDirectionFlipInterval").floatValue = parameters.chaserOrbitDirectionFlipInterval;
                    }

                    if (parameters.chaserOrbitSpeedMultiplier > 0f)
                    {
                        so.FindProperty("orbitSpeedMultiplier").floatValue = parameters.chaserOrbitSpeedMultiplier;
                    }

                    if (parameters.chaserChargeSpeedMultiplier > 0f)
                    {
                        so.FindProperty("chargeSpeedMultiplier").floatValue = parameters.chaserChargeSpeedMultiplier;
                    }

                    if (parameters.chaserChargeApproachSpeedMultiplier > 0f)
                    {
                        so.FindProperty("chargeApproachSpeedMultiplier").floatValue = parameters.chaserChargeApproachSpeedMultiplier;
                    }

                    if (parameters.chaserChargeDuration > 0f)
                    {
                        so.FindProperty("chargeDuration").floatValue = parameters.chaserChargeDuration;
                    }

                    if (parameters.chaserChargeCooldown > 0f)
                    {
                        so.FindProperty("chargeCooldown").floatValue = parameters.chaserChargeCooldown;
                    }

                    if (parameters.chaserChargeMinRange > 0f)
                    {
                        so.FindProperty("chargeMinRange").floatValue = parameters.chaserChargeMinRange;
                    }

                    if (parameters.chaserChargeMaxRange > 0f)
                    {
                        so.FindProperty("chargeMaxRange").floatValue = parameters.chaserChargeMaxRange;
                    }

                    if (parameters.chaserChargeTurnMultiplier > 0f)
                    {
                        so.FindProperty("chargeTurnMultiplier").floatValue = parameters.chaserChargeTurnMultiplier;
                    }

                    if (parameters.chaserHeavySpeedMultiplier > 0f)
                    {
                        so.FindProperty("heavySpeedMultiplier").floatValue = parameters.chaserHeavySpeedMultiplier;
                    }

                    if (parameters.chaserHeavyTurnMultiplier > 0f)
                    {
                        so.FindProperty("heavyTurnMultiplier").floatValue = parameters.chaserHeavyTurnMultiplier;
                    }
                });
            }

            if (parameters.addShooter)
            {
                EnemyShooter shooter = root.AddComponent<EnemyShooter>();
                GameObject muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(root.transform);
                muzzle.transform.localPosition = new Vector3(0f, 0f, (parameters.scale.z * 0.6f) + 0.6f);

                EnemyProjectile enemyProjectile = parameters.enemyProjectilePrefab != null
                    ? parameters.enemyProjectilePrefab.GetComponent<EnemyProjectile>()
                    : null;

                ConfigureSerialized(shooter, so =>
                {
                    so.FindProperty("projectilePrefab").objectReferenceValue = enemyProjectile;
                    so.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
                    so.FindProperty("aimPivot").objectReferenceValue = root.transform;
                    so.FindProperty("fireRate").floatValue = parameters.shooterRate <= 0f ? 1.2f : parameters.shooterRate;
                    so.FindProperty("projectileSpeed").floatValue = parameters.shooterProjectileSpeed <= 0f ? 14f : parameters.shooterProjectileSpeed;
                    so.FindProperty("projectileLifetime").floatValue = 3f;
                    so.FindProperty("timeDamage").floatValue = parameters.shooterDamageAsTimeLoss <= 0f ? 2f : parameters.shooterDamageAsTimeLoss;
                    so.FindProperty("range").floatValue = parameters.shooterRange <= 0f ? 16f : parameters.shooterRange;
                    so.FindProperty("minimumRange").floatValue = Mathf.Max(0f, parameters.shooterMinimumRange);
                    so.FindProperty("usePredictiveAim").boolValue = parameters.shooterUsePredictiveAim;
                    if (parameters.shooterPredictiveLeadSeconds > 0f)
                    {
                        so.FindProperty("predictiveLeadSeconds").floatValue = parameters.shooterPredictiveLeadSeconds;
                    }

                    if (parameters.shooterWindupSeconds > 0f)
                    {
                        so.FindProperty("windupSeconds").floatValue = parameters.shooterWindupSeconds;
                    }

                    so.FindProperty("fireMode").enumValueIndex = (int)parameters.shooterFireMode;
                    if (parameters.shooterBurstCount > 0)
                    {
                        so.FindProperty("burstCount").intValue = parameters.shooterBurstCount;
                    }

                    if (parameters.shooterBurstInterval > 0f)
                    {
                        so.FindProperty("burstInterval").floatValue = parameters.shooterBurstInterval;
                    }

                    if (parameters.shooterSpreadProjectiles > 0)
                    {
                        so.FindProperty("spreadProjectiles").intValue = parameters.shooterSpreadProjectiles;
                    }

                    if (parameters.shooterSpreadAngle > 0f)
                    {
                        so.FindProperty("spreadAngle").floatValue = parameters.shooterSpreadAngle;
                    }
                });
            }

            if (parameters.addBossPhaseController)
            {
                EnemyBossPhaseController bossPhases = root.AddComponent<EnemyBossPhaseController>();
                ConfigureSerialized(bossPhases, so =>
                {
                    if (parameters.bossPhase2Threshold > 0f && parameters.bossPhase2Threshold < 1f)
                    {
                        so.FindProperty("phase2Threshold").floatValue = parameters.bossPhase2Threshold;
                    }

                    if (parameters.bossPhase3Threshold > 0f && parameters.bossPhase3Threshold < 1f)
                    {
                        so.FindProperty("phase3Threshold").floatValue = parameters.bossPhase3Threshold;
                    }

                    if (parameters.bossPhase2MoveMultiplier > 0f)
                    {
                        so.FindProperty("phase2MoveMultiplier").floatValue = parameters.bossPhase2MoveMultiplier;
                    }

                    if (parameters.bossPhase3MoveMultiplier > 0f)
                    {
                        so.FindProperty("phase3MoveMultiplier").floatValue = parameters.bossPhase3MoveMultiplier;
                    }

                    if (parameters.bossPhase2FireRateMultiplier > 0f)
                    {
                        so.FindProperty("phase2FireRateMultiplier").floatValue = parameters.bossPhase2FireRateMultiplier;
                    }

                    if (parameters.bossPhase3FireRateMultiplier > 0f)
                    {
                        so.FindProperty("phase3FireRateMultiplier").floatValue = parameters.bossPhase3FireRateMultiplier;
                    }
                });
            }

            return SaveAsPrefab(parameters.path, root);
        }

        private static GameObject CreatePlayerPrefab(BootstrapAssets assets)
        {
            string path = $"{PrefabsFolder}/P_Player.prefab";

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Player";
            root.GetComponent<Renderer>().sharedMaterial = assets.playerMaterial;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            PlayerController playerController = root.AddComponent<PlayerController>();
            HitFlashSquash playerHitFlash = root.AddComponent<HitFlashSquash>();
            ConfigureSerialized(playerHitFlash, so =>
            {
                so.FindProperty("flashColor").colorValue = new Color(1f, 0.38f, 0.32f, 1f);
            });

            GameObject weaponMount = new GameObject("WeaponMount");
            weaponMount.transform.SetParent(root.transform);
            weaponMount.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(weaponMount.transform);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.75f);

            WeaponController weaponController = weaponMount.AddComponent<WeaponController>();
            ConfigureSerialized(weaponController, so =>
            {
                Projectile projectileComponent = assets.playerProjectilePrefab != null
                    ? assets.playerProjectilePrefab.GetComponent<Projectile>()
                    : null;
                so.FindProperty("weapon").objectReferenceValue = assets.pulseRifle;
                so.FindProperty("fallbackProjectilePrefab").objectReferenceValue = projectileComponent;
                so.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
                so.FindProperty("alignProjectileHeightToOwner").boolValue = true;
                so.FindProperty("projectileHeightOffsetFromOwner").floatValue = 0f;
            });

            ConfigureSerialized(playerController, so =>
            {
                so.FindProperty("inputActions").objectReferenceValue = assets.inputActions;
                so.FindProperty("aimPivot").objectReferenceValue = root.transform;
                so.FindProperty("weaponController").objectReferenceValue = weaponController;
                so.FindProperty("controllerAimDeadzone").floatValue = 0.18f;
                so.FindProperty("controllerAimPrioritySeconds").floatValue = 0.45f;
                so.FindProperty("enableAimAssist").boolValue = true;
                so.FindProperty("aimAssistRadius").floatValue = 26f;
                so.FindProperty("mouseAimAssistAngle").floatValue = 5f;
                so.FindProperty("controllerAimAssistAngle").floatValue = 16f;
                so.FindProperty("mouseAimAssistStrength").floatValue = 0.22f;
                so.FindProperty("controllerAimAssistStrength").floatValue = 0.76f;
                so.FindProperty("stickyAimBonusAngle").floatValue = 6f;
                so.FindProperty("stickyAimDuration").floatValue = 0.24f;
            });

            return SaveAsPrefab(path, root);
        }

        private static WeaponDefinition CreateOrUpdateWeaponDefinition(
            string path,
            string weaponName,
            float fireRate,
            float damage,
            GameObject projectilePrefab,
            float projectileSpeed,
            float projectileLifetime)
        {
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            Projectile projectileComponent = projectilePrefab != null ? projectilePrefab.GetComponent<Projectile>() : null;
            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("weaponName").stringValue = weaponName;
            so.FindProperty("fireRate").floatValue = fireRate;
            so.FindProperty("damage").floatValue = damage;
            so.FindProperty("projectilePrefab").objectReferenceValue = projectileComponent;
            so.FindProperty("projectileSpeed").floatValue = projectileSpeed;
            so.FindProperty("projectileLifetime").floatValue = projectileLifetime;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static VolumeProfile CreateOrUpdateGameplayVolumeProfile(string path)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            Vignette vignette = GetOrCreateVolumeComponent<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.78f);
            vignette.rounded.Override(false);

            ChromaticAberration chromatic = GetOrCreateVolumeComponent<ChromaticAberration>(profile);
            chromatic.active = true;
            chromatic.intensity.Override(0.02f);

            LensDistortion lens = GetOrCreateVolumeComponent<LensDistortion>(profile);
            lens.active = true;
            lens.intensity.Override(0f);
            lens.scale.Override(1f);

            ColorAdjustments colorAdjustments = GetOrCreateVolumeComponent<ColorAdjustments>(profile);
            colorAdjustments.active = true;
            colorAdjustments.saturation.Override(0f);
            colorAdjustments.contrast.Override(6f);
            colorAdjustments.postExposure.Override(0f);
            colorAdjustments.colorFilter.Override(Color.white);

            Bloom bloom = GetOrCreateVolumeComponent<Bloom>(profile);
            bloom.active = true;
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(0.92f);
            bloom.scatter.Override(0.7f);

            profile.name = "VP_GameplayFeel";
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrCreateVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                return component;
            }

            return profile.Add<T>(true);
        }

        private static Material LoadMaterialFromPaths(IEnumerable<string> candidatePaths, string fallbackPath, Color fallbackColor)
        {
            if (candidatePaths != null)
            {
                foreach (string path in candidatePaths)
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material != null)
                    {
                        return material;
                    }
                }
            }

            return CreateOrUpdateMaterial(fallbackPath, fallbackColor);
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * 0.12f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateCheapSurfaceMaterial(string path, Material sourceMaterial, Color fallbackColor)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            Color color = ExtractMaterialColor(sourceMaterial, fallbackColor);
            Texture baseMap = ExtractBaseTexture(sourceMaterial);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", baseMap);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", baseMap);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0f);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Color ExtractMaterialColor(Material sourceMaterial, Color fallbackColor)
        {
            if (sourceMaterial == null)
            {
                return fallbackColor;
            }

            if (sourceMaterial.HasProperty("_BaseColor"))
            {
                return sourceMaterial.GetColor("_BaseColor");
            }

            if (sourceMaterial.HasProperty("_Color"))
            {
                return sourceMaterial.GetColor("_Color");
            }

            return fallbackColor;
        }

        private static Texture ExtractBaseTexture(Material sourceMaterial)
        {
            if (sourceMaterial == null)
            {
                return null;
            }

            if (sourceMaterial.HasProperty("_BaseMap"))
            {
                return sourceMaterial.GetTexture("_BaseMap");
            }

            if (sourceMaterial.HasProperty("_MainTex"))
            {
                return sourceMaterial.GetTexture("_MainTex");
            }

            return null;
        }

        private static void ConfigureEnvironmentRenderer(Renderer renderer, bool largeSurface)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;

            if (largeSurface)
            {
                renderer.rendererPriority = -10;
            }

            renderer.gameObject.isStatic = true;
        }

        private static GameObject SaveAsPrefab(string path, GameObject tempRoot)
        {
            RemoveMissingScriptsRecursive(tempRoot);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempRoot, path);
            UnityEngine.Object.DestroyImmediate(tempRoot);
            return prefab;
        }

        private static void RemoveMissingScriptsRecursive(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            foreach (Transform child in root.transform)
            {
                RemoveMissingScriptsRecursive(child.gameObject);
            }
        }

        private static int CountMissingScriptsRecursive(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }

            int missing = 0;
            Component[] components = root.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    missing++;
                }
            }

            foreach (Transform child in root.transform)
            {
                missing += CountMissingScriptsRecursive(child.gameObject);
            }

            return missing;
        }

        private static bool EnsureEnemyPrefabComponents(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            bool changed = false;
            if (root.GetComponent<Health>() != null && root.GetComponent<Rigidbody>() != null && root.GetComponent<EnemyImpactResponse>() == null)
            {
                EnemyImpactResponse impact = root.AddComponent<EnemyImpactResponse>();
                ConfigureEnemyImpactDefaults(root.name, impact);
                changed = true;
            }

            foreach (Transform child in root.transform)
            {
                if (EnsureEnemyPrefabComponents(child.gameObject))
                {
                    changed = true;
                }
            }

            return changed;
        }

        private static void ConfigureEnemyImpactDefaults(string objectName, EnemyImpactResponse impact)
        {
            if (impact == null)
            {
                return;
            }

            float knockback = 2.4f;
            float killKnockback = 3.8f;
            float stunDuration = 0.08f;

            if (!string.IsNullOrEmpty(objectName))
            {
                if (objectName.IndexOf("Drone", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    knockback = 2.6f;
                    killKnockback = 4.1f;
                    stunDuration = 0.1f;
                }
                else if (objectName.IndexOf("Turret", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    knockback = 2f;
                    killKnockback = 3.2f;
                    stunDuration = 0.08f;
                }
                else if (objectName.IndexOf("Hunter", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    knockback = 2.35f;
                    killKnockback = 3.8f;
                    stunDuration = 0.09f;
                }
                else if (objectName.IndexOf("Tank", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    knockback = 1.7f;
                    killKnockback = 2.8f;
                    stunDuration = 0.06f;
                }
                else if (objectName.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    knockback = 1.3f;
                    killKnockback = 2.3f;
                    stunDuration = 0.05f;
                }
            }

            ConfigureSerialized(impact, so =>
            {
                so.FindProperty("knockbackStrength").floatValue = knockback;
                so.FindProperty("killKnockbackStrength").floatValue = killKnockback;
                so.FindProperty("stunDuration").floatValue = stunDuration;
                so.FindProperty("velocityDamping").floatValue = 0.36f;
                so.FindProperty("flattenToMovementPlane").boolValue = true;
            });
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(x => x.path == scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static void EnsureGeneratedFolders()
        {
            EnsureFolder("Assets", "SixtyGenerated");
            EnsureFolder(GeneratedRoot, "Materials");
            EnsureFolder(GeneratedRoot, "Prefabs");
            EnsureFolder(GeneratedRoot, "ScriptableObjects");
            EnsureFolder(GeneratedRoot, "Scenes");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string path = $"{parent}/{folderName}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static InputActionAsset ResolveInputActionsAsset()
        {
            string[] knownPaths =
            {
                "Assets/InputSystem_Actions.inputactions",
                "Assets/Settings/InputSystem_Actions.inputactions"
            };

            for (int i = 0; i < knownPaths.Length; i++)
            {
                InputActionAsset found = AssetDatabase.LoadAssetAtPath<InputActionAsset>(knownPaths[i]);
                if (found != null)
                {
                    return found;
                }
            }

            string[] guids = AssetDatabase.FindAssets("InputSystem_Actions t:InputActionAsset");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            }

            return null;
        }

        private static TMP_FontAsset ResolveLiberationSansFontAsset()
        {
            const string preferredPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(preferredPath);
            if (fontAsset != null)
            {
                return fontAsset;
            }

            string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (fontAsset != null)
                {
                    return fontAsset;
                }
            }

            if (TMP_Settings.defaultFontAsset != null)
            {
                return TMP_Settings.defaultFontAsset;
            }

            TMP_Settings.LoadDefaultSettings();
            return TMP_Settings.defaultFontAsset;
        }

        private static void ConfigureSerialized(UnityEngine.Object target, Action<SerializedObject> configure)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            configure(serializedObject);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
#endif
