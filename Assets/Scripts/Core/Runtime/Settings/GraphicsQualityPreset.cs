using System;
using UnityEngine;

namespace Core.Runtime.Settings
{
    /// 修改当前平台管线副本的画质预设，不替换制作 Renderer。
    [CreateAssetMenu(menuName = "SleepyDemos/Settings/Graphics Quality Preset")]
    public sealed class GraphicsQualityPreset : ScriptableObject
    {
        [SerializeField] private GraphicsQualityLevel level = GraphicsQualityLevel.High;
        [SerializeField, Range(0.1f, 2)] private float renderScale = 1;
        [SerializeField, Min(0)] private float shadowDistance = 40;
        [SerializeField] private int msaaSamples = 1;
        [SerializeField, Range(0, 4)] private int textureMipmapLimit;
        [SerializeField, Range(0.1f, 4)] private float lodBias = 2;

        /// 当前预设对应的用户档位。
        public GraphicsQualityLevel Level => level;

        /// 世界渲染比例；DLSS 的实际比例由 SDK 决定。
        public float RenderScale => renderScale;

        /// 实时阴影距离。
        public float ShadowDistance => shadowDistance;

        /// 无时域抗锯齿时的 MSAA 采样数。
        public int MsaaSamples => msaaSamples;

        /// 纹理 mip 层级限制。
        public int TextureMipmapLimit => textureMipmapLimit;

        /// 场景 LOD 距离倍率。
        public float LodBias => lodBias;

        /// <summary>供制作工具配置正式预设；非法值不修改已有配置。</summary>
        /// <param name="quality">Low、Medium 或 High。</param>
        /// <param name="scale">0.1–2 的世界渲染比例。</param>
        /// <param name="shadows">非负有限阴影距离。</param>
        /// <param name="samples">1、2、4 或 8。</param>
        /// <param name="mipLimit">0–4 的纹理 mip 限制。</param>
        /// <param name="lod">0.1–4 的 LOD 倍率。</param>
        public void Configure(GraphicsQualityLevel quality, float scale, float shadows, int samples, int mipLimit, float lod)
        {
            ValidateValues(quality, scale, shadows, samples, mipLimit, lod);
            level = quality;
            renderScale = scale;
            shadowDistance = shadows;
            msaaSamples = samples;
            textureMipmapLimit = mipLimit;
            lodBias = lod;
        }

        /// 验证 Inspector 或外部数据传入的制作配置。
        public void Validate() => ValidateValues(level, renderScale, shadowDistance, msaaSamples, textureMipmapLimit, lodBias);

        private static void ValidateValues(GraphicsQualityLevel quality, float scale, float shadows, int samples, int mipLimit, float lod)
        {
            if (quality == GraphicsQualityLevel.Current || !Enum.IsDefined(typeof(GraphicsQualityLevel), quality)
                || !GraphicsPreferences.FiniteRange(scale, 0.1f, 2)
                || !GraphicsPreferences.FiniteRange(shadows, 0, 10000)
                || samples != 1 && samples != 2 && samples != 4 && samples != 8
                || mipLimit < 0 || mipLimit > 4 || !GraphicsPreferences.FiniteRange(lod, 0.1f, 4))
            {
                throw new ArgumentException("画质预设无效。");
            }
        }
    }
}
