using System;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Localization;
using Core.Runtime.Rendering.Streamline;
using Core.Runtime.Settings;
using UnityEngine;

namespace Hotfix
{
    /// 画质分页维护请求候选，能力与生效状态来自公共显示服务。
    [Module("Settings")]
    [UIBind("GraphicsSettingsPanelView")]
    public partial class GraphicsSettingsPanelView : SettingsPanelView
    {
        protected override void OnGameObjectInitialize()
        {
            InitializeRows(
                UISettingRow_WindowMode,
                UISettingRow_Resolution,
                UISettingRow_Quality,
                UISettingRow_VSync,
                UISettingRow_FrameLimit,
                UISettingRow_Dlss,
                UISettingRow_Antialiasing,
                UISettingRow_MsaaSamples,
                UISettingRow_Brightness,
                UISettingRow_ColorGrading,
                UISettingRow_Bloom,
                UISettingRow_Vignette,
                UISettingRow_DepthOfField,
                UISettingRow_ChromaticAberration,
                UISettingRow_LensDistortion,
                UISettingRow_FilmGrain);
            boundGraphics = null;
            dlssEdited = false;
            base.OnGameObjectInitialize();
        }

        private GraphicsPreferences boundGraphics;
        private bool dlssEdited;

        public override void Refresh()
        {
            if (!IsLoaded) return;
            var graphics = Candidate.Graphics;
            if (!ReferenceEquals(boundGraphics, graphics))
            {
                boundGraphics = graphics;
                dlssEdited = false;
            }
            UISettingRow_WindowMode.gameObject.SetActive(DisplaySettingsRuntime.SupportsDesktopDisplay);
            UISettingRow_Resolution.gameObject.SetActive(DisplaySettingsRuntime.SupportsDesktopDisplay);
            if (DisplaySettingsRuntime.SupportsDesktopDisplay)
            {
                var modes = new[] { -1, (int)FullScreenMode.Windowed, (int)FullScreenMode.FullScreenWindow, (int)FullScreenMode.ExclusiveFullScreen };
                Select(UISettingRow_WindowMode, "Settings.DisplayMode", modes, graphics.WindowMode,
                    value => graphics.WindowMode = value,
                    value => WindowModeLabel(value < 0 ? Screen.fullScreenMode : (FullScreenMode)value));
                var resolutions = new List<Vector2Int> { Vector2Int.zero, new(Screen.width, Screen.height) };
                foreach (var resolution in DisplaySettingsRuntime.AvailableResolutions)
                {
                    var size = new Vector2Int(resolution.width, resolution.height);
                    if (!resolutions.Contains(size)) resolutions.Add(size);
                }
                var requested = new Vector2Int(graphics.Width, graphics.Height);
                if (!resolutions.Contains(requested)) resolutions.Add(requested);
                Select(UISettingRow_Resolution, "Settings.Resolution", resolutions, requested,
                    value => { graphics.Width = value.x; graphics.Height = value.y; },
                    value => value == Vector2Int.zero ? $"{Screen.width} × {Screen.height}" : $"{value.x} × {value.y}");
            }
            Select(UISettingRow_Quality, "Settings.Quality", new[] { GraphicsQualityLevel.Current, GraphicsQualityLevel.Low, GraphicsQualityLevel.Medium, GraphicsQualityLevel.High },
                graphics.Quality, value => graphics.Quality = value, value => value == GraphicsQualityLevel.Current
                    ? QualitySettings.names[QualitySettings.GetQualityLevel()] : LanguageService.Get("Graphics." + value));
            UISettingRow_VSync.SetLabel("Settings.VSync");
            UISettingRow_VSync.SetToggle(graphics.VSync < 0 ? QualitySettings.vSyncCount > 0 : graphics.VSync > 0,
                value => ChangeAndRefresh(() => graphics.VSync = value ? 1 : 0));
            var frameLimits = new List<int> { -1 };
            frameLimits.AddRange(GameSettingsService.Options.FrameLimits);
            bool vSync = graphics.VSync < 0 ? QualitySettings.vSyncCount > 0 : graphics.VSync > 0;
            int authoredFrameLimit = DisplaySettingsRuntime.AuthoredFrameLimit;
            string defaultFrameLimit = authoredFrameLimit > 0 ? authoredFrameLimit + " FPS"
                : LanguageService.Get(DisplaySettingsRuntime.SupportsDesktopDisplay && !vSync ? "Graphics.Unlimited" : "Common.Auto");
            Select(UISettingRow_FrameLimit, "Settings.FrameLimit", frameLimits, graphics.FrameLimit, value => graphics.FrameLimit = value,
                value => value < 0 ? defaultFrameLimit : value == 0 ? LanguageService.Get("Graphics.Unlimited") : value + " FPS");
            UISettingRow_FrameLimit.SetAvailable(!DisplaySettingsRuntime.SupportsDesktopDisplay || !vSync,
                DisplaySettingsRuntime.SupportsDesktopDisplay && vSync ? LanguageService.Get("Settings.FrameLimitVSync") : null);

            var dlss = new[] { 0, (int)StreamlineDlssMode.Quality, (int)StreamlineDlssMode.Balanced, (int)StreamlineDlssMode.Performance,
                (int)StreamlineDlssMode.UltraPerformance, (int)StreamlineDlssMode.Dlaa };
            bool supportsDlss = StreamlineRuntime.CanEnable(out _);
            Select(UISettingRow_Dlss, "Settings.Dlss", dlss, graphics.DlssMode, value =>
                { graphics.DlssMode = value; dlssEdited = true; },
                value => value == 0 ? LanguageService.Get("Common.Off") : value == (int)StreamlineDlssMode.Dlaa ? "DLAA"
                    : "DLSS " + LanguageService.Get("Graphics.Dlss" + (StreamlineDlssMode)value), value => value == 0 || supportsDlss);
            UISettingRow_Dlss.SetAvailable(true, supportsDlss ? null : LanguageService.Get("Settings.DlssUnavailable"));
            var nativeModes = new List<GraphicsAaMode> { GraphicsAaMode.CameraDefault, GraphicsAaMode.Off, GraphicsAaMode.Fxaa, GraphicsAaMode.Smaa };
            if (DisplaySettingsRuntime.CanUseAntialiasing(GraphicsAaMode.Taa, out _)) nativeModes.Add(GraphicsAaMode.Taa);
            var samples = new List<int>();
            foreach (int sample in new[] { 2, 4, 8 })
            {
                if (DisplaySettingsRuntime.CanUseAntialiasing(GraphicsAaMode.Msaa, sample, out _)) samples.Add(sample);
            }
            bool supportsMsaa = samples.Count > 0;
            if (supportsMsaa) nativeModes.Add(GraphicsAaMode.Msaa);
            bool savedUnavailable = !nativeModes.Contains(graphics.Antialiasing);
            if (savedUnavailable) nativeModes.Add(graphics.Antialiasing);
            Select(UISettingRow_Antialiasing, "Graphics.NativeAa", nativeModes, graphics.Antialiasing, value =>
            {
                if (value == GraphicsAaMode.Taa && !DisplaySettingsRuntime.CanUseAntialiasing(value, out _)
                    || value == GraphicsAaMode.Msaa && samples.Count == 0) return;
                graphics.Antialiasing = value;
                if (value == GraphicsAaMode.Msaa && !samples.Contains(graphics.MsaaSamples)) graphics.MsaaSamples = samples[0];
            }, value => AaLabel(value) + (savedUnavailable && value == graphics.Antialiasing ? " · " + LanguageService.Get("Settings.Unavailable") : string.Empty),
                value => value == GraphicsAaMode.Msaa ? supportsMsaa
                    : value != GraphicsAaMode.Taa || DisplaySettingsRuntime.CanUseAntialiasing(value, out _));
            if (!samples.Contains(graphics.MsaaSamples)) samples.Add(graphics.MsaaSamples);
            Select(UISettingRow_MsaaSamples, "Graphics.MsaaSamples", samples, graphics.MsaaSamples, value =>
            {
                if (DisplaySettingsRuntime.CanUseAntialiasing(GraphicsAaMode.Msaa, value, out _)) graphics.MsaaSamples = value;
            }, value => value + "×", value => DisplaySettingsRuntime.CanUseAntialiasing(GraphicsAaMode.Msaa, value, out _));
            bool managedCandidate = ManagesNativeAntialiasing(graphics.DlssMode,
                StreamlineRuntime.RequestedMode.HasValue ? (int)StreamlineRuntime.RequestedMode.Value : 0,
                supportsDlss, StreamlineRuntime.IsBusy, DisplaySettingsRuntime.IsAntialiasingManagedByUpscaler, dlssEdited);
            UISettingRow_Antialiasing.SetAvailable(!managedCandidate, managedCandidate ? LanguageService.Get("Graphics.UpscalerAa") : null);
            UISettingRow_MsaaSamples.gameObject.SetActive(graphics.Antialiasing == GraphicsAaMode.Msaa);
            UISettingRow_MsaaSamples.SetAvailable(!managedCandidate, managedCandidate ? LanguageService.Get("Graphics.UpscalerAa") : null);
            var brightness = UISettingRow_Brightness;
            brightness.SetLabel("Settings.Brightness");
            brightness.SetValue(graphics.Brightness.ToString("P0", LanguageService.CurrentCulture));
            brightness.SetSlider(GameSettingsService.Options.Brightness, graphics.Brightness, value =>
            {
                brightness.SetValue(value.ToString("P0", LanguageService.CurrentCulture));
                Change(() => graphics.Brightness = value);
            });
            foreach (var (effect, row) in new[]
            {
                (PostProcessEffects.ColorGrading, UISettingRow_ColorGrading),
                (PostProcessEffects.Bloom, UISettingRow_Bloom),
                (PostProcessEffects.Vignette, UISettingRow_Vignette),
                (PostProcessEffects.DepthOfField, UISettingRow_DepthOfField),
                (PostProcessEffects.ChromaticAberration, UISettingRow_ChromaticAberration),
                (PostProcessEffects.LensDistortion, UISettingRow_LensDistortion),
                (PostProcessEffects.FilmGrain, UISettingRow_FilmGrain)
            })
            {
                row.SetLabel("Presentation." + effect);
                row.SetToggle((graphics.AllowedEffects & effect) != 0, allowed => Change(() =>
                    graphics.AllowedEffects = allowed ? graphics.AllowedEffects | effect : graphics.AllowedEffects & ~effect));
            }
        }

        public static string AaLabel(GraphicsAaMode value) => value == GraphicsAaMode.CameraDefault ? LanguageService.Get("Graphics.CameraDefault")
            : value == GraphicsAaMode.Off ? LanguageService.Get("Common.Off") : value.ToString().ToUpperInvariant();

        private static string WindowModeLabel(FullScreenMode value) => LanguageService.Get(value switch
        {
            FullScreenMode.Windowed or FullScreenMode.MaximizedWindow => "Graphics.Windowed",
            FullScreenMode.FullScreenWindow => "Graphics.Borderless",
            _ => "Graphics.Fullscreen"
        });

        // 同一请求已经回退时允许编辑原生 AA；明确的新候选/重试仍预告接管，未保存不改运行时。
        internal static bool ManagesNativeAntialiasing(int candidateMode, int runtimeRequest, bool canEnable,
            bool busy, bool effective, bool edited)
        {
            return candidateMode != 0 && canEnable && (edited || candidateMode != runtimeRequest || busy || effective);
        }

        private void ChangeAndRefresh(Action update)
        {
            Change(update);
            Refresh();
        }

        private void Select<T>(UISettingRow row, string textKey, IList<T> values, T selected, Action<T> update, Func<T, string> label, Func<T, bool> available = null)
        {
            var labels = new List<string>();
            int index = -1;
            for (int i = 0; i < values.Count; i++)
            {
                labels.Add(label(values[i]));
                if (EqualityComparer<T>.Default.Equals(selected, values[i])) index = i;
            }
            row.SetLabel(textKey);
            row.SetOptions(labels, index, changed => ChangeAndRefresh(() => update(values[changed])),
                available == null ? null : item => available(values[item]));
        }
    }
}
