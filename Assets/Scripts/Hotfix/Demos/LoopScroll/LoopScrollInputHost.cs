using System.Collections;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Inputs;
using SleepyStudios.LoopScroll.Samples;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hotfix.Demos.LoopScroll
{
    /// 本项目的动态 Showcase 输入桥接；不修改独立包或复制其列表实现。
    [DefaultExecutionOrder(-100)]
    public sealed class LoopScrollInputHost : MonoBehaviour
    {
        [SerializeField] private LoopSamplePage page;
        [SerializeField] private bool isMainMenu;
        private UIMenuScope scope;
        private GameObject ownedEventSystem;
        private void Awake()
        {
            InputDeviceState.Initialize();
            if (EventSystem.current != null) return;
            ownedEventSystem = new GameObject("ShowcaseEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(ownedEventSystem, gameObject.scene);
            var module = ownedEventSystem.GetComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions(); UIRootManager.ConfigureMenuBindings(module);
        }
        private IEnumerator Start()
        {
            while (page != null && !page.IsReady) yield return null;
            if (page == null) yield break;
            foreach (var target in GetComponentsInChildren<Selectable>(true))
            {
                if (target.GetComponent<UIStateInteraction>() != null || target.targetGraphic == null) continue;
                var original = target.targetGraphic.color;
                var state = target.gameObject.AddComponent<UIState>();
                var definitions = new List<UIStateInfo>();
                string[] names = { "Normal", "Hover", "Focused", "Pressed", "Disabled" };
                var scale = target.transform.localScale;
                var feedback = new GameObject("InteractionFeedback", typeof(RectTransform), typeof(CanvasGroup));
                feedback.layer = target.gameObject.layer; feedback.transform.SetParent(target.transform, false);
                var frame = (RectTransform)feedback.transform;
                frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.offsetMin = frame.offsetMax = Vector2.zero;
                var group = feedback.GetComponent<CanvasGroup>(); group.interactable = group.blocksRaycasts = false;
                for (int edge = 0; edge < 4; edge++)
                {
                    var image = new GameObject("Edge" + edge, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    image.gameObject.layer = target.gameObject.layer; image.transform.SetParent(frame, false);
                    image.color = new Color(.2f, .85f, 1); image.raycastTarget = false;
                    var rect = image.rectTransform;
                    rect.anchorMin = edge < 2 ? new Vector2(0, edge) : new Vector2(edge - 2, 0);
                    rect.anchorMax = edge < 2 ? new Vector2(1, edge) : new Vector2(edge - 2, 1);
                    rect.sizeDelta = edge < 2 ? new Vector2(0, 3) : new Vector2(3, 0);
                }
                Color[] tint = { original, Color.Lerp(original, Color.white, .28f), Color.Lerp(original, Color.white, .12f),
                    Color.Lerp(original, Color.black, .38f), new Color(.42f, .44f, .47f, .6f) };
                for (int i = 0; i < names.Length; i++) definitions.Add(new UIStateInfo
                {
                    stateName = names[i], properties = new List<UIStateProperty>
                    {
                        new UIStateProperty { propertyType = UIStatePropertyType.GraphicColor, target = target.targetGraphic, colorValue = tint[i] },
                        new UIStateProperty { propertyType = UIStatePropertyType.CanvasGroupAlpha, target = group, floatValue = i == 2 ? 1 : i == 1 ? .35f : 0 },
                        new UIStateProperty { propertyType = UIStatePropertyType.TransformLocalScale, target = target.transform, vector3Value = scale * (target is Button && i == 3 ? .96f : 1) }
                    }
                });
                state.ConfigureStates(definitions); target.transition = Selectable.Transition.None;
                target.gameObject.AddComponent<UIStateInteraction>().Bind(state);
                if (target is Button) target.gameObject.AddComponent<UICancelRelay>();
            }
            scope = gameObject.AddComponent<UIMenuScope>(); scope.Canceled += Back;
        }
        private void Back()
        {
            if (isMainMenu) SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
            else page.NavigateTo("menu");
        }
        private void OnDestroy()
        {
            if (scope != null) scope.Canceled -= Back;
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
        }
    }
}
