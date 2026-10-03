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
                var colors = target.colors;
                Color[] tint = { colors.normalColor, colors.highlightedColor, colors.selectedColor, colors.pressedColor, colors.disabledColor };
                for (int i = 0; i < names.Length; i++) definitions.Add(new UIStateInfo
                {
                    stateName = names[i], properties = new List<UIStateProperty>
                    { new UIStateProperty { propertyType = UIStatePropertyType.GraphicColor, target = target.targetGraphic, colorValue = original * tint[i] } }
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
