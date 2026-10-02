#if UNITY_EDITOR
using System;
using System.Collections;
using Hotfix.JinxCasino.Adapters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 真实Collider射线及相机恢复回归；不代替已装配机台的视觉和设备实玩。
    public sealed class JinxCasinoTableInteractionTests
    {
        private GameObject root;
        private Camera camera;
        private JinxCasinoStation station;
        private JinxCasinoTableTarget first;
        private JinxCasinoTableTarget second;
        private JinxCasinoTableFocus focus;
        private JinxCasinoTableSelection selection;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("JinxTable regression");
            var cameraObject = new GameObject("Borrowed camera");
            cameraObject.transform.SetParent(root.transform);
            camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(0, 1, -3); camera.fieldOfView = 62;
            var tableObject = new GameObject("Table"); tableObject.transform.SetParent(root.transform);
            station = tableObject.AddComponent<JinxCasinoStation>();
            var pose = new GameObject("FocusPose").transform; pose.SetParent(station.transform);
            pose.position = new Vector3(0, 1, -1);
            first = MakeTarget("confirm", new Vector3(-0.3f, 1, 0), 10);
            second = MakeTarget("pull", new Vector3(0.3f, 1, 0), 20);
            station.ConfigureTable("sample-slots-01", pose, 46, new[] { second, first });
            focus = new JinxCasinoTableFocus(camera);
            selection = new JinxCasinoTableSelection(); selection.Bind(station);
            Physics.SyncTransforms();
        }

        private JinxCasinoTableTarget MakeTarget(string id, Vector3 position, int order)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = id; go.transform.SetParent(station.transform);
            go.transform.position = position; go.transform.localScale = Vector3.one * 0.2f;
            var target = go.AddComponent<JinxCasinoTableTarget>();
            target.Configure(id, JinxCasinoTableAction.Primary, 0, order, new[] { go.GetComponent<Renderer>() }, null);
            return target;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            selection?.Dispose(); focus?.Dispose(); Object.Destroy(root); yield return null;
        }

        [Test]
        public void FocusRestoresExactBorrowedPoseAndRejectsNestedEntry()
        {
            var position = camera.transform.position;
            var rotation = Quaternion.Euler(12, 17, 0); camera.transform.rotation = rotation;
            Assert.That(focus.TryEnter(station), Is.True);
            Assert.That(focus.TryEnter(station), Is.False);
            Assert.That(focus.IsReady, Is.False);
            focus.Tick(0.35f);
            Assert.That(focus.IsReady, Is.True);
            Assert.That(Vector3.Distance(camera.transform.position, station.FocusPose.position), Is.LessThan(0.00001f));
            station.transform.position += Vector3.right;
            focus.Tick(0.02f);
            Assert.That(Vector3.Distance(camera.transform.position, station.FocusPose.position), Is.LessThan(0.00001f));
            focus.Exit(); focus.Tick(0.35f);
            Assert.That(focus.IsActive, Is.False);
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(0.001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(62));
        }

        [Test]
        public void DisabledStationRestoresCameraWithoutNeedingTransitionCompletion()
        {
            var position = camera.transform.position;
            focus.TryEnter(station, 0); station.gameObject.SetActive(false); focus.Tick(0);
            Assert.That(focus.IsActive, Is.False);
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(camera.fieldOfView, Is.EqualTo(62));
        }

        [Test]
        public void NavigationSkipsDisabledTargetsAndNeverInvokesThem()
        {
            int commands = 0; first.Invoked += _ => commands++;
            Assert.That(selection.Navigate(1), Is.SameAs(first));
            Assert.That(selection.TryInvoke(), Is.True);
            first.SetAvailable(false, "已经提交");
            Assert.That(selection.TryInvoke(), Is.False);
            Assert.That(selection.Navigate(1), Is.SameAs(second));
            second.SetAvailable(false, "等待开奖");
            Assert.That(selection.Navigate(-1), Is.Null);
            Assert.That(commands, Is.EqualTo(1));
        }

        [Test]
        public void RealPointerRayCannotClickThroughBlockerOrAnotherTable()
        {
            Vector3 screenPoint = camera.WorldToScreenPoint(first.transform.position);
            Ray ray = camera.ScreenPointToRay(screenPoint);
            Assert.That(selection.Point(ray), Is.SameAs(first));
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.SetParent(root.transform);
            blocker.transform.position = ray.GetPoint(1); blocker.transform.localScale = Vector3.one * 0.4f;
            Physics.SyncTransforms();
            Assert.That(selection.Point(ray), Is.Null);
            blocker.SetActive(false);
            second.transform.SetParent(root.transform);
            selection.Bind(station); Physics.SyncTransforms();
            Assert.That(selection.Point(camera.ScreenPointToRay(camera.WorldToScreenPoint(second.transform.position))), Is.Null);
        }

        [Test]
        public void TableBindingRejectsDuplicateIdsAndExternalTargets()
        {
            Assert.Throws<ArgumentException>(() => station.ConfigureTable("id", station.FocusPose, 48, new[] { first, first }));
            second.transform.SetParent(root.transform);
            Assert.Throws<ArgumentException>(() => station.ConfigureTable("id", station.FocusPose, 48, new[] { second }));
            Assert.That(station.StationId, Is.EqualTo("sample-slots-01"));
        }

        [Test]
        public void PointerRestoresHighlightWhenSameTargetBecomesAvailableAgain()
        {
            var ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(first.transform.position));
            var renderer = first.GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            selection.Point(ray); renderer.GetPropertyBlock(properties);
            Color focusedColor = properties.GetColor("_BaseColor");
            first.SetAvailable(false, "等待完成");
            first.SetAvailable(true);
            Assert.That(selection.Point(ray), Is.SameAs(first));
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_BaseColor"), Is.EqualTo(focusedColor));
        }

        [UnityTest]
        public IEnumerator DestroyedTargetDoesNotBreakRemainingNavigation()
        {
            selection.Navigate(1);
            Object.Destroy(first.gameObject); yield return null;
            Assert.That(selection.Navigate(1), Is.SameAs(second));
            Assert.That(selection.Navigate(-1), Is.SameAs(second));
        }
    }
}
#endif
