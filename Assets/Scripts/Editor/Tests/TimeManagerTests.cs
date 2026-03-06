using System.Reflection;
using NUnit.Framework;
using Sixty.Core;
using UnityEngine;

namespace Sixty.Tests.EditMode
{
    public class TimeManagerTests
    {
        private const string DeathCountPrefsKey = "Sixty.DeathCount";

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(DeathCountPrefsKey);
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            TimeManager[] managers = Object.FindObjectsByType<TimeManager>(FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null)
                {
                    Object.DestroyImmediate(managers[i].gameObject);
                }
            }

            GameObject iaUpdateManager = GameObject.Find("[IaUpdateManager]");
            if (iaUpdateManager != null)
            {
                Object.DestroyImmediate(iaUpdateManager);
            }
        }

        [Test]
        public void ResetRunClock_UsesDeathScalingAndCap()
        {
            TimeManager manager = CreateManager();
            SetField(manager, "baseTimeSeconds", 60f);
            SetField(manager, "timePerDeathSeconds", 10f);
            SetField(manager, "maxStartingTimeSeconds", 90f);
            SetAutoProperty(manager, "<DeathCount>k__BackingField", 4);

            manager.ResetRunClock();

            Assert.That(manager.TimeRemaining, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void Tick_DoesNotAdvanceWhenClockIsPaused()
        {
            TimeManager manager = CreateManager();
            SetField(manager, "baseTimeSeconds", 20f);
            SetField(manager, "timePerDeathSeconds", 0f);
            SetField(manager, "maxStartingTimeSeconds", 20f);
            SetAutoProperty(manager, "<DeathCount>k__BackingField", 0);
            manager.ResetRunClock();

            manager.SetClockPaused(true);
            manager.Tick(5f);
            Assert.That(manager.TimeRemaining, Is.EqualTo(20f).Within(0.001f));

            manager.SetClockPaused(false);
            manager.Tick(5f);
            Assert.That(manager.TimeRemaining, Is.EqualTo(15f).Within(0.001f));
        }

        [Test]
        public void TakeDamage_TriggersTimeoutOnlyOnce()
        {
            TimeManager manager = CreateManager();
            SetField(manager, "restartSceneOnTimeout", false);
            SetAutoProperty(manager, "<TimeRemaining>k__BackingField", 1f);
            SetField(manager, "hasTimedOut", false);
            SetField(manager, "isClockPaused", false);

            int timeoutCount = 0;
            manager.OnTimeOut += () => timeoutCount++;

            int initialDeathCount = manager.DeathCount;
            manager.TakeDamage(2f);
            manager.TakeDamage(2f);

            Assert.That(manager.TimeRemaining, Is.EqualTo(0f).Within(0.001f));
            Assert.That(manager.DeathCount, Is.EqualTo(initialDeathCount + 1));
            Assert.That(timeoutCount, Is.EqualTo(1));
        }

        private static TimeManager CreateManager()
        {
            GameObject go = new GameObject("TimeManager_Test");
            return go.AddComponent<TimeManager>();
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }

        private static void SetAutoProperty(object target, string backingFieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(backingFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing backing field '{backingFieldName}'.");
            field.SetValue(target, value);
        }
    }
}
