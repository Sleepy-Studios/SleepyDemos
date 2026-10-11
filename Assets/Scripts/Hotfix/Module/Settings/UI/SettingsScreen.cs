using System;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Core.Runtime.Rendering.Streamline;
using Core.Runtime.Settings;
using Hotfix.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hotfix
{
    /// 设置宿主制作引用；分类显示和独立分页生命周期交给现有 ViewTab。
    public sealed class SettingsScreen : MonoBehaviour
    {
        [SerializeField] private ViewTab viewTab;
        [SerializeField] private RectTransform design;
        [SerializeField] private GameObject[] pageRoots = Array.Empty<GameObject>();
        [SerializeField] private CanvasGroup controls;
        [SerializeField] private Button apply;
        [SerializeField] private Button cancel;
        [SerializeField] private Button reset;
        [SerializeField] private Button close;
        [SerializeField] private UIMenuScope menu;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text categoryTitle;
        [SerializeField] private TMP_Text help;
        [SerializeField] private TMP_Text feedback;
        [SerializeField] private TMP_Text actual;
        [SerializeField] private TMP_Text applyLabel;
        [SerializeField] private TMP_Text cancelLabel;
        [SerializeField] private TMP_Text resetLabel;
        [SerializeField] private TMP_Text categoryTemplateText;
        private readonly List<View> views = new();
        private readonly List<SettingsCategory> categories = new();
        private GameSettingsSnapshot candidate;
        private GameSceneId host;
        private SettingsCategory category;
        private bool busy;
        private bool dirty;
        private bool active;
        private UnityAction applyAction;
        private UnityAction cancelAction;
        private Action cancelMenu;

        public SettingsCategory Category => category;
        public bool IsListening => InputPage?.IsListening == true;
        private InputSettingsPage InputPage
        {
            get
            {
                int index = categories.IndexOf(SettingsCategory.Input);
                return index >= 0 ? ((SettingsPanelView)views[index]).Editor as InputSettingsPage : null;
            }
        }

        /// <summary>初始化正式嵌套分页，不创建 UI、Canvas 或第二套分页系统。</summary>
        /// <param name="owner">父 View 持有子页销毁所有权，CloseAll 等待正式清理完成。</param>
        /// <param name="sceneHost">Hub 允许语言分类；各 Demo 保留自己的制作外观。</param>
        /// <param name="submit">请求提交，事务由宿主与页面外 Flow 负责。</param>
        /// <param name="dismiss">请求取消或关闭。</param>
        public void Initialize(View owner, GameSceneId sceneHost, Action submit, Action dismiss)
        {
            if (pageRoots.Length != 5 || viewTab == null || viewTab.UiTab == null)
            {
                throw new InvalidOperationException("设置宿主需要正式 ViewTab 与五个独立分页引用。");
            }
            host = sceneHost;
            RefreshLayout();
            candidate = GameSettingsService.Current;
            foreach (SettingsCategory item in Enum.GetValues(typeof(SettingsCategory)))
            {
                if (item == SettingsCategory.Language && host != GameSceneId.Hub)
                {
                    continue;
                }
                var panel = CreatePanel(item);
                owner.AddSubView(panel);
                panel.SetData(candidate, OnEdited);
                panel.InitWithGameObject(pageRoots[(int)item]);
                if (!panel.IsLoaded)
                {
                    throw new InvalidOperationException("设置分页初始化失败：" + item);
                }
                foreach (var row in panel.Editor.Rows)
                {
                    if (row.Control != null && row.Control.TryGetComponent<UIDropdown>(out var dropdown))
                    {
                        dropdown.SetPopupRoot(controls.transform);
                    }
                }
                categories.Add(item);
                views.Add(panel);
            }
            LanguageService.SetText(title, "Settings.Title");
            LanguageService.SetText(applyLabel, "Common.Apply");
            LanguageService.SetText(cancelLabel, "Common.Cancel");
            LanguageService.SetText(resetLabel, "Common.ResetDefaults");
            LanguageService.SetFont(categoryTemplateText);
            applyAction = () => submit();
            cancelAction = () => dismiss();
            cancelMenu = dismiss;
            apply.onClick.AddListener(applyAction);
            cancel.onClick.AddListener(cancelAction);
            close.onClick.AddListener(cancelAction);
            reset.onClick.AddListener(RestoreCategoryDefaults);
            menu.Canceled += cancelMenu;
            viewTab.Register(OnCategorySelected);
            viewTab.UiTab.TrySelect = _ => !busy;
        }

        /// <summary>交付新的独立候选；替换前释放全部改键会话，不能沿用旧候选 JSON。</summary>
        /// <param name="value">已保存设置或提交失败后保留的页面候选。</param>
        /// <param name="initialCategory">Demo 请求语言页时回到画质页。</param>
        public void Bind(GameSettingsSnapshot value, SettingsCategory initialCategory)
        {
            ReleaseEditors();
            candidate = value.Copy();
            category = categories.Contains(initialCategory) ? initialCategory : SettingsCategory.Graphics;
            dirty = false;
            foreach (SettingsPanelView panel in views)
            {
                panel.SetData(candidate, OnEdited);
            }
            if (active)
            {
                SelectCategory();
            }
            RefreshActual();
            RefreshButtons();
        }

        /// 激活 ViewTab；隐藏的宿主可重新进入，保留嵌套页用于导航回滚。
        public void Activate()
        {
            if (active)
            {
                return;
            }
            active = true;
            DisplaySettingsRuntime.Changed += RefreshRendering;
            foreach (SettingsPanelView panel in views)
            {
                panel.SetData(candidate, OnEdited);
            }
            SelectCategory();
            SetBusy(false);
            RefreshActual();
        }

        /// 停止编辑与改键，不提前销毁 ViewTab 拥有的嵌套页。
        public void Deactivate()
        {
            active = false;
            DisplaySettingsRuntime.Changed -= RefreshRendering;
            ReleaseEditors();
        }

        /// 捕获本页候选副本，不让提交任务持有可继续编辑的引用。
        public GameSettingsSnapshot Capture() => candidate.Copy();

        /// <summary>应用期间阻止重复提交与编辑，取消入口继续可用。</summary>
        /// <param name="value">本页或全局设置事务是否正在进行。</param>
        public void SetBusy(bool value)
        {
            busy = value;
            controls.interactable = !value;
            if (value)
            {
                LanguageService.SetText(feedback, "Settings.Applying");
            }
            RefreshButtons();
        }

        /// <summary>显示事务结果；内部诊断保留到 Console，不猜测原文翻译。</summary>
        /// <param name="result">服务提交、取消或失败的真实结果。</param>
        public void ShowResult(SettingsApplyResult result)
        {
            if (result.Succeeded)
            {
                Bind(GameSettingsService.Current, category);
            }
            LanguageService.SetText(feedback, result.Succeeded ? "Settings.Applied" : result.Canceled ? "Settings.Cancelled" : "Settings.ApplyFailed");
            if (!string.IsNullOrEmpty(result.Message))
            {
                Debug.Log("[Settings] " + result.Message);
            }
            RefreshActual();
            RefreshButtons();
        }

        /// 菜单返回优先退出监听，下一次返回才取消整个设置页。
        public void CancelRebind() => InputPage?.CancelRebind();

        private void SelectCategory()
        {
            var names = new List<string>();
            foreach (var item in categories)
            {
                names.Add(LanguageService.Get("Settings." + item));
            }
            // 候选回显不属于用户改页；语言重建仍在事务内时也必须初始化当前子页。
            var trySelect = viewTab.UiTab.TrySelect;
            try
            {
                viewTab.UiTab.TrySelect = null;
                viewTab.Init(names, views: views, index: categories.IndexOf(category), enableAnimation: false, isAsync: false);
            }
            finally
            {
                viewTab.UiTab.TrySelect = trySelect;
            }
            foreach (var item in viewTab.UiTab.Items)
            {
                foreach (var text in item.GetComponentsInChildren<TMP_Text>(true))
                {
                    LanguageService.SetFont(text);
                }
            }
        }

        private void OnCategorySelected(int index)
        {
            category = categories[index];
            LanguageService.SetText(categoryTitle, "Settings." + category);
            LanguageService.SetText(help, category == SettingsCategory.Language ? "Settings.LanguageHelp" : "Settings.Help" + category);
            RefreshActual();
            RefreshButtons();
        }

        private void OnEdited()
        {
            dirty = true;
            if (!busy)
            {
                LanguageService.SetText(feedback, "Settings.Pending");
            }
            RefreshActual();
            RefreshButtons();
        }

        private void RestoreCategoryDefaults()
        {
            if (busy)
            {
                return;
            }
            var defaults = GameSettingsService.GetDefaults();
            ReleaseEditors();
            switch (category)
            {
                case SettingsCategory.Graphics:
                    candidate.Graphics = defaults.Graphics.Copy();
                    break;
                case SettingsCategory.Audio:
                    candidate.Audio = defaults.Audio.Copy();
                    break;
                case SettingsCategory.Input:
                    candidate.Input = defaults.Input.Copy();
                    candidate.BindingOverrides = new Dictionary<string, string>(defaults.BindingOverrides);
                    break;
                case SettingsCategory.Language:
                    candidate.Language = defaults.Language;
                    break;
                case SettingsCategory.Accessibility:
                    candidate.Accessibility = defaults.Accessibility.Copy();
                    break;
            }
            foreach (SettingsPanelView panel in views)
            {
                panel.SetData(candidate, OnEdited);
            }
            OnEdited();
        }

        private void ReleaseEditors()
        {
            foreach (SettingsPanelView panel in views)
            {
                if (panel.Editor != null)
                {
                    panel.Editor.Release();
                }
            }
        }

        private void RefreshButtons()
        {
            if (apply != null)
            {
                apply.interactable = dirty && !busy && !IsListening;
            }
            if (reset != null)
            {
                reset.interactable = !busy;
            }
        }

        private void RefreshRendering()
        {
            if (category == SettingsCategory.Graphics && viewTab.CurrentClickView is SettingsPanelView panel && panel.IsEnable)
            {
                panel.Editor.Refresh();
            }
            RefreshActual();
        }

        private void RefreshActual()
        {
            if (actual == null || candidate == null)
            {
                return;
            }
            var current = GameSettingsService.Current;
            switch (category)
            {
                case SettingsCategory.Graphics:
                    var graphics = candidate.Graphics;
                    string requested = graphics.DlssMode == 0 ? AaLabel(graphics.Antialiasing, graphics.MsaaSamples)
                        : UpscalerLabel(StreamlineRuntime.DecodePreference(graphics.DlssMode));
                    string effective = DisplaySettingsRuntime.IsAntialiasingManagedByUpscaler
                        ? UpscalerLabel(StreamlineRuntime.EffectiveMode) : AaLabel(DisplaySettingsRuntime.EffectiveAntialiasing, DisplaySettingsRuntime.EffectiveMsaaSamples);
                    LanguageService.SetText(actual, "Settings.GraphicsState", null, requested, effective, Screen.width, Screen.height);
                    break;
                case SettingsCategory.Audio:
                    LanguageService.SetText(actual, "Settings.AudioState", null, current.Audio.Muted ? 0 : current.Audio.Master);
                    break;
                case SettingsCategory.Input:
                    LanguageService.SetText(actual, "Settings.InputState");
                    break;
                case SettingsCategory.Language:
                    string name = LanguageService.CurrentLanguage;
                    foreach (var language in LanguageService.Languages)
                    {
                        if (language.Id == name)
                        {
                            name = language.DisplayName;
                            break;
                        }
                    }
                    LanguageService.SetText(actual, "Settings.LanguageState", null, name);
                    break;
                case SettingsCategory.Accessibility:
                    LanguageService.SetText(actual, "Settings.MotionState", null, LanguageService.Get(current.Accessibility.IsReducedMotion ? "Common.On" : "Common.Off"));
                    break;
            }
        }

        private static string AaLabel(GraphicsAaMode mode, int samples) => mode == GraphicsAaMode.Msaa
            ? "MSAA " + samples + "×" : GraphicsSettingsPage.AaLabel(mode);
        private static string UpscalerLabel(StreamlineDlssMode? mode) => mode == StreamlineDlssMode.Dlaa ? "DLAA"
            : mode.HasValue ? "DLSS " + LanguageService.Get("Graphics.Dlss" + mode.Value) : LanguageService.Get("Common.Off");

        private static SettingsPanelView CreatePanel(SettingsCategory item) => item switch
        {
            SettingsCategory.Graphics => new GraphicsSettingsPanelView(),
            SettingsCategory.Audio => new AudioSettingsPanelView(),
            SettingsCategory.Input => new InputSettingsPanelView(),
            SettingsCategory.Language => new LanguageSettingsPanelView(),
            SettingsCategory.Accessibility => new AccessibilitySettingsPanelView(),
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };

        private void OnRectTransformDimensionsChange() => RefreshLayout();

        private void RefreshLayout()
        {
            if (design == null)
            {
                return;
            }
            var size = ((RectTransform)transform).rect.size;
            if (size.x > 0 && size.y > 0)
            {
                design.localScale = Vector3.one * Mathf.Min(size.x / 1920, size.y / 1080);
            }
        }

        private void OnDestroy()
        {
            Deactivate();
            if (apply != null)
            {
                apply.onClick.RemoveListener(applyAction);
            }
            if (cancel != null)
            {
                cancel.onClick.RemoveListener(cancelAction);
            }
            if (close != null)
            {
                close.onClick.RemoveListener(cancelAction);
            }
            if (reset != null)
            {
                reset.onClick.RemoveListener(RestoreCategoryDefaults);
            }
            if (menu != null)
            {
                menu.Canceled -= cancelMenu;
            }
            if (viewTab != null)
            {
                viewTab.Unregister(OnCategorySelected);
                if (viewTab.UiTab != null)
                {
                    viewTab.UiTab.TrySelect = null;
                }
            }
        }
    }
}
