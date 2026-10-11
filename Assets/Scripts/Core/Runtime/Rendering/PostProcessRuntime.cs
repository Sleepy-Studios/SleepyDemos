using System;
using System.Collections.Generic;
using Core.Runtime.Settings;
using Core.Runtime.Rendering.Streamline;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Core.Runtime.Rendering
{
    /// 世界相机的临时画面效果与玩家过滤；不改场景 Profile 或 UI 画面。
    public static class PostProcessRuntime
    {
        private static readonly List<Scope> Scopes = new();
        private static readonly Dictionary<Type, Backup> Backups = new();
        private static readonly List<Backup> Pending = new();
        private static readonly Type[] GradingTypes =
        {
            typeof(ColorAdjustments), typeof(WhiteBalance), typeof(ChannelMixer), typeof(LiftGammaGain),
            typeof(ShadowsMidtonesHighlights), typeof(SplitToning), typeof(ColorCurves), typeof(ColorLookup), typeof(Tonemapping)
        };

        private static Camera camera;
        private static bool authoredPostProcessing;
        private static GraphicsPreferences graphics = new();
        private static AccessibilityPreferences accessibility = new();
        private static bool reducedMotion;
        private static VolumeProfile neutral;
        private static Scope basePreset;

        /// 当前作用域数量，关闭或解绑后为零。
        public static int ActiveScopeCount => Scopes.Count;

        /// <summary>应用玩家允许的效果、亮度和减少动态策略，不保存偏好。</summary>
        /// <param name="preferences">后处理允许集合与亮度；亮度一保持场景作者曝光。</param>
        /// <param name="accessibilityPreferences">自动策略在设置变化时读取系统减少动态偏好。</param>
        public static void ApplySettings(GraphicsPreferences preferences, AccessibilityPreferences accessibilityPreferences)
        {
            if (preferences == null || accessibilityPreferences == null)
            {
                throw new ArgumentNullException(preferences == null ? nameof(preferences) : nameof(accessibilityPreferences));
            }

            preferences.Validate();
            accessibilityPreferences.Validate();
            graphics = preferences.Copy();
            accessibility = accessibilityPreferences.Copy();
            reducedMotion = accessibility.IsReducedMotion;
            RefreshCamera();
        }

        /// <summary>绑定当前世界相机；更换或解绑会释放旧场景全部临时作用域。</summary>
        /// <param name="worldCamera">世界相机；空表示恢复旧相机并解绑。</param>
        public static void BindCamera(Camera worldCamera)
        {
            if (ReferenceEquals(camera, worldCamera))
            {
                return;
            }

            RestoreStack();
            RenderPipelineManager.endCameraRendering -= OnCameraRendered;
            if (camera != null)
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = authoredPostProcessing;
            }

            ClearScopes();
            SetBasePreset(null);
            camera = worldCamera;
            if (camera != null)
            {
                authoredPostProcessing = camera.GetUniversalAdditionalCameraData().renderPostProcessing;
                RenderPipelineManager.endCameraRendering += OnCameraRendered;
                RefreshCamera();
            }
        }

        /// <summary>复制并叠加预设；后加入的作用域仅覆盖自身勾选的参数。</summary>
        /// <param name="preset">URP Profile 制作预设；必须先绑定世界相机。</param>
        /// <returns>Dispose 时只移除本作用域；乱序和重复释放均安全。</returns>
        public static Scope Push(PostProcessPreset preset)
        {
            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            if (camera == null)
            {
                throw new InvalidOperationException("临时画面效果需要已绑定的世界相机。");
            }

            var scope = new Scope(preset);
            Scopes.Add(scope);
            RefreshCamera();
            return scope;
        }

        /// <summary>设置当前相机的基础预设，在临时作用域之前混合；失败保留旧基底。</summary>
        /// <param name="preset">只读制作预设；null 清除基底并恢复作者效果，切相机时也自动清除。</param>
        public static void SetBasePreset(PostProcessPreset preset)
        {
            if (preset != null && camera == null)
            {
                throw new InvalidOperationException("基础画面效果需要已绑定的世界相机。");
            }

            var next = preset == null ? null : new Scope(preset);
            RestoreStack();
            var previous = basePreset;
            basePreset = next;
            previous?.Dispose();
            RefreshCamera();
        }

        /// 释放作用域与参数缓存，并恢复原相机后处理状态。
        public static void Shutdown()
        {
            BindCamera(null);
            ClearScopes();
            SetBasePreset(null);
            Backups.Clear();
            if (neutral != null)
            {
                foreach (var component in neutral.components)
                {
                    CoreUtils.Destroy(component);
                }

                CoreUtils.Destroy(neutral);
                neutral = null;
            }

            graphics = new GraphicsPreferences();
            accessibility = new AccessibilityPreferences();
            reducedMotion = false;
        }

        internal static void ApplyToStack(Camera renderingCamera)
        {
            if (renderingCamera != camera || camera == null)
            {
                return;
            }

            RestoreStack();
            var stack = VolumeManager.instance.stack;
            if (stack == null || !stack.isValid)
            {
                return;
            }

            try
            {
                if (basePreset != null)
                {
                    ApplyScope(stack, basePreset);
                }

                foreach (var scope in Scopes)
                {
                    ApplyScope(stack, scope);
                }

                var allowed = graphics.AllowedEffects;
                if ((allowed & PostProcessEffects.ColorGrading) == 0)
                {
                    foreach (var type in GradingTypes)
                    {
                        ResetComponent(stack, type);
                    }
                }

                Filter<Bloom>(stack, allowed, PostProcessEffects.Bloom);
                Filter<Vignette>(stack, allowed, PostProcessEffects.Vignette);
                Filter<DepthOfField>(stack, allowed, PostProcessEffects.DepthOfField);
                Filter<ChromaticAberration>(stack, allowed, PostProcessEffects.ChromaticAberration, reducedMotion);
                Filter<LensDistortion>(stack, allowed, PostProcessEffects.LensDistortion, reducedMotion);
                Filter<FilmGrain>(stack, allowed, PostProcessEffects.FilmGrain, reducedMotion);
                if (reducedMotion)
                {
                    ResetComponent(stack, typeof(MotionBlur));
                }

                var color = stack.GetComponent<ColorAdjustments>();
                if (color != null && !Mathf.Approximately(graphics.Brightness, 1))
                {
                    Save(color);
                    color.postExposure.Override(color.postExposure.value + Mathf.Log(graphics.Brightness, 2));
                }
            }
            catch
            {
                RestoreStack();
                throw;
            }
        }

        private static void ApplyScope(VolumeStack stack, Scope scope)
        {
            foreach (var component in scope.Profile.components)
            {
                if (!component.active)
                {
                    continue;
                }

                var target = stack.GetComponent(component.GetType());
                if (target != null)
                {
                    Save(target);
                    component.Override(target, scope.Weight);
                }
            }
        }

        private static void Filter<T>(VolumeStack stack, PostProcessEffects allowed, PostProcessEffects effect, bool suppress = false)
            where T : VolumeComponent
        {
            if ((allowed & effect) == 0 || suppress)
            {
                ResetComponent(stack, typeof(T));
            }
        }

        private static bool IsSupported(Type type)
        {
            return Array.IndexOf(GradingTypes, type) >= 0 || type == typeof(Bloom) || type == typeof(Vignette)
                || type == typeof(DepthOfField) || type == typeof(ChromaticAberration)
                || type == typeof(LensDistortion) || type == typeof(FilmGrain);
        }

        private static void ResetComponent(VolumeStack stack, Type type)
        {
            var target = stack.GetComponent(type);
            if (target == null)
            {
                return;
            }

            neutral ??= ScriptableObject.CreateInstance<VolumeProfile>();
            var defaults = neutral.components.Find(component => component.GetType() == type);
            if (defaults == null)
            {
                defaults = neutral.Add(type, true);
                defaults.hideFlags = HideFlags.HideAndDontSave;
            }

            Save(target);
            defaults.Override(target, 1);
        }

        private static void Save(VolumeComponent target)
        {
            var type = target.GetType();
            if (!Backups.TryGetValue(type, out var backup))
            {
                backup = new Backup(target);
                Backups.Add(type, backup);
            }

            if (backup.Target == target)
            {
                return;
            }

            backup.Capture(target);
            Pending.Add(backup);
        }

        private static void RestoreStack()
        {
            foreach (var backup in Pending)
            {
                backup.Restore();
            }

            Pending.Clear();
        }

        private static void OnCameraRendered(ScriptableRenderContext context, Camera renderedCamera)
        {
            if (renderedCamera == camera)
            {
                RestoreStack();
            }
        }

        private static void RefreshCamera()
        {
            if (camera != null)
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = authoredPostProcessing
                    || basePreset != null || Scopes.Count > 0 || !Mathf.Approximately(graphics.Brightness, 1)
                    || StreamlineRuntime.ActiveSession != null && StreamlineRuntime.BoundCamera == camera;
            }
        }

        private static void ClearScopes()
        {
            for (int index = Scopes.Count - 1; index >= 0; index--)
            {
                Scopes[index].Dispose();
            }
        }

        /// 临时效果句柄；更新参数不会提升其覆盖优先级，也不会修改制作 SO。
        public sealed class Scope : IDisposable
        {
            internal VolumeProfile Profile { get; private set; }
            internal float Weight { get; private set; }
            private bool disposed;

            internal Scope(PostProcessPreset preset) => Update(preset);

            /// <summary>原子切换参数组合；验证和复制成功后才替换当前运行时副本。</summary>
            /// <param name="preset">非空制作预设；失败保留原效果与作用域顺序。</param>
            public void Update(PostProcessPreset preset)
            {
                EnsureActive();
                if (preset == null)
                {
                    throw new ArgumentNullException(nameof(preset));
                }

                preset.Validate();
                var next = CloneProfile(preset.Profile, preset.Weight);
                Replace(next, preset.Weight);
            }

            /// <summary>编辑现有原生参数的候选副本；成功后再复制提交，回调保留的引用不会修改已生效状态。</summary>
            /// <param name="edit">仅修改候选已有组件的参数，不增删或替换组件，不修改共享纹理等资源。异常或非有限参数保留原状态。</param>
            public void Update(Action<VolumeProfile> edit)
            {
                EnsureActive();
                if (edit == null)
                {
                    throw new ArgumentNullException(nameof(edit));
                }

                float weight = Weight;
                var candidate = CloneProfile(Profile, weight);
                var owned = candidate.components.ToArray();
                try
                {
                    edit(candidate);
                    EnsureActive();
                    if (candidate == null || candidate.components.Count != owned.Length)
                    {
                        throw new ArgumentException("参数更新不能增删组件或销毁候选。", nameof(edit));
                    }

                    for (int index = 0; index < owned.Length; index++)
                    {
                        if (!ReferenceEquals(candidate.components[index], owned[index]))
                        {
                            throw new ArgumentException("参数更新不能替换或重排组件。", nameof(edit));
                        }
                    }

                    var next = CloneProfile(candidate, weight);
                    Replace(next, weight);
                }
                finally
                {
                    foreach (var component in owned)
                    {
                        CoreUtils.Destroy(component);
                    }

                    CoreUtils.Destroy(candidate);
                }
            }

            /// 只移除本作用域；重复释放安全。
            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                RestoreStack();
                Scopes.Remove(this);
                DestroyProfile(Profile);
                Profile = null;
                RefreshCamera();
            }

            private void EnsureActive()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(Scope));
                }
            }

            private void Replace(VolumeProfile next, float weight)
            {
                RestoreStack();
                var previous = Profile;
                Profile = next;
                Weight = weight;
                DestroyProfile(previous);
                RefreshCamera();
            }
        }

        private static VolumeProfile CloneProfile(VolumeProfile source, float weight)
        {
            if (source == null || !GraphicsPreferences.FiniteRange(weight, 0, 1))
            {
                throw new ArgumentException("后处理 Profile 或权重无效。");
            }

            ValidateProfile(source);
            var copy = ScriptableObject.CreateInstance<VolumeProfile>();
            copy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                foreach (var component in source.components)
                {
                    var next = Object.Instantiate(component);
                    next.hideFlags = HideFlags.HideAndDontSave;
                    copy.components.Add(next);
                }

                return copy;
            }
            catch
            {
                DestroyProfile(copy);
                throw;
            }
        }

        private static void ValidateProfile(VolumeProfile profile)
        {
            var types = new HashSet<Type>();
            foreach (var component in profile.components)
            {
                if (component == null || !IsSupported(component.GetType()) || !types.Add(component.GetType()))
                {
                    throw new ArgumentException("Profile 只能包含七类效果内已支持且不重复的 URP 原生组件。");
                }

                foreach (var parameter in component.parameters)
                {
                    bool finite = true;
                    switch (parameter)
                    {
                        case VolumeParameter<float> number:
                            finite = float.IsFinite(number.value);
                            break;
                        case VolumeParameter<Vector2> vector2:
                            finite = float.IsFinite(vector2.value.x) && float.IsFinite(vector2.value.y);
                            break;
                        case VolumeParameter<Vector3> vector3:
                            finite = float.IsFinite(vector3.value.x) && float.IsFinite(vector3.value.y) && float.IsFinite(vector3.value.z);
                            break;
                        case VolumeParameter<Vector4> vector4:
                            finite = float.IsFinite(vector4.value.x) && float.IsFinite(vector4.value.y)
                                && float.IsFinite(vector4.value.z) && float.IsFinite(vector4.value.w);
                            break;
                        case VolumeParameter<Color> color:
                            finite = float.IsFinite(color.value.r) && float.IsFinite(color.value.g)
                                && float.IsFinite(color.value.b) && float.IsFinite(color.value.a);
                            break;
                    }

                    if (!finite)
                    {
                        throw new ArgumentException("后处理原生参数不能包含 NaN 或 Infinity。");
                    }
                }
            }
        }

        private static void DestroyProfile(VolumeProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            foreach (var component in profile.components)
            {
                CoreUtils.Destroy(component);
            }

            CoreUtils.Destroy(profile);
        }

        private sealed class Backup
        {
            private readonly bool[] overrides;
            private readonly VolumeParameter[] values;
            internal VolumeComponent Target { get; private set; }

            internal Backup(VolumeComponent target)
            {
                overrides = new bool[target.parameters.Count];
                values = new VolumeParameter[target.parameters.Count];
                for (int index = 0; index < values.Length; index++)
                {
                    // 参数快照不拥有 TextureCurve/Texture，不能以组件销毁释放场景的资源。
                    values[index] = (VolumeParameter)target.parameters[index].Clone();
                }
            }

            internal void Capture(VolumeComponent target)
            {
                Target = target;
                for (int index = 0; index < overrides.Length; index++)
                {
                    values[index].SetValue(target.parameters[index]);
                    overrides[index] = target.parameters[index].overrideState;
                }
            }

            internal void Restore()
            {
                if (Target != null)
                {
                    for (int index = 0; index < overrides.Length; index++)
                    {
                        Target.parameters[index].SetValue(values[index]);
                        Target.parameters[index].overrideState = overrides[index];
                    }
                }

                Target = null;
            }
        }
    }
}
