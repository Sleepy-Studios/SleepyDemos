using System;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 只管理现有相机的桌面聚焦；宿主据IsActive屏蔽角色移动，不改变钱包或游戏时钟。
    public sealed class JinxCasinoTableFocus : IDisposable
    {
        private readonly Camera camera;
        private Vector3 returnPosition;
        private Quaternion returnRotation;
        private float returnFieldOfView;
        private Vector3 fromPosition;
        private Quaternion fromRotation;
        private float fromFieldOfView;
        private float elapsed;
        private float duration;
        private bool isReturning;
        private bool hasCapturedPose;
        private JinxCasinoStation station;
        private Behaviour focusOwner;
        private Transform focusPose;
        private float focusedFieldOfView;

        /// 包括进入/退出过渡，活动期间禁止探索移动。
        public bool IsActive => focusOwner != null || isReturning;
        /// 过渡完成后才允许提交桌面操作。
        public bool IsReady => focusOwner != null && !isReturning && elapsed >= duration;
        /// 当前聚焦的具体实例，退出过渡期间仍保留。
        public JinxCasinoStation Station => station;

        /// <summary>绑定本地现有相机，不创建Camera或AudioListener。</summary>
        /// <param name="localCamera">受宿主管理的唯一游戏相机。</param>
        public JinxCasinoTableFocus(Camera localCamera)
        {
            camera = localCamera != null ? localCamera : throw new ArgumentNullException(nameof(localCamera));
        }

        /// <summary>进入已装配的机台；已有活动聚焦时拒绝串台。</summary>
        /// <param name="target">具体场景机台，必须有稳定ID与桌面挂点。</param>
        /// <param name="seconds">过渡秒数；0表示立即聚焦。</param>
        /// <returns>是否开始进入该机台。</returns>
        public bool TryEnter(JinxCasinoStation target, float seconds = 0.35f)
        {
            return target != null && target.HasTableInteraction && TryEnter(target, target.FocusPose, target.FocusFieldOfView, seconds);
        }

        /// <summary>聚焦柜台等已有场景交互点；仍借用唯一相机并保留同一退出契约。</summary>
        /// <param name="owner">拥有挂点的启用组件。</param>
        /// <param name="pose">属于该组件层级的保存挂点。</param>
        /// <param name="fieldOfView">30至75度的桌面视野。</param>
        /// <param name="seconds">过渡秒数。</param>
        /// <returns>是否成功开始聚焦。</returns>
        public bool TryEnter(Behaviour owner, Transform pose, float fieldOfView, float seconds = .35f)
        {
            if (IsActive || owner == null || !owner.isActiveAndEnabled || pose == null || !pose.IsChildOf(owner.transform) || camera == null ||
                float.IsNaN(fieldOfView) || float.IsInfinity(fieldOfView)) return false;
            returnPosition = camera.transform.position;
            returnRotation = camera.transform.rotation;
            returnFieldOfView = camera.fieldOfView;
            hasCapturedPose = true;
            station = owner as JinxCasinoStation; focusOwner = owner; focusPose = pose;
            focusedFieldOfView = Mathf.Clamp(fieldOfView, 30, 75); isReturning = false;
            BeginTransition(seconds);
            Tick(0);
            return true;
        }

        /// <summary>开始退出；不取消已提交局，不操作规则随机状态。</summary>
        /// <param name="seconds">恢复相机的过渡秒数；0为立即恢复。</param>
        public void Exit(float seconds = 0.35f)
        {
            if (!IsActive || isReturning) return;
            isReturning = true;
            BeginTransition(seconds);
            Tick(0);
        }

        private void BeginTransition(float seconds)
        {
            elapsed = 0;
            duration = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0.35f : Mathf.Max(0, seconds);
            fromPosition = camera.transform.position;
            fromRotation = camera.transform.rotation;
            fromFieldOfView = camera.fieldOfView;
        }

        /// <summary>跟随机台挂点更新聚焦；宿主暂停时可传0，退出销毁会立即恢复。</summary>
        /// <param name="deltaSeconds">非负真实帧间隔，不推进冒险规则时钟。</param>
        public void Tick(float deltaSeconds)
        {
            if (camera == null) return;
            if (!isReturning && (focusOwner == null || focusPose == null || !focusOwner.isActiveAndEnabled))
            {
                if (hasCapturedPose) RestoreImmediately();
                return;
            }
            if (!IsActive) return;
            elapsed += float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0 : Mathf.Max(0, deltaSeconds);
            float t = duration <= 0 ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
            Vector3 destination = isReturning ? returnPosition : focusPose.position;
            Quaternion rotation = isReturning ? returnRotation : focusPose.rotation;
            float fov = isReturning ? returnFieldOfView : focusedFieldOfView;
            camera.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, destination, t), Quaternion.Slerp(fromRotation, rotation, t));
            camera.fieldOfView = Mathf.Lerp(fromFieldOfView, fov, t);
            if (isReturning && t >= 1) { station = null; focusOwner = null; focusPose = null; isReturning = false; hasCapturedPose = false; }
        }

        /// 宿主销毁或机台失效时恢复相机，避免下次进入保留桌面FOV。
        public void RestoreImmediately()
        {
            if (camera != null && hasCapturedPose)
            {
                camera.transform.SetPositionAndRotation(returnPosition, returnRotation);
                camera.fieldOfView = returnFieldOfView;
            }
            station = null; focusOwner = null; focusPose = null; isReturning = false; hasCapturedPose = false;
        }

        /// 生命周期结束时恢复借用的相机。
        public void Dispose() => RestoreImmediately();
    }
}
