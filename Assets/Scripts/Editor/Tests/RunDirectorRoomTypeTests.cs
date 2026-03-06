using System.Reflection;
using NUnit.Framework;
using Sixty.Gameplay;
using UnityEngine;

namespace Sixty.Tests.EditMode
{
    public class RunDirectorRoomTypeTests
    {
        [TearDown]
        public void TearDown()
        {
            RunDirector[] directors = Object.FindObjectsByType<RunDirector>(FindObjectsSortMode.None);
            for (int i = 0; i < directors.Length; i++)
            {
                if (directors[i] != null)
                {
                    Object.DestroyImmediate(directors[i].gameObject);
                }
            }

            GameObject iaUpdateManager = GameObject.Find("[IaUpdateManager]");
            if (iaUpdateManager != null)
            {
                Object.DestroyImmediate(iaUpdateManager);
            }
        }

        [Test]
        public void DetermineRoomType_LastRoomIsAlwaysBoss()
        {
            RunDirector director = CreateDirector();
            SetField(director, "totalRooms", 10);
            SetField(director, "guaranteedCombatRooms", 2);

            RunDirector.RoomType roomType = DetermineRoomType(director, 10);
            Assert.That(roomType, Is.EqualTo(RunDirector.RoomType.Boss));
        }

        [Test]
        public void DetermineRoomType_RespectsGuaranteedCombatRooms()
        {
            RunDirector director = CreateDirector();
            SetField(director, "totalRooms", 10);
            SetField(director, "guaranteedCombatRooms", 2);

            Assert.That(DetermineRoomType(director, 1), Is.EqualTo(RunDirector.RoomType.Combat));
            Assert.That(DetermineRoomType(director, 2), Is.EqualTo(RunDirector.RoomType.Combat));
        }

        [Test]
        public void DetermineRoomType_ForcesRewardAfterMaxGap()
        {
            RunDirector director = CreateDirector();
            SetField(director, "totalRooms", 10);
            SetField(director, "guaranteedCombatRooms", 0);
            SetField(director, "rewardRoomChance", 0f);
            SetField(director, "riskRoomChance", 0f);
            SetField(director, "maxRoomsWithoutReward", 3);
            SetField(director, "roomsSinceLastReward", 3);

            RunDirector.RoomType roomType = DetermineRoomType(director, 4);
            Assert.That(roomType, Is.EqualTo(RunDirector.RoomType.Reward));
        }

        [Test]
        public void DetermineRoomType_ForcesMinimumRewardsNearRunEnd()
        {
            RunDirector director = CreateDirector();
            SetField(director, "totalRooms", 6);
            SetField(director, "guaranteedCombatRooms", 0);
            SetField(director, "rewardRoomChance", 0f);
            SetField(director, "riskRoomChance", 0f);
            SetField(director, "minRewardRoomsPerRun", 2);
            SetField(director, "rewardRoomsSpawned", 0);
            SetField(director, "roomsSinceLastReward", 0);

            RunDirector.RoomType roomType = DetermineRoomType(director, 4);
            Assert.That(roomType, Is.EqualTo(RunDirector.RoomType.Reward));
        }

        private static RunDirector CreateDirector()
        {
            GameObject go = new GameObject("RunDirector_Test");
            return go.AddComponent<RunDirector>();
        }

        private static RunDirector.RoomType DetermineRoomType(RunDirector director, int roomNumber)
        {
            MethodInfo method = typeof(RunDirector).GetMethod("DetermineRoomType", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Could not find private method DetermineRoomType.");
            object result = method.Invoke(director, new object[] { roomNumber });
            return (RunDirector.RoomType)result;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
