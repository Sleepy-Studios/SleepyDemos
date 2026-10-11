using Core.Runtime;
using Core.Runtime.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix
{
    /// 体验页固定制作引用，子面板各自维护控件与作用域。
    public sealed class PresentationLabBindings : MonoBehaviour
    {
        [SerializeField] private RectTransform preview;
        [SerializeField] private CanvasGroup previewHud;
        [SerializeField] private ViewTab viewTab;
        [SerializeField] private GameObject[] panels;
        [SerializeField] private PresentationPostProcessRow[] effects;
        [SerializeField] private PresentationTimePanel time;
        [SerializeField] private PresentationCinematicPanel cinematic;
        [SerializeField] private Button play;
        [SerializeField] private Button stop;
        [SerializeField] private Button reset;
        [SerializeField] private Button close;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text clock;
        [SerializeField] private PresentationLabel[] labels;

        public RectTransform Preview => preview;
        public CanvasGroup PreviewHud => previewHud;
        public ViewTab Tabs => viewTab;
        public PresentationPostProcessRow[] Effects => effects;
        public PresentationTimePanel TimePanel => time;
        public PresentationCinematicPanel CinematicPanel => cinematic;
        public Button Play => play;
        public Button Stop => stop;
        public Button Reset => reset;
        public Button Close => close;
        public TMP_Text Status => status;
        public TMP_Text Clock => clock;

        /// 让制作坐标适应实际父 View，预览仍由其真实容器几何承载。
        public void RefreshLayout()
        {
            var design = (RectTransform)transform;
            var parent = (RectTransform)design.parent;
            float scale = Mathf.Min(parent.rect.width / 1920, parent.rect.height / 1080);
            design.anchorMin = design.anchorMax = design.pivot = Vector2.one * 0.5f;
            design.sizeDelta = new Vector2(1920, 1080);
            design.anchoredPosition = Vector2.zero;
            if (!Mathf.Approximately(design.localScale.x, scale))
            {
                design.localScale = new Vector3(scale, scale, 1);
            }
        }

        /// 仅在绑定完成后解析文案与 FontId，不监听语言变化。
        public void Initialize()
        {
            RefreshLayout();
            foreach (var dropdown in GetComponentsInChildren<UIDropdown>(true))
            {
                dropdown.SetPopupRoot(preview.parent);
            }
            foreach (var label in labels)
            {
                label.Apply();
            }
            foreach (var row in effects)
            {
                row.Initialize();
            }
            time.Initialize();
            cinematic.Initialize();
            viewTab.Init(new[] { LanguageService.Get("Presentation.Post"), LanguageService.Get("Presentation.Time"),
                LanguageService.Get("Presentation.Cinematic") }, localViewInstances: panels, index: 2, enableAnimation: false, isAsync: false);
            foreach (var item in viewTab.UiTab.Items)
            {
                foreach (var text in item.GetComponentsInChildren<TMP_Text>(true))
                {
                    LanguageService.SetFont(text);
                }
            }
            SelectTab(2);
        }

        /// <summary>切换本地模块控件；不销毁仍在运行的其它模块作用域。</summary>
        /// <param name="index">零画面处理、一时间控制、二电影演出。</param>
        public void SelectTab(int index)
        {
            var content = (RectTransform)panels[index].transform.parent;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, index == 0 ? effects.Length * 132 : index == 2 ? 710 : 430);
            var scroll = content.GetComponentInParent<ScrollRect>(true);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }
    }

}
