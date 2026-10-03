using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class StartupLoadingView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text stepText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text sizeText;
        [SerializeField] private Image progressFill;

        /// <summary>
        /// 显示启动状态机报告的真实进度和阶段信息。
        /// </summary>
        /// <param name="progress">当前进度，限制在 0 到 1；界面不自行推算或推进进度。</param>
        /// <param name="step">阶段名称；空白时隐藏阶段文本节点。</param>
        /// <param name="description">阶段说明；空白时隐藏说明文本节点。</param>
        /// <param name="size">实际资源大小信息；空白时隐藏大小文本节点。</param>
        public void SetProgress(float progress, string step, string description = null, string size = null)
        {
            progress = Mathf.Clamp01(progress);
            if (progressFill != null)
            {
                progressFill.fillAmount = progress;
            }

            if (progressText != null)
            {
                progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
            }

            SetOptionalText(stepText, step);
            SetOptionalText(descriptionText, description);
            SetOptionalText(sizeText, size);
        }

        /// <summary>
        /// 设置启动加载标题。
        /// </summary>
        /// <param name="title">标题内容；为空时清空标题。</param>
        public void SetTitle(string title)
        {
            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
            }
        }

        /// <summary>
        /// 替换直接引用的启动背景。
        /// </summary>
        /// <param name="sprite">背景图片；为空时保留当前背景。</param>
        public void SetBackground(Sprite sprite)
        {
            if (backgroundImage != null && sprite != null)
            {
                backgroundImage.sprite = sprite;
            }
        }

        private static void SetOptionalText(TMP_Text target, string value)
        {
            if (target == null)
            {
                return;
            }

            var hasText = !string.IsNullOrWhiteSpace(value);
            target.text = hasText ? value : string.Empty;
            target.gameObject.SetActive(hasText);
        }
    }
}
