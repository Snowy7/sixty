using NUnit.Framework;
using Sixty.Player;
using UnityEngine;

namespace Sixty.Tests.EditMode
{
    public class RunPassiveControllerTests
    {
        [TearDown]
        public void TearDown()
        {
            RunPassiveController[] controllers = Object.FindObjectsByType<RunPassiveController>(FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null)
                {
                    Object.DestroyImmediate(controllers[i].gameObject);
                }
            }

            GameObject iaUpdateManager = GameObject.Find("[IaUpdateManager]");
            if (iaUpdateManager != null)
            {
                Object.DestroyImmediate(iaUpdateManager);
            }
        }

        [Test]
        public void TryApplyPassive_OnlyAllowsOneSelection()
        {
            RunPassiveController controller = CreateController();

            bool firstApplied = controller.TryApplyPassive(RunPassiveType.Adrenaline);
            bool secondApplied = controller.TryApplyPassive(RunPassiveType.Overclock);

            Assert.That(firstApplied, Is.True);
            Assert.That(secondApplied, Is.False);
            Assert.That(controller.ActivePassive, Is.EqualTo(RunPassiveType.Adrenaline));
            Assert.That(controller.ActivePassiveLabel, Is.EqualTo("Adrenaline"));
        }

        [Test]
        public void PassiveLabels_ReturnExpectedValues()
        {
            Assert.That(RunPassiveController.GetPassiveLabel(RunPassiveType.Overclock), Is.EqualTo("Overclock"));
            Assert.That(RunPassiveController.GetPassiveLabel(RunPassiveType.SecondWind), Is.EqualTo("Second Wind"));
            Assert.That(RunPassiveController.GetPassiveLabel(RunPassiveType.None), Is.EqualTo("None"));
        }

        private static RunPassiveController CreateController()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "PassiveController_Test";
            go.AddComponent<Rigidbody>();
            go.AddComponent<PlayerController>();
            return go.AddComponent<RunPassiveController>();
        }
    }
}
