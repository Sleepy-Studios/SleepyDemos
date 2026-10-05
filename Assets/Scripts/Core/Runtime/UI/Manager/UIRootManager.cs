using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Core.Runtime
{
    public sealed class UIRootManager : Singleton<UIRootManager>
    {
        private readonly struct LayerDefinition
        {
            public LayerDefinition(UILayer layer, int sortingOrder, bool enableRaycaster)
            {
                Layer = layer;
                SortingOrder = sortingOrder;
                EnableRaycaster = enableRaycaster;
            }

            public UILayer Layer { get; }
            public int SortingOrder { get; }
            public bool EnableRaycaster { get; }
        }

        private static readonly LayerDefinition[] LayerDefinitions =
        {
            new LayerDefinition(UILayer.Underground, 0, false),
            new LayerDefinition(UILayer.Base, 100, true),
            new LayerDefinition(UILayer.Foreground, 150, true),
            new LayerDefinition(UILayer.Pop, 200, true),
            new LayerDefinition(UILayer.Decorate, 250, true),
            new LayerDefinition(UILayer.Tip, 300, true)
        };

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private readonly Dictionary<UILayer, Transform> roots = new Dictionary<UILayer, Transform>();
        private Transform tipContentRoot;

        public UIInteractionGate InteractionGate { get; } = new UIInteractionGate();

        public Graphic Mask { get; private set; }
        public Camera BaseCamera { get; private set; }
        public Camera UICamera { get; private set; }
        public Transform Root { get; private set; }

        public async UniTask BuildUIRootAsync()
        {
            if (Root != null)
            {
                // 再次进入启动场景时复用 UI 根，但基础相机属于新的场景实例。
                var camera = Camera.main;
                if (BaseCamera == null && camera != null) BindToBaseCamera(camera);
                return;
            }

            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0)
            {
                throw new InvalidOperationException("项目缺少 UI Layer，无法初始化 Core UI 运行时。");
            }

            EnsureCameraStack(uiLayer);

            var rootGo = new GameObject(
                "UIRootCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            rootGo.layer = uiLayer;
            Object.DontDestroyOnLoad(rootGo);
            Root = rootGo.transform;

            ConfigureRootCanvas(rootGo);
            CreateLayerRoots(uiLayer);
            CreateTipContentRoot(uiLayer);
            CreateMask();
            CreateInteractionGate(uiLayer);
            EnsureEventSystem();
            await UniTask.Yield();
        }

        public Transform GetRoot(UILayer layer)
        {
            if (Root == null)
            {
                BuildUIRootAsync().Forget();
            }

            return roots.TryGetValue(layer, out var root) ? root : Root;
        }

        internal Transform GetViewRoot(UILayer layer)
        {
            return layer == UILayer.Tip && tipContentRoot != null
                ? tipContentRoot
                : GetRoot(layer);
        }

        /// <summary>
        /// 将持久化 UI Camera 重新绑定到指定 URP Base Camera。
        /// </summary>
        /// <param name="baseCamera">当前活动内容场景的基础相机。</param>
        public void BindToBaseCamera(Camera baseCamera)
        {
            if (baseCamera == null)
            {
                throw new ArgumentNullException(nameof(baseCamera));
            }

            if (UICamera == null)
            {
                throw new InvalidOperationException("UI Camera 尚未初始化，无法重新绑定基础相机。");
            }

            Rendering.Streamline.StreamlineRuntime.DetachCamera();

            if (BaseCamera != null && BaseCamera != baseCamera)
            {
                var previousData = BaseCamera.GetUniversalAdditionalCameraData();
                previousData.cameraStack.Remove(UICamera);
            }

            var uiLayer = LayerMask.NameToLayer("UI");
            baseCamera.cullingMask &= ~(1 << uiLayer);
            var baseData = baseCamera.GetUniversalAdditionalCameraData();
            baseData.renderType = CameraRenderType.Base;
            if (!baseData.cameraStack.Contains(UICamera))
            {
                baseData.cameraStack.Add(UICamera);
            }

            BaseCamera = baseCamera;
            UICamera.depth = baseCamera.depth + 1;
            Rendering.Streamline.StreamlineRuntime.BindCamera(baseCamera, UICamera);
        }

        private void ConfigureRootCanvas(GameObject rootGo)
        {
            var canvas = rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = UICamera;
            canvas.planeDistance = 10f;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;

            var scaler = rootGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void EnsureCameraStack(int uiLayer)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var mainGo = new GameObject("Main Camera");
                mainGo.tag = "MainCamera";
                mainCamera = mainGo.AddComponent<Camera>();
            }

            mainCamera.clearFlags = CameraClearFlags.Skybox;

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
            UICamera.cullingMask = 1 << uiLayer;
            UICamera.depth = mainCamera.depth + 1;

            var uiData = UICamera.GetUniversalAdditionalCameraData();
            uiData.renderType = CameraRenderType.Overlay;
            BindToBaseCamera(mainCamera);
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

        private void CreateLayerRoots(int uiLayer)
        {
            roots.Clear();
            for (var i = 0; i < LayerDefinitions.Length; i++)
            {
                CreateLayerRoot(LayerDefinitions[i], uiLayer);
            }
        }

        private void CreateLayerRoot(LayerDefinition definition, int uiLayer)
        {
            var go = new GameObject(
                $"{definition.Layer}Layer",
                typeof(RectTransform),
                typeof(Canvas));
            go.layer = uiLayer;

            var rectTransform = go.GetComponent<RectTransform>();
            rectTransform.SetParent(Root, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = definition.SortingOrder;

            if (definition.EnableRaycaster)
            {
                go.AddComponent<GraphicRaycaster>();
            }

            roots.Add(definition.Layer, rectTransform);
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

        private void CreateTipContentRoot(int uiLayer)
        {
            var contentGo = new GameObject("TipContent", typeof(RectTransform));
            contentGo.layer = uiLayer;
            var rect = contentGo.GetComponent<RectTransform>();
            rect.SetParent(GetRoot(UILayer.Tip), false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            tipContentRoot = rect;
        }

        private void CreateInteractionGate(int uiLayer)
        {
            var gateGo = new GameObject(
                "InteractionGate",
                typeof(RectTransform),
                typeof(Image));
            gateGo.layer = uiLayer;
            var rect = gateGo.GetComponent<RectTransform>();
            rect.SetParent(GetRoot(UILayer.Tip), false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = gateGo.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;
            InteractionGate.Initialize(image);
            InteractionGate.EnsureOnTop();
        }

        /// <summary>宿主和独立动态示例共用的 Submit/Cancel 用途绑定。</summary>
        /// <param name="module">当前唯一的 UI 输入模块。</param>
        public static void ConfigureMenuBindings(InputSystemUIInputModule module)
        {
            foreach (var action in new[] { module.submit?.action, module.cancel?.action })
            {
                if (action == null) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    string path = action.bindings[i].path;
                    if (path == "<Gamepad>/buttonSouth") action.ApplyBindingOverride(i, "<Gamepad>/{Submit}");
                    else if (path == "<Gamepad>/buttonEast") action.ApplyBindingOverride(i, "<Gamepad>/{Cancel}");
                }
            }
        }

        private void EnsureEventSystem()
        {
            Core.Runtime.Inputs.InputDeviceState.Initialize();
            if (EventSystem.current != null)
            {
                var existingModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
                if (existingModule != null) ConfigureMenuBindings(existingModule);
                return;
            }

            var go = new GameObject("EventSystem");
            go.transform.SetParent(Root, false);
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            ConfigureMenuBindings(module);
        }
    }
}
