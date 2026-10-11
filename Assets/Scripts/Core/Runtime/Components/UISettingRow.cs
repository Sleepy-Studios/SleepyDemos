using System;
using System.Collections.Generic;
using Core.Runtime.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 设置行只处理现有控件与显示，候选和保存由宿主负责；外观由 Prefab Variant 维护。
    public sealed class UISettingRow : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text value;
        [SerializeField] private TMP_Text reason;
        [SerializeField] private int fontId;
        [SerializeField] private bool overrideLabelFont;
        [SerializeField] private Selectable control;
        [SerializeField] private UIDropdown dropdown;
        [SerializeField] private UITab options;
        [SerializeField] private TMP_Text optionTemplateText;
        [SerializeField] private Slider slider;
        [SerializeField] private UIBtnSwitch toggle;
        [SerializeField] private TMP_Text onLabel;
        [SerializeField] private TMP_Text offLabel;
        [SerializeField] private Button action;

        private Action<float> sliderChanged;
        private Action actionClicked;

        /// 同一分页内稳定字段身份，不依赖层级名字或生成索引。
        public string Key => key;
        /// 用于焦点恢复的实际交互节点。
        public Selectable Control => control;

        private void Awake()
        {
            if (slider != null) slider.onValueChanged.AddListener(OnSliderChanged);
            if (action != null) action.onClick.AddListener(OnActionClicked);
        }

        /// <summary>绑定一次性的行标题，保留 Variant 的字号、颜色和间距。</summary>
        /// <param name="textKey">公共词表的稳定键。</param>
        public void SetLabel(string textKey) => LanguageService.SetText(label, textKey, overrideLabelFont ? fontId : null);

        /// <summary>设置动态值；不把绑定名称或数字当作词表键。</summary>
        /// <param name="text">已按当前文化格式化的业务显示值。</param>
        public void SetValue(string text)
        {
            if (value == null) return;
            LanguageService.SetFont(value, fontId);
            value.text = text;
        }

        /// <summary>一次性设置下拉候选，不发出初始化选择事件。</summary>
        /// <param name="values">已查询的本页文案或动态显示值。</param>
        /// <param name="selected">当前候选索引。</param>
        /// <param name="changed">用户选择回调，覆盖旧回调。</param>
        /// <param name="available">可选的逐项能力判断；不可用项保留显示但禁止选择。</param>
        public void SetOptions(IList<string> values, int selected, Action<int> changed, Func<int, bool> available = null)
        {
            if (dropdown == null) throw new InvalidOperationException("此设置行没有下拉控件。");
            if (optionTemplateText != null) LanguageService.SetFont(optionTemplateText, fontId);
            SetValue(selected >= 0 && selected < values.Count ? values[selected] : string.Empty);
            if (options != null) options.TrySelect = available;
            dropdown.SetData(values, changed, selected, isAsync: false);
            if (options == null) return;
            for (int index = 0; index < options.Items.Count; index++)
            {
                var item = options.Items[index];
                var selectable = item.GetComponent<Selectable>();
                if (selectable != null) selectable.interactable = available?.Invoke(index) ?? true;
                foreach (var text in item.GetComponentsInChildren<TMP_Text>(true))
                {
                    LanguageService.SetFont(text, fontId);
                }
            }
        }

        /// <summary>设置滑条候选范围并静默回显。</summary>
        /// <param name="range">制作配置允许范围。</param>
        /// <param name="selected">当前候选。</param>
        /// <param name="changed">用户修改回调，覆盖旧回调。</param>
        public void SetSlider(Vector2 range, float selected, Action<float> changed)
        {
            if (slider == null) throw new InvalidOperationException("此设置行没有滑条。");
            sliderChanged = null;
            slider.minValue = range.x;
            slider.maxValue = range.y;
            slider.SetValueWithoutNotify(selected);
            sliderChanged = changed;
        }

        /// <summary>设置开关候选并静默回显。</summary>
        /// <param name="selected">当前候选状态。</param>
        /// <param name="changed">用户修改回调，覆盖旧回调。</param>
        public void SetToggle(bool selected, Action<bool> changed)
        {
            if (toggle == null) throw new InvalidOperationException("此设置行没有开关。");
            if (onLabel != null) LanguageService.SetText(onLabel, "Common.On");
            if (offLabel != null) LanguageService.SetText(offLabel, "Common.Off");
            toggle.SetAction(changed);
            toggle.SetStatus(selected, false);
        }

        /// <summary>设置动作按钮回调。</summary>
        /// <param name="clicked">点击回调，覆盖旧回调。</param>
        public void SetAction(Action clicked) => actionClicked = clicked;

        /// <summary>显示实际支持状态，不改变候选请求值。</summary>
        /// <param name="enabled">当前是否可操作。</param>
        /// <param name="message">禁用或降级原因；空时隐藏原因文本。</param>
        public void SetAvailable(bool enabled, string message = null)
        {
            if (control != null) control.interactable = enabled;
            if (reason == null) return;
            LanguageService.SetFont(reason, fontId);
            reason.text = message ?? string.Empty;
            reason.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void OnSliderChanged(float selected) => sliderChanged?.Invoke(selected);
        private void OnActionClicked() => actionClicked?.Invoke();

        private void OnDestroy()
        {
            if (slider != null) slider.onValueChanged.RemoveListener(OnSliderChanged);
            if (action != null) action.onClick.RemoveListener(OnActionClicked);
            sliderChanged = null;
            actionClicked = null;
        }
    }
}
