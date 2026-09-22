using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Core.Runtime.Rendering.Streamline
{
    /// 全局偏好与当前主相机状态；设置 UI 不拥有原生会话。
    public static partial class StreamlineRuntime
    {
        private const string PreferenceKey = "SleepyDemos.Graphics.DlssMode";
        private static Camera boundCamera;
        private static Camera boundUi;
        private static StreamlineRuntimeDriver driver;
        private static int generation;
        private static bool changing;
        private static string unavailableReason;
        private static Vector2Int observedSize;
        private static float resizeAt;

        /// 用户选择，可能因当前设备或相机不支持而尚未生效。
        public static StreamlineDlssMode? RequestedMode { get; private set; }
        /// 当前已成功执行的模式；空表示正常渲染。
        public static StreamlineDlssMode? EffectiveMode => ActiveSession?.HasEvaluatedFrame == true ? ActiveSession.Mode : null;
        /// 当前设置正在应用。
        public static bool IsBusy => changing || ActiveSession?.IsBusy == true;
        /// 当前生效状态或不可用原因。
        public static string Status => unavailableReason ?? ActiveSession?.Error ??
            (IsBusy ? "正在切换…" : EffectiveMode.HasValue ? "运行中" : RequestedMode.HasValue ? "等待首帧" : "已关闭");
        /// 当前渲染输入尺寸。
        public static Vector2Int InputSize => ActiveSession?.InputSize ?? ReadSize();
        /// 当前显示输出尺寸。
        public static Vector2Int OutputSize => ActiveSession?.OutputSize ?? ReadSize();
        /// 当前绑定的玩法主相机。
        public static Camera BoundCamera => boundCamera;
        /// 偏好或实际状态发生变化。
        public static event Action Changed;

        /// <summary>验证保存的枚举；未知版本的值按关闭处理。</summary>
        /// <param name="value">保存的整数值。</param>
        public static StreamlineDlssMode? DecodePreference(int value)
        {
            switch ((StreamlineDlssMode)value)
            {
                case StreamlineDlssMode.Quality: case StreamlineDlssMode.Balanced:
                case StreamlineDlssMode.Performance: case StreamlineDlssMode.UltraPerformance:
                case StreamlineDlssMode.Dlaa: return (StreamlineDlssMode)value;
                default: return null;
            }
        }

        private static void InitializeSharedSettings()
        {
            RequestedMode = ReadSavedMode();
            var go = new GameObject("Streamline Runtime") { hideFlags = HideFlags.DontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            driver = go.AddComponent<StreamlineRuntimeDriver>();
        }

        /// 读取持久化偏好，不初始化图形资源。
        public static StreamlineDlssMode? ReadSavedMode() => DecodePreference(PlayerPrefs.GetInt(PreferenceKey, 0));

        /// <summary>保存全局档位并应用到当前相机；不能应用时保留偏好。</summary>
        /// <param name="mode">空表示关闭。</param>
        public static void SetMode(StreamlineDlssMode? mode)
        {
            if (mode.HasValue && DecodePreference((int)mode.Value) == null) throw new ArgumentOutOfRangeException(nameof(mode));
            Initialize();
            RequestedMode = mode;
            PlayerPrefs.SetInt(PreferenceKey, mode.HasValue ? (int)mode.Value : 0);
            PlayerPrefs.Save();
            ReapplyAsync().Forget();
            Changed?.Invoke();
        }

        /// <summary>公共 UI 相机绑定完成后登记当前主相机。</summary>
        /// <param name="camera">玩法拥有的主相机。</param>
        /// <param name="uiCamera">公共 Overlay UI 相机。</param>
        public static void BindCamera(Camera camera, Camera uiCamera)
        {
            if (!Application.isPlaying) return;
            Initialize();
            DetachCamera();
            boundCamera = camera;
            boundUi = uiCamera;
            observedSize = ReadSize();
            ReapplyAsync().Forget();
        }

        /// 切换或卸载旧相机前排空会话，恢复原相机与 UI 栈。
        public static void DetachCamera()
        {
            generation++;
            changing = false;
            ActiveSession?.Dispose();
            boundCamera = null;
            boundUi = null;
        }

        /// <summary>查询当前配置是否可尝试，实际硬件结果以 SDK 返回为准。</summary>
        /// <param name="reason">不能接入的原因。</param>
        public static bool CanEnable(out string reason)
        {
            reason = null;
            if (!IsBackendSupported) reason = "仅支持 Windows Editor 的 DX12/Vulkan。";
            else if (RequiresRestart) reason = "原生清理失败，请重启 Editor。";
            else if (boundCamera == null || boundUi == null) reason = "当前没有可接入的公共主相机。";
            else if (boundCamera.orthographic || boundCamera.stereoEnabled || boundCamera.rect != new Rect(0, 0, 1, 1)) reason = "当前相机的正交、XR 或局部视口配置尚不支持。";
            else if (ActiveSession == null)
            {
                var data = boundCamera.GetUniversalAdditionalCameraData();
                if (boundCamera.targetTexture != null || data.renderType != CameraRenderType.Base ||
                    data.cameraStack.Count != 1 || data.cameraStack[0] != boundUi) reason = "仅支持屏幕主相机与公共 UI Camera Stack。";
            }
            else if (boundCamera.GetUniversalAdditionalCameraData().cameraStack.Count != 0)
                reason = "启用期间主相机增加了额外 Camera Stack，已回退。";
            return reason == null;
        }

        private static async UniTaskVoid ReapplyAsync()
        {
            int ticket = ++generation;
            changing = true;
            while (ActiveSession?.IsBusy == true)
            {
                await UniTask.Yield();
                if (ticket != generation) return;
            }
            try
            {
                ActiveSession?.Dispose();
                unavailableReason = null;
                if (!RequestedMode.HasValue) return;
                // BindToBaseCamera 先于导航启用目标相机；下一帧再快照，避免把临时禁用状态当作恢复值。
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                if (ticket != generation) return;
                if (!CanEnable(out unavailableReason)) return;
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline == null) { unavailableReason = "当前管线不是 URP。"; return; }
                var session = new StreamlineCameraSession(boundCamera, pipeline, boundUi);
                session.Changed += NotifyChanged;
                bool applied = await session.ApplyAsync(RequestedMode, ReadSize());
                if (ticket != generation) return;
                if (!applied) { unavailableReason = session.Error; session.Dispose(); }
            }
            catch (Exception exception)
            {
                unavailableReason = exception.Message;
                ActiveSession?.Dispose();
            }
            finally
            {
                if (ticket == generation) { changing = false; Changed?.Invoke(); }
            }
        }

        private static void NotifyChanged() => Changed?.Invoke();
        private static Vector2Int ReadSize() => new Vector2Int(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height));

        internal static void UpdateRuntime()
        {
            if (boundCamera == null && UIRootManager.Instance.BaseCamera != null)
                BindCamera(UIRootManager.Instance.BaseCamera, UIRootManager.Instance.UICamera);
            ActiveSession?.Tick();
            if (!IsBusy && ActiveSession != null && !CanEnable(out string compatibilityError))
            {
                unavailableReason = compatibilityError;
                ActiveSession.Dispose();
                Changed?.Invoke();
            }
            if (!IsBusy && ActiveSession != null && !string.IsNullOrEmpty(ActiveSession.Error))
            {
                unavailableReason = ActiveSession.Error;
                ActiveSession.Dispose();
                Changed?.Invoke();
            }
            var size = ReadSize();
            if (size != observedSize)
            {
                observedSize = size;
                resizeAt = Time.unscaledTime + 0.25f;
                Changed?.Invoke();
            }
            if (!IsBusy && ActiveSession != null && size != ActiveSession.OutputSize && Time.unscaledTime >= resizeAt) ReapplyAsync().Forget();
        }

        /// 切镜和暂停恢复时重置当前相机历史。
        public static void ResetHistory() => ActiveSession?.ResetHistory();

        internal static void Shutdown()
        {
            DetachCamera();
            IsInitialized = false;
            Changed = null;
            driver = null;
        }
    }

    internal sealed class StreamlineRuntimeDriver : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnEnable() => UnityEditor.EditorApplication.pauseStateChanged += OnEditorPause;
        private void OnDisable() => UnityEditor.EditorApplication.pauseStateChanged -= OnEditorPause;
        private void OnEditorPause(UnityEditor.PauseState state) => StreamlineRuntime.ResetHistory();
#endif
        private void Update() => StreamlineRuntime.UpdateRuntime();
        private void OnApplicationPause(bool paused) => StreamlineRuntime.ResetHistory();
        private void OnApplicationFocus(bool focused) => StreamlineRuntime.ResetHistory();
        private void OnDestroy() => StreamlineRuntime.Shutdown();
    }
}
