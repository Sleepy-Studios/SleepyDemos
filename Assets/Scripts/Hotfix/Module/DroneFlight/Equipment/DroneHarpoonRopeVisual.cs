using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>无碰撞 Verlet/PBD 视觉绳；物理张力由 DroneHarpoonModule 独立计算。</summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class DroneHarpoonRopeVisual : MonoBehaviour
    {
        [SerializeField, Range(8, 32)] private int segmentCount = 18;
        [SerializeField, Range(1, 8)] private int constraintIterations = 4;

        private LineRenderer line;
        private Vector3[] positions;
        private Vector3[] previous;
        private bool visible;
        private bool initialized;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            Rebuild();
            SetVisible(false);
        }

        internal void SetVisible(bool value)
        {
            if (value && !visible)
            {
                initialized = false;
            }

            visible = value;
            if (line != null)
            {
                line.enabled = value;
            }
        }

        /// <summary>在发射、解除或停靠边界清除上一段绳形和 Verlet 速度。</summary>
        internal void ResetSimulation(Vector3 start, Vector3 end)
        {
            if (positions == null || positions.Length != segmentCount + 1)
            {
                Rebuild();
            }

            InitializeLine(start, end);
            initialized = true;
            if (line != null)
            {
                line.positionCount = positions.Length;
                line.SetPositions(positions);
            }
        }

        internal void Step(Vector3 start, Vector3 end, float targetLength, float deltaTime)
        {
            if (!visible || line == null)
            {
                return;
            }

            if (positions == null || positions.Length != segmentCount + 1)
            {
                Rebuild();
            }

            if (!initialized)
            {
                InitializeLine(start, end);
                initialized = true;
            }

            var acceleration = Physics.gravity;
            var deltaSquared = deltaTime * deltaTime;
            for (var index = 1; index < positions.Length - 1; index++)
            {
                var current = positions[index];
                positions[index] += positions[index] - previous[index] + acceleration * deltaSquared;
                previous[index] = current;
            }

            positions[0] = start;
            positions[^1] = end;
            var distance = Vector3.Distance(start, end);
            var segmentLength = Mathf.Max(distance, targetLength) / segmentCount;
            for (var iteration = 0; iteration < constraintIterations; iteration++)
            {
                positions[0] = start;
                positions[^1] = end;
                for (var index = 0; index < positions.Length - 1; index++)
                {
                    var delta = positions[index + 1] - positions[index];
                    var length = Mathf.Max(0.0001f, delta.magnitude);
                    var correction = delta * ((length - segmentLength) / length);
                    if (index > 0)
                    {
                        positions[index] += correction * 0.5f;
                    }

                    if (index + 1 < positions.Length - 1)
                    {
                        positions[index + 1] -= correction * 0.5f;
                    }
                }
            }

            line.positionCount = positions.Length;
            line.SetPositions(positions);
        }

        private void Rebuild()
        {
            segmentCount = Mathf.Max(2, segmentCount);
            positions = new Vector3[segmentCount + 1];
            previous = new Vector3[segmentCount + 1];
            initialized = false;
            if (line != null)
            {
                line.positionCount = positions.Length;
            }
        }

        private void InitializeLine(Vector3 start, Vector3 end)
        {
            for (var index = 0; index < positions.Length; index++)
            {
                var value = index / (float)(positions.Length - 1);
                positions[index] = Vector3.Lerp(start, end, value);
                previous[index] = positions[index];
            }
        }
    }
}
