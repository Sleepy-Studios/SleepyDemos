using System;
using Core.Runtime;
using Core.Runtime.Localization;
using Core.Runtime.Rendering;
using Core.Runtime.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hotfix
{
    /// 一个效果的独立体验行，复用公共设置行和同一优先级的画面作用域。
    public sealed class PresentationPostProcessRow : MonoBehaviour
    {
        [SerializeField] private PostProcessEffects effect;
        [SerializeField] private PostProcessPreset preset;
        [SerializeField] private string captionKey;
        [SerializeField] private UISettingRow amountRow;
        [SerializeField] private UISettingRow toggleRow;
        [SerializeField] private float defaultAmount = 0.5f;
        private PostProcessRuntime.Scope scope;
        private float amount;
        private bool enabledRequest;
        private bool allowed = true;

        /// 解析制作词条，不自动开启效果或保存玩家设置。
        public void Initialize()
        {
            amountRow.SetLabel(captionKey);
            toggleRow.SetLabel("Presentation.Enabled");
            ResetControls();
        }

        /// <summary>显示实际许可，禁止项保留请求；最终过滤由公共后处理运行时执行。</summary>
        /// <param name="graphics">已生效画质偏好副本。</param>
        /// <param name="accessibility">已生效减少动态偏好副本。</param>
        public void RefreshLimits(GraphicsPreferences graphics, AccessibilityPreferences accessibility)
        {
            allowed = (graphics.AllowedEffects & effect) != 0
                && (!accessibility.IsReducedMotion || effect != PostProcessEffects.FilmGrain
                    && effect != PostProcessEffects.ChromaticAberration && effect != PostProcessEffects.LensDistortion);
            toggleRow.SetAvailable(allowed);
            amountRow.SetAvailable(allowed && enabledRequest, allowed ? null : LanguageService.Get("Presentation.Limited"));
        }

        /// 恢复本页初始选择，不改变制作预设。
        public void ResetControls()
        {
            Stop();
            enabledRequest = false;
            amount = defaultAmount;
            toggleRow.SetToggle(false, OnToggle);
            amountRow.SetSlider(new Vector2(0, 1), amount, OnAmount);
            amountRow.SetValue(LanguageService.Format("Presentation.Amount", amount));
            amountRow.SetAvailable(false);
        }

        /// 释放自己持有的效果，保留本页控件选择。
        public void Stop()
        {
            scope?.Dispose();
            scope = null;
        }

        /// 应用已选择且当前允许的效果。
        public void Play()
        {
            if (enabledRequest && allowed)
            {
                scope ??= PostProcessRuntime.Push(preset);
                scope.Update(profile => ApplyAmount(profile, effect, amount));
            }
        }

        private void OnToggle(bool value)
        {
            enabledRequest = value;
            amountRow.SetAvailable(value && allowed);
            if (value)
            {
                Play();
            }
            else
            {
                Stop();
            }
        }

        private void OnAmount(float value)
        {
            amount = value;
            amountRow.SetValue(LanguageService.Format("Presentation.Amount", value));
            if (scope != null)
            {
                scope.Update(profile => ApplyAmount(profile, effect, value));
            }
        }

        private static void ApplyAmount(VolumeProfile profile, PostProcessEffects selected, float value)
        {
            switch (selected)
            {
                case PostProcessEffects.ColorGrading:
                    profile.TryGet<ColorAdjustments>(out var grading);
                    grading.saturation.Override(value * 100 - 50);
                    break;
                case PostProcessEffects.Bloom:
                    profile.TryGet<Bloom>(out var bloom);
                    bloom.intensity.Override(value * 3);
                    break;
                case PostProcessEffects.Vignette:
                    profile.TryGet<Vignette>(out var vignette);
                    vignette.intensity.Override(value * 0.8f);
                    break;
                case PostProcessEffects.DepthOfField:
                    profile.TryGet<DepthOfField>(out var depth);
                    depth.aperture.Override(Mathf.Lerp(16, 1, value));
                    break;
                case PostProcessEffects.ChromaticAberration:
                    profile.TryGet<ChromaticAberration>(out var chromatic);
                    chromatic.intensity.Override(value);
                    break;
                case PostProcessEffects.LensDistortion:
                    profile.TryGet<LensDistortion>(out var lens);
                    lens.intensity.Override(value * -0.7f);
                    break;
                case PostProcessEffects.FilmGrain:
                    profile.TryGet<FilmGrain>(out var grain);
                    grain.intensity.Override(value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(selected));
            }
        }
        private void OnDestroy() => Stop();
    }
}
