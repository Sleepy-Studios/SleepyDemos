using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// 将遥控器代理动画、RT 预览、全屏 Camera 接管和输入上下文绑定到状态机。
    /// </summary>
    public sealed class DroneRemoteControllerExperience : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private DroneCameraRig droneCameraRig;
        [SerializeField] private DronePlayerInput flightInput;
        [SerializeField] private Transform remoteControllerRoot;
        [SerializeField] private MeshRenderer remoteScreenRenderer;
        [SerializeField] private Vector2Int previewResolution = new(1280, 720);

        private readonly DroneRemoteControlSequence sequence = new();
        private RenderTexture previewTexture;
        private Material runtimeScreenMaterial;
        private DroneRemoteControlState appliedState = (DroneRemoteControlState)(-1);

        /// <summary>当前接管流程状态。</summary>
        internal DroneRemoteControlState State => sequence.State;

        /// <summary>供运行诊断读取的接管流程状态名。</summary>
        public string CurrentStateName => sequence.State.ToString();

        /// <summary>运行时 RT 是否仍被持有。</summary>
        internal bool HasPreviewTexture => previewTexture != null;

        private void Awake()
        {
            if (flightInput != null)
            {
                flightInput.enabled = false;
            }

            ApplyState(force: true);
        }

        private void Update()
        {
            sequence.Step(Time.unscaledDeltaTime);
            ApplyProxyAnimation();
            ApplyState(force: false);

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                if (sequence.State == DroneRemoteControlState.GroundIdle)
                {
                    sequence.BeginEnter();
                }
                else if (sequence.State == DroneRemoteControlState.Preview)
                {
                    sequence.ExpandToFullscreen();
                }
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                sequence.BeginExit();
            }
        }

        private void OnDestroy()
        {
            ReleasePreviewTexture();
            if (runtimeScreenMaterial != null)
            {
                Destroy(runtimeScreenMaterial);
            }
        }

        /// <summary>开始拿起并开启遥控器。</summary>
        internal void BeginEnter()
        {
            sequence.BeginEnter();
        }

        /// <summary>从 RT 预览推进到全屏接管。</summary>
        internal void ExpandToFullscreen()
        {
            sequence.ExpandToFullscreen();
        }

        /// <summary>退出无人机控制并恢复玩家相机。</summary>
        internal void BeginExit()
        {
            sequence.BeginExit();
        }

        /// <summary>
        /// 由场景装配器或测试夹具绑定接管流程依赖。
        /// </summary>
        internal void Configure(
            Camera player,
            DroneCameraRig rig,
            DronePlayerInput input,
            Transform controllerRoot,
            MeshRenderer screenRenderer)
        {
            playerCamera = player;
            droneCameraRig = rig;
            flightInput = input;
            remoteControllerRoot = controllerRoot;
            remoteScreenRenderer = screenRenderer;
            ApplyState(force: true);
        }

        private void ApplyState(bool force)
        {
            if (!force && appliedState == sequence.State)
            {
                return;
            }

            appliedState = sequence.State;
            var droneCamera = droneCameraRig != null ? droneCameraRig.OutputCamera : null;
            switch (sequence.State)
            {
                case DroneRemoteControlState.Preview:
                    EnsurePreviewTexture();
                    if (droneCamera != null)
                    {
                        droneCamera.targetTexture = previewTexture;
                        droneCamera.enabled = true;
                    }

                    if (playerCamera != null)
                    {
                        playerCamera.enabled = true;
                    }
                    break;
                case DroneRemoteControlState.Fullscreen:
                    if (droneCamera != null)
                    {
                        droneCamera.targetTexture = null;
                        droneCamera.enabled = true;
                    }

                    if (playerCamera != null)
                    {
                        playerCamera.enabled = false;
                    }

                    if (flightInput != null)
                    {
                        flightInput.enabled = true;
                    }
                    break;
                case DroneRemoteControlState.GroundIdle:
                    if (droneCamera != null)
                    {
                        droneCamera.targetTexture = null;
                        droneCamera.enabled = false;
                    }

                    if (playerCamera != null)
                    {
                        playerCamera.enabled = true;
                    }

                    if (flightInput != null)
                    {
                        flightInput.enabled = false;
                    }

                    ReleasePreviewTexture();
                    break;
            }
        }

        private void ApplyProxyAnimation()
        {
            if (remoteControllerRoot == null)
            {
                return;
            }

            // 全屏阶段由无人机 Camera 独占画面，遥控器代理必须退出世界渲染，
            // 否则第三人称/环绕相机会从远处拍到仍挂在玩家 Camera 下的代理模型。
            var visible = sequence.State != DroneRemoteControlState.GroundIdle
                          && sequence.State != DroneRemoteControlState.Fullscreen;
            remoteControllerRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var progress = sequence.State == DroneRemoteControlState.Exiting
                ? 1f - sequence.NormalizedProgress
                : sequence.NormalizedProgress;
            remoteControllerRoot.localPosition = Vector3.Lerp(new Vector3(0f, -0.5f, 0.8f), new Vector3(0f, -0.15f, 0.45f), progress);
            remoteControllerRoot.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, progress);
        }

        private void EnsurePreviewTexture()
        {
            if (previewTexture == null)
            {
                previewTexture = new RenderTexture(
                    Mathf.Max(320, previewResolution.x),
                    Mathf.Max(180, previewResolution.y),
                    24)
                {
                    name = "DroneRemotePreviewRT"
                };
                previewTexture.Create();
            }

            if (remoteScreenRenderer != null)
            {
                if (runtimeScreenMaterial == null)
                {
                    runtimeScreenMaterial = new Material(remoteScreenRenderer.sharedMaterial);
                    remoteScreenRenderer.material = runtimeScreenMaterial;
                }

                runtimeScreenMaterial.mainTexture = previewTexture;
            }
        }

        private void ReleasePreviewTexture()
        {
            if (previewTexture == null)
            {
                return;
            }

            previewTexture.Release();
            Destroy(previewTexture);
            previewTexture = null;
            if (runtimeScreenMaterial != null)
            {
                runtimeScreenMaterial.mainTexture = null;
            }
        }
    }
}
