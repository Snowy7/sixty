using System.Reflection;
using NUnit.Framework;
using Sixty.World;
using UnityEngine;

namespace Sixty.Tests.EditMode
{
    public class RewardPickupTests
    {
        [TearDown]
        public void TearDown()
        {
            RewardPickup[] pickups = Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None);
            for (int i = 0; i < pickups.Length; i++)
            {
                if (pickups[i] != null)
                {
                    Object.DestroyImmediate(pickups[i].gameObject);
                }
            }

            GameObject iaUpdateManager = GameObject.Find("[IaUpdateManager]");
            if (iaUpdateManager != null)
            {
                Object.DestroyImmediate(iaUpdateManager);
            }
        }

        [Test]
        public void Configure_SetsLabel()
        {
            RewardPickup pickup = CreateRewardPickup();
            pickup.Configure("Damage Up", Color.red, _ => { });

            Assert.That(pickup.Label, Is.EqualTo("Damage Up"));
        }

        [Test]
        public void DoesNotAutoCollectWithoutInteractInput()
        {
            RewardPickup pickup = CreateRewardPickup();
            int collectedCount = 0;
            pickup.Configure("Speed Up", Color.yellow, _ => collectedCount++);

            SetField(pickup, "playerInRange", true);
            SetField(pickup, "canCollectAt", 0f);
            pickup.OnIaUpdate(0.1f);

            Assert.That(collectedCount, Is.EqualTo(0));
        }

        private static RewardPickup CreateRewardPickup()
        {
            GameObject go = new GameObject("RewardPickup_Test");
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            return go.AddComponent<RewardPickup>();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
