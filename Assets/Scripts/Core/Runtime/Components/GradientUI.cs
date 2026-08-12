using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 根据 Graphic 顶点的局部坐标应用水平或垂直颜色渐变。
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class GradientUI : BaseMeshEffect
    {
        public enum GradientDirection
        {
            Vertical,
            Horizontal
        }

        [SerializeField] private GradientDirection direction = GradientDirection.Vertical;
        [SerializeField, Range(-1f, 1f)] private float offset;
        [SerializeField] private Gradient gradient = new Gradient();

        private readonly List<UIVertex> vertices = new List<UIVertex>();

        /// 渐变方向。
        public GradientDirection Direction
        {
            get => direction;
            set
            {
                if (direction == value) return;
                direction = value;
                SetVerticesDirty();
            }
        }

        /// 渐变采样偏移。
        public float Offset
        {
            get => offset;
            set
            {
                float next = Mathf.Clamp(value, -1f, 1f);
                if (Mathf.Approximately(offset, next)) return;
                offset = next;
                SetVerticesDirty();
            }
        }

        /// 渐变配置。
        public Gradient Gradient
        {
            get => gradient;
            set
            {
                gradient = value;
                SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper.currentVertCount == 0 || gradient == null) return;

            vertices.Clear();
            helper.GetUIVertexStream(vertices);
            if (vertices.Count == 0) return;

            float min = GetAxis(vertices[0].position);
            float max = min;
            for (int i = 1; i < vertices.Count; i++)
            {
                float coordinate = GetAxis(vertices[i].position);
                min = Mathf.Min(min, coordinate);
                max = Mathf.Max(max, coordinate);
            }

            float range = max - min;
            if (range <= Mathf.Epsilon) return;

            UIVertex vertex = default;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                float t = Mathf.Clamp01((GetAxis(vertex.position) - min) / range - offset);
                vertex.color = gradient.Evaluate(t);
                helper.SetUIVertex(vertex, i);
            }
        }

        private float GetAxis(Vector3 position)
        {
            return direction == GradientDirection.Vertical ? position.y : position.x;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif

        private void SetVerticesDirty()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }
}
