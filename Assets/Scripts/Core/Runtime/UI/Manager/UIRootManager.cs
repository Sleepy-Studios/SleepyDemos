using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class UIRootManager : Singleton<UIRootManager>
    {
        private readonly Dictionary<UILayer, Transform> roots = new Dictionary<UILayer, Transform>();
        private readonly Vector2 referenceResolution = new Vector2(1920, 1080);
        private int openOrder;

        public Graphic Mask { get; private set; }
        public Camera UICamera { get; private set; }
        public Transform Root { get; private set; }

        public async UniTask BuildUIRoot()
        {
            if (Root != null)
            {
                return;
            }

            var rootGo = new GameObject("UIRoot");
            Object.DontDestroyOnLoad(rootGo);
            Root = rootGo.transform;

            EnsureCameraStack();
            CreateLayerRoots();
            CreateMask();
            EnsureEventSystem();
            await UniTask.Yield();
        }

        public Transform GetRoot(UILayer layer)
        {
            if (Root == null)
            {
                BuildUIRoot().Forget();
            }

            return roots.TryGetValue(layer, out var root) ? root : Root;
        }

        public void AttachViewCanvas(View view)
        {
            if (view.gameObject == null)
            {
                return;
            }

            var canvas = view.gameObject.GetComponent<Canvas>() ?? view.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = UICamera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = (int)view.Level * 100 + ++openOrder * 5;
            canvas.planeDistance = 10f;

            var scaler = view.gameObject.GetComponent<CanvasScaler>() ?? view.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            if (view.gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                view.gameObject.AddComponent<GraphicRaycaster>();
            }

            view.OpenOrder = openOrder;
        }

        private void EnsureCameraStack()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var mainGo = new GameObject("Main Camera");
                mainGo.tag = "MainCamera";
                mainCamera = mainGo.AddComponent<Camera>();
            }

            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
            var mainData = mainCamera.GetUniversalAdditionalCameraData();
            mainData.renderType = CameraRenderType.Base;

            var uiLayer = LayerMask.NameToLayer("UI");
            var uiGo = new GameObject("UI Camera");
            if (IsTagDefined("UICamera"))
            {
                uiGo.tag = "UICamera";
            }
            Object.DontDestroyOnLoad(uiGo);
            UICamera = uiGo.AddComponent<Camera>();
            UICamera.clearFlags = CameraClearFlags.Depth;
            UICamera.orthographic = false;
            UICamera.fieldOfView = 60f;
            UICamera.nearClipPlane = 0.01f;
            UICamera.farClipPlane = 100f;
            UICamera.cullingMask = uiLayer >= 0 ? 1 << uiLayer : -1;
            UICamera.depth = mainCamera.depth + 1;

            var uiData = UICamera.GetUniversalAdditionalCameraData();
            uiData.renderType = CameraRenderType.Overlay;
            if (!mainData.cameraStack.Contains(UICamera))
            {
                mainData.cameraStack.Add(UICamera);
            }
        }

        private static bool IsTagDefined(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            try
            {
                GameObject.FindGameObjectWithTag(tag);
                return true;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private void CreateLayerRoots()
        {
            roots.Clear();
            CreateLayerRoot(UILayer.Underground);
            CreateLayerRoot(UILayer.Base);
            CreateLayerRoot(UILayer.Foreground);
            CreateLayerRoot(UILayer.Pop);
            CreateLayerRoot(UILayer.Decorate);
            CreateLayerRoot(UILayer.Tip);
        }

        private void CreateLayerRoot(UILayer layer)
        {
            var go = new GameObject(layer.ToString());
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(Root, false);
            roots.Add(layer, go.transform);
        }

        private void CreateMask()
        {
            var maskGo = new GameObject("Mask");
            maskGo.layer = LayerMask.NameToLayer("UI");
            maskGo.transform.SetParent(GetRoot(UILayer.Pop), false);
            var rect = maskGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = maskGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.65f);
            image.raycastTarget = true;
            var button = maskGo.AddComponent<Button>();
            button.targetGraphic = image;
            maskGo.transform.localScale = Vector3.zero;
            Mask = image;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.transform.SetParent(Root, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
