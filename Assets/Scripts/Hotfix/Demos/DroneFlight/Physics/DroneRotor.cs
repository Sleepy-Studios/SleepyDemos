using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// X 架四旋翼的固定位置语义。
    /// </summary>
    internal enum DroneRotorPosition
    {
        FrontLeft,
        FrontRight,
        RearLeft,
        RearRight
    }

    /// <summary>
    /// 从机体上方俯视时的旋翼转向。
    /// </summary>
    internal enum DroneRotorDirection
    {
        Clockwise = -1,
        CounterClockwise = 1
    }

    /// <summary>
    /// 将场景中的施力点、旋向和可选视觉桨叶绑定为一个旋翼描述。
    /// </summary>
    public sealed class DroneRotor : MonoBehaviour
    {
        [SerializeField] private DroneRotorPosition position;
        [SerializeField] private DroneRotorDirection direction;
        [SerializeField] private Transform visualPropeller;
        [SerializeField] private DroneRotorVisual rotorVisual;
        [SerializeField] private Transform forceAxis;

        /// <summary>旋翼在 X 架中的固定位置。</summary>
        internal DroneRotorPosition Position => position;

        /// <summary>从机体上方观察的旋转方向。</summary>
        internal DroneRotorDirection Direction => direction;

        /// <summary>推力施加位置和方向。</summary>
        internal Transform ForceTransform => transform;

        /// <summary>物理推力方向；正式机体固定为无人机根节点的局部 +Y。</summary>
        internal Vector3 ForceDirection => forceAxis != null ? forceAxis.up : transform.up;

        /// <summary>可选的视觉桨叶。</summary>
        internal Transform VisualPropeller => visualPropeller;

        /// 平滑旋翼视觉驱动；不得参与推力计算。
        /// <summary>
        /// 由运行时装配器或测试工况显式设置旋翼语义。
        /// </summary>
        /// <param name="rotorPosition">X 架位置。</param>
        /// <param name="rotorDirection">俯视旋向。</param>
        /// <param name="propeller">可选视觉桨叶。</param>
        internal void Configure(
            DroneRotorPosition rotorPosition,
            DroneRotorDirection rotorDirection,
            Transform propeller = null,
            DroneRotorVisual visual = null,
            Transform physicalForceAxis = null)
        {
            position = rotorPosition;
            direction = rotorDirection;
            visualPropeller = propeller;
            rotorVisual = visual;
            forceAxis = physicalForceAxis;
            rotorVisual?.Configure(propeller, rotorDirection, physicalForceAxis);
        }

        /// <summary>
        /// 将电机模型的真实转速提交给视觉驱动。
        /// </summary>
        /// <param name="rpm">电机当前转速，单位 rpm。</param>
        internal void SetVisualRpm(float rpm)
        {
            if (rotorVisual == null && visualPropeller != null)
            {
                rotorVisual = visualPropeller.GetComponent<DroneRotorVisual>();
            }

            if (rotorVisual == null)
            {
                return;
            }

            rotorVisual.Configure(visualPropeller, direction, forceAxis);
            rotorVisual.SetRpm(rpm);
        }

        /// 停止视觉旋翼并清空累计相位。
        internal void ResetVisual()
        {
            rotorVisual?.ResetVisual();
        }
    }
}
