using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class StartupLoadingView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text stepText;
        [SerializeField] private Text progressText;
        [SerializeField] private Text sizeText;
        [SerializeField] private Image progressFill;

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

            if (stepText != null)
            {
                stepText.text = step ?? string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = description ?? string.Empty;
            }

            if (sizeText != null)
            {
                sizeText.text = size ?? string.Empty;
            }
        }

        public void SetTitle(string title)
        {
            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
            }
        }

        public void SetBackground(Sprite sprite)
        {
            if (backgroundImage != null && sprite != null)
            {
                backgroundImage.sprite = sprite;
            }
        }
    }
}
