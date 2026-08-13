using System.Linq;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Tests
{
    public sealed class DronePrototypeContractTests
    {
        private const string PrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/DronePrototype.prefab";
        private const string ConfigPath = "Assets/LoadResources/Demos/drone_flight/Data/DroneFlightConfig.asset";

        [Test]
        public void Prefab_ContainsExactlyOneConfiguredRotorForEveryPosition()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab, Is.Not.Null);
            var rotors = prefab.GetComponentsInChildren<DroneRotor>(true);
            Assert.That(rotors, Has.Length.EqualTo(4));
            Assert.That(rotors.Select(rotor => rotor.Position).Distinct().Count(), Is.EqualTo(4));
            Assert.That(rotors.All(rotor => rotor.VisualPropeller != null), Is.True);
        }

        [Test]
        public void Prefab_RotorDirectionsMatchDocumentedXLayout()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var rotors = prefab.GetComponentsInChildren<DroneRotor>(true)
                .ToDictionary(rotor => rotor.Position);

            Assert.That(rotors[DroneRotorPosition.FrontLeft].Direction, Is.EqualTo(DroneRotorDirection.CounterClockwise));
            Assert.That(rotors[DroneRotorPosition.FrontRight].Direction, Is.EqualTo(DroneRotorDirection.Clockwise));
            Assert.That(rotors[DroneRotorPosition.RearLeft].Direction, Is.EqualTo(DroneRotorDirection.Clockwise));
            Assert.That(rotors[DroneRotorPosition.RearRight].Direction, Is.EqualTo(DroneRotorDirection.CounterClockwise));

            Assert.That(rotors[DroneRotorPosition.FrontLeft].transform.localPosition.x, Is.LessThan(0f));
            Assert.That(rotors[DroneRotorPosition.FrontLeft].transform.localPosition.z, Is.GreaterThan(0f));
            Assert.That(rotors[DroneRotorPosition.RearRight].transform.localPosition.x, Is.GreaterThan(0f));
            Assert.That(rotors[DroneRotorPosition.RearRight].transform.localPosition.z, Is.LessThan(0f));
        }

        [Test]
        public void Prefab_DynamicBodyUsesPrimitiveCompositeColliders()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab.GetComponentsInChildren<BoxCollider>(true).Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(prefab.GetComponentsInChildren<MeshCollider>(true), Is.Empty);
        }

        [Test]
        public void Config_DefaultValuesArePhysicallyValid()
        {
            var config = AssetDatabase.LoadAssetAtPath<DroneFlightConfig>(ConfigPath);

            Assert.That(config, Is.Not.Null);
            Assert.That(config.TryValidate(out var diagnostic), Is.True, diagnostic);
            Assert.That(config.MaximumRpm, Is.GreaterThan(0f));
            Assert.That(config.ThrustCoefficient, Is.GreaterThan(0f));
        }
    }
}
