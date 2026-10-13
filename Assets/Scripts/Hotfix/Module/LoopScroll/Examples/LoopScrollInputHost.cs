using System;
using System.Collections;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Inputs;
using Cysharp.Threading.Tasks;
using SleepyStudios.LoopScroll.Samples;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hotfix.LoopScroll.Examples
{
    /// 本项目的动态 Showcase 输入桥接；不修改独立包或复制其列表实现。
    [DefaultExecutionOrder(-100)]
    public sealed class LoopScrollInputHost : MonoBehaviour
    {
        private const string EntranceScenePath = "Assets/Scenes/AppEntrance.unity";

        [SerializeField] private LoopSamplePage page;
        [SerializeField] private bool isMainMenu;
        [SerializeField] private Sprite focusBorder;
        [SerializeField] private StartupLoadingView returnLoadingPrefab;
        private UIMenuScope scope;
        private GameObject ownedEventSystem;
        private StartupLoadingView returnLoading;
        private AsyncOperation returnOperation;
        private bool returningToEntrance;

        private void Awake()
        {
            InputDeviceState.Initialize();
            if (EventSystem.current != null)
            {
                return;
            }
            ownedEventSystem = new GameObject("ShowcaseEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(ownedEventSystem, gameObject.scene);
            var module = ownedEventSystem.GetComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            UIRootManager.ConfigureMenuBindings(module);
        }

        private IEnumerator Start()
        {
            while (page != null && !page.IsReady)
            {
                yield return null;
            }
            if (page == null)
            {
                yield break;
            }
            if (focusBorder == null)
            {
                Debug.LogError("[LoopScrollInputHost] 请在宿主场景绑定公共 FocusBorder Sprite。", this);
                yield break;
            }
            foreach (var target in GetComponentsInChildren<Selectable>(true))
            {
                if (target.GetComponent<UIState>() != null || target.targetGraphic == null)
                {
                    continue;
                }
                var original = target.targetGraphic.color;
                var state = target.gameObject.AddComponent<UIState>();
                var definitions = new List<UIStateInfo>();
                string[] names = { "Normal", "Hover", "Focused", "Pressed", "Disabled" };
                var scale = target.transform.localScale;
                var feedback = new GameObject("InteractionFeedback", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                feedback.layer = target.gameObject.layer;
                feedback.transform.SetParent(target.transform, false);
                var frame = (RectTransform)feedback.transform;
                frame.anchorMin = Vector2.zero;
                frame.anchorMax = Vector2.one;
                frame.offsetMin = frame.offsetMax = Vector2.zero;
                var group = feedback.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                var image = feedback.GetComponent<Image>();
                image.sprite = focusBorder;
                image.type = Image.Type.Sliced;
                image.color = UIColors.Common.LoopFocus;
                image.raycastTarget = false;
                Color[] tint =
                {
                    original,
                    Color.Lerp(original, Color.white, 0.28f),
                    Color.Lerp(original, Color.white, 0.12f),
                    Color.Lerp(original, Color.black, 0.38f),
                    UIColors.Common.LoopDisabled
                };
                for (int i = 0; i < names.Length; i++)
                {
                    definitions.Add(new UIStateInfo
                    {
                        stateName = names[i],
                        properties = new List<UIStateProperty>
                        {
                            new UIStateProperty
                            {
                                propertyType = UIStatePropertyType.GraphicColor,
                                target = target.targetGraphic,
                                colorValue = tint[i]
                            },
                            new UIStateProperty
                            {
                                propertyType = UIStatePropertyType.CanvasGroupAlpha,
                                target = group,
                                floatValue = i == 2 ? 1f : i == 1 ? 0.35f : 0f
                            },
                            new UIStateProperty
                            {
                                propertyType = UIStatePropertyType.TransformLocalScale,
                                target = target.transform,
                                vector3Value = scale
                            }
                        }
                    });
                }
                state.ConfigureStates(definitions);
                target.transition = Selectable.Transition.None;
                if (target is Button)
                {
                    target.gameObject.AddComponent<UICancelRelay>();
                }
            }
            scope = gameObject.AddComponent<UIMenuScope>();
            scope.Canceled += Back;
        }

        private void Back()
        {
            if (page == null || page.IsNavigating || returningToEntrance)
            {
                return;
            }
            if (!isMainMenu)
            {
                page.NavigateTo("menu");
                return;
            }
            returningToEntrance = true;
            ReturnToEntranceAsync().Forget();
        }

        private async UniTaskVoid ReturnToEntranceAsync()
        {
            CanvasGroup interaction = null;
            bool previousInteractable = false;
            bool previousRaycasts = false;
            try
            {
                if (returnLoadingPrefab == null)
                {
                    throw new InvalidOperationException("宿主场景未绑定 StartupLoading Prefab。");
                }
                var cancellation = this.GetCancellationTokenOnDestroy();
                returnLoading = Instantiate(returnLoadingPrefab);
                SceneManager.MoveGameObjectToScene(returnLoading.gameObject, gameObject.scene);
                returnLoading.SetTitle("SleepyDemos");
                returnLoading.SetProgress(0f, LoopSampleLanguage.IsEnglish ? "Returning to Hub" : "正在返回主菜单");
                interaction = gameObject.GetOrAddComponent<CanvasGroup>();
                previousInteractable = interaction.interactable;
                previousRaycasts = interaction.blocksRaycasts;
                interaction.interactable = false;
                interaction.blocksRaycasts = false;
                scope?.SetNavigationEnabled(false);
                // 先让独立 Loading 跨过完整呈现帧，再启动真实入口加载。
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, cancellation);
#if UNITY_EDITOR
                if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(EntranceScenePath) == null)
                {
                    throw new InvalidOperationException("AppEntrance 场景不存在。");
                }
                returnOperation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    EntranceScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
                if (!Application.CanStreamedLevelBeLoaded(EntranceScenePath))
                {
                    throw new InvalidOperationException("构建未包含 AppEntrance 场景。");
                }
                returnOperation = SceneManager.LoadSceneAsync(EntranceScenePath, LoadSceneMode.Single);
#endif
                if (returnOperation == null)
                {
                    throw new InvalidOperationException("AppEntrance 未创建加载操作。");
                }
                returnOperation.allowSceneActivation = false;
                while (returnOperation.progress < 0.9f)
                {
                    returnLoading.SetProgress(returnOperation.progress,
                        LoopSampleLanguage.IsEnglish ? "Loading entry scene" : "正在载入入口场景");
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
                }
                returnLoading.SetProgress(returnOperation.progress,
                    LoopSampleLanguage.IsEnglish ? "Entering Hub" : "正在进入主菜单");
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, cancellation);
                returnOperation.allowSceneActivation = true;
                while (!returnOperation.isDone)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
                }
                if (returnLoading != null)
                {
                    returnLoading.SetProgress(1f, LoopSampleLanguage.IsEnglish ? "Loaded" : "加载完成");
                }
            }
            catch (OperationCanceledException)
            {
                // 来源销毁终止呈现；已启动的原生操作仍须释放激活闸。
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LoopScrollInputHost] 返回 AppEntrance 失败：{exception.Message}", this);
            }
            finally
            {
                if (returnOperation != null)
                {
                    returnOperation.allowSceneActivation = true;
                    returnOperation = null;
                }
                if (returnLoading != null)
                {
                    Destroy(returnLoading.gameObject);
                    returnLoading = null;
                }
                if (interaction != null)
                {
                    interaction.interactable = previousInteractable;
                    interaction.blocksRaycasts = previousRaycasts;
                }
                if (scope != null)
                {
                    scope.SetNavigationEnabled(true);
                }
                returningToEntrance = false;
            }
        }

        private void OnDestroy()
        {
            if (scope != null)
            {
                scope.Canceled -= Back;
            }
            if (returnOperation != null)
            {
                returnOperation.allowSceneActivation = true;
            }
            if (returnLoading != null)
            {
                Destroy(returnLoading.gameObject);
            }
            if (ownedEventSystem != null)
            {
                Destroy(ownedEventSystem);
            }
        }
    }
}
