using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>按真实电机转速平滑驱动单个旋翼的纯视觉节点。</summary>
    public sealed class DroneRotorVisual : MonoBehaviour
    {
        [SerializeField] private Transform bladeRoot;

        private DroneRotorDirection direction;

        /// 当前电机转速，单位 rpm。
        internal float CurrentRpm { get; private set; }

        /// 未取模的累计视觉旋转角，便于验证高速旋转方向。
        internal double AccumulatedDegrees { get; private set; }

        private void Update()
        {
            StepVisual(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 绑定桨叶根节点与俯视旋向。
        /// </summary>
        /// <param name="root">只承载视觉网格的旋转根节点。</param>
        /// <param name="rotorDirection">从机体上方俯视的旋翼方向。</param>
        internal void Configure(Transform root, DroneRotorDirection rotorDirection)
        {
            bladeRoot = root;
            direction = rotorDirection;
        }

        /// <summary>
        /// 提交最近一次物理步得到的真实电机转速。
        /// </summary>
        /// <param name="rpm">非负有限转速，非法值按零处理。</param>
        internal void SetRpm(float rpm)
        {
            CurrentRpm = float.IsFinite(rpm) ? Mathf.Max(0f, rpm) : 0f;
        }

        /// <summary>
        /// 使用渲染帧时间推进旋翼相位，不参与飞行物理。
        /// </summary>
        /// <param name="deltaTime">非缩放渲染帧时长。</param>
        internal void StepVisual(float deltaTime)
        {
            if (bladeRoot == null || !float.IsFinite(deltaTime) || deltaTime <= 0f || CurrentRpm <= 0f)
            {
                return;
            }

            var degrees = CurrentRpm * 6d * deltaTime * (float)direction;
            AccumulatedDegrees += degrees;
            bladeRoot.Rotate(0f, (float)degrees, 0f, Space.Self);
        }

        /// 将视觉转速和累计相位归零。
        internal void ResetVisual()
        {
            CurrentRpm = 0f;
            AccumulatedDegrees = 0d;
        }
    }
}
