#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sixty.CameraSystem;
using Sixty.Combat;
using Sixty.Core;
using Sixty.Enemies;
using Sixty.Gameplay;
using Sixty.Player;
using Sixty.UI;
using Sixty.Rendering;
using Sixty.World;
using Ia.Core.Update;
using UIElements = UnityEngine.UIElements;
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
        private const string ImportedVisualsFolder = GeneratedRoot + "/ImportedVisuals";
        private const string ScenePath = ScenesFolder + "/Sixty_Playable.unity";
        private const float EnemyHoverHeight = 1.4f;
        private const string ReferenceProjectRoot = "D:/Coding/Game Development/GameJams/ScifiShooterTopDown";
        private const string DecimateMaterialsFolder = "Assets/Plugins/Decimate/Grid Master/URP/Materials";
        private const string ScalableMaterialsFolder = "Assets/Plugins/Scalable Grid Prototype Materials/Materials";
        private const string ScalableGroundMaterialsFolder = "Assets/Plugins/Scalable Grid Prototype Materials/Materials/Ground";
        private const float ArenaWallHalfExtent = 24f;
        private const float ArenaWallHeight = 3.2f;
        private const float ArenaWallThickness = 1f;
        private const float ArenaFloorSize = 44f;
        private const float DoorOpeningWidth = 6.8f;
        private const int RoomChainCount = 10;
        private const float RoomChainSpacing = 58f;
        private const float CorridorWidth = 9f;
        private const float CorridorLength = 10f;
        private const float VoidFloorY = -0.12f;
        private static readonly Vector3[] SpawnTemplatePositions =
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
            public Material guideMaterial;
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
            public RewardPickup rewardPickupPrefab;

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

        [MenuItem("Tools/Sixty/Rebuild Materials Only")]
        public static void RebuildMaterialsOnly()
        {
            EnsureGeneratedFolders();
            BootstrapAssets assets = CreateOrUpdateAssets();
            if (assets == null) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("SIXTY", "Materials rebuilt.", "OK");
        }

        [MenuItem("Tools/Sixty/Rebuild Prefabs Only")]
        public static void RebuildPrefabsOnly()
        {
            EnsureGeneratedFolders();
            BootstrapAssets assets = CreateOrUpdateAssets();
            if (assets == null) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("SIXTY", "Prefabs rebuilt (includes materials as dependencies).", "OK");
        }

        [MenuItem("Tools/Sixty/Rebuild Enemy Prefabs Only")]
        public static void RebuildEnemyPrefabsOnly()
        {
            EnsureGeneratedFolders();
            BootstrapAssets assets = CreateOrUpdateAssets();
            if (assets == null) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("SIXTY", "Enemy prefabs rebuilt with current health/stats.", "OK");
        }

        [MenuItem("Tools/Sixty/Update Active Scene RunDirector")]
        public static void UpdateActiveSceneRunDirector()
        {
            RunDirector director = UnityEngine.Object.FindFirstObjectByType<RunDirector>();
            if (director == null)
            {
                EditorUtility.DisplayDialog("SIXTY", "No RunDirector found in active scene.", "OK");
                return;
            }

            EnsureGeneratedFolders();
            BootstrapAssets assets = CreateOrUpdateAssets();
            if (assets == null) return;
            ConfigureRunDirectorBase(director, assets);
            EditorUtility.SetDirty(director);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("SIXTY", "RunDirector updated with current enemy prefabs and settings.", "OK");
        }

        private static BootstrapAssets CreateOrUpdateAssets()
        {
            InputActionAsset inputActions = ResolveInputActionsAsset();
            if (inputActions == null)
            {
                Debug.LogError("Input actions asset not found. Expected an InputActionAsset named 'InputSystem_Actions'.");
                return null;
            }

            ImportReferenceVisualAssets();

            BootstrapAssets assets = new BootstrapAssets
            {
                inputActions = inputActions,
                floorMaterial = LoadMaterialFromPaths(
                    new[]
                    {
                        $"{ImportedVisualsFolder}/Shader Graphs_Ground.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-03-URP.mat",
                        $"{ScalableGroundMaterialsFolder}/DarkGray_Ground_Prototype.mat"
                    },
                    $"{MaterialsFolder}/M_Floor.mat",
                    new Color(0.08f, 0.09f, 0.12f)),
                floorTrimMaterial = LoadMaterialFromPaths(
                    new[]
                    {
                        $"{DecimateMaterialsFolder}/GM-Grid-02-URP.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-23-URP.mat",
                        $"{ImportedVisualsFolder}/Shader Graphs_Ground.mat",
                        $"{ScalableGroundMaterialsFolder}/Gray_Ground_Prototype.mat"
                    },
                    $"{MaterialsFolder}/M_FloorTrim.mat",
                    new Color(0.12f, 0.14f, 0.18f)),
                wallMaterial = LoadMaterialFromPaths(
                    new[]
                    {
                        $"{ImportedVisualsFolder}/Wall.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-19-URP.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-11-URP.mat",
                        $"{ScalableMaterialsFolder}/Dark_Gray_Prototype.mat"
                    },
                    $"{MaterialsFolder}/M_Wall.mat",
                    new Color(0.05f, 0.06f, 0.09f)),
                coverMaterial = LoadMaterialFromPaths(
                    new[]
                    {
                        $"{DecimateMaterialsFolder}/GM-Grid-21-URP 1.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-21-URP.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-21-URP 2.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-19-URP.mat",
                        $"{ScalableMaterialsFolder}/Gray_Prototype.mat"
                    },
                    $"{MaterialsFolder}/M_Cover.mat",
                    new Color(0.07f, 0.08f, 0.11f)),
                accentMaterial = LoadMaterialFromPaths(
                    new[]
                    {
                        $"{DecimateMaterialsFolder}/GM-Grid-21-URP.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-21-URP 1.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-23-URP.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-19-URP.mat"
                    },
                    $"{MaterialsFolder}/M_Accent.mat",
                    new Color(0.05f, 0.65f, 0.72f)),
                guideMaterial = LoadMaterialFromPaths(
                    new[]
                    {
                        $"{DecimateMaterialsFolder}/GM-Grid-23-URP.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-21-URP 1.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-21-URP.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-19-URP.mat",
                        $"{DecimateMaterialsFolder}/GM-Grid-02-URP.mat"
                    },
                    $"{MaterialsFolder}/M_Guide.mat",
                    new Color(0.05f, 0.6f, 0.68f)),
                // Use generated standard URP materials for gameplay actors/projectiles so hit flash tinting is always visible.
                // Player: bright cyan to contrast enemy magenta
                playerMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Player.mat", new Color(0.0f, 0.95f, 0.9f)),
                playerProjectileMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_PlayerProjectile.mat", new Color(0.3f, 1f, 0.95f)),
                // Enemies: hot magenta/pink spectrum
                enemyProjectileMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_EnemyProjectile.mat", new Color(1f, 0.1f, 0.55f)),
                pickupMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_ClockPickup.mat", new Color(1f, 0.85f, 0.15f)),
                droneMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Drone.mat", new Color(1f, 0.08f, 0.65f)),
                turretMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Turret.mat", new Color(0.95f, 0.15f, 0.75f)),
                hunterMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Hunter.mat", new Color(1f, 0.12f, 0.58f)),
                tankMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Tank.mat", new Color(0.88f, 0.1f, 0.5f)),
                bossMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_Boss.mat", new Color(1f, 0.05f, 0.42f))
            };

            assets.floorPerformanceMaterial = CreateOrUpdateCheapSurfaceMaterial(
                $"{MaterialsFolder}/M_Floor_Perf.mat",
                assets.floorMaterial,
                new Color(0.08f, 0.09f, 0.12f));

            assets.floorTrimPerformanceMaterial = CreateOrUpdateCheapSurfaceMaterial(
                $"{MaterialsFolder}/M_FloorTrim_Perf.mat",
                assets.floorTrimMaterial,
                new Color(0.12f, 0.14f, 0.18f));

            assets.gameplayVolumeProfile = CreateOrUpdateGameplayVolumeProfile($"{ScriptableFolder}/VP_GameplayFeel.asset");

            assets.playerProjectilePrefab = CreatePlayerProjectilePrefab(assets);
            assets.enemyProjectilePrefab = CreateEnemyProjectilePrefab(assets);
            assets.clockPickupPrefab = CreateClockPickupPrefab(assets);
            assets.rewardPickupPrefab = CreateRewardPickupPrefab(assets);

            assets.pulseRifle = CreateOrUpdateWeaponDefinition(
                $"{ScriptableFolder}/W_PulseRifle.asset",
                "Pulse Rifle",
                8f,
                12f,
                assets.playerProjectilePrefab,
                45f,
                2f,
                1,
                0f);

            assets.shotgun = CreateOrUpdateWeaponDefinition(
                $"{ScriptableFolder}/W_Shotgun.asset",
                "Shotgun",
                1.6f,
                10f,
                assets.playerProjectilePrefab,
                36f,
                0.7f,
                8,
                18f);

            assets.chargeBeam = CreateOrUpdateWeaponDefinition(
                $"{ScriptableFolder}/W_ChargeBeam.asset",
                "Charge Beam",
                1f,
                80f,
                assets.playerProjectilePrefab,
                60f,
                1.25f,
                1,
                0f);

            assets.playerPrefab = CreatePlayerPrefab(assets);
            assets.dronePrefab = CreateEnemyPrefab(new EnemyPrefabBuildParams
            {
                path = $"{PrefabsFolder}/P_Enemy_Drone.prefab",
                name = "Enemy_Drone",
                primitiveType = PrimitiveType.Sphere,
                scale = new Vector3(1.1f, 1.1f, 1.1f),
                material = assets.droneMaterial,
                maxHealth = 15f,
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
                maxHealth = 25f,
                destroyOnDeath = true,
                addChaser = true,
                moveSpeed = 2.6f,
                stoppingDistance = 8.5f,
                chaserMoveMode = EnemyChaser.MovementMode.DirectChase,
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
                maxHealth = 20f,
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
                maxHealth = 60f,
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
                maxHealth = 400f,
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
            Material wallMaterial = assets.wallMaterial != null ? assets.wallMaterial : assets.coverMaterial;
            Material trimMaterial = assets.floorTrimMaterial != null ? assets.floorTrimMaterial : assets.floorMaterial;
            Material guideMaterial = assets.guideMaterial != null ? assets.guideMaterial : assets.accentMaterial;
            Material floorMaterial = assets.floorMaterial != null ? assets.floorMaterial : trimMaterial;

            // World root with RuntimeArenaBuilder
            GameObject world = new GameObject("World");
            RuntimeArenaBuilder arenaBuilder = world.AddComponent<RuntimeArenaBuilder>();
            // Wall face material: warm tan/beige for the reference layered look
            Material wallFaceMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_WallFace.mat", new Color(0.48f, 0.42f, 0.38f));
            if (wallFaceMaterial != null)
            {
                // Subtle emission for architectural surfaces, not gameplay glow
                if (wallFaceMaterial.HasProperty("_EmissionColor"))
                    wallFaceMaterial.SetColor("_EmissionColor", new Color(0.48f, 0.42f, 0.38f) * 0.4f);
                if (wallFaceMaterial.HasProperty("_Smoothness"))
                    wallFaceMaterial.SetFloat("_Smoothness", 0.35f);
                if (wallFaceMaterial.HasProperty("_Metallic"))
                    wallFaceMaterial.SetFloat("_Metallic", 0.05f);
                EditorUtility.SetDirty(wallFaceMaterial);
            }

            ConfigureSerialized(arenaBuilder, so =>
            {
                so.FindProperty("roomCount").intValue = RoomChainCount;
                so.FindProperty("roomSpacing").floatValue = RoomChainSpacing;
                so.FindProperty("wallHalfExtent").floatValue = ArenaWallHalfExtent;
                so.FindProperty("wallHeight").floatValue = ArenaWallHeight;
                so.FindProperty("wallThickness").floatValue = ArenaWallThickness;
                so.FindProperty("doorOpeningWidth").floatValue = DoorOpeningWidth;
                so.FindProperty("corridorWidth").floatValue = CorridorWidth;
                so.FindProperty("layoutSeed").intValue = 42;
                so.FindProperty("floorMaterial").objectReferenceValue = floorMaterial;
                so.FindProperty("wallMaterial").objectReferenceValue = wallMaterial;
                so.FindProperty("wallFaceMaterial").objectReferenceValue = wallFaceMaterial;
                so.FindProperty("trimMaterial").objectReferenceValue = trimMaterial;
                so.FindProperty("accentMaterial").objectReferenceValue = assets.accentMaterial;
                so.FindProperty("guideMaterial").objectReferenceValue = guideMaterial;
                so.FindProperty("wallClusterDensity").intValue = 14;
            });

            RoomLayoutDirector roomLayoutDirector = world.AddComponent<RoomLayoutDirector>();
            ConfigureSerialized(roomLayoutDirector, so =>
            {
                so.FindProperty("useProceduralGeneration").boolValue = true;
                so.FindProperty("coverMaterial").objectReferenceValue = assets.coverMaterial;
                so.FindProperty("accentMaterial").objectReferenceValue = assets.accentMaterial;
                so.FindProperty("arenaHalfExtent").floatValue = 20f;
                so.FindProperty("roomEdgePadding").floatValue = 3.2f;
                so.FindProperty("combatObstacleMin").intValue = 7;
                so.FindProperty("combatObstacleMax").intValue = 12;
                so.FindProperty("riskObstacleMin").intValue = 11;
                so.FindProperty("riskObstacleMax").intValue = 16;
                so.FindProperty("deterministicSelectionByRoom").boolValue = true;
            });

            GroundGridInfluenceController groundGrid = world.AddComponent<GroundGridInfluenceController>();
            ConfigureSerialized(groundGrid, so =>
            {
                so.FindProperty("maxTrackedEnemies").intValue = 96;
                so.FindProperty("refreshInterval").floatValue = 0.08f;
                so.FindProperty("influenceRadius").floatValue = 3.6f;
                so.FindProperty("falloffExponent").floatValue = 1.4f;
                so.FindProperty("applyPlayerHighlight").boolValue = true;
            });

            // Void city compute shader generator
            ComputeShader voidCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/VoidCity.compute");
            Shader voidShader = Shader.Find("Sixty/VoidCityInstanced");
            if (voidCompute != null && voidShader != null)
            {
                Material voidInstanceMaterial = new Material(voidShader);
                voidInstanceMaterial.enableInstancing = true;
                voidInstanceMaterial.SetColor("_BaseColor", new Color(0.06f, 0.07f, 0.09f, 1f));
                voidInstanceMaterial.SetColor("_TopColor", new Color(0.12f, 0.14f, 0.18f, 1f));
                voidInstanceMaterial.SetColor("_GlowColor", new Color(0.4f, 0.85f, 0.95f, 1f));
                voidInstanceMaterial.SetFloat("_GlowIntensity", 4.0f);
                voidInstanceMaterial.SetFloat("_AmbientStrength", 0.35f);
                voidInstanceMaterial.SetFloat("_NoiseScale", 0.04f);
                voidInstanceMaterial.SetFloat("_NoiseStrength", 0.45f);
                voidInstanceMaterial.SetFloat("_CavityWidth", 0.06f);
                voidInstanceMaterial.SetFloat("_CavityStrength", 0.55f);
                voidInstanceMaterial.SetColor("_CavityColor", new Color(0.02f, 0.025f, 0.035f, 1f));
                voidInstanceMaterial.SetFloat("_TriangleScale", 0.5f);
                voidInstanceMaterial.SetFloat("_TriangleDensity", 0.06f);
                voidInstanceMaterial.SetFloat("_TriangleBrightness", 1.3f);
                AssetDatabase.CreateAsset(voidInstanceMaterial, $"{MaterialsFolder}/M_VoidCity.mat");

                GameObject voidGo = new GameObject("VoidCityGenerator");
                voidGo.transform.SetParent(world.transform, false);
                VoidCityGenerator voidGen = voidGo.AddComponent<VoidCityGenerator>();
                ConfigureSerialized(voidGen, so =>
                {
                    so.FindProperty("computeShader").objectReferenceValue = voidCompute;
                    so.FindProperty("instanceMaterial").objectReferenceValue = voidInstanceMaterial;
                    so.FindProperty("cellSize").floatValue = 2.5f;
                    so.FindProperty("baseY").floatValue = -0.2f;
                    so.FindProperty("maxInstances").intValue = 32768;
                    so.FindProperty("extentX").floatValue = 300f;
                    so.FindProperty("extentZ").floatValue = 200f;
                });
            }

            // Gameplay root
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
            ConfigureRunDirectorBase(runDirector, assets);

            ConfigureSerialized(roomLayoutDirector, so =>
            {
                so.FindProperty("runDirector").objectReferenceValue = runDirector;
            });

            // Runtime scene initializer wires everything at Awake
            VoidCityGenerator voidGenRef = world.GetComponentInChildren<VoidCityGenerator>();
            RuntimeSceneInitializer initializer = world.AddComponent<RuntimeSceneInitializer>();
            ConfigureSerialized(initializer, so =>
            {
                so.FindProperty("arenaBuilder").objectReferenceValue = arenaBuilder;
                so.FindProperty("runDirector").objectReferenceValue = runDirector;
                so.FindProperty("roomLayoutDirector").objectReferenceValue = roomLayoutDirector;
                so.FindProperty("groundGrid").objectReferenceValue = groundGrid;
                so.FindProperty("voidCityGenerator").objectReferenceValue = voidGenRef;
            });

            // Player
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(assets.playerPrefab);
            player.name = "Player";
            player.tag = "Player";
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
            ScreenFlashOverlay overlay = BuildHud(runDirector, assets, mainCamera);
            ConfigureSerialized(gameFeel, so =>
            {
                so.FindProperty("cameraFollow").objectReferenceValue = follow;
                so.FindProperty("screenFlashOverlay").objectReferenceValue = overlay;
                so.FindProperty("postProcessFeedback").objectReferenceValue = postProcessFeedback;
                so.FindProperty("sfxSource").objectReferenceValue = feelAudio;
                so.FindProperty("optimizeArenaSurfaces").boolValue = false;
            });
            ConfigureSerialized(postProcessFeedback, so =>
            {
                so.FindProperty("volume").objectReferenceValue = gameplayVolume;
            });

            BuildLighting();
            EnsureEventSystem();

            SceneManager.SetActiveScene(scene);
        }

        private static RoomLayoutDirector BuildArena(Transform parent, BootstrapAssets assets, out RunExitDoor[] exitDoors, out Renderer[] groundRenderers, out Transform[] roomAnchors)
        {
            Material groundMaterial = assets.floorMaterial != null
                ? assets.floorMaterial
                : (assets.floorPerformanceMaterial != null ? assets.floorPerformanceMaterial : assets.floorTrimMaterial);
            Material wallMaterial = assets.wallMaterial != null ? assets.wallMaterial : assets.coverMaterial;
            Material guideMaterial = assets.guideMaterial != null ? assets.guideMaterial : assets.accentMaterial;
            Material trimMaterial = assets.floorTrimMaterial != null ? assets.floorTrimMaterial : groundMaterial;
            Material accentMaterial = assets.accentMaterial != null ? assets.accentMaterial : guideMaterial;

            Material whiteGlowMaterial = CreateOrUpdateMaterial($"{MaterialsFolder}/M_VoidGlowWhite.mat", Color.white);
            if (whiteGlowMaterial != null && whiteGlowMaterial.HasProperty("_EmissionColor"))
            {
                whiteGlowMaterial.SetColor("_EmissionColor", Color.white * 2.4f);
                EditorUtility.SetDirty(whiteGlowMaterial);
            }

            List<Renderer> gatheredGroundRenderers = new List<Renderer>(RoomChainCount * 4);
            List<RunExitDoor> gatheredDoors = new List<RunExitDoor>(RoomChainCount);

            Transform roomRoot = new GameObject("RoomChain").transform;
            roomRoot.SetParent(parent, false);
            Transform corridorRoot = new GameObject("Corridors").transform;
            corridorRoot.SetParent(parent, false);
            Transform gateRoot = new GameObject("WallGates").transform;
            gateRoot.SetParent(parent, false);

            roomAnchors = new Transform[RoomChainCount];
            for (int i = 0; i < RoomChainCount; i++)
            {
                Vector3 center = new Vector3(i * RoomChainSpacing, 0f, 0f);
                GameObject anchor = new GameObject($"RoomAnchor_{i + 1:00}");
                anchor.transform.SetParent(parent, false);
                anchor.transform.position = center;
                roomAnchors[i] = anchor.transform;

                GameObject roomFloor = CreateBlock(
                    roomRoot,
                    groundMaterial,
                    center + new Vector3(0f, 0.055f, 0f),
                    new Vector3(ArenaFloorSize, 0.1f, ArenaFloorSize),
                    $"RoomFloor_{i + 1:00}",
                    false);
                Renderer roomFloorRenderer = roomFloor.GetComponent<Renderer>();
                ConfigureEnvironmentRenderer(roomFloorRenderer, true);
                gatheredGroundRenderers.Add(roomFloorRenderer);

                CreateRoomGuideLines(roomRoot, center, guideMaterial);
                BuildRoomSpawnMarkers(roomRoot, center, guideMaterial);
                BuildRoomPerimeter(roomRoot, center, wallMaterial, i > 0, i < RoomChainCount - 1);
                BuildRoomStaticDetail(roomRoot, center, trimMaterial, wallMaterial, i);

                if (i < RoomChainCount - 1)
                {
                    Vector3 nextCenter = new Vector3((i + 1) * RoomChainSpacing, 0f, 0f);
                    Renderer corridorRenderer = BuildCorridor(corridorRoot, center, nextCenter, groundMaterial, wallMaterial, guideMaterial);
                    if (corridorRenderer != null)
                    {
                        gatheredGroundRenderers.Add(corridorRenderer);
                    }

                    Vector3 gatePos = center + new Vector3(ArenaWallHalfExtent - (ArenaWallThickness * 0.48f), 0f, 0f);
                    RunExitDoor gate = CreateWallGate(
                        gateRoot,
                        wallMaterial,
                        accentMaterial,
                        $"Gate_{i + 1:00}_To_{i + 2:00}",
                        RunExitDoorDirection.East,
                        gatePos,
                        Quaternion.Euler(0f, 90f, 0f));
                    if (gate != null)
                    {
                        gatheredDoors.Add(gate);
                    }
                }
            }

            float chainLength = ((RoomChainCount - 1) * RoomChainSpacing) + ArenaFloorSize + 42f;
            float chainWidth = ArenaFloorSize + 46f;
            Vector3 voidCenter = new Vector3(((RoomChainCount - 1) * RoomChainSpacing) * 0.5f, VoidFloorY, 0f);
            GameObject baseFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            baseFloor.name = "VoidFloor";
            baseFloor.transform.SetParent(parent);
            baseFloor.transform.position = voidCenter;
            baseFloor.transform.localScale = new Vector3(chainLength / 10f, 1f, chainWidth / 10f);
            Renderer baseFloorRenderer = baseFloor.GetComponent<Renderer>();
            baseFloorRenderer.sharedMaterial = trimMaterial;
            ConfigureEnvironmentRenderer(baseFloorRenderer, true);
            gatheredGroundRenderers.Add(baseFloorRenderer);

            BuildOutsideVoidGeometry(parent, trimMaterial, wallMaterial, whiteGlowMaterial, chainLength, chainWidth, voidCenter.x);

            exitDoors = gatheredDoors.ToArray();
            groundRenderers = gatheredGroundRenderers.ToArray();

            RoomLayoutDirector director = parent.GetComponent<RoomLayoutDirector>();
            if (director == null)
            {
                director = parent.gameObject.AddComponent<RoomLayoutDirector>();
            }

            ConfigureSerialized(director, so =>
            {
                so.FindProperty("useProceduralGeneration").boolValue = true;
                so.FindProperty("coverMaterial").objectReferenceValue = assets.coverMaterial;
                so.FindProperty("accentMaterial").objectReferenceValue = assets.accentMaterial;
                so.FindProperty("arenaHalfExtent").floatValue = 20f;
                so.FindProperty("roomEdgePadding").floatValue = 3.2f;
                so.FindProperty("combatObstacleMin").intValue = 7;
                so.FindProperty("combatObstacleMax").intValue = 12;
                so.FindProperty("riskObstacleMin").intValue = 11;
                so.FindProperty("riskObstacleMax").intValue = 16;
                so.FindProperty("deterministicSelectionByRoom").boolValue = true;
            });

            return director;
        }

        private static void SetInitialLayoutState(
            GameObject[] combatLayouts,
            GameObject[] rewardLayouts,
            GameObject[] riskLayouts,
            GameObject[] bossLayouts)
        {
            SetLayoutGroupActive(combatLayouts, false);
            SetLayoutGroupActive(rewardLayouts, false);
            SetLayoutGroupActive(riskLayouts, false);
            SetLayoutGroupActive(bossLayouts, false);

            if (combatLayouts != null && combatLayouts.Length > 0 && combatLayouts[0] != null)
            {
                combatLayouts[0].SetActive(true);
            }
        }

        private static void BuildRoomPerimeter(Transform parent, Vector3 center, Material wallMaterial, bool openWest, bool openEast)
        {
            CreateWall(parent, wallMaterial, center + new Vector3(0f, 1.6f, ArenaWallHalfExtent), new Vector3(ArenaWallHalfExtent * 2f, ArenaWallHeight, ArenaWallThickness), $"Wall_North_{center.x:0}");
            CreateWall(parent, wallMaterial, center + new Vector3(0f, 1.6f, -ArenaWallHalfExtent), new Vector3(ArenaWallHalfExtent * 2f, ArenaWallHeight, ArenaWallThickness), $"Wall_South_{center.x:0}");

            if (openWest)
            {
                BuildSideOpeningWall(parent, center, -1f, wallMaterial, $"Wall_West_{center.x:0}");
            }
            else
            {
                CreateWall(parent, wallMaterial, center + new Vector3(-ArenaWallHalfExtent, 1.6f, 0f), new Vector3(ArenaWallThickness, ArenaWallHeight, ArenaWallHalfExtent * 2f), $"Wall_West_{center.x:0}");
            }

            if (openEast)
            {
                BuildSideOpeningWall(parent, center, 1f, wallMaterial, $"Wall_East_{center.x:0}");
            }
            else
            {
                CreateWall(parent, wallMaterial, center + new Vector3(ArenaWallHalfExtent, 1.6f, 0f), new Vector3(ArenaWallThickness, ArenaWallHeight, ArenaWallHalfExtent * 2f), $"Wall_East_{center.x:0}");
            }
        }

        private static void BuildSideOpeningWall(Transform parent, Vector3 center, float sideSign, Material wallMaterial, string namePrefix)
        {
            float segmentLength = Mathf.Max(2f, ((ArenaWallHalfExtent * 2f) - DoorOpeningWidth) * 0.5f);
            float segmentOffset = (DoorOpeningWidth * 0.5f) + (segmentLength * 0.5f);
            float wallX = center.x + (ArenaWallHalfExtent * sideSign);

            CreateWall(parent, wallMaterial, new Vector3(wallX, 1.6f, center.z + segmentOffset), new Vector3(ArenaWallThickness, ArenaWallHeight, segmentLength), $"{namePrefix}_Top");
            CreateWall(parent, wallMaterial, new Vector3(wallX, 1.6f, center.z - segmentOffset), new Vector3(ArenaWallThickness, ArenaWallHeight, segmentLength), $"{namePrefix}_Bottom");
            CreateBlock(parent, wallMaterial, new Vector3(wallX, ArenaWallHeight - 0.24f, center.z), new Vector3(ArenaWallThickness, 0.5f, DoorOpeningWidth + 1.1f), $"{namePrefix}_Header");
        }

        private static Renderer BuildCorridor(Transform parent, Vector3 roomA, Vector3 roomB, Material floorMaterial, Material wallMaterial, Material guideMaterial)
        {
            Vector3 center = (roomA + roomB) * 0.5f;
            float corridorLength = Mathf.Max(CorridorLength, Vector3.Distance(roomA, roomB) - (ArenaWallHalfExtent * 2f));
            GameObject floor = CreateBlock(parent, floorMaterial, center + new Vector3(0f, 0.05f, 0f), new Vector3(corridorLength, 0.08f, CorridorWidth), $"Corridor_{roomA.x:0}_{roomB.x:0}", false);
            Renderer floorRenderer = floor.GetComponent<Renderer>();
            ConfigureEnvironmentRenderer(floorRenderer, true);

            CreateBlock(parent, guideMaterial, center + new Vector3(0f, 0.096f, 0f), new Vector3(Mathf.Max(1f, corridorLength - 0.8f), 0.02f, 0.34f), $"CorridorGuide_{roomA.x:0}_{roomB.x:0}", false);
            CreateWall(parent, wallMaterial, center + new Vector3(0f, 1.4f, (CorridorWidth * 0.5f) + 0.4f), new Vector3(corridorLength, 2.8f, 0.8f), $"CorridorWall_N_{roomA.x:0}");
            CreateWall(parent, wallMaterial, center + new Vector3(0f, 1.4f, -((CorridorWidth * 0.5f) + 0.4f)), new Vector3(corridorLength, 2.8f, 0.8f), $"CorridorWall_S_{roomA.x:0}");
            return floorRenderer;
        }

        private static RunExitDoor CreateWallGate(
            Transform parent,
            Material wallMaterial,
            Material accentMaterial,
            string name,
            RunExitDoorDirection direction,
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;
            root.transform.rotation = worldRotation;

            GameObject slabRoot = new GameObject("GateWall");
            slabRoot.transform.SetParent(root.transform, false);
            slabRoot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            slabRoot.transform.localRotation = Quaternion.identity;

            GameObject wall = CreateBlockLocal(slabRoot.transform, wallMaterial, Vector3.zero, new Vector3(DoorOpeningWidth + 0.3f, ArenaWallHeight, ArenaWallThickness + 0.12f), "WallSegment");
            GameObject line = CreateBlockLocal(slabRoot.transform, accentMaterial, new Vector3(0f, -1.53f, -0.52f), new Vector3(DoorOpeningWidth, 0.04f, 0.1f), "GroundLine", false);

            GameObject trigger = new GameObject("EntryTrigger");
            trigger.transform.SetParent(root.transform, false);
            trigger.transform.localPosition = new Vector3(0f, 1.2f, -1.25f);
            BoxCollider triggerCollider = trigger.AddComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector3(DoorOpeningWidth + 1.2f, 2.5f, 2.7f);

            RunExitDoor door = trigger.AddComponent<RunExitDoor>();
            ConfigureSerialized(door, so =>
            {
                so.FindProperty("direction").enumValueIndex = (int)direction;
                so.FindProperty("doorVisual").objectReferenceValue = slabRoot.transform;
                so.FindProperty("entryTrigger").objectReferenceValue = triggerCollider;
                SerializedProperty renderers = so.FindProperty("targetRenderers");
                renderers.arraySize = 2;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = wall.GetComponent<Renderer>();
                renderers.GetArrayElementAtIndex(1).objectReferenceValue = line.GetComponent<Renderer>();
                so.FindProperty("openHeight").floatValue = 4.5f;
                so.FindProperty("animationSpeed").floatValue = 8.2f;
                so.FindProperty("lockedColor").colorValue = new Color(1f, 0.22f, 0.62f, 1f);
                so.FindProperty("unlockedColor").colorValue = new Color(0.24f, 1f, 0.92f, 1f);
            });

            return door;
        }

        private static void CreateRoomGuideLines(Transform parent, Vector3 center, Material guideMaterial)
        {
            CreateBlock(parent, guideMaterial, center + new Vector3(0f, 0.108f, 0f), new Vector3(2f, 0.02f, 26f), $"Guide_NS_{center.x:0}", false);
            CreateBlock(parent, guideMaterial, center + new Vector3(0f, 0.108f, 0f), new Vector3(26f, 0.02f, 2f), $"Guide_EW_{center.x:0}", false);
        }

        private static void BuildRoomSpawnMarkers(Transform parent, Vector3 center, Material markerMaterial)
        {
            for (int i = 0; i < SpawnTemplatePositions.Length; i++)
            {
                Vector3 local = SpawnTemplatePositions[i];
                Vector3 pos = center + new Vector3(local.x, 0.028f, local.z);
                CreateBlock(parent, markerMaterial, pos, new Vector3(0.7f, 0.03f, 0.7f), $"SpawnMarker_{center.x:0}_{i:00}", false);
            }
        }

        private static void BuildRoomStaticDetail(Transform parent, Vector3 center, Material trimMaterial, Material wallMaterial, int roomIndex)
        {
            float edge = ArenaWallHalfExtent - 2f;
            CreateBlock(parent, trimMaterial, center + new Vector3(-edge, 1.1f, -edge), new Vector3(1.4f, 2.2f, 1.4f), $"EdgeCluster_{roomIndex:00}_A", false);
            CreateBlock(parent, trimMaterial, center + new Vector3(edge, 0.85f, edge), new Vector3(1.2f, 1.7f, 1.2f), $"EdgeCluster_{roomIndex:00}_B", false);
            CreateBlock(parent, wallMaterial, center + new Vector3(edge - 2.2f, 0.5f, -edge + 1.8f), new Vector3(1f, 1f, 1f), $"EdgeCluster_{roomIndex:00}_C", false);
        }

        private static void BuildOutsideVoidGeometry(Transform parent, Material voidMaterial, Material monolithMaterial, Material glowMaterial, float chainLength, float chainWidth, float centerX)
        {
            Transform root = new GameObject("OutsideVoidGeometry").transform;
            root.SetParent(parent, false);

            System.Random rng = new System.Random(101923);
            float minX = centerX - (chainLength * 0.55f);
            float maxX = centerX + (chainLength * 0.55f);
            float minZ = -(chainWidth * 0.6f);
            float maxZ = chainWidth * 0.6f;

            for (int i = 0; i < 120; i++)
            {
                float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
                float z = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());
                if (Mathf.Abs(z) < (ArenaWallHalfExtent + 8f) &&
                    x > -ArenaWallHalfExtent &&
                    x < (((RoomChainCount - 1) * RoomChainSpacing) + ArenaWallHalfExtent))
                {
                    continue;
                }

                float yScale = Mathf.Lerp(0.8f, 8f, (float)rng.NextDouble());
                float xz = Mathf.Lerp(0.8f, 2.4f, (float)rng.NextDouble());
                Vector3 pos = new Vector3(x, (yScale * 0.5f) - 0.02f, z);
                CreateBlock(root, monolithMaterial, pos, new Vector3(xz, yScale, xz), $"VoidMonolith_{i:000}", false);
            }

            for (int i = 0; i < 42; i++)
            {
                float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
                float z = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());
                if (Mathf.Abs(z) < (ArenaWallHalfExtent + 8f) &&
                    x > -ArenaWallHalfExtent &&
                    x < (((RoomChainCount - 1) * RoomChainSpacing) + ArenaWallHalfExtent))
                {
                    continue;
                }

                float size = Mathf.Lerp(0.55f, 1.6f, (float)rng.NextDouble());
                Vector3 pos = new Vector3(x, Mathf.Lerp(1f, 8f, (float)rng.NextDouble()), z);
                CreateBlock(root, glowMaterial, pos, Vector3.one * size, $"VoidGlowCube_{i:000}", false);
            }

            CreateBlock(root, voidMaterial, new Vector3(centerX, VoidFloorY - 0.06f, 0f), new Vector3(chainLength + 30f, 0.08f, chainWidth + 30f), "VoidBasePlate", false);
        }

        private static void SetLayoutGroupActive(GameObject[] layouts, bool active)
        {
            if (layouts == null)
            {
                return;
            }

            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i] != null)
                {
                    layouts[i].SetActive(active);
                }
            }
        }

        private static GameObject BuildCombatLayoutA(Transform parent, BootstrapAssets assets)
        {
            GameObject root = new GameObject("Layout_Combat_A");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;

            CreateBlock(root.transform, assets.coverMaterial, new Vector3(-9f, 0.9f, -9f), new Vector3(3f, 1.8f, 1.2f), "Cover_A1");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(9f, 0.9f, -9f), new Vector3(3f, 1.8f, 1.2f), "Cover_A2");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(-9f, 0.9f, 9f), new Vector3(3f, 1.8f, 1.2f), "Cover_A3");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(9f, 0.9f, 9f), new Vector3(3f, 1.8f, 1.2f), "Cover_A4");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(0f, 0.9f, -12f), new Vector3(4.2f, 1.8f, 1.2f), "Cover_A5");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(0f, 0.9f, 12f), new Vector3(4.2f, 1.8f, 1.2f), "Cover_A6");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(-12f, 0.9f, 0f), new Vector3(1.2f, 1.8f, 4.2f), "Cover_A7");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(12f, 0.9f, 0f), new Vector3(1.2f, 1.8f, 4.2f), "Cover_A8");
            return root;
        }

        private static GameObject BuildCombatLayoutB(Transform parent, BootstrapAssets assets)
        {
            GameObject root = new GameObject("Layout_Combat_B");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;

            CreateBlock(root.transform, assets.coverMaterial, new Vector3(0f, 0.9f, 0f), new Vector3(2f, 1.8f, 7f), "Cover_B1");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(0f, 0.9f, 0f), new Vector3(7f, 1.8f, 2f), "Cover_B2");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(-14f, 0.9f, -6f), new Vector3(3f, 1.8f, 1.2f), "Cover_B3");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(14f, 0.9f, 6f), new Vector3(3f, 1.8f, 1.2f), "Cover_B4");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(-6f, 0.9f, 14f), new Vector3(1.2f, 1.8f, 3.4f), "Cover_B5");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(6f, 0.9f, -14f), new Vector3(1.2f, 1.8f, 3.4f), "Cover_B6");
            return root;
        }

        private static GameObject BuildCombatLayoutC(Transform parent, BootstrapAssets assets)
        {
            GameObject root = new GameObject("Layout_Combat_C");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;

            CreateBlock(root.transform, assets.coverMaterial, new Vector3(-7f, 0.9f, -7f), new Vector3(2.2f, 1.8f, 6.5f), "Cover_C1");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(7f, 0.9f, 7f), new Vector3(2.2f, 1.8f, 6.5f), "Cover_C2");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(-7f, 0.9f, 7f), new Vector3(6.5f, 1.8f, 2.2f), "Cover_C3");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(7f, 0.9f, -7f), new Vector3(6.5f, 1.8f, 2.2f), "Cover_C4");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(0f, 0.9f, 15f), new Vector3(5.5f, 1.8f, 1f), "Cover_C5");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(0f, 0.9f, -15f), new Vector3(5.5f, 1.8f, 1f), "Cover_C6");
            return root;
        }

        private static GameObject BuildRewardLayoutA(Transform parent, BootstrapAssets assets)
        {
            GameObject root = new GameObject("Layout_Reward_A");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;

            CreateBlock(root.transform, assets.accentMaterial, new Vector3(0f, 0.15f, 0f), new Vector3(8f, 0.2f, 8f), "Pad_A1", false);
            CreateBlock(root.transform, assets.accentMaterial, new Vector3(-8f, 0.5f, 0f), new Vector3(1.8f, 1f, 1.8f), "Pedestal_A1");
            CreateBlock(root.transform, assets.accentMaterial, new Vector3(8f, 0.5f, 0f), new Vector3(1.8f, 1f, 1.8f), "Pedestal_A2");
            CreateBlock(root.transform, assets.accentMaterial, new Vector3(0f, 0.5f, 8f), new Vector3(1.8f, 1f, 1.8f), "Pedestal_A3");
            return root;
        }

        private static GameObject BuildRewardLayoutB(Transform parent, BootstrapAssets assets)
        {
            GameObject root = new GameObject("Layout_Reward_B");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;

            CreateBlock(root.transform, assets.accentMaterial, new Vector3(0f, 0.15f, 0f), new Vector3(10f, 0.2f, 10f), "Pad_B1", false);
            CreateBlock(root.transform, assets.accentMaterial, new Vector3(-7f, 0.6f, -7f), new Vector3(1.5f, 1.2f, 1.5f), "Pedestal_B1");
            CreateBlock(root.transform, assets.accentMaterial, new Vector3(7f, 0.6f, -7f), new Vector3(1.5f, 1.2f, 1.5f), "Pedestal_B2");
            CreateBlock(root.transform, assets.accentMaterial, new Vector3(0f, 0.6f, 8f), new Vector3(1.5f, 1.2f, 1.5f), "Pedestal_B3");
            return root;
        }

        private static GameObject BuildRiskLayoutA(Transform parent, BootstrapAssets assets)
        {
            GameObject root = new GameObject("Layout_Risk_A");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;

            CreateBlock(root.transform, assets.coverMaterial, new Vector3(0f, 0.9f, -14f), new Vector3(10f, 1.8f, 1.2f), "Cover_R1");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(0f, 0.9f, 14f), new Vector3(10f, 1.8f, 1.2f), "Cover_R2");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(-14f, 0.9f, 0f), new Vector3(1.2f, 1.8f, 10f), "Cover_R3");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(14f, 0.9f, 0f), new Vector3(1.2f, 1.8f, 10f), "Cover_R4");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(-5f, 0.9f, 0f), new Vector3(1.2f, 1.8f, 7f), "Cover_R5");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(5f, 0.9f, 0f), new Vector3(1.2f, 1.8f, 7f), "Cover_R6");
            return root;
        }

        private static GameObject BuildRiskLayoutB(Transform parent, BootstrapAssets assets)
        {
            GameObject root = new GameObject("Layout_Risk_B");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;

            CreateBlock(root.transform, assets.coverMaterial, new Vector3(0f, 0.9f, 0f), new Vector3(2.2f, 1.8f, 12f), "Cover_RB1");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(0f, 0.9f, 0f), new Vector3(12f, 1.8f, 2.2f), "Cover_RB2");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(-11f, 0.9f, 11f), new Vector3(2f, 1.8f, 2f), "Cover_RB3");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(11f, 0.9f, -11f), new Vector3(2f, 1.8f, 2f), "Cover_RB4");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(11f, 0.9f, 11f), new Vector3(2f, 1.8f, 2f), "Cover_RB5");
            CreateBlock(root.transform, assets.coverMaterial, new Vector3(-11f, 0.9f, -11f), new Vector3(2f, 1.8f, 2f), "Cover_RB6");
            return root;
        }

        private static GameObject BuildBossLayout(Transform parent, BootstrapAssets assets)
        {
            GameObject root = new GameObject("Layout_Boss");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;

            CreateBlock(root.transform, assets.accentMaterial, new Vector3(-14f, 0.9f, -14f), new Vector3(3f, 1.8f, 3f), "BossPillar_1");
            CreateBlock(root.transform, assets.accentMaterial, new Vector3(14f, 0.9f, -14f), new Vector3(3f, 1.8f, 3f), "BossPillar_2");
            CreateBlock(root.transform, assets.accentMaterial, new Vector3(-14f, 0.9f, 14f), new Vector3(3f, 1.8f, 3f), "BossPillar_3");
            CreateBlock(root.transform, assets.accentMaterial, new Vector3(14f, 0.9f, 14f), new Vector3(3f, 1.8f, 3f), "BossPillar_4");
            CreateBlock(root.transform, assets.accentMaterial, new Vector3(0f, 0.11f, 0f), new Vector3(8f, 0.04f, 8f), "BossCenterMark", false);
            return root;
        }

        private static Transform[] BuildSpawnPoints(Transform parent)
        {
            GameObject spawnRoot = new GameObject("SpawnPoints");
            spawnRoot.transform.SetParent(parent);
            spawnRoot.transform.localPosition = Vector3.zero;

            Transform[] points = new Transform[SpawnTemplatePositions.Length];
            for (int i = 0; i < SpawnTemplatePositions.Length; i++)
            {
                GameObject point = new GameObject($"SpawnPoint_{i + 1:00}");
                point.transform.SetParent(spawnRoot.transform);
                point.transform.localPosition = SpawnTemplatePositions[i];
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
            cam.transform.position = new Vector3(0f, 26f, -18f);
            cam.transform.rotation = Quaternion.Euler(57f, 26f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.015f, 0.03f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 420f;

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
            ConfigureSerialized(follow, so =>
            {
                so.FindProperty("offset").vector3Value = new Vector3(-8f, 26f, -18f);
                so.FindProperty("followLerpSpeed").floatValue = 8.5f;
                so.FindProperty("lookAtTarget").boolValue = true;
            });

            return cam;
        }

        private static void BuildLighting()
        {
            // Dim directional for overall shape - darker atmosphere
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.55f;
            light.color = new Color(0.75f, 0.82f, 0.92f);
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            // Cyan fill lights for sci-fi atmosphere
            Vector3[] cyanPositions =
            {
                new Vector3(26f, 8f, 20f),
                new Vector3(-26f, 8f, -20f),
                new Vector3(58f, 8f, 22f),
                new Vector3(116f, 8f, -22f),
                new Vector3(200f, 8f, 18f),
                new Vector3(350f, 8f, -18f)
            };
            for (int i = 0; i < cyanPositions.Length; i++)
            {
                GameObject fill = new GameObject($"CyanFill_{i + 1:00}");
                Light fillLight = fill.AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.range = 28f;
                fillLight.intensity = 1.1f;
                fillLight.color = new Color(0.1f, 0.85f, 0.95f);
                fillLight.shadows = LightShadows.None;
                fill.transform.position = cyanPositions[i];
            }

            // Magenta accent lights
            Vector3[] magentaPositions =
            {
                new Vector3(0f, 6f, -28f),
                new Vector3(116f, 6f, 28f),
                new Vector3(290f, 6f, -28f)
            };
            for (int i = 0; i < magentaPositions.Length; i++)
            {
                GameObject accent = new GameObject($"MagentaAccent_{i + 1:00}");
                Light accentLight = accent.AddComponent<Light>();
                accentLight.type = LightType.Point;
                accentLight.range = 18f;
                accentLight.intensity = 0.7f;
                accentLight.color = new Color(1f, 0.15f, 0.6f);
                accentLight.shadows = LightShadows.None;
                accent.transform.position = magentaPositions[i];
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.08f, 0.12f);
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

        private static ScreenFlashOverlay BuildHud(RunDirector runDirector, BootstrapAssets assets, Camera mainCamera)
        {
            // UI Toolkit HUD
            UIElements.VisualTreeAsset hudAsset = AssetDatabase.LoadAssetAtPath<UIElements.VisualTreeAsset>("Assets/UI/GameHud.uxml");

            GameObject hudGo = new GameObject("HUD");
            UIElements.UIDocument uiDoc = hudGo.AddComponent<UIElements.UIDocument>();
            if (hudAsset != null)
            {
                uiDoc.visualTreeAsset = hudAsset;
                uiDoc.sortingOrder = 100;
            }

            GameHudController hudCtrl = hudGo.AddComponent<GameHudController>();
            ConfigureSerialized(hudCtrl, so =>
            {
                so.FindProperty("uiDocument").objectReferenceValue = uiDoc;
                so.FindProperty("runDirector").objectReferenceValue = runDirector;
                so.FindProperty("lowTimeThreshold").floatValue = 10f;
                so.FindProperty("pulseDuration").floatValue = 0.2f;
                so.FindProperty("notificationDuration").floatValue = 2.5f;
            });

            // Screen flash overlay still uses UGUI Canvas for full-screen image flash
            GameObject canvasGo = new GameObject("FlashCanvas", typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

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

        private static RectTransform CreateHudPanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            RectTransform rect = panel.GetComponent<RectTransform>();
            if (alignment == TextAlignmentOptions.Top)
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
            }

            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static void ConfigureRunDirector(RunDirector director, BootstrapAssets assets, Transform[] spawnPoints, RunExitDoor[] exitDoors, Transform[] roomAnchors)
        {
            ConfigureSerialized(director, so =>
            {
                SerializedProperty spawnPointsProperty = so.FindProperty("spawnPoints");
                spawnPointsProperty.arraySize = spawnPoints.Length;
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    spawnPointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
                }

                SerializedProperty roomAnchorsProperty = so.FindProperty("roomAnchors");
                if (roomAnchorsProperty != null)
                {
                    roomAnchorsProperty.arraySize = roomAnchors != null ? roomAnchors.Length : 0;
                    if (roomAnchors != null)
                    {
                        for (int i = 0; i < roomAnchors.Length; i++)
                        {
                            roomAnchorsProperty.GetArrayElementAtIndex(i).objectReferenceValue = roomAnchors[i];
                        }
                    }
                }

                so.FindProperty("bossPrefab").objectReferenceValue = assets.bossPrefab;
                so.FindProperty("clockPickupPrefab").objectReferenceValue = assets.clockPickupPrefab;
                so.FindProperty("rewardPickupPrefab").objectReferenceValue = assets.rewardPickupPrefab;
                SerializedProperty exitDoorsProperty = so.FindProperty("exitDoors");
                if (exitDoorsProperty != null)
                {
                    exitDoorsProperty.arraySize = exitDoors != null ? exitDoors.Length : 0;
                    if (exitDoors != null)
                    {
                        for (int i = 0; i < exitDoors.Length; i++)
                        {
                            exitDoorsProperty.GetArrayElementAtIndex(i).objectReferenceValue = exitDoors[i];
                        }
                    }
                }

                so.FindProperty("requireDoorTransition").boolValue = true;
                so.FindProperty("currentRoomDoorRadius").floatValue = 36f;
                so.FindProperty("exitDoorAutoAdvanceTimeout").floatValue = 18f;
                so.FindProperty("transitionSpawnOffset").floatValue = 6.5f;
                so.FindProperty("totalRooms").intValue = RoomChainCount;
                so.FindProperty("shotgunWeapon").objectReferenceValue = assets.shotgun;
                so.FindProperty("chargeBeamWeapon").objectReferenceValue = assets.chargeBeam;
                so.FindProperty("rewardRoomChance").floatValue = 0.2f;
                so.FindProperty("riskRoomChance").floatValue = 0.1f;
                so.FindProperty("guaranteedCombatRooms").intValue = 2;
                so.FindProperty("minRewardRoomsPerRun").intValue = 2;
                so.FindProperty("maxRoomsWithoutReward").intValue = 3;
                so.FindProperty("riskGuaranteedClockPickups").intValue = 1;
                so.FindProperty("riskEnemyMultiplier").floatValue = 1.35f;
                so.FindProperty("roomEnemyScalePerRoom").floatValue = 0.45f;
                so.FindProperty("lowTimePressureThreshold").floatValue = 20f;
                so.FindProperty("lowTimeEnemyBonusMultiplier").floatValue = 0.55f;
                so.FindProperty("maxAdditionalEnemiesFromPressure").intValue = 3;
                so.FindProperty("fireRateRewardMultiplier").floatValue = 1.16f;
                so.FindProperty("damageRewardMultiplier").floatValue = 1.22f;
                so.FindProperty("projectileSpeedRewardMultiplier").floatValue = 1.14f;
                so.FindProperty("timeRewardSeconds").floatValue = 8f;
                so.FindProperty("rewardSpawnRadius").floatValue = 4.5f;
                so.FindProperty("rewardHoverHeight").floatValue = 1.15f;

                SerializedProperty enemyList = so.FindProperty("enemyPrefabs");
                enemyList.arraySize = 0;
                AddEnemySpawnEntry(enemyList, "Drone", assets.dronePrefab, 1, 2.4f);
                AddEnemySpawnEntry(enemyList, "Turret", assets.turretPrefab, 2, 1.5f);
                AddEnemySpawnEntry(enemyList, "Hunter", assets.hunterPrefab, 4, 1.8f);
                AddEnemySpawnEntry(enemyList, "Tank", assets.tankPrefab, 6, 1.1f);
            });
        }

        private static void ConfigureRunDirectorBase(RunDirector director, BootstrapAssets assets)
        {
            ConfigureSerialized(director, so =>
            {
                so.FindProperty("bossPrefab").objectReferenceValue = assets.bossPrefab;
                so.FindProperty("clockPickupPrefab").objectReferenceValue = assets.clockPickupPrefab;
                so.FindProperty("rewardPickupPrefab").objectReferenceValue = assets.rewardPickupPrefab;
                so.FindProperty("requireDoorTransition").boolValue = true;
                so.FindProperty("currentRoomDoorRadius").floatValue = 36f;
                so.FindProperty("exitDoorAutoAdvanceTimeout").floatValue = 18f;
                so.FindProperty("transitionSpawnOffset").floatValue = 6.5f;
                so.FindProperty("totalRooms").intValue = RoomChainCount;
                so.FindProperty("shotgunWeapon").objectReferenceValue = assets.shotgun;
                so.FindProperty("chargeBeamWeapon").objectReferenceValue = assets.chargeBeam;
                so.FindProperty("rewardRoomChance").floatValue = 0.2f;
                so.FindProperty("riskRoomChance").floatValue = 0.1f;
                so.FindProperty("guaranteedCombatRooms").intValue = 2;
                so.FindProperty("minRewardRoomsPerRun").intValue = 2;
                so.FindProperty("maxRoomsWithoutReward").intValue = 3;
                so.FindProperty("riskGuaranteedClockPickups").intValue = 1;
                so.FindProperty("riskEnemyMultiplier").floatValue = 1.35f;
                so.FindProperty("roomEnemyScalePerRoom").floatValue = 0.45f;
                so.FindProperty("lowTimePressureThreshold").floatValue = 20f;
                so.FindProperty("lowTimeEnemyBonusMultiplier").floatValue = 0.55f;
                so.FindProperty("maxAdditionalEnemiesFromPressure").intValue = 3;
                so.FindProperty("fireRateRewardMultiplier").floatValue = 1.16f;
                so.FindProperty("damageRewardMultiplier").floatValue = 1.22f;
                so.FindProperty("projectileSpeedRewardMultiplier").floatValue = 1.14f;
                so.FindProperty("timeRewardSeconds").floatValue = 8f;
                so.FindProperty("rewardSpawnRadius").floatValue = 4.5f;
                so.FindProperty("rewardHoverHeight").floatValue = 1.15f;

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

        private static GameObject CreateBlockLocal(Transform parent, Material material, Vector3 localPosition, Vector3 localScale, string name, bool withCollider = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localRotation = Quaternion.identity;
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
            root.GetComponent<Renderer>().enabled = false;
            root.GetComponent<Collider>().isTrigger = true;
            root.AddComponent<Projectile>();

            // Elongated shard visual
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "Shard";
            shard.transform.SetParent(root.transform, false);
            shard.transform.localScale = new Vector3(0.6f, 0.6f, 1.8f);
            shard.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            shard.GetComponent<Renderer>().sharedMaterial = assets.playerProjectileMaterial;
            Collider shardCol = shard.GetComponent<Collider>();
            if (shardCol != null) UnityEngine.Object.DestroyImmediate(shardCol);

            return SaveAsPrefab(path, root);
        }

        private static GameObject CreateEnemyProjectilePrefab(BootstrapAssets assets)
        {
            string path = $"{PrefabsFolder}/P_Projectile_Enemy.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Projectile_Enemy";
            root.transform.localScale = Vector3.one * 0.25f;
            root.GetComponent<Renderer>().enabled = false;
            root.GetComponent<Collider>().isTrigger = true;
            root.AddComponent<EnemyProjectile>();

            // Diamond visual
            GameObject diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diamond.name = "Diamond";
            diamond.transform.SetParent(root.transform, false);
            diamond.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            diamond.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            diamond.GetComponent<Renderer>().sharedMaterial = assets.enemyProjectileMaterial;
            Collider diamondCol = diamond.GetComponent<Collider>();
            if (diamondCol != null) UnityEngine.Object.DestroyImmediate(diamondCol);

            return SaveAsPrefab(path, root);
        }

        private static ClockPickup CreateClockPickupPrefab(BootstrapAssets assets)
        {
            string path = $"{PrefabsFolder}/P_ClockPickup.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "ClockPickup";
            root.transform.localScale = new Vector3(0.65f, 0.2f, 0.65f);
            root.GetComponent<Renderer>().enabled = false;
            root.GetComponent<Collider>().isTrigger = true;
            root.AddComponent<SpinBob>();
            root.AddComponent<ClockPickup>();

            // Floating crystal visual
            AddVisualCube(root.transform, assets.pickupMaterial, Vector3.zero, new Vector3(0.4f, 0.6f, 0.4f), Quaternion.Euler(45f, 0f, 45f));
            AddVisualCube(root.transform, assets.pickupMaterial, new Vector3(0f, 0.2f, 0f), new Vector3(0.2f, 0.25f, 0.2f), Quaternion.Euler(0f, 45f, 0f));

            GameObject prefab = SaveAsPrefab(path, root);
            return prefab.GetComponent<ClockPickup>();
        }

        private static RewardPickup CreateRewardPickupPrefab(BootstrapAssets assets)
        {
            string path = $"{PrefabsFolder}/P_RewardPickup.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "RewardPickup";
            root.transform.localScale = new Vector3(0.9f, 0.2f, 0.9f);
            root.GetComponent<Renderer>().sharedMaterial = assets.accentMaterial;
            root.GetComponent<Collider>().isTrigger = true;
            root.AddComponent<SpinBob>();
            RewardPickup pickup = root.AddComponent<RewardPickup>();

            TMP_FontAsset fontAsset = ResolveLiberationSansFontAsset();
            GameObject label = new GameObject("Label", typeof(TextMeshPro));
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            TextMeshPro labelText = label.GetComponent<TextMeshPro>();
            labelText.font = fontAsset;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 2.4f;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.text = "Reward";
            labelText.color = Color.white;
            labelText.rectTransform.sizeDelta = new Vector2(6f, 2f);

            ConfigureSerialized(pickup, so =>
            {
                so.FindProperty("labelText").objectReferenceValue = labelText;
                SerializedProperty renderers = so.FindProperty("targetRenderers");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = root.GetComponent<Renderer>();
            });

            GameObject prefab = SaveAsPrefab(path, root);
            return prefab.GetComponent<RewardPickup>();
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

        private static void BuildCrystallineVisual(GameObject root, Vector3 baseScale, Material material, string enemyType)
        {
            Renderer rootRenderer = root.GetComponent<Renderer>();
            if (rootRenderer != null)
            {
                rootRenderer.enabled = false;
            }

            GameObject visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.transform.localPosition = Vector3.zero;

            float s = Mathf.Max(baseScale.x, baseScale.z);
            float c = s * 0.34f;
            float g = s * 0.09f;
            float step = c + g;

            if (enemyType == "Drone")
            {
                // Tall center + compact side cubes
                AddVisualCube(visualRoot.transform, material, Vector3.zero, new Vector3(c * 0.8f, c * 2.2f, c * 0.8f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(step, 0f, 0f), new Vector3(c * 0.65f, c * 0.8f, c * 0.65f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(-step, 0f, 0f), new Vector3(c * 0.65f, c * 0.8f, c * 0.65f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, step * 1.2f, 0f), new Vector3(c * 0.5f, c * 0.7f, c * 0.5f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, -step * 0.7f, step * 0.6f), new Vector3(c * 0.4f, c * 0.5f, c * 0.4f), Quaternion.identity);
            }
            else if (enemyType == "Turret")
            {
                // Tall central tower + side pylons
                AddVisualCube(visualRoot.transform, material, Vector3.zero, new Vector3(c * 0.85f, c * 2.8f, c * 0.85f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, step * 1.5f, 0f), new Vector3(c * 0.6f, c * 1.2f, c * 0.6f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(step, -step * 0.3f, 0f), new Vector3(c * 0.55f, c * 1.5f, c * 0.55f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(-step, -step * 0.3f, 0f), new Vector3(c * 0.55f, c * 1.5f, c * 0.55f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(step, step * 0.6f, 0f), new Vector3(c * 0.4f, c * 0.6f, c * 0.4f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(-step, step * 0.6f, 0f), new Vector3(c * 0.4f, c * 0.6f, c * 0.4f), Quaternion.identity);
            }
            else if (enemyType == "Hunter")
            {
                // Tall center spine + swept wings
                AddVisualCube(visualRoot.transform, material, Vector3.zero, new Vector3(c * 0.7f, c * 2.0f, c * 0.9f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(step, -step * 0.2f, 0f), new Vector3(c * 0.9f, c * 0.5f, c * 0.7f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(-step, -step * 0.2f, 0f), new Vector3(c * 0.9f, c * 0.5f, c * 0.7f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(step * 1.8f, -step * 0.4f, 0f), new Vector3(c * 0.55f, c * 0.35f, c * 0.5f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(-step * 1.8f, -step * 0.4f, 0f), new Vector3(c * 0.55f, c * 0.35f, c * 0.5f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, step * 0.9f, 0f), new Vector3(c * 0.45f, c * 0.8f, c * 0.45f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, -step * 0.5f, -step * 0.8f), new Vector3(c * 0.35f, c * 0.7f, c * 0.35f), Quaternion.identity);
            }
            else if (enemyType == "Tank")
            {
                // Massive tall center + heavy surrounding blocks
                float tc = c * 1.15f;
                float ts = tc + g;
                AddVisualCube(visualRoot.transform, material, Vector3.zero, new Vector3(tc * 0.9f, tc * 2.6f, tc * 0.9f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(ts, -ts * 0.3f, 0f), new Vector3(tc * 0.85f, tc * 1.4f, tc * 0.85f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(-ts, -ts * 0.3f, 0f), new Vector3(tc * 0.85f, tc * 1.4f, tc * 0.85f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, -ts * 0.3f, ts), new Vector3(tc * 0.7f, tc * 1.0f, tc * 0.7f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, -ts * 0.3f, -ts), new Vector3(tc * 0.7f, tc * 1.0f, tc * 0.7f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, ts * 1.2f, 0f), new Vector3(tc * 0.6f, tc * 0.8f, tc * 0.6f), Quaternion.identity);
            }
            else if (enemyType == "Boss")
            {
                // Imposing tall center pillar + orbital cubes
                float bc = c * 1.35f;
                float bs = bc + g * 1.5f;
                // Dominant center column
                AddVisualCube(visualRoot.transform, material, Vector3.zero, new Vector3(bc * 0.9f, bc * 3.2f, bc * 0.9f), Quaternion.identity);
                // Mid-height ring
                AddVisualCube(visualRoot.transform, material, new Vector3(bs, 0f, 0f), new Vector3(bc * 0.75f, bc * 1.8f, bc * 0.75f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(-bs, 0f, 0f), new Vector3(bc * 0.75f, bc * 1.8f, bc * 0.75f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, 0f, bs), new Vector3(bc * 0.7f, bc * 1.2f, bc * 0.7f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, 0f, -bs), new Vector3(bc * 0.7f, bc * 1.2f, bc * 0.7f), Quaternion.identity);
                // Diagonal satellites (varied heights)
                AddVisualCube(visualRoot.transform, material, new Vector3(bs * 0.8f, bs * 0.8f, bs * 0.8f), new Vector3(bc * 0.5f, bc * 1.0f, bc * 0.5f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(-bs * 0.8f, bs * 0.8f, -bs * 0.8f), new Vector3(bc * 0.5f, bc * 1.0f, bc * 0.5f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(bs * 0.8f, -bs * 0.6f, -bs * 0.8f), new Vector3(bc * 0.45f, bc * 0.7f, bc * 0.45f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(-bs * 0.8f, -bs * 0.6f, bs * 0.8f), new Vector3(bc * 0.45f, bc * 0.7f, bc * 0.45f), Quaternion.identity);
                // Crown
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, bs * 2f, 0f), new Vector3(bc * 0.55f, bc * 1.4f, bc * 0.55f), Quaternion.identity);
                // Accent floaters
                AddVisualCube(visualRoot.transform, material, new Vector3(bs * 1.4f, bs * 0.3f, 0f), new Vector3(bc * 0.3f, bc * 0.5f, bc * 0.3f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(-bs * 1.4f, -bs * 0.3f, 0f), new Vector3(bc * 0.3f, bc * 0.5f, bc * 0.3f), Quaternion.identity);
            }
            else
            {
                // Default - tall center + small orbiting cubes
                AddVisualCube(visualRoot.transform, material, Vector3.zero, new Vector3(c * 0.8f, c * 2.0f, c * 0.8f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(step, 0f, 0f), new Vector3(c * 0.6f, c * 0.8f, c * 0.6f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(-step, 0f, 0f), new Vector3(c * 0.6f, c * 0.8f, c * 0.6f), Quaternion.identity);
                AddVisualCube(visualRoot.transform, material, new Vector3(0f, step, 0f), new Vector3(c * 0.5f, c * 0.9f, c * 0.5f), Quaternion.identity);
            }
        }

        private static void AddVisualCube(Transform parent, Material material, Vector3 localPos, Vector3 localScale, Quaternion localRotation)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Shard";
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = localScale;
            cube.transform.localRotation = localRotation;

            Renderer renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Collider col = cube.GetComponent<Collider>();
            if (col != null)
            {
                UnityEngine.Object.DestroyImmediate(col);
            }
        }

        private static void BuildPlayerVisual(GameObject root, Material material)
        {
            Renderer rootRenderer = root.GetComponent<Renderer>();
            if (rootRenderer != null)
            {
                rootRenderer.enabled = false;
            }

            GameObject visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.transform.localPosition = Vector3.zero;

            float c = 0.30f;
            float g = 0.10f;
            float step = c + g;

            // Tall center pillar - dominant vertical element
            AddVisualCube(visualRoot.transform, material, new Vector3(0f, 0f, 0f), new Vector3(c * 0.9f, c * 2.4f, c * 0.9f), Quaternion.identity);

            // Shoulder cubes (wide, shorter)
            AddVisualCube(visualRoot.transform, material, new Vector3(-step, step * 0.3f, 0f), new Vector3(c * 0.8f, c * 1.5f, c * 0.8f), Quaternion.identity);
            AddVisualCube(visualRoot.transform, material, new Vector3(step, step * 0.3f, 0f), new Vector3(c * 0.8f, c * 1.5f, c * 0.8f), Quaternion.identity);

            // Arm tips (small, lower)
            AddVisualCube(visualRoot.transform, material, new Vector3(-step * 2f, 0f, 0f), new Vector3(c * 0.55f, c * 0.9f, c * 0.55f), Quaternion.identity);
            AddVisualCube(visualRoot.transform, material, new Vector3(step * 2f, 0f, 0f), new Vector3(c * 0.55f, c * 0.9f, c * 0.55f), Quaternion.identity);

            // Lower body
            AddVisualCube(visualRoot.transform, material, new Vector3(0f, -step * 1.2f, 0f), new Vector3(c * 0.75f, c * 1.0f, c * 0.75f), Quaternion.identity);

            // Front/back depth
            AddVisualCube(visualRoot.transform, material, new Vector3(0f, step * 0.2f, step), new Vector3(c * 0.6f, c * 0.7f, c * 0.6f), Quaternion.identity);
            AddVisualCube(visualRoot.transform, material, new Vector3(0f, -step * 0.2f, -step), new Vector3(c * 0.5f, c * 1.1f, c * 0.5f), Quaternion.identity);

            // Head crown (tall thin)
            AddVisualCube(visualRoot.transform, material, new Vector3(0f, step * 1.8f, 0f), new Vector3(c * 0.65f, c * 1.3f, c * 0.65f), Quaternion.identity);

            // Tiny floating accents
            AddVisualCube(visualRoot.transform, material, new Vector3(step * 0.6f, step * 2.2f, step * 0.3f), new Vector3(c * 0.25f, c * 0.45f, c * 0.25f), Quaternion.identity);
            AddVisualCube(visualRoot.transform, material, new Vector3(-step * 0.5f, -step * 1.0f, -step * 0.3f), new Vector3(c * 0.3f, c * 0.2f, c * 0.3f), Quaternion.identity);
        }

        private static GameObject CreateEnemyPrefab(EnemyPrefabBuildParams parameters)
        {
            GameObject root = GameObject.CreatePrimitive(parameters.primitiveType);
            root.name = parameters.name;
            root.transform.localScale = parameters.scale;
            root.GetComponent<Renderer>().sharedMaterial = parameters.material;

            string enemyType = "Default";
            if (parameters.name.Contains("Drone")) enemyType = "Drone";
            else if (parameters.name.Contains("Turret")) enemyType = "Turret";
            else if (parameters.name.Contains("Hunter")) enemyType = "Hunter";
            else if (parameters.name.Contains("Tank")) enemyType = "Tank";
            else if (parameters.name.Contains("Boss")) enemyType = "Boss";

            BuildCrystallineVisual(root, parameters.scale, parameters.material, enemyType);

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

            BuildPlayerVisual(root, assets.playerMaterial);

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            PlayerController playerController = root.AddComponent<PlayerController>();
            root.AddComponent<RunPassiveController>();
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
            float projectileLifetime,
            int projectileCount,
            float spreadAngle)
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
            SerializedProperty projectileCountProperty = so.FindProperty("projectileCount");
            if (projectileCountProperty != null)
            {
                projectileCountProperty.intValue = Mathf.Max(1, projectileCount);
            }

            SerializedProperty spreadProperty = so.FindProperty("spreadAngle");
            if (spreadProperty != null)
            {
                spreadProperty.floatValue = Mathf.Max(0f, spreadAngle);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ImportReferenceVisualAssets()
        {
            string assetsRoot = Path.Combine(ReferenceProjectRoot, "Assets");
            if (!Directory.Exists(assetsRoot))
            {
                return;
            }

            string[] relativePaths =
            {
                "Shaders/Ground.shadergraph",
                "Shaders/Ground.shadergraph.meta",
                "Shaders/Wall.shadergraph",
                "Shaders/Wall.shadergraph.meta",
                "Shaders/GridFunctions.hlsl",
                "Shaders/GridFunctions.hlsl.meta",
                "Materials/Shader Graphs_Ground.mat",
                "Materials/Shader Graphs_Ground.mat.meta",
                "Materials/Wall.mat",
                "Materials/Wall.mat.meta"
            };

            bool copiedAny = false;
            string projectRoot = Directory.GetCurrentDirectory();
            for (int i = 0; i < relativePaths.Length; i++)
            {
                string relative = relativePaths[i];
                string sourcePath = Path.Combine(assetsRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                string targetAssetPath = $"{ImportedVisualsFolder}/{Path.GetFileName(relative)}";
                string targetPath = Path.Combine(projectRoot, targetAssetPath.Replace('/', Path.DirectorySeparatorChar));
                string targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Copy(sourcePath, targetPath, true);
                copiedAny = true;
            }

            if (copiedAny)
            {
                AssetDatabase.Refresh();
            }
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
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.72f);
            vignette.rounded.Override(false);

            ChromaticAberration chromatic = GetOrCreateVolumeComponent<ChromaticAberration>(profile);
            chromatic.active = true;
            chromatic.intensity.Override(0.035f);

            LensDistortion lens = GetOrCreateVolumeComponent<LensDistortion>(profile);
            lens.active = true;
            lens.intensity.Override(0f);
            lens.scale.Override(1f);

            ColorAdjustments colorAdjustments = GetOrCreateVolumeComponent<ColorAdjustments>(profile);
            colorAdjustments.active = true;
            colorAdjustments.saturation.Override(-6f);
            colorAdjustments.contrast.Override(18f);
            colorAdjustments.postExposure.Override(-0.15f);
            colorAdjustments.colorFilter.Override(new Color(0.95f, 1f, 1f, 1f));

            Bloom bloom = GetOrCreateVolumeComponent<Bloom>(profile);
            bloom.active = true;
            bloom.intensity.Override(1.8f);
            bloom.threshold.Override(0.5f);
            bloom.scatter.Override(0.85f);
            bloom.tint.Override(new Color(0.55f, 0.92f, 1f, 1f));

            Tonemapping tonemapping = GetOrCreateVolumeComponent<Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            FilmGrain filmGrain = GetOrCreateVolumeComponent<FilmGrain>(profile);
            filmGrain.active = true;
            filmGrain.type.Override(FilmGrainLookup.Thin1);
            filmGrain.intensity.Override(0.2f);
            filmGrain.response.Override(0.72f);

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
            // Entity materials (player, enemies, projectiles, pickups) use crystal shader
            // Wall/architectural materials use standard lit
            string filename = System.IO.Path.GetFileNameWithoutExtension(path);
            if (filename.StartsWith("M_Player") || filename.StartsWith("M_Drone") ||
                filename.StartsWith("M_Turret") || filename.StartsWith("M_Hunter") ||
                filename.StartsWith("M_Tank") || filename.StartsWith("M_Boss") ||
                filename.StartsWith("M_EnemyProjectile") || filename.StartsWith("M_ClockPickup"))
            {
                return CreateOrUpdateCrystalMaterial(path, color);
            }

            return CreateOrUpdateLitMaterial(path, color);
        }

        private static Material CreateOrUpdateCrystalMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader crystalShader = Shader.Find("Sixty/CrystalEntity");

            if (crystalShader == null)
            {
                // Fallback if crystal shader not yet compiled
                return CreateOrUpdateLitMaterial(path, color);
            }

            if (material == null)
            {
                material = new Material(crystalShader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = crystalShader;
            }

            // Crystal shader properties
            material.SetColor("_BaseColor", color);
            material.SetColor("_EdgeColor", Color.Lerp(color, Color.white, 0.5f));
            material.SetColor("_EmissionColor", color);
            material.SetFloat("_EmissionIntensity", 1.5f);
            material.SetFloat("_FresnelPower", 3.0f);
            material.SetFloat("_FresnelIntensity", 0.6f);
            material.SetFloat("_FacetStrength", 0.85f);
            material.SetFloat("_SpecularPower", 64f);
            material.SetFloat("_SpecularIntensity", 1.2f);
            material.SetFloat("_AmbientStrength", 0.25f);
            material.SetFloat("_CavityWidth", 0.06f);
            material.SetFloat("_CavityStrength", 0.5f);
            material.SetColor("_CavityColor", Color.Lerp(color, Color.black, 0.7f));

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateLitMaterial(string path, Color color)
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
                material.SetColor("_EmissionColor", color * 2.8f);
            }

            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.88f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.35f);
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
            EnsureFolder(GeneratedRoot, "ImportedVisuals");
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
            serializedObject.UpdateIfRequiredOrScript();

            try
            {
                configure(serializedObject);
            }
            catch (NullReferenceException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to configure serialized properties for {target.GetType().FullName}. " +
                    "A serialized field name in SixtyBootstrapBuilder is likely out of date for this Unity version.",
                    ex);
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
#endif
