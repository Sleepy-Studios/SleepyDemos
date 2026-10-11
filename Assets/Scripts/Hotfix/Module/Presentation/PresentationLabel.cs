using Core.Runtime.Localization;
using TMPro;
using UnityEngine;

namespace Hotfix
{
    /// 制作叶文本的稳定词条引用，生命周期中一次性设置字体与文案。
    public sealed class PresentationLabel : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private TMP_Text text;

        /// 由所属页面初始化调用，不扫描场景，也不覆盖制作字号与布局。
        public void Apply() => LanguageService.SetText(text, key);
    }
}
