namespace Hotfix
{
    using Core.Runtime;
    using Core.Runtime.Localization;
    using TMPro;
    using UnityEngine;

    [Module("Common")]
    [UIBind("CommonLoadingView")]
    public partial class CommonLoadingView : View
    {
        private UIProgressBar progressBar;
        private float currentProgress;

        protected override void OnGameObjectInitialize()
        {
            progressBar = Image_ProgressFill.GetComponent<UIProgressBar>();
            ResetProgress();
        }

        /// <summary>
        /// 设置加载标题。
        /// </summary>
        /// <param name="title">语言尚未就绪或未指定键时使用的原文；为空时清空。</param>
        /// <param name="titleKey">明确的文案键；语言就绪后一次性应用词条 FontId 与材质。</param>
        public void SetTitle(string title, string titleKey = null)
        {
            if (TextMeshProUGUI_Title != null)
            {
                SetLoadingText(TextMeshProUGUI_Title, title, titleKey);
            }
        }

        /// <summary>
        /// 更新单调递增的加载进度和说明文本。
        /// </summary>
        /// <param name="progress">目标进度，会限制到 0 到 1 且不会倒退。</param>
        /// <param name="step">当前加载步骤。</param>
        /// <param name="description">步骤补充说明；为空时清空。</param>
        /// <param name="size">可用的真实大小信息；为空时隐藏大小文本。</param>
        /// <param name="stepKey">明确的步骤文案键；为空或语言未就绪时显示 step 原文。</param>
        /// <param name="descriptionKey">完整描述句式键；为空或语言未就绪时显示 description 原文。</param>
        /// <param name="args">描述句式参数；步骤文案不使用这些参数。</param>
        public void SetProgress(float progress, string step, string description = null, string size = null,
            string stepKey = null, string descriptionKey = null, params object[] args)
        {
            currentProgress = Mathf.Max(currentProgress, Mathf.Clamp01(progress));
            if (Image_ProgressFill != null)
            {
                progressBar.SetValue(currentProgress);
            }

            if (TextMeshProUGUI_ProgressText != null)
            {
                int percent = Mathf.RoundToInt(currentProgress * 100f);
                SetLoadingText(TextMeshProUGUI_ProgressText, $"{percent}%", "Loading.Progress", percent);
            }

            if (TextMeshProUGUI_Step != null)
            {
                SetLoadingText(TextMeshProUGUI_Step, step, stepKey);
            }

            if (TextMeshProUGUI_Description != null)
            {
                SetLoadingText(TextMeshProUGUI_Description, description, descriptionKey, args);
            }

            if (TextMeshProUGUI_SizeText != null)
            {
                var hasSize = !string.IsNullOrWhiteSpace(size);
                TextMeshProUGUI_SizeText.text = hasSize ? size : string.Empty;
                TextMeshProUGUI_SizeText.gameObject.SetActive(hasSize);
            }
        }

        /// 将加载进度恢复到初始状态。
        public void ResetProgress()
        {
            currentProgress = 0f;
            if (Image_ProgressFill != null)
            {
                progressBar.SetValue(0f);
            }

            if (TextMeshProUGUI_ProgressText != null)
            {
                SetLoadingText(TextMeshProUGUI_ProgressText, "0%", "Loading.Progress", 0);
            }

            if (TextMeshProUGUI_SizeText != null)
            {
                TextMeshProUGUI_SizeText.text = string.Empty;
                TextMeshProUGUI_SizeText.gameObject.SetActive(false);
            }
        }

        private static void SetLoadingText(TMP_Text target, string fallback, string key, params object[] args)
        {
            if (LanguageService.IsInitialized && !string.IsNullOrWhiteSpace(key))
            {
                LanguageService.SetText(target, key, null, args);
            }
            else
            {
                target.text = fallback ?? string.Empty;
            }
        }
    }
}
