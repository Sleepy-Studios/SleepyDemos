using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Runtime.Rendering
{
    /// 演出使用的 URP 原生 Volume 预设；只覆盖 Profile 勾选的参数。
    [CreateAssetMenu(menuName = "SleepyDemos/Settings/Post Process Preset")]
    public sealed class PostProcessPreset : ScriptableObject
    {
        [SerializeField] private VolumeProfile profile;
        [SerializeField, Range(0, 1)] private float weight = 1;

        /// 制作 Profile，运行时必须复制后使用。
        public VolumeProfile Profile => profile;

        /// 此预设对已评估场景状态的混合权重。
        public float Weight => weight;

        /// <summary>制作工具设置正式 Volume Profile 与混合权重。</summary>
        /// <param name="template">非空 URP Profile；不会修改其组件。</param>
        /// <param name="blendWeight">零到一的有限权重。</param>
        public void Configure(VolumeProfile template, float blendWeight = 1)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (!Settings.GraphicsPreferences.FiniteRange(blendWeight, 0, 1))
            {
                throw new ArgumentOutOfRangeException(nameof(blendWeight));
            }

            profile = template;
            weight = blendWeight;
        }

        /// 验证 Inspector 制作值。
        public void Validate()
        {
            if (profile == null || !Settings.GraphicsPreferences.FiniteRange(weight, 0, 1))
            {
                throw new ArgumentException("后处理预设缺少 Profile 或混合权重无效。");
            }
        }
    }
}
