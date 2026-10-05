using System;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Rendering.Streamline;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Hotfix.Dlss
{
    /// DLSS Demo 的导航与观察视角宿主。
    public sealed class DlssDemoController : MonoBehaviour
    {
        [SerializeField]
        private InputActionAsset actions;

        private InputActionSession input;

        private DlssData data;

        private readonly CancellationTokenSource lifetime = new();

        private bool IsCurrent => !lifetime.IsCancellationRequested && ReferenceEquals(GlobalData.Get<DlssData>(), data);

        private DlssControlsView controls;

        internal InputActionSession Actions => input;

        internal bool AcceptsControls => data != null && !data.IsExiting && !SettingsOpen;

        private bool SettingsOpen => data?.SettingsOpen == true;

        [SerializeField]
        private Camera worldCamera;
        [SerializeField]
        private Transform movingObject;
        [SerializeField]
        private Transform spinningObject;

        private Vector3 homePosition, movingOrigin;

        private Quaternion homeRotation;

        private float yaw, pitch;

        private bool exiting => data?.IsExiting == true;

        private readonly GameplayInputSettings inputSettings = new();

        private void Start() => InitializeAsync().Forget();

        private async UniTask InitializeAsync()
        {
            homePosition = worldCamera.transform.position;
            homeRotation = worldCamera.transform.rotation;
            movingOrigin = movingObject != null ? movingObject.position : Vector3.zero;
            ResetCamera();
            input = new InputActionSession(actions);
            input.SetMap("Observe");
            data = GlobalData.Add(new DlssData(this));
            var opened = await UIManager.Instance.ShowAsync<DlssControlsView>(view => view.SetData(input.Asset), new UIShowOptions(false), lifetime.Token);
            if (opened.Status == UIOperationStatus.Failed)
                throw opened.Exception;
            if (IsCurrent)
                controls = opened.View as DlssControlsView;
        }

        private void Update()
        {
            if (exiting)
                return;
            if (movingObject != null)
                movingObject.position = movingOrigin + Vector3.right * Mathf.Sin(Time.time * 0.65f) * 1.5f;
            if (spinningObject != null)
                spinningObject.Rotate(new Vector3(12, 28, 8) * Time.deltaTime);
            UpdateCameraInput();
            if (input?.Pressed("Exit") == true)
                RequestExit();
            if (input?.Pressed("Settings") == true)
                OpenSettings();
        }

        private void UpdateCameraInput()
        {
            if (input == null)
                return;
            string map = SettingsOpen ? "Menu" : "Observe";
            if (input.Map?.name != map)
            {
                input.SetMap(map);
            }

            if (SettingsOpen)
                return;
            if (input.Pressed("Reset"))
                GlobalData.Dispatch(new DlssControlAction("Reset"));
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 mouse = input.Held("LookHold") && !overUi ? input.Read<Vector2>("MouseLook") : Vector2.zero;
            Vector2 pad = input.ReadVector("Look");
            Vector2 touch = controls != null ? controls.Look : Vector2.zero;
            Vector2 degrees = GameplayInputMath.LookDegrees(mouse, touch, pad, Time.unscaledDeltaTime, .12f, inputSettings);
            yaw += degrees.x;
            pitch = Mathf.Clamp(pitch - degrees.y, -75, 75);
            worldCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector2 move = input.ReadVector("Move") + (controls != null ? controls.Move : Vector2.zero);
            float speed = input.Held("Sprint") || controls?.Sprint == true ? 8 : 3;
            worldCamera.transform.position += (worldCamera.transform.forward * move.y + worldCamera.transform.right * move.x) * (speed * Time.unscaledDeltaTime);
        }

        /// 打开公共设置并马上退出观察动作；关闭后等松键再恢复。
        public void OpenSettings() => GlobalData.Dispatch(new DlssControlAction("Settings"));

        internal void ShowSettings() => ShowSettingsAsync().Forget();

        private async UniTask ShowSettingsAsync()
        {
            if (!IsCurrent)
                return;
            input?.SetMap("Menu");
            if (controls != null)
                await UIManager.Instance.CloseAsync(controls, false);
            var result = await UIManager.Instance.ShowAsync<DlssSettingsView>(cancellationToken: lifetime.Token);
            if (IsCurrent && result.Status == UIOperationStatus.Failed)
            {
                Debug.LogException(result.Exception, this);
                GlobalData.Dispatch(new DlssSettingsClosedAction(data));
            }
        }

        internal void RestoreControls() => RestoreControlsAsync().Forget();

        private async UniTask RestoreControlsAsync()
        {
            if (!IsCurrent)
                return;
            input?.SetMap("Observe");
            var opened = await UIManager.Instance.ShowAsync<DlssControlsView>(view => view.SetData(input.Asset), new UIShowOptions(false), lifetime.Token);
            if (IsCurrent && opened.Status == UIOperationStatus.Failed)
                Debug.LogException(opened.Exception, this);
            if (IsCurrent)
                controls = opened.View as DlssControlsView;
        }

        private void OnDestroy()
        {
            lifetime.Cancel();
            input?.Dispose();
            data?.Handler.Dispose();
            if (controls != null)
                DisposeControlsAsync(controls).Forget();
            if (ReferenceEquals(GlobalData.Get<DlssData>(), data))
                GlobalData.Remove<DlssData>();
            lifetime.Dispose();
        }

        private static async UniTask DisposeControlsAsync(DlssControlsView view)
        {
            await UIManager.Instance.CloseAsync(view, false);
            UIManager.Instance.cacheStack.Remove(view);
            await view.DestroyAsync();
        }

        /// 恢复初始视角并重置时域历史。
        public void ResetCamera()
        {
            if (worldCamera == null)
                return;
            worldCamera.transform.SetPositionAndRotation(homePosition, homeRotation);
            Vector3 angles = homeRotation.eulerAngles;
            yaw = angles.y;
            pitch = angles.x > 180 ? angles.x - 360 : angles.x;
            StreamlineRuntime.ResetHistory();
        }

        /// 返回 Hub，先收口具体 View 和 GPU 会话。
        public void RequestExit() => GlobalData.Dispatch(new DlssControlAction("Exit"));

        internal void ExitScene() => ExitAsync().Forget();

        private async UniTask ExitAsync()
        {
            if (controls != null)
            {
                await DisposeControlsAsync(controls);
                controls = null;
            }

            var result = await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
            if (IsCurrent && result.Status != GameSceneSwitchStatus.Succeeded)
            {
                GlobalData.Dispatch(new DlssRestoreAction(data));
                RestoreControls();
            }
        }
    }
}
