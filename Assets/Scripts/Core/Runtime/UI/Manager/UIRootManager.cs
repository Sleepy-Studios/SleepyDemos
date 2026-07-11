using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
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

        public Graphic Mask { get; private set; }
        public Camera UICamera { get; private set; }
        public Transform Root { get; private set; }

        public async UniTask BuildUIRoot()
        {
            if (Root != null)
            {
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
            mainCamera.cullingMask &= ~(1 << uiLayer);
            var mainData = mainCamera.GetUniversalAdditionalCameraData();
            mainData.renderType = CameraRenderType.Base;

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
