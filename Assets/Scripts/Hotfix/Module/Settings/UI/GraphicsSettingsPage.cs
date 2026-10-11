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
    public sealed class GraphicsSettingsPage : SettingsPage
    {
        public override void Refresh()
        {
            var graphics = Candidate.Graphics;
            Row("WindowMode").gameObject.SetActive(DisplaySettingsRuntime.SupportsDesktopDisplay);
            Row("Resolution").gameObject.SetActive(DisplaySettingsRuntime.SupportsDesktopDisplay);
            if (DisplaySettingsRuntime.SupportsDesktopDisplay)
            {
                var modes = new[] { -1, (int)FullScreenMode.Windowed, (int)FullScreenMode.FullScreenWindow, (int)FullScreenMode.ExclusiveFullScreen };
                Select("WindowMode", "Settings.DisplayMode", modes, graphics.WindowMode,
                    value => graphics.WindowMode = value, value => value switch
                    {
                        -1 => LanguageService.Get("Graphics.Current"),
                        (int)FullScreenMode.Windowed => LanguageService.Get("Graphics.Windowed"),
                        (int)FullScreenMode.FullScreenWindow => LanguageService.Get("Graphics.Borderless"),
                        _ => LanguageService.Get("Graphics.Fullscreen")
                    });
                var resolutions = new List<Vector2Int> { Vector2Int.zero, new(Screen.width, Screen.height) };
                foreach (var resolution in DisplaySettingsRuntime.AvailableResolutions)
                {
                    var size = new Vector2Int(resolution.width, resolution.height);
                    if (!resolutions.Contains(size)) resolutions.Add(size);
                }
                var requested = new Vector2Int(graphics.Width, graphics.Height);
                if (!resolutions.Contains(requested)) resolutions.Add(requested);
                Select("Resolution", "Settings.Resolution", resolutions, requested,
                    value => { graphics.Width = value.x; graphics.Height = value.y; },
                    value => value == Vector2Int.zero ? LanguageService.Get("Graphics.Current") : $"{value.x} × {value.y}");
            }
            Select("Quality", "Settings.Quality", new[] { GraphicsQualityLevel.Current, GraphicsQualityLevel.Low, GraphicsQualityLevel.Medium, GraphicsQualityLevel.High },
                graphics.Quality, value => graphics.Quality = value, value => LanguageService.Get("Graphics." + value));
            Row("VSync").SetLabel("Settings.VSync");
            Row("VSync").SetToggle(graphics.VSync < 0 ? QualitySettings.vSyncCount > 0 : graphics.VSync > 0,
                value => ChangeAndRefresh(() => graphics.VSync = value ? 1 : 0));
            var frameLimits = new List<int> { -1 };
            frameLimits.AddRange(GameSettingsService.Options.FrameLimits);
            Select("FrameLimit", "Settings.FrameLimit", frameLimits, graphics.FrameLimit, value => graphics.FrameLimit = value,
                value => value < 0 ? LanguageService.Get("Graphics.Current") : value == 0 ? LanguageService.Get("Graphics.Unlimited") : value + " FPS");
            bool vSync = graphics.VSync < 0 ? QualitySettings.vSyncCount > 0 : graphics.VSync > 0;
            Row("FrameLimit").SetAvailable(!DisplaySettingsRuntime.SupportsDesktopDisplay || !vSync,
                DisplaySettingsRuntime.SupportsDesktopDisplay && vSync ? LanguageService.Get("Settings.FrameLimitVSync") : null);

            var dlss = new[] { 0, (int)StreamlineDlssMode.Quality, (int)StreamlineDlssMode.Balanced, (int)StreamlineDlssMode.Performance,
                (int)StreamlineDlssMode.UltraPerformance, (int)StreamlineDlssMode.Dlaa };
            bool supportsDlss = StreamlineRuntime.CanEnable(out _);
            Select("Dlss", "Settings.Dlss", dlss, graphics.DlssMode, value => graphics.DlssMode = value,
                value => value == 0 ? LanguageService.Get("Common.Off") : value == (int)StreamlineDlssMode.Dlaa ? "DLAA"
                    : "DLSS " + LanguageService.Get("Graphics.Dlss" + (StreamlineDlssMode)value), value => value == 0 || supportsDlss);
            Row("Dlss").SetAvailable(true, supportsDlss ? null : LanguageService.Get("Settings.DlssUnavailable"));
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
            Select("Antialiasing", "Graphics.NativeAa", nativeModes, graphics.Antialiasing, value =>
            {
                if (value == GraphicsAaMode.Taa && !DisplaySettingsRuntime.CanUseAntialiasing(value, out _)
                    || value == GraphicsAaMode.Msaa && samples.Count == 0) return;
                graphics.Antialiasing = value;
                if (value == GraphicsAaMode.Msaa && !samples.Contains(graphics.MsaaSamples)) graphics.MsaaSamples = samples[0];
            }, value => AaLabel(value) + (savedUnavailable && value == graphics.Antialiasing ? " · " + LanguageService.Get("Settings.Unavailable") : string.Empty),
                value => value == GraphicsAaMode.Msaa ? supportsMsaa
                    : value != GraphicsAaMode.Taa || DisplaySettingsRuntime.CanUseAntialiasing(value, out _));
            if (!samples.Contains(graphics.MsaaSamples)) samples.Add(graphics.MsaaSamples);
            Select("MsaaSamples", "Graphics.MsaaSamples", samples, graphics.MsaaSamples, value =>
            {
                if (DisplaySettingsRuntime.CanUseAntialiasing(GraphicsAaMode.Msaa, value, out _)) graphics.MsaaSamples = value;
            }, value => value + "×", value => DisplaySettingsRuntime.CanUseAntialiasing(GraphicsAaMode.Msaa, value, out _));
            bool managedCandidate = graphics.DlssMode != 0 && supportsDlss;
            Row("Antialiasing").SetAvailable(!managedCandidate, managedCandidate ? LanguageService.Get("Graphics.UpscalerAa") : null);
            Row("MsaaSamples").gameObject.SetActive(graphics.Antialiasing == GraphicsAaMode.Msaa);
            Row("MsaaSamples").SetAvailable(!managedCandidate, managedCandidate ? LanguageService.Get("Graphics.UpscalerAa") : null);
            var brightness = Row("Brightness");
            brightness.SetLabel("Settings.Brightness");
            brightness.SetValue(graphics.Brightness.ToString("P0", LanguageService.CurrentCulture));
            brightness.SetSlider(GameSettingsService.Options.Brightness, graphics.Brightness, value =>
            {
                brightness.SetValue(value.ToString("P0", LanguageService.CurrentCulture));
                Change(() => graphics.Brightness = value);
            });
            foreach (var effect in new[] { PostProcessEffects.ColorGrading, PostProcessEffects.Bloom, PostProcessEffects.Vignette, PostProcessEffects.DepthOfField,
                PostProcessEffects.ChromaticAberration, PostProcessEffects.LensDistortion, PostProcessEffects.FilmGrain })
            {
                var row = Row(effect.ToString());
                row.SetLabel("Presentation." + effect);
                row.SetToggle((graphics.AllowedEffects & effect) != 0, allowed => Change(() =>
                    graphics.AllowedEffects = allowed ? graphics.AllowedEffects | effect : graphics.AllowedEffects & ~effect));
            }
        }

        public static string AaLabel(GraphicsAaMode value) => value == GraphicsAaMode.CameraDefault ? LanguageService.Get("Graphics.CameraDefault")
            : value == GraphicsAaMode.Off ? LanguageService.Get("Common.Off") : value.ToString().ToUpperInvariant();

        private void ChangeAndRefresh(Action update)
        {
            Change(update);
            Refresh();
        }

        private void Select<T>(string field, string textKey, IList<T> values, T selected, Action<T> update, Func<T, string> label, Func<T, bool> available = null)
        {
            var labels = new List<string>();
            int index = -1;
            for (int i = 0; i < values.Count; i++)
            {
                labels.Add(label(values[i]));
                if (EqualityComparer<T>.Default.Equals(selected, values[i])) index = i;
            }
            var row = Row(field);
            row.SetLabel(textKey);
            row.SetOptions(labels, index, changed => ChangeAndRefresh(() => update(values[changed])),
                available == null ? null : item => available(values[item]));
        }
    }
}
