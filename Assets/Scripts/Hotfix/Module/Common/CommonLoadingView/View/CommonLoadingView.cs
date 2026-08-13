namespace Hotfix
{
    using Core.Runtime;
    using UnityEngine;

    [Module("Common")]
    [Mvc("CommonLoadingView")]
    public partial class CommonLoadingView : View
    {
        private float currentProgress;

        protected override void OnGameObjectInitialize()
        {
            ResetProgress();
        }

        /// <summary>
        /// 设置加载标题。
        /// </summary>
        /// <param name="title">为空时显示空字符串。</param>
        public void SetTitle(string title)
        {
            if (TextMeshProUGUI_Title != null)
            {
                TextMeshProUGUI_Title.text = title ?? string.Empty;
            }
        }

        /// <summary>
        /// 更新单调递增的加载进度和说明文本。
        /// </summary>
        /// <param name="progress">目标进度，会限制到 0 到 1 且不会倒退。</param>
        /// <param name="step">当前加载步骤。</param>
        /// <param name="description">步骤补充说明；为空时清空。</param>
        /// <param name="size">可用的真实大小信息；为空时隐藏大小文本。</param>
        public void SetProgress(float progress, string step, string description = null, string size = null)
        {
            currentProgress = Mathf.Max(currentProgress, Mathf.Clamp01(progress));
            if (Image_ProgressFill != null)
            {
                Image_ProgressFill.fillAmount = currentProgress;
            }

            if (TextMeshProUGUI_ProgressText != null)
            {
                TextMeshProUGUI_ProgressText.text = $"{Mathf.RoundToInt(currentProgress * 100f)}%";
            }

            if (TextMeshProUGUI_Step != null)
            {
                TextMeshProUGUI_Step.text = step ?? string.Empty;
            }

            if (TextMeshProUGUI_Description != null)
            {
                TextMeshProUGUI_Description.text = description ?? string.Empty;
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
                Image_ProgressFill.fillAmount = 0f;
            }

            if (TextMeshProUGUI_ProgressText != null)
            {
                TextMeshProUGUI_ProgressText.text = "0%";
            }

            if (TextMeshProUGUI_SizeText != null)
            {
                TextMeshProUGUI_SizeText.text = string.Empty;
                TextMeshProUGUI_SizeText.gameObject.SetActive(false);
            }
        }
    }
}
