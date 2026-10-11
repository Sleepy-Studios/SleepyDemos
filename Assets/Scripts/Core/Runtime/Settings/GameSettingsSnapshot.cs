using System;
using System.Collections.Generic;
using Core.Runtime.Inputs;
using UnityEngine;

namespace Core.Runtime.Settings
{
    /// 公共画质预设；Current 保留当前平台的制作配置。
    public enum GraphicsQualityLevel { Current, Low, Medium, High }

    /// 原生抗锯齿；CameraDefault 保留相机作者配置，DLSS 启用时由渲染服务协调。
    public enum GraphicsAaMode { CameraDefault, Off, Fxaa, Smaa, Taa }

    /// 自动读取系统偏好，或由玩家明确选择减少动态。
    public enum ReducedMotionMode { Auto, On, Off }

    /// 玩家允许使用的后处理类型；关闭某项不改变 Demo 的制作预设。
    [Flags]
    public enum PostProcessEffects
    {
        None = 0,
        ColorGrading = 1,
        Bloom = 2,
        Vignette = 4,
        DepthOfField = 8,
        ChromaticAberration = 16,
        LensDistortion = 32,
        FilmGrain = 64,
        All = 127
    }

    /// 本机公共偏好的完整候选；保存者拥有独立副本。
    [Serializable]
    public sealed class GameSettingsSnapshot
    {
        public int Version = 1;
        public GraphicsPreferences Graphics = new();
        public AudioPreferences Audio = new();
        public GameplayInputSettings Input = new();
        public AccessibilityPreferences Accessibility = new();
        public string Language = "zh-CN";
        public Dictionary<string, string> BindingOverrides = new();

        /// 创建独立候选，不与已生效设置共享可变字段。
        public GameSettingsSnapshot Copy()
        {
            return new GameSettingsSnapshot
            {
                Version = Version,
                Graphics = Graphics?.Copy(),
                Audio = Audio?.Copy(),
                Input = Input?.Copy(),
                Accessibility = Accessibility?.Copy(),
                Language = Language,
                BindingOverrides = BindingOverrides == null ? null : new Dictionary<string, string>(BindingOverrides)
            };
        }

        /// 拒绝非有限值、未知版本和不完整候选。
        public void Validate()
        {
            if (Version != 1 || Graphics == null || Audio == null || Input == null || !Input.IsValid
                || Accessibility == null || string.IsNullOrWhiteSpace(Language) || BindingOverrides == null)
            {
                throw new ArgumentException("公共设置记录无效。");
            }

            Graphics.Validate();
            Audio.Validate();
            Accessibility.Validate();
            foreach (var entry in BindingOverrides)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null || entry.Value.Length > 65536)
                {
                    throw new ArgumentException("按键覆盖记录无效。");
                }
            }
        }
    }

    /// 显示与画质偏好；零尺寸、负刷新设置保留当前窗口，默认不改变平台制作配置。
    [Serializable]
    public sealed class GraphicsPreferences
    {
        public int Width;
        public int Height;
        public int WindowMode = -1;
        public int VSync = -1;
        public int FrameLimit = -1;
        public GraphicsQualityLevel Quality = GraphicsQualityLevel.Current;
        public GraphicsAaMode Antialiasing = GraphicsAaMode.CameraDefault;
        public int DlssMode;
        public float Brightness = 1;
        public PostProcessEffects AllowedEffects = PostProcessEffects.All;

        /// 创建独立候选。
        public GraphicsPreferences Copy() => (GraphicsPreferences)MemberwiseClone();

        /// 验证制作配置与玩家候选的允许范围。
        public void Validate()
        {
            if (Width < 0 || Height < 0 || Width > 16384 || Height > 16384
                || (Width == 0) != (Height == 0) || WindowMode < -1 || WindowMode > (int)FullScreenMode.Windowed
                || VSync < -1 || VSync > 1 || FrameLimit < -1 || FrameLimit > 360
                || !Enum.IsDefined(typeof(GraphicsQualityLevel), Quality)
                || !Enum.IsDefined(typeof(GraphicsAaMode), Antialiasing)
                || !FiniteRange(Brightness, 0.5f, 2)
                || (AllowedEffects & ~PostProcessEffects.All) != 0)
            {
                throw new ArgumentException("显示与画质设置无效。");
            }
        }

        internal static bool FiniteRange(float value, float minimum, float maximum)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
        }
    }

    /// 公共线性音量增益，与 Demo 自己的分类音量相乘。
    [Serializable]
    public sealed class AudioPreferences
    {
        public float Master = 1;
        public float Music = 1;
        public float Effects = 1;
        public float Ui = 1;
        public bool Muted;

        /// 创建独立候选。
        public AudioPreferences Copy() => (AudioPreferences)MemberwiseClone();

        /// 音量必须是零到一之间的有限值。
        public void Validate()
        {
            if (!GraphicsPreferences.FiniteRange(Master, 0, 1) || !GraphicsPreferences.FiniteRange(Music, 0, 1)
                || !GraphicsPreferences.FiniteRange(Effects, 0, 1) || !GraphicsPreferences.FiniteRange(Ui, 0, 1))
            {
                throw new ArgumentException("声音设置无效。");
            }
        }
    }

    /// 公共辅助偏好，只约束表现，不修改 Demo 的玩法设置。
    [Serializable]
    public sealed class AccessibilityPreferences
    {
        [NonSerialized] private bool? systemReducedMotion;
        public ReducedMotionMode ReducedMotion = ReducedMotionMode.Auto;
        public bool CameraShake = true;
        public bool ShowInputPrompts = true;

        /// 当前公共减少动态策略。
        [Newtonsoft.Json.JsonIgnore]
        public bool IsReducedMotion => ReducedMotion == ReducedMotionMode.On
            || ReducedMotion == ReducedMotionMode.Auto && (systemReducedMotion ??= SystemMotionPreference.ReadReducedMotion());

        /// 创建独立候选。
        public AccessibilityPreferences Copy()
        {
            var copy = (AccessibilityPreferences)MemberwiseClone();
            copy.systemReducedMotion = null;
            return copy;
        }

        /// 拒绝未知策略。
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(ReducedMotionMode), ReducedMotion))
            {
                throw new ArgumentException("辅助功能设置无效。");
            }
        }
    }

    /// 设置应用的可等待结果，取消与失败分别报告。
    public readonly struct SettingsApplyResult
    {
        public bool Succeeded { get; }
        public bool Canceled { get; }
        public string Message { get; }

        /// <summary>记录一次设置提交的结果。</summary>
        /// <param name="succeeded">全部应用并保存成功。</param>
        /// <param name="message">给界面显示的反馈。</param>
        /// <param name="canceled">用户取消或显示确认超时。</param>
        public SettingsApplyResult(bool succeeded, string message = null, bool canceled = false)
        {
            Succeeded = succeeded;
            Message = message;
            Canceled = canceled;
        }
    }
}
