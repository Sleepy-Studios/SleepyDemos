using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>集中处理四旋翼排序、执行器推进与调试向量，避免飞控编排器直接维护施力细节。</summary>
    internal static class DroneRotorActuatorRuntime
    {
        internal const int RotorCount = 4;

        /// <summary>
        /// 按固定 X 架位置整理旋翼引用。
        /// </summary>
        /// <param name="rotors">Prefab 或测试夹具中的全部旋翼。</param>
        /// <param name="orderedRotors">长度必须为四的目标数组。</param>
        /// <param name="error">失败时返回中文结构诊断。</param>
        internal static bool TryOrder(
            DroneRotor[] rotors,
            DroneRotor[] orderedRotors,
            out string error)
        {
            error = string.Empty;
            if (rotors == null || rotors.Length != RotorCount || orderedRotors == null
                || orderedRotors.Length != RotorCount)
            {
                error = $"需要 {RotorCount} 个 DroneRotor，当前找到 {rotors?.Length ?? 0} 个。";
                return false;
            }

            var assigned = new bool[RotorCount];
            foreach (var rotor in rotors)
            {
                if (rotor == null)
                {
                    error = "旋翼引用为空。";
                    return false;
                }

                var index = (int)rotor.Position;
                if (index < 0 || index >= RotorCount || assigned[index])
                {
                    error = $"Rotor 位置重复或越界：{rotor.Position}。";
                    return false;
                }

                assigned[index] = true;
                orderedRotors[index] = rotor;
            }

            return true;
        }

        /// <summary>
        /// 将四个电机输出转换为四点真实施力和反扭矩。
        /// </summary>
        /// <param name="body">无人机主刚体。</param>
        /// <param name="rotors">按 X 架位置排序的四个旋翼。</param>
        /// <param name="motors">与旋翼一一对应的电机模型。</param>
        /// <param name="states">接收本固定步电机状态的数组。</param>
        /// <param name="output">物理控制分配后的四电机命令。</param>
        /// <param name="deltaTime">固定步时长。</param>
        /// <returns>本固定步四个旋翼的总推力，单位 N。</returns>
        internal static float StepAndApply(
            Rigidbody body,
            DroneRotor[] rotors,
            DroneMotorModel[] motors,
            DroneMotorState[] states,
            QuadrotorMotorOutput output,
            float deltaTime)
        {
            var commands = new[] { output.FrontLeft, output.FrontRight, output.RearLeft, output.RearRight };
            var totalThrust = 0f;
            for (var index = 0; index < RotorCount; index++)
            {
                var state = motors[index].Step(commands[index], deltaTime);
                states[index] = state;
                totalThrust += state.ThrustNewtons;
                var rotor = rotors[index];
                body.AddForceAtPosition(
                    rotor.ForceDirection * state.ThrustNewtons,
                    rotor.ForceTransform.position,
                    ForceMode.Force);
                body.AddTorque(
                    rotor.ForceDirection * state.ReactionTorqueNewtonMeters * (float)rotor.Direction,
                    ForceMode.Force);
                rotor.SetVisualRpm(state.Rpm);
            }

            return totalThrust;
        }

        internal static bool TryGetDebugVector(
            DroneRotor[] rotors,
            DroneMotorState[] states,
            int index,
            out Vector3 origin,
            out Vector3 thrustForce)
        {
            if (index < 0 || index >= RotorCount || rotors[index] == null)
            {
                origin = Vector3.zero;
                thrustForce = Vector3.zero;
                return false;
            }

            origin = rotors[index].ForceTransform.position;
            thrustForce = rotors[index].ForceDirection * states[index].ThrustNewtons;
            return true;
        }

        internal static Vector3 SumThrust(DroneRotor[] rotors, DroneMotorState[] states)
        {
            var total = Vector3.zero;
            for (var index = 0; index < RotorCount; index++)
            {
                if (rotors[index] != null)
                {
                    total += rotors[index].ForceDirection * states[index].ThrustNewtons;
                }
            }

            return total;
        }
    }
}
