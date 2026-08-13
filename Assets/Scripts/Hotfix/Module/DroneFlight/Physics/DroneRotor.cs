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

        /// <summary>旋翼在 X 架中的固定位置。</summary>
        internal DroneRotorPosition Position => position;

        /// <summary>从机体上方观察的旋转方向。</summary>
        internal DroneRotorDirection Direction => direction;

        /// <summary>推力施加位置和方向。</summary>
        internal Transform ForceTransform => transform;

        /// <summary>可选的视觉桨叶。</summary>
        internal Transform VisualPropeller => visualPropeller;

        /// <summary>
        /// 由运行时装配器或测试工况显式设置旋翼语义。
        /// </summary>
        /// <param name="rotorPosition">X 架位置。</param>
        /// <param name="rotorDirection">俯视旋向。</param>
        /// <param name="propeller">可选视觉桨叶。</param>
        internal void Configure(
            DroneRotorPosition rotorPosition,
            DroneRotorDirection rotorDirection,
            Transform propeller = null)
        {
            position = rotorPosition;
            direction = rotorDirection;
            visualPropeller = propeller;
        }
    }
}
