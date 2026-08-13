using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Hotfix.Tests
{
    public sealed class DroneLandingGearStateMachineTests
    {
        [Test]
        public void StateMachine_TransitionsBothDirectionsAndResetsDeployed()
        {
            var stateMachine = new DroneLandingGearStateMachine();

            stateMachine.Step(false, 1f, 0.25f);
            Assert.That(stateMachine.State, Is.EqualTo(DroneLandingGearState.Retracting));
            Assert.That(stateMachine.NormalizedPosition, Is.EqualTo(0.25f).Within(0.001f));

            stateMachine.Step(false, 1f, 0.75f);
            Assert.That(stateMachine.State, Is.EqualTo(DroneLandingGearState.Retracted));

            stateMachine.Step(true, 1f, 0.5f);
            Assert.That(stateMachine.State, Is.EqualTo(DroneLandingGearState.Deploying));

            stateMachine.ResetDeployed();
            Assert.That(stateMachine.State, Is.EqualTo(DroneLandingGearState.Deployed));
            Assert.That(stateMachine.NormalizedPosition, Is.Zero);
        }

        [Test]
        public void Controller_ToggleChangesOnlyManualTargetOncePerCall()
        {
            var root = new GameObject("GearFixture");
            var controller = root.AddComponent<DroneLandingGearController>();

            Assert.That(controller.IsDeploymentRequested, Is.True);
            controller.Toggle();
            Assert.That(controller.IsDeploymentRequested, Is.False);
            root.transform.position = Vector3.up * 100f;
            Assert.That(controller.IsDeploymentRequested, Is.False, "高度变化不得自动改变目标。");
            controller.Toggle();
            Assert.That(controller.IsDeploymentRequested, Is.True);
            Object.DestroyImmediate(root);
        }
    }
}
