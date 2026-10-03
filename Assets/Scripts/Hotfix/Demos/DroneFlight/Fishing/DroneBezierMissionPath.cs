using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>捕鱼任务可重复采样的路径阶段。</summary>
    internal enum DroneMissionPathSection
    {
        Entry,
        Orbit,
        Dive
    }

    [Serializable]
    internal sealed class DroneBezierSegment
    {
        [SerializeField] private Transform start;
        [SerializeField] private Transform startHandle;
        [SerializeField] private Transform endHandle;
        [SerializeField] private Transform end;

        internal bool IsValid => start != null && startHandle != null && endHandle != null && end != null;

        internal Vector3 Evaluate(float time)
        {
            var t = Mathf.Clamp01(time);
            var inverse = 1f - t;
            return inverse * inverse * inverse * start.position
                   + 3f * inverse * inverse * t * startHandle.position
                   + 3f * inverse * t * t * endHandle.position
                   + t * t * t * end.position;
        }

        internal Vector3 EvaluateTangent(float time)
        {
            var t = Mathf.Clamp01(time);
            var inverse = 1f - t;
            return 3f * inverse * inverse * (startHandle.position - start.position)
                   + 6f * inverse * t * (endHandle.position - startHandle.position)
                   + 3f * t * t * (end.position - endHandle.position);
        }

        internal void Configure(Transform point0, Transform point1, Transform point2, Transform point3)
        {
            start = point0;
            startHandle = point1;
            endHandle = point2;
            end = point3;
        }
    }

    /// <summary>以鱼的水面投影为原点，由入场、闭合环绕和俯冲三组贝塞尔段组成。</summary>
    public sealed class DroneBezierMissionPath : MonoBehaviour
    {
        [SerializeField] private DroneBezierSegment[] entrySegments = Array.Empty<DroneBezierSegment>();
        [SerializeField] private DroneBezierSegment[] orbitSegments = Array.Empty<DroneBezierSegment>();
        [SerializeField] private DroneBezierSegment[] diveSegments = Array.Empty<DroneBezierSegment>();
        [SerializeField, Min(4)] private int lengthSamplesPerSegment = 20;

        private float entryLength;
        private float orbitLength;
        private float diveLength;

        /// 入场路径起点，也是无人机出生标记。
        internal Vector3 EntryStart => Evaluate(DroneMissionPathSection.Entry, 0f);

        /// 俯冲路径终点，也是水面上方射击悬停点。
        internal Vector3 DiveEnd => Evaluate(DroneMissionPathSection.Dive, 1f);

        private void Awake()
        {
            RecalculateLengths();
        }

        private void OnValidate()
        {
            lengthSamplesPerSegment = Mathf.Max(4, lengthSamplesPerSegment);
            RecalculateLengths();
        }

        internal Vector3 Evaluate(DroneMissionPathSection section, float normalizedTime)
        {
            return EvaluateSegments(GetSegments(section), normalizedTime, false);
        }

        internal Vector3 EvaluateTangent(DroneMissionPathSection section, float normalizedTime)
        {
            return EvaluateSegments(GetSegments(section), normalizedTime, true);
        }

        internal float GetApproximateLength(DroneMissionPathSection section)
        {
            return section switch
            {
                DroneMissionPathSection.Entry => entryLength,
                DroneMissionPathSection.Orbit => orbitLength,
                DroneMissionPathSection.Dive => diveLength,
                _ => 0f
            };
        }

        internal void RecalculateLengths()
        {
            entryLength = ApproximateLength(entrySegments);
            orbitLength = ApproximateLength(orbitSegments);
            diveLength = ApproximateLength(diveSegments);
        }

        internal void Configure(
            DroneBezierSegment[] entry,
            DroneBezierSegment[] orbit,
            DroneBezierSegment[] dive)
        {
            entrySegments = entry ?? Array.Empty<DroneBezierSegment>();
            orbitSegments = orbit ?? Array.Empty<DroneBezierSegment>();
            diveSegments = dive ?? Array.Empty<DroneBezierSegment>();
            RecalculateLengths();
        }

        private DroneBezierSegment[] GetSegments(DroneMissionPathSection section)
        {
            return section switch
            {
                DroneMissionPathSection.Entry => entrySegments,
                DroneMissionPathSection.Orbit => orbitSegments,
                DroneMissionPathSection.Dive => diveSegments,
                _ => Array.Empty<DroneBezierSegment>()
            };
        }

        private static Vector3 EvaluateSegments(
            DroneBezierSegment[] segments,
            float normalizedTime,
            bool tangent)
        {
            if (segments == null || segments.Length == 0)
            {
                return Vector3.zero;
            }

            var scaled = Mathf.Clamp01(normalizedTime) * segments.Length;
            var index = Mathf.Min(Mathf.FloorToInt(scaled), segments.Length - 1);
            var localTime = index == segments.Length - 1 && normalizedTime >= 1f
                ? 1f
                : scaled - index;
            var segment = segments[index];
            if (segment == null || !segment.IsValid)
            {
                return Vector3.zero;
            }

            return tangent ? segment.EvaluateTangent(localTime) : segment.Evaluate(localTime);
        }

        private float ApproximateLength(DroneBezierSegment[] segments)
        {
            if (segments == null || segments.Length == 0)
            {
                return 0f;
            }

            var length = 0f;
            foreach (var segment in segments)
            {
                if (segment == null || !segment.IsValid)
                {
                    continue;
                }

                var previous = segment.Evaluate(0f);
                for (var index = 1; index <= lengthSamplesPerSegment; index++)
                {
                    var current = segment.Evaluate(index / (float)lengthSamplesPerSegment);
                    length += Vector3.Distance(previous, current);
                    previous = current;
                }
            }

            return length;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawSection(entrySegments, new Color(0.2f, 0.8f, 1f));
            DrawSection(orbitSegments, new Color(1f, 0.75f, 0.15f));
            DrawSection(diveSegments, new Color(1f, 0.25f, 0.25f));
        }

        private static void DrawSection(DroneBezierSegment[] segments, Color color)
        {
            if (segments == null)
            {
                return;
            }

            Gizmos.color = color;
            foreach (var segment in segments)
            {
                if (segment == null || !segment.IsValid)
                {
                    continue;
                }

                var previous = segment.Evaluate(0f);
                for (var index = 1; index <= 24; index++)
                {
                    var current = segment.Evaluate(index / 24f);
                    Gizmos.DrawLine(previous, current);
                    previous = current;
                }
            }
        }
#endif
    }
}
