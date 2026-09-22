using System;
using Core.Runtime;
using Core.Runtime.Rendering.Streamline;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Hotfix.Dlss
{
    /// DLSS Demo 的导航、设置与观察视角宿主。
    public sealed class DlssDemoController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private UniversalRenderPipelineAsset pipeline;
        [SerializeField] private Transform movingObject;
        [SerializeField] private Transform spinningObject;
        private StreamlineCameraSession session;
        private DlssSettingsView view;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Vector3 movingOrigin;
        private float yaw, pitch;
        private bool ready, exiting;
        private float nextStatusTime, resizeStableAt;
        private Vector2Int observedSize;

        /// 当前世界相机会话。
        public StreamlineCameraSession Session => session;
        /// 设置或导航正在执行。
        public bool IsBusy => !ready || exiting || session == null || session.IsBusy || !session.HasRenderedFrame;
        /// 供设置 View 订阅的变化通知。
        public event Action Changed;

        /// <summary>由 Demo Builder 装配固定场景引用。</summary>
        /// <param name="camera">离屏世界相机。</param>
        /// <param name="renderPipeline">独立体验管线。</param>
        /// <param name="moving">平移演示物体。</param>
        /// <param name="spinning">旋转演示物体。</param>
        public void Configure(Camera camera, UniversalRenderPipelineAsset renderPipeline, Transform moving, Transform spinning)
        {
            worldCamera = camera; pipeline = renderPipeline; movingObject = moving; spinningObject = spinning;
        }

        private void Start()
        {
            homePosition = worldCamera.transform.position;
            homeRotation = worldCamera.transform.rotation;
            movingOrigin = movingObject != null ? movingObject.position : Vector3.zero;
            ResetCamera();
            BeginAsync().Forget();
        }

        private async UniTaskVoid BeginAsync()
        {
            try
            {
                var navigator = GameSceneNavigator.Instance;
                if (navigator == null) throw new InvalidOperationException("请从 AppEntrance 启动，经 Hub 进入 DLSS Demo。");
                await navigator.WaitUntilStableAsync(GameSceneId.Dlss, this.GetCancellationTokenOnDestroy());
                session = new StreamlineCameraSession(worldCamera, pipeline);
                session.Changed += OnSessionChanged;
                var shown = await UIManager.Instance.ShowAsync<DlssSettingsView, DlssDemoController>(this,
                    new UIShowOptions(animated: false, hidePrevious: false));
                if (shown.Status != UIOperationStatus.Succeeded && shown.Status != UIOperationStatus.Ignored)
                    throw new InvalidOperationException(shown.Exception?.Message ?? "设置面板加载失败。");
                if (this == null)
                {
                    if (shown.View != null) await UIManager.Instance.CloseAsync(shown.View, false);
                    return;
                }
                view = shown.View as DlssSettingsView;
                observedSize = ReadOutputSize();
                await session.ApplyAsync(StreamlineDlssMode.Quality, observedSize);
                if (this == null) return;
                ready = true;
                Changed?.Invoke();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogError("[DLSS Demo] " + exception.Message, this);
            }
        }

        /// <summary>请求切换 DLSS；忙碌时保持当前设置。</summary>
        /// <param name="mode">null 为关闭，其余为质量模式。</param>
        public void SetMode(StreamlineDlssMode? mode)
        {
            if (!IsBusy) ApplyModeAsync(mode).Forget();
        }

        private async UniTaskVoid ApplyModeAsync(StreamlineDlssMode? mode)
        {
            var applyingSession = session;
            if (applyingSession == null) return;
            bool applied = await applyingSession.ApplyAsync(mode, ReadOutputSize());
            if (this == null || session != applyingSession) return;
            if (!applied && !string.IsNullOrEmpty(applyingSession.Error)) Debug.LogWarning("[DLSS Demo] " + applyingSession.Error, this);
            Changed?.Invoke();
        }

        private void Update()
        {
            if (!ready || exiting || session == null) return;
            session.Tick();
            if (movingObject != null) movingObject.position = movingOrigin + Vector3.right * Mathf.Sin(Time.time * 0.65f) * 1.5f;
            if (spinningObject != null) spinningObject.Rotate(new Vector3(12, 28, 8) * Time.deltaTime);
            var size = ReadOutputSize();
            if (size != observedSize) { observedSize = size; resizeStableAt = Time.unscaledTime + 0.25f; }
            if (!session.IsBusy && size != session.OutputSize && Time.unscaledTime >= resizeStableAt)
                ApplyModeAsync(session.Mode).Forget();
            if (!IsBusy) UpdateCameraInput();
            if (Time.unscaledTime >= nextStatusTime)
            {
                nextStatusTime = Time.unscaledTime + 0.25f;
                Changed?.Invoke();
            }
            if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame) RequestExit();
        }

        private void UpdateCameraInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame) ResetCamera();
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Mouse.current != null && Mouse.current.rightButton.isPressed && !overUi)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                yaw += delta.x * 0.12f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.12f, -75, 75);
                worldCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }
            Vector3 movement = Vector3.zero;
            if (keyboard.wKey.isPressed) movement += worldCamera.transform.forward;
            if (keyboard.sKey.isPressed) movement -= worldCamera.transform.forward;
            if (keyboard.dKey.isPressed) movement += worldCamera.transform.right;
            if (keyboard.aKey.isPressed) movement -= worldCamera.transform.right;
            float speed = keyboard.leftShiftKey.isPressed ? 8 : 3;
            worldCamera.transform.position += movement * (speed * Time.unscaledDeltaTime);
        }

        /// 恢复初始视角并重置时域历史。
        public void ResetCamera()
        {
            if (worldCamera == null) return;
            worldCamera.transform.SetPositionAndRotation(homePosition, homeRotation);
            Vector3 angles = homeRotation.eulerAngles;
            yaw = angles.y; pitch = angles.x > 180 ? angles.x - 360 : angles.x;
            session?.ResetHistory();
        }

        /// 返回 Hub，先收口具体 View 和 GPU 会话。
        public void RequestExit()
        {
            if (!exiting) ExitAsync().Forget();
        }

        private async UniTaskVoid ExitAsync()
        {
            exiting = true;
            Changed?.Invoke();
            while (session != null && session.IsBusy) await UniTask.Yield();
            if (view != null) { await UIManager.Instance.CloseAsync(view, false); view = null; }
            ReleaseSession();
            var result = await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
            if (result.Status == GameSceneSwitchStatus.Failed && this != null)
            {
                Debug.LogError("[DLSS Demo] 返回 Hub 失败：" + result.Error, this);
                ready = false; exiting = false;
                BeginAsync().Forget();
            }
        }

        private static Vector2Int ReadOutputSize() => new Vector2Int(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height));
        private void OnSessionChanged() => Changed?.Invoke();

        private void ReleaseSession()
        {
            if (session == null) return;
            session.Changed -= OnSessionChanged;
            session.Dispose();
            session = null;
        }

        private void OnDestroy()
        {
            ReleaseSession();
            Changed = null;
        }
    }
}
