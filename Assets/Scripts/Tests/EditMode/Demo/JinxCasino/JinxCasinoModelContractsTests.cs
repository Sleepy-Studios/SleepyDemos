using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class JinxCasinoModelContractsTests
    {
        [Test]
        public void CandidateAxisMarkersFollowUnityForwardUpAndRight()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Tests/EditMode/Demo/JinxCasino/Fixtures/AxisGate.fbx");
            Assert.That(source, Is.Not.Null);
            var root = Object.Instantiate(source);
            try
            {
                var markers = root.GetComponentsInChildren<Transform>();
                var front = System.Array.Find(markers, value => value.name == "Front");
                var right = System.Array.Find(markers, value => value.name == "Right");
                var up = System.Array.Find(markers, value => value.name == "Up");
                Assert.That(front, Is.Not.Null); Assert.That(right, Is.Not.Null); Assert.That(up, Is.Not.Null);
                Debug.Log("Jinx FBX axis: front=" + front.position + ", right=" + right.position + ", up=" + up.position + ", root=" + root.transform.eulerAngles);
                Assert.That(Vector3.Distance(front.position, Vector3.forward), Is.LessThan(.025f));
                Assert.That(Vector3.Distance(right.position, Vector3.right), Is.LessThan(.025f));
                Assert.That(Vector3.Distance(up.position, Vector3.up), Is.LessThan(.025f));
                foreach (var marker in markers)
                {
                    Assert.That(marker.localScale.x, Is.GreaterThan(0));
                    Assert.That(marker.localScale.y, Is.GreaterThan(0));
                    Assert.That(marker.localScale.z, Is.GreaterThan(0));
                    Assert.That(Quaternion.Angle(marker.localRotation, Quaternion.identity), Is.LessThan(.01f));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
