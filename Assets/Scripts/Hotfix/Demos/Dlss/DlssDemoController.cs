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
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform movingObject;
        [SerializeField] private Transform spinningObject;
        private Vector3 homePosition, movingOrigin;
        private Quaternion homeRotation;
        private float yaw, pitch;
        private bool exiting;

        /// <summary>由 Demo Builder 装配固定场景引用。</summary>
        /// <param name="camera">玩法主相机。</param>
        /// <param name="moving">平移演示物体。</param>
        /// <param name="spinning">旋转演示物体。</param>
        public void Configure(Camera camera, Transform moving, Transform spinning)
        {
            worldCamera = camera; movingObject = moving; spinningObject = spinning;
        }

        private void Start()
        {
            homePosition = worldCamera.transform.position;
            homeRotation = worldCamera.transform.rotation;
            movingOrigin = movingObject != null ? movingObject.position : Vector3.zero;
            ResetCamera();

        }

        private void Update()
        {
            if (exiting) return;
            if (movingObject != null) movingObject.position = movingOrigin + Vector3.right * Mathf.Sin(Time.time * 0.65f) * 1.5f;
            if (spinningObject != null) spinningObject.Rotate(new Vector3(12, 28, 8) * Time.deltaTime);
            UpdateCameraInput();
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
