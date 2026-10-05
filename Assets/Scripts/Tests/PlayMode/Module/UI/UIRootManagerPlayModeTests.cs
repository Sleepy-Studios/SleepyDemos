using System.Collections;
using System.Collections.Generic;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Tests.Module
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
        private Core.Runtime.Rendering.Streamline.StreamlineDlssMode? savedDlssMode;
        private bool restoreDlssMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var existingMainCamera = Camera.main;
            yield return UIRootManager.Instance.BuildUIRootAsync().ToCoroutine();
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
            if (restoreDlssMode)
            {
                Core.Runtime.Rendering.Streamline.StreamlineRuntime.SetMode(savedDlssMode);
                restoreDlssMode = false;
            }
        }

        [UnityTest]
        public IEnumerator BuildUIRoot_RebindsAfterPreviousBaseCameraIsDestroyed()
        {
            var manager = UIRootManager.Instance;
            var replacement = new GameObject("Temporary scene camera", typeof(Camera));
            manager.BindToBaseCamera(replacement.GetComponent<Camera>());
            Object.Destroy(replacement);
            yield return null;
            yield return manager.BuildUIRootAsync().ToCoroutine();
            Assert.That(manager.BaseCamera, Is.SameAs(Camera.main));
            Assert.That(manager.BaseCamera.GetUniversalAdditionalCameraData().cameraStack, Does.Contain(manager.UICamera));
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

            var interactionGate = manager.GetRoot(UILayer.Tip).Find("InteractionGate")
                ?.GetComponent<Image>();
            var tipContent = manager.GetRoot(UILayer.Tip).Find("TipContent");
            Assert.That(tipContent, Is.Not.Null);
            Assert.That(interactionGate, Is.Not.Null);
            Assert.That(tipContent.GetSiblingIndex(), Is.LessThan(interactionGate.transform.GetSiblingIndex()));
            Assert.That(interactionGate.color, Is.EqualTo(Color.clear));
            Assert.That(interactionGate.raycastTarget, Is.False);
            Assert.That(manager.InteractionGate.Count, Is.Zero);
        }

        [Test]
        public void InteractionGate_DoesNotMutateModalMaskPresentation()
        {
            var manager = UIRootManager.Instance;
            var mask = manager.Mask;
            var button = mask.GetComponent<Button>();
            var parent = mask.transform.parent;
            mask.transform.SetSiblingIndex(0);
            mask.transform.localScale = new Vector3(0.8f, 0.7f, 1f);
            button.interactable = false;
            var siblingIndex = mask.transform.GetSiblingIndex();
            var scale = mask.transform.localScale;
            var color = mask.color;

            manager.InteractionGate.Acquire();
            manager.InteractionGate.Release();

            Assert.That(mask.transform.parent, Is.SameAs(parent));
            Assert.That(mask.transform.GetSiblingIndex(), Is.EqualTo(siblingIndex));
            Assert.That(mask.transform.localScale, Is.EqualTo(scale));
            Assert.That(button.interactable, Is.False);
            Assert.That(mask.color, Is.EqualTo(color));
        }

        [UnityTest]
        public IEnumerator BuildUIRoot_WhenCalledAgain_ReusesRootAndCamera()
        {
            var manager = UIRootManager.Instance;
            var root = manager.Root;
            var uiCamera = manager.UICamera;

            yield return manager.BuildUIRootAsync().ToCoroutine();

            Assert.That(manager.Root, Is.SameAs(root));
            Assert.That(manager.UICamera, Is.SameAs(uiCamera));
        }

        [UnityTest]
        public IEnumerator BindToBaseCamera_MovesOverlayCameraBetweenUrpStacks()
        {
            savedDlssMode = Core.Runtime.Rendering.Streamline.StreamlineRuntime.RequestedMode;
            restoreDlssMode = true;
            Core.Runtime.Rendering.Streamline.StreamlineRuntime.SetMode(null);
            var manager = UIRootManager.Instance;
            var previousCamera = manager.BaseCamera;
            var nextObject = new GameObject("Next Base Camera");
            var nextCamera = nextObject.AddComponent<Camera>();

            manager.BindToBaseCamera(nextCamera);
            yield return null;

            Assert.That(manager.BaseCamera, Is.SameAs(nextCamera));
            Assert.That(
                nextCamera.GetUniversalAdditionalCameraData().cameraStack,
                Does.Contain(manager.UICamera));
            Assert.That(
                previousCamera.GetUniversalAdditionalCameraData().cameraStack,
                Has.No.Member(manager.UICamera));

            Object.Destroy(nextObject);
        }

        [UnityTest]
        public IEnumerator CloseAll_WhenMaskIsVisible_HidesMask()
        {
            var rootManager = UIRootManager.Instance;
            yield return UIManager.Instance.InitializeAsync().ToCoroutine();
            rootManager.Mask.transform.localScale = Vector3.one;

            yield return UIManager.Instance.CloseAllAsync().ToCoroutine();

            Assert.That(rootManager.Mask.transform.localScale, Is.EqualTo(Vector3.zero));
        }
    }
}
