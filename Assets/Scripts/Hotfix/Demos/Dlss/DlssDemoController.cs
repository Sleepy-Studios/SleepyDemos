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
        [SerializeField] private InputActionAsset actions;
        private InputActionSession input;
        private IResourceLoader controlsLoader;
        private GameObject controlsObject;
        private bool loadingControls;
        private DlssControlsPresenter controls;
        internal InputActionSession Actions => input;
        internal bool AcceptsControls => !exiting && !SettingsOpen;
        private bool SettingsOpen => UIManager.Instance.Get<DlssSettingsView>()?.IsSettingsPanelOpen == true;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform movingObject;
        [SerializeField] private Transform spinningObject;
        private Vector3 homePosition, movingOrigin;
        private Quaternion homeRotation;
        private float yaw, pitch;
        private bool exiting;
        private readonly GameplayInputSettings inputSettings = new();

        private async UniTaskVoid Start()
        {
            homePosition = worldCamera.transform.position;
            homeRotation = worldCamera.transform.rotation;
            movingOrigin = movingObject != null ? movingObject.position : Vector3.zero;
            ResetCamera();
            input = new InputActionSession(actions); input.SetMap("Observe");
            controlsLoader = ResourceServices.CreateLoader();
            var loader = controlsLoader;
            loadingControls = true;
            try
            {
                var instance = await loader.InstantiateAsync("LoadResources/Demos/dlss/Prefabs/UI/DlssControls", UIRootManager.Instance.GetRoot(UILayer.Decorate));
                if (this == null) { if (instance != null) loader.ReleaseInstance(instance); return; }
                if (instance == null) return;
                controlsObject = instance;
                controls = instance.GetComponent<DlssControlsPresenter>(); controls.Bind(this);
            }
            finally
            {
                // 加载器尚在等待时不提前释放资源句柄；场景销毁后的迟到实例也由同一所有者回收。
                loadingControls = false;
                if (this == null) loader.Dispose();
            }
        }

        private void Update()
        {
            if (exiting) return;
            if (movingObject != null) movingObject.position = movingOrigin + Vector3.right * Mathf.Sin(Time.time * 0.65f) * 1.5f;
            if (spinningObject != null) spinningObject.Rotate(new Vector3(12, 28, 8) * Time.deltaTime);
            UpdateCameraInput();
            if (input?.Pressed("Exit") == true) RequestExit();
            if (input?.Pressed("Settings") == true) OpenSettings();
        }

        private void UpdateCameraInput()
        {
            if (input == null) return;
            string map = SettingsOpen ? "Menu" : "Observe";
            if (input.Map?.name != map)
            {
                input.SetMap(map); controls?.gameObject.SetActive(!SettingsOpen);
            }
            if (SettingsOpen) return;
            if (input.Pressed("Reset")) ResetCamera();
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 mouse = input.Held("LookHold") && !overUi ? input.Read<Vector2>("MouseLook") : Vector2.zero;
            Vector2 pad = input.ReadVector("Look");
            Vector2 touch = controls != null ? controls.Look : Vector2.zero;
            Vector2 degrees = GameplayInputMath.LookDegrees(mouse, touch, pad, Time.unscaledDeltaTime, .12f, inputSettings);
            yaw += degrees.x; pitch = Mathf.Clamp(pitch - degrees.y, -75, 75);
            worldCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector2 move = input.ReadVector("Move") + (controls != null ? controls.Move : Vector2.zero);
            float speed = input.Held("Sprint") || controls?.Sprint == true ? 8 : 3;
            worldCamera.transform.position += (worldCamera.transform.forward * move.y + worldCamera.transform.right * move.x) * (speed * Time.unscaledDeltaTime);
        }

        /// 打开公共设置并马上退出观察动作；关闭后等松键再恢复。
        public void OpenSettings()
        {
            UIManager.Instance.Get<DlssSettingsView>()?.SetSettingsPanelOpen(true);
            input?.SetMap("Menu"); controls?.gameObject.SetActive(false);
        }
        private void OnDestroy()
        {
            input?.Dispose();
            if (controlsObject != null) controlsLoader?.ReleaseInstance(controlsObject);
            if (!loadingControls) controlsLoader?.Dispose();
        }

        /// 恢复初始视角并重置时域历史。
        public void ResetCamera()
        {
            if (worldCamera == null) return;
            worldCamera.transform.SetPositionAndRotation(homePosition, homeRotation);
            Vector3 angles = homeRotation.eulerAngles;
            yaw = angles.y; pitch = angles.x > 180 ? angles.x - 360 : angles.x;
            StreamlineRuntime.ResetHistory();
        }

        /// 返回 Hub，先收口具体 View 和 GPU 会话。
        public void RequestExit()
        {
            if (!exiting) ExitAsync().Forget();
        }

        private async UniTaskVoid ExitAsync()
        {
            exiting = true;
            var result = await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
            if (result.Status == GameSceneSwitchStatus.Failed) exiting = false;
        }
    }
}
