using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>由端点和目标绳长确定的无碰撞视觉绳；物理张力由 DroneHarpoonModule 独立计算。</summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class DroneHarpoonRopeVisual : MonoBehaviour
    {
        [SerializeField, Range(8, 32)] private int segmentCount = 18;
        [SerializeField] private float maximumVisualSagMeters = 0.75f;

        private LineRenderer line;
        private Vector3[] positions;
        private bool visible;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            Rebuild();
            SetVisible(false);
        }

        internal void SetVisible(bool value)
        {
            visible = value;
            if (line != null)
            {
                line.enabled = value;
            }
        }

        /// <summary>在发射、解除或停靠边界立即重建一条有限直线。</summary>
        internal void ResetSimulation(Vector3 start, Vector3 end)
        {
            if (positions == null || positions.Length != segmentCount + 1)
            {
                Rebuild();
            }

            InitializeLine(start, end);
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

            if (!IsFinite(start) || !IsFinite(end) || !float.IsFinite(targetLength))
            {
                ResetSimulation(Vector3.zero, Vector3.zero);
                return;
            }

            var distance = Vector3.Distance(start, end);
            var effectiveLength = Mathf.Max(distance, Mathf.Max(0f, targetLength));
            var slack = Mathf.Sqrt(Mathf.Max(0f, effectiveLength * effectiveLength - distance * distance));
            var sag = Mathf.Min(Mathf.Max(0f, maximumVisualSagMeters), slack * 0.5f);
            for (var index = 0; index < positions.Length; index++)
            {
                var value = index / (float)(positions.Length - 1);
                positions[index] = Vector3.Lerp(start, end, value)
                                   + Vector3.down * (4f * value * (1f - value) * sag);
            }

            line.positionCount = positions.Length;
            line.SetPositions(positions);
        }

        private void Rebuild()
        {
            segmentCount = Mathf.Max(2, segmentCount);
            positions = new Vector3[segmentCount + 1];
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
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
