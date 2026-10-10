using System;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 原作跟随镜头；表现插值、相对偏移、视野与震动均使用独立比赛时间。
    public sealed class KagitKartCameraRig : IDisposable
    {
        private readonly Camera camera;
        private readonly KagitKartCameraTuning tuning;
        private readonly KagitKartVehicle player;
        private readonly float pageY;
        private bool initialized;
        private float yaw;
        private float height;
        private float horizontalFov;
        private Vector3 offset;
        private float shakeAmount;
        private float shakeTime;
        private bool shakeEnabled = true;

        public bool LookBack { get; private set; }
        public bool ShakeEnabled
        {
            get => shakeEnabled;
            set
            {
                shakeEnabled = value;
                if (!value)
                {
                    shakeAmount = 0;
                }
            }
        }

        /// <summary>绑定本场摄像机与玩家，退出时 Dispose 解除规则订阅。</summary>
        /// <param name="camera">场景唯一主相机。</param>
        /// <param name="tuning">原作镜头参数。</param>
        /// <param name="player">本场玩家车辆。</param>
        /// <param name="pageY">书页高度，掉落时保持镜头位于书页之上。</param>
        public KagitKartCameraRig(Camera camera, KagitKartCameraTuning tuning, KagitKartVehicle player, float pageY)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.pageY = pageY;
            camera.nearClipPlane = tuning.near;
            camera.farClipPlane = tuning.far;
            player.Signaled += OnSignal;
        }

        /// <summary>在车辆表现插值后推进镜头，暂停传零保持姿态。</summary>
        /// <param name="dt">未缩放比赛表现时间，内部最多推进 0.1 秒。</param>
        /// <param name="position">已经转换为 Unity 坐标的车辆插值位置。</param>
        public void Advance(float dt, Vector3 position)
        {
            dt = Mathf.Clamp(dt, 0, 0.1f);
            float targetYaw = player.Heading + tuning.driftYawShare * player.VisualYaw;
            if (!initialized)
            {
                yaw = targetYaw;
                offset = -Forward(yaw) * tuning.distance;
                height = position.y + tuning.height;
                horizontalFov = tuning.hfovRest;
                initialized = true;
            }
            else
            {
                yaw += Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, targetYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad
                    * (1 - Mathf.Pow(0.5f, dt / tuning.yawHalfLife));
            }
            Vector3 forward = Forward(yaw);
            bool boosting = player.BoostTime > 0;
            Vector3 desired = -forward * (tuning.distance + (boosting ? tuning.boostDistance : 0));
            offset = Vector3.LerpUnclamped(offset, desired, 1 - Mathf.Pow(0.5f, dt / tuning.posHalfLife));
            height = Damp(height, position.y + tuning.height,
                player.Grounded ? tuning.heightHalfLife : tuning.airHeightHalfLife, dt);
            float speed = Mathf.Clamp(Mathf.Abs(player.Speed) / Mathf.Max(1, player.TopSpeed), 0, 1.2f);
            float factor = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.3f, 1, speed));
            float fov = boosting ? tuning.hfovBoost : Mathf.Lerp(tuning.hfovRest, tuning.hfovTop, factor);
            horizontalFov = Damp(horizontalFov, fov, tuning.fovHalfLife, dt);
            float camY = Mathf.Max(height, position.y + 1.2f);
            if (position.y < pageY + 1)
            {
                camY = Mathf.Max(camY, pageY + 1);
            }
            LookBack = player.Controls.LookBack && !player.Autopilot;
            Vector3 look;
            if (LookBack)
            {
                forward = Forward(player.Heading);
                camera.transform.position = position + forward * tuning.lookBackDistance + Vector3.up * tuning.height * 0.9f;
                look = position - forward * tuning.lookAhead + Vector3.up * tuning.lookHeight;
            }
            else
            {
                camera.transform.position = new Vector3(position.x + offset.x, camY, position.z + offset.z);
                look = position + forward * tuning.lookAhead + Vector3.up * tuning.lookHeight;
            }
            if (ShakeEnabled && shakeAmount > 0.001f)
            {
                shakeTime += dt;
                camera.transform.position += new Vector3(Mathf.Sin(shakeTime * 71),
                    Mathf.Sin(shakeTime * 53 + 1) * 0.6f, Mathf.Sin(shakeTime * 67 + 2)) * shakeAmount;
                shakeAmount = Damp(shakeAmount, 0, 1 / tuning.shakeDecay, dt);
            }
            camera.transform.LookAt(look);
            camera.fieldOfView = Mathf.Clamp(2 * Mathf.Atan(Mathf.Tan(horizontalFov * Mathf.Deg2Rad / 2)
                / Mathf.Max(0.1f, camera.aspect)) * Mathf.Rad2Deg, tuning.vfovMin, tuning.vfovMax);
        }

        public void Dispose() => player.Signaled -= OnSignal;

        private void OnSignal(KagitKartVehicle kart, KagitKartVehicleSignal signal)
        {
            if (ShakeEnabled && (signal == KagitKartVehicleSignal.Hit || signal == KagitKartVehicleSignal.WallHit))
            {
                float amount = signal == KagitKartVehicleSignal.Hit ? tuning.shakeHit : tuning.shakeWall
                    * (0.35f + 0.65f * Mathf.Clamp01(kart.LastImpactSpeed * Mathf.Sin(kart.LastImpactAngle) / 20));
                shakeAmount = Mathf.Max(shakeAmount, amount);
            }
        }

        private static Vector3 Forward(float angle) => new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
        private static float Damp(float value, float target, float halfLife, float dt) =>
            Mathf.LerpUnclamped(value, target, 1 - Mathf.Pow(0.5f, dt / halfLife));
    }
}
