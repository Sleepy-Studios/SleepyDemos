using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>由端点和目标绳长确定的无碰撞视觉绳；物理张力由 DroneHarpoonModule 独立计算。</summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class DroneHarpoonRopeVisual : MonoBehaviour
    {
        [SerializeField, Range(8, 32)] private int segmentCount = 18;
        [SerializeField] private float maximumVisualSagMeters = 0.75f;
        [SerializeField] private float sagResponseSeconds = 0.08f;
        [SerializeField] private float tautThresholdMeters = 0.005f;
        [SerializeField] private float slackReleaseThresholdMeters = 0.008f;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;

        private LineRenderer line;
        private Vector3[] positions;
        private bool visible;
        private bool consideredTaut = true;
        private float targetLength;
        private float smoothedSag;
        private float sagVelocity;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            Rebuild();
            SetVisible(false);
        }

        private void OnEnable()
        {
            line ??= GetComponent<LineRenderer>();
            if (!visible)
            {
                ForceHiddenAndClear();
            }
        }

        private void OnDisable()
        {
            ForceHiddenAndClear();
        }

        private void LateUpdate()
        {
            if (!visible || line == null || startPoint == null || endPoint == null)
            {
                ForceHiddenAndClear();
                return;
            }

            line.enabled = true;
            DrawInterpolatedFrame(startPoint.position, endPoint.position, Time.unscaledDeltaTime);
        }

        internal void ConfigureEndpoints(Transform start, Transform end)
        {
            startPoint = start;
            endPoint = end;
        }

        internal void SetTargetLength(float value)
        {
            targetLength = Mathf.Max(0f, value);
        }

        internal void SetVisible(bool value)
        {
            visible = value;
            if (!value)
            {
                ForceHiddenAndClear();
                return;
            }

            if (line != null)
            {
                line.enabled = true;
            }
        }

        /// <summary>在发射、解除或停靠边界立即重建一条有限直线。</summary>
        internal void ResetSimulation(Vector3 start, Vector3 end)
        {
            if (positions == null || positions.Length != segmentCount + 1)
            {
                Rebuild();
            }

            smoothedSag = 0f;
            sagVelocity = 0f;
            consideredTaut = true;
            InitializeLine(start, end);
            if (line != null)
            {
                line.positionCount = positions.Length;
                line.SetPositions(positions);
            }
        }

        private void DrawInterpolatedFrame(Vector3 start, Vector3 end, float deltaTime)
        {
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
            var slackDistance = Mathf.Max(0f, targetLength - distance);
            if (consideredTaut)
            {
                consideredTaut = slackDistance <= Mathf.Max(tautThresholdMeters, slackReleaseThresholdMeters);
            }
            else
            {
                consideredTaut = slackDistance <= Mathf.Max(0f, tautThresholdMeters);
            }

            var effectiveLength = Mathf.Max(distance, targetLength);
            var slack = Mathf.Sqrt(Mathf.Max(0f, effectiveLength * effectiveLength - distance * distance));
            var targetSag = consideredTaut
                ? 0f
                : Mathf.Min(Mathf.Max(0f, maximumVisualSagMeters), slack * 0.5f);
            smoothedSag = Mathf.SmoothDamp(
                smoothedSag,
                targetSag,
                ref sagVelocity,
                Mathf.Max(0.001f, sagResponseSeconds),
                Mathf.Infinity,
                Mathf.Max(0f, deltaTime));
            for (var index = 0; index < positions.Length; index++)
            {
                var value = index / (float)(positions.Length - 1);
                positions[index] = Vector3.Lerp(start, end, value)
                                   + Vector3.down * (4f * value * (1f - value) * smoothedSag);
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

        // 隐藏态每个渲染帧都清除 Renderer 与旧顶点，避免生命周期重启时闪回上一条长曲线。
        private void ForceHiddenAndClear()
        {
            smoothedSag = 0f;
            sagVelocity = 0f;
            consideredTaut = true;
            if (line == null)
            {
                return;
            }

            line.enabled = false;
            line.positionCount = 0;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
