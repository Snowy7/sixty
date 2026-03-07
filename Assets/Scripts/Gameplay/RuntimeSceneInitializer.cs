using Sixty.Rendering;
using Sixty.UI;
using Sixty.World;
using UnityEngine;

namespace Sixty.Gameplay
{
    [DefaultExecutionOrder(-100)]
    public class RuntimeSceneInitializer : MonoBehaviour
    {
        [SerializeField] private RuntimeArenaBuilder arenaBuilder;
        [SerializeField] private RunDirector runDirector;
        [SerializeField] private RoomLayoutDirector roomLayoutDirector;
        [SerializeField] private GroundGridInfluenceController groundGrid;
        [SerializeField] private VoidCityGenerator voidCityGenerator;

        private void Awake()
        {
            if (arenaBuilder == null || runDirector == null)
                return;

            arenaBuilder.BuildArena();
            Transform[] spawnPoints = arenaBuilder.BuildSpawnPoints();

            runDirector.SetRuntimeArenaRefs(
                arenaBuilder.RoomAnchors,
                spawnPoints,
                arenaBuilder.ExitDoors);

            if (groundGrid != null)
            {
                groundGrid.SetRuntimeRenderers(arenaBuilder.GroundRenderers);
            }

            // Initialize void cityscape with room exclusion zones
            if (voidCityGenerator != null && arenaBuilder.GridPositions != null)
            {
                Vector3[] roomCenters = new Vector3[arenaBuilder.RoomAnchors.Length];
                for (int i = 0; i < roomCenters.Length; i++)
                {
                    roomCenters[i] = arenaBuilder.RoomAnchors[i].position;
                }

                // Center the void around the room chain
                Vector3 min = roomCenters[0];
                Vector3 max = roomCenters[0];
                for (int i = 1; i < roomCenters.Length; i++)
                {
                    min = Vector3.Min(min, roomCenters[i]);
                    max = Vector3.Max(max, roomCenters[i]);
                }
                Vector3 center = (min + max) * 0.5f;
                voidCityGenerator.SetCenter(center);

                voidCityGenerator.SetRoomExclusions(roomCenters, arenaBuilder.GridWallHalfExtent);
                voidCityGenerator.SetCorridorExclusions(arenaBuilder.CorridorMins, arenaBuilder.CorridorMaxs);
                voidCityGenerator.Generate();
            }

            // Move player to first room
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null && arenaBuilder.RoomAnchors != null && arenaBuilder.RoomAnchors.Length > 0)
            {
                player.transform.position = arenaBuilder.RoomAnchors[0].position + new Vector3(0f, 0.95f, 0f);
            }

            // Fade in from black on scene start
            SceneTransitionOverlay.EnsureInstance();
            SceneTransitionOverlay.Instance?.FadeIn();
        }
    }
}
