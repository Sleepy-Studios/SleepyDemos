using System.Collections;
using System.Collections.Generic;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Core.Tests.UI
{
    public sealed class UIRootManagerPlayModeTests
    {
        private static readonly IReadOnlyDictionary<UILayer, int> SortingOrders =
            new Dictionary<UILayer, int>
            {
                { UILayer.Underground, 0 },
                { UILayer.Base, 100 },
                { UILayer.Foreground, 150 },
                { UILayer.Pop, 200 },
                { UILayer.Decorate, 250 },
                { UILayer.Tip, 300 }
            };

        private GameObject createdMainCamera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var existingMainCamera = Camera.main;
            yield return UIRootManager.Instance.BuildUIRoot().ToCoroutine();
            if (existingMainCamera == null && Camera.main != null)
            {
                createdMainCamera = Camera.main.gameObject;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var manager = UIRootManager.Instance;
            if (manager.Root != null)
            {
                Object.Destroy(manager.Root.gameObject);
            }

            if (manager.UICamera != null)
            {
                Object.Destroy(manager.UICamera.gameObject);
            }

            if (createdMainCamera != null)
            {
                Object.Destroy(createdMainCamera);
            }

            yield return null;
        }

        [Test]
        public void BuildUIRoot_CreatesConfiguredRootCanvasAndPerspectiveCamera()
        {
            var manager = UIRootManager.Instance;
            Assert.That(manager.Root.name, Is.EqualTo("UIRootCanvas"));

            var canvas = manager.Root.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(canvas.worldCamera, Is.SameAs(manager.UICamera));
            Assert.That(canvas.planeDistance, Is.EqualTo(10f));
            Assert.That(canvas.additionalShaderChannels, Is.EqualTo(
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent));
            Assert.That(manager.Root.GetComponent<GraphicRaycaster>(), Is.Null);

            var scaler = manager.Root.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f));

            Assert.That(manager.UICamera, Is.Not.Null);
            Assert.That(manager.UICamera.orthographic, Is.False);
            Assert.That(manager.UICamera.fieldOfView, Is.EqualTo(60f));
            Assert.That(manager.UICamera.nearClipPlane, Is.EqualTo(0.01f));
            Assert.That(manager.UICamera.farClipPlane, Is.EqualTo(100f));
        }

        [Test]
        public void BuildUIRoot_CreatesConfiguredLayerCanvasesAndMask()
        {
            var manager = UIRootManager.Instance;
            foreach (var pair in SortingOrders)
            {
                var layerRoot = manager.GetRoot(pair.Key);
                Assert.That(layerRoot.name, Is.EqualTo($"{pair.Key}Layer"));

                var rectTransform = layerRoot as RectTransform;
                Assert.That(rectTransform, Is.Not.Null);
                Assert.That(rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(rectTransform.offsetMax, Is.EqualTo(Vector2.zero));

                var canvas = layerRoot.GetComponent<Canvas>();
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.overrideSorting, Is.True);
                Assert.That(canvas.sortingOrder, Is.EqualTo(pair.Value));
                Assert.That(layerRoot.GetComponent<CanvasScaler>(), Is.Null);

                var shouldRaycast = pair.Key != UILayer.Underground;
                Assert.That(layerRoot.GetComponent<GraphicRaycaster>() != null, Is.EqualTo(shouldRaycast));
            }

            Assert.That(manager.Mask, Is.Not.Null);
            Assert.That(manager.Mask.transform.parent, Is.SameAs(manager.GetRoot(UILayer.Pop)));
        }

        [UnityTest]
        public IEnumerator BuildUIRoot_WhenCalledAgain_ReusesRootAndCamera()
        {
            var manager = UIRootManager.Instance;
            var root = manager.Root;
            var uiCamera = manager.UICamera;

            yield return manager.BuildUIRoot().ToCoroutine();

            Assert.That(manager.Root, Is.SameAs(root));
            Assert.That(manager.UICamera, Is.SameAs(uiCamera));
        }
    }
}
