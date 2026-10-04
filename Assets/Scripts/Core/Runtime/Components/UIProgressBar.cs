using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 只更新 Filled 图片的数值和颜色；背景、遮罩及业务缓动由调用方维护。
    [DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed class UIProgressBar : MonoBehaviour
    {
        private Image fill;
        private Image Fill => fill != null ? fill : fill = GetComponent<Image>();

        /// 当前归一化填充值。
        public float Value => Fill.fillAmount;

        /// <summary>设置归一化进度，不触发交互回调。</summary>
        /// <param name="value">限制到 0–1；NaN 按零处理。</param>
        public void SetValue(float value)
        {
            Fill.fillAmount = float.IsNaN(value) ? 0 : Mathf.Clamp01(value);
        }

        /// <summary>设置填充颜色，不修改 Sprite、背景或遮罩。</summary>
        /// <param name="value">填充图片的完整 RGBA 颜色。</param>
        public void SetColor(Color value) => Fill.color = value;
    }
}
