using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Core.Runtime.Rendering.Streamline;
using Core.Runtime.Settings;
using TMPro;
using UnityEngine;

namespace Hotfix
{
    /// Hub 设置直接管理分类、独立候选与提交；五个分页由 ViewTab 按需加载。
    [Module("Settings")]
    [UIBind("HubSettingsView")]
    public partial class HubSettingsView : View
    {
        private readonly List<View> views = new();
        private readonly List<SettingsCategory> categories = new();
        private GameSettingsSnapshot candidate;
        private SettingsCategory category;
        private bool busy;
        private bool dirty;
        private bool active;
        private CancellationTokenSource lifetime;
        private CancellationTokenSource submittingCancellation;
        private UniTaskCompletionSource<SettingsApplyResult> submitting;
        private bool closing;
        private Vector2 layoutSize;

        public SettingsCategory Category => category;
        public bool IsListening => InputPage?.IsListening == true;
        private InputSettingsPanelView InputPage
        {
            get
            {
                int index = categories.IndexOf(SettingsCategory.Input);
                return index >= 0 ? views[index] as InputSettingsPanelView : null;
            }
        }

        protected override void OnGameObjectInitialize()
        {
            if (ViewTab_Editor_controls == null || ViewTab_Editor_controls.UiTab == null)
            {
                throw new InvalidOperationException("设置宿主需要正式 ViewTab 引用。");
            }
            layoutSize = Vector2.zero;
            RefreshLayout();
            candidate = GameSettingsService.Current;
            foreach (SettingsCategory item in Enum.GetValues(typeof(SettingsCategory)))
            {
                var panel = CreatePanel(item);
                AddSubView(panel);
                panel.SetData(candidate, OnEdited, CanvasGroup_Editor_controls.transform);
                categories.Add(item);
                views.Add(panel);
            }
            LanguageService.SetText(TextMeshProUGUI_Title, "Settings.Title");
            LanguageService.SetText(TextMeshProUGUI_Label1, "Common.Apply");
            LanguageService.SetText(TextMeshProUGUI_Label2, "Common.Cancel");
            LanguageService.SetText(TextMeshProUGUI_Label, "Common.ResetDefaults");
            LanguageService.SetFont(TextMeshProUGUI_Desc);
            Button_Apply.onClick.AddListener(OnApply);
            Button_Cancel.onClick.AddListener(OnCancel);
            Button_Close.onClick.AddListener(OnCancel);
            Button_Reset.onClick.AddListener(RestoreCategoryDefaults);
            UIMenuScope_Design.Canceled += OnCancel;
            ViewTab_Editor_controls.Register(OnCategorySelected);
            ViewTab_Editor_controls.UiTab.TrySelect = _ => !busy;
            BindData<SettingsData>(RefreshState);
            BindUpdate(RefreshLayout);
            base.OnGameObjectInitialize();
        }

        /// <summary>每次打开从当前保存快照创建完整候选，不提前加载未选分页。</summary>
        /// <param name="initialCategory">默认画质；切换语言重建时回到语言分类。</param>
        public void SetData(SettingsCategory initialCategory = SettingsCategory.Graphics)
            => BindCandidate(GameSettingsService.Current, initialCategory);

        protected override void OnShow()
        {
            lifetime = new CancellationTokenSource();
            Activate();
            RefreshLayout();
            RefreshState(GlobalData.Get<SettingsData>());
            base.OnShow();
        }

        protected override void OnHide()
        {
            submittingCancellation?.Cancel();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            Deactivate();
            base.OnHide();
        }

        private void RefreshState(SettingsData data)
        {
            SetBusy(submitting != null || data?.IsApplying == true || GameSettingsService.IsApplying || HubLanguageFlow.IsChanging);
        }

        private void OnApply()
        {
            if (submitting != null || lifetime == null || IsListening || SettingsFlow.IsSubmitting)
            {
                return;
            }
            var source = new UniTaskCompletionSource<SettingsApplyResult>();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            submitting = source;
            submittingCancellation = cancellation;
            SetBusy(true);
            SubmitAsync(Capture(), cancellation, source).Forget();
        }

        private async UniTask SubmitAsync(GameSettingsSnapshot candidate, CancellationTokenSource cancellation,
            UniTaskCompletionSource<SettingsApplyResult> source)
        {
            SettingsApplyResult result;
            try
            {
                result = await SettingsFlow.ApplyAsync(candidate, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                result = new SettingsApplyResult(false, canceled: true);
            }
            catch (Exception exception)
            {
                result = new SettingsApplyResult(false, exception.Message);
            }
            try
            {
                if (ReferenceEquals(submitting, source))
                {
                    submitting = null;
                    submittingCancellation = null;
                    if (IsEnable && gameObject != null)
                    {
                        ShowResult(result);
                        RefreshState(GlobalData.Get<SettingsData>());
                    }
                }
            }
            finally
            {
                cancellation.Dispose();
                source.TrySetResult(result);
            }
        }

        private void OnCancel()
        {
            if (IsListening)
            {
                CancelRebind();
                return;
            }
            CancelAndCloseAsync().Forget();
        }

        private async UniTask CancelAndCloseAsync()
        {
            if (closing)
            {
                return;
            }
            closing = true;
            try
            {
                var source = submitting;
                submittingCancellation?.Cancel();
                if (source != null)
                {
                    await source.Task;
                }
                await UIManager.Instance.CloseAsync(this);
            }
            finally
            {
                closing = false;
            }
        }

        /// <summary>页面外语言事务完成后将结果交给重建出的新宿主。</summary>
        /// <param name="result">完整语言应用与 UI 重建事务的结果。</param>
        public void ShowApplyResult(SettingsApplyResult result)
        {
            ShowResult(result);
            RefreshState(GlobalData.Get<SettingsData>());
        }

        /// <summary>交付新的独立候选；替换前释放全部改键会话，不能沿用旧候选 JSON。</summary>
        /// <param name="value">已保存设置或提交失败后保留的页面候选。</param>
        /// <param name="initialCategory">打开或提交后重新选择的 Hub 分类。</param>
        private void BindCandidate(GameSettingsSnapshot value, SettingsCategory initialCategory)
        {
            ReleaseEditors();
            candidate = value.Copy();
            category = categories.Contains(initialCategory) ? initialCategory : SettingsCategory.Graphics;
            dirty = false;
            foreach (SettingsPanelView panel in views)
            {
                panel.SetData(candidate, OnEdited, CanvasGroup_Editor_controls.transform);
            }
            if (active)
            {
                SelectCategory();
            }
            RefreshActual();
            RefreshButtons();
        }

        /// 激活 ViewTab；隐藏的宿主可重新进入，保留嵌套页用于导航回滚。
        private void Activate()
        {
            if (active)
            {
                return;
            }
            active = true;
            DisplaySettingsRuntime.Changed += RefreshRendering;
            foreach (SettingsPanelView panel in views)
            {
                panel.SetData(candidate, OnEdited, CanvasGroup_Editor_controls.transform);
            }
            SelectCategory();
            SetBusy(false);
            RefreshActual();
        }

        /// 停止编辑与改键，不提前销毁 ViewTab 拥有的嵌套页。
        private void Deactivate()
        {
            active = false;
            DisplaySettingsRuntime.Changed -= RefreshRendering;
            ReleaseEditors();
        }

        /// 捕获本页候选副本，不让提交任务持有可继续编辑的引用。
        public GameSettingsSnapshot Capture() => candidate.Copy();

        /// <summary>应用期间阻止重复提交与编辑，取消入口继续可用。</summary>
        /// <param name="value">本页或全局设置事务是否正在进行。</param>
        private void SetBusy(bool value)
        {
            busy = value;
            CanvasGroup_Editor_controls.interactable = !value;
            if (value)
            {
                LanguageService.SetText(TextMeshProUGUI_Feedback, "Settings.Applying");
            }
            RefreshButtons();
        }

        /// <summary>显示事务结果；内部诊断保留到 Console，不猜测原文翻译。</summary>
        /// <param name="result">服务提交、取消或失败的真实结果。</param>
        private void ShowResult(SettingsApplyResult result)
        {
            if (result.Succeeded)
            {
                BindCandidate(GameSettingsService.Current, category);
            }
            LanguageService.SetText(TextMeshProUGUI_Feedback, result.Succeeded ? "Settings.Applied" : result.Canceled ? "Settings.Cancelled" : "Settings.ApplyFailed");
            if (!string.IsNullOrEmpty(result.Message))
            {
                Debug.Log("[Settings] " + result.Message);
            }
            RefreshActual();
            RefreshButtons();
        }

        /// 菜单返回优先退出监听，下一次返回才取消整个设置页。
        private void CancelRebind() => InputPage?.CancelRebind();

        private void SelectCategory()
        {
            var names = new List<string>();
            foreach (var item in categories)
            {
                names.Add(LanguageService.Get("Settings." + item));
            }
            // UITab 异步制作期间只额外允许既定初选；语言重建的忙碌门卫不阻止首次加载。
            int selected = categories.IndexOf(category);
            ViewTab_Editor_controls.UiTab.TrySelect = index => !busy || index == selected;
            ViewTab_Editor_controls.Init(names, views: views, index: selected, action: () =>
            {
                ViewTab_Editor_controls.UiTab.TrySelect = _ => !busy;
                RefreshCategoryFonts();
            }, enableAnimation: false, isAsync: true);
        }

        private void RefreshCategoryFonts()
        {
            foreach (var item in ViewTab_Editor_controls.UiTab.Items)
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
            LanguageService.SetText(TextMeshProUGUI_Category_title, "Settings." + category);
            LanguageService.SetText(TextMeshProUGUI_Help, category == SettingsCategory.Language ? "Settings.LanguageHelp" : "Settings.Help" + category);
            RefreshActual();
            RefreshButtons();
        }

        private void OnEdited()
        {
            dirty = true;
            if (!busy)
            {
                LanguageService.SetText(TextMeshProUGUI_Feedback, "Settings.Pending");
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
                panel.SetData(candidate, OnEdited, CanvasGroup_Editor_controls.transform);
            }
            OnEdited();
        }

        private void ReleaseEditors()
        {
            foreach (SettingsPanelView panel in views)
            {
                if (panel.IsLoaded)
                {
                    panel.Release();
                }
            }
        }

        private void RefreshButtons()
        {
            if (Button_Apply != null)
            {
                Button_Apply.interactable = dirty && !busy && !IsListening;
            }
            if (Button_Reset != null)
            {
                Button_Reset.interactable = !busy;
            }
        }

        private void RefreshRendering()
        {
            if (category == SettingsCategory.Graphics && ViewTab_Editor_controls.CurrentClickView is SettingsPanelView panel && panel.IsEnable)
            {
                panel.Refresh();
            }
            RefreshActual();
        }

        private void RefreshActual()
        {
            if (TextMeshProUGUI_Actual_state == null || candidate == null)
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
                    LanguageService.SetText(TextMeshProUGUI_Actual_state, "Settings.GraphicsState", null, requested, effective, Screen.width, Screen.height);
                    break;
                case SettingsCategory.Audio:
                    LanguageService.SetText(TextMeshProUGUI_Actual_state, "Settings.AudioState", null, current.Audio.Muted ? 0 : current.Audio.Master);
                    break;
                case SettingsCategory.Input:
                    LanguageService.SetText(TextMeshProUGUI_Actual_state, "Settings.InputState");
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
                    LanguageService.SetText(TextMeshProUGUI_Actual_state, "Settings.LanguageState", null, name);
                    break;
                case SettingsCategory.Accessibility:
                    LanguageService.SetText(TextMeshProUGUI_Actual_state, "Settings.MotionState", null, LanguageService.Get(current.Accessibility.IsReducedMotion ? "Common.On" : "Common.Off"));
                    break;
            }
        }

        private static string AaLabel(GraphicsAaMode mode, int samples) => mode == GraphicsAaMode.Msaa
            ? "MSAA " + samples + "×" : GraphicsSettingsPanelView.AaLabel(mode);
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

        private void RefreshLayout()
        {
            if (RectTransform_Design == null)
            {
                return;
            }
            var size = ((RectTransform)transform).rect.size;
            if (size.x > 0 && size.y > 0 && size != layoutSize)
            {
                layoutSize = size;
                RectTransform_Design.localScale = Vector3.one * Mathf.Min(size.x / 1920, size.y / 1080);
            }
        }

        protected override void OnDestroy()
        {
            submittingCancellation?.Cancel();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            Deactivate();
            if (Button_Apply != null)
            {
                Button_Apply.onClick.RemoveListener(OnApply);
            }
            if (Button_Cancel != null)
            {
                Button_Cancel.onClick.RemoveListener(OnCancel);
            }
            if (Button_Close != null)
            {
                Button_Close.onClick.RemoveListener(OnCancel);
            }
            if (Button_Reset != null)
            {
                Button_Reset.onClick.RemoveListener(RestoreCategoryDefaults);
            }
            if (UIMenuScope_Design != null)
            {
                UIMenuScope_Design.Canceled -= OnCancel;
            }
            if (ViewTab_Editor_controls != null)
            {
                ViewTab_Editor_controls.Unregister(OnCategorySelected);
                if (ViewTab_Editor_controls.UiTab != null)
                {
                    ViewTab_Editor_controls.UiTab.TrySelect = null;
                }
            }
            base.OnDestroy();
        }
    }
}
