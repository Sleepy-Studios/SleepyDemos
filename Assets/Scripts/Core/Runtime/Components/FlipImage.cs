using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 对任意 UGUI Graphic 的顶点做水平、垂直翻转或平面旋转。
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class FlipImage : BaseMeshEffect
    {
        [SerializeField] private bool flipHorizontal = true;
        [SerializeField] private bool flipVertical;
        [SerializeField] private float rotationAngle;

        /// 是否水平翻转。
        public bool FlipHorizontal
        {
            get => flipHorizontal;
            set
            {
                if (flipHorizontal == value) return;
                flipHorizontal = value;
                SetVerticesDirty();
            }
        }

        /// 是否垂直翻转。
        public bool FlipVertical
        {
            get => flipVertical;
            set
            {
                if (flipVertical == value) return;
                flipVertical = value;
                SetVerticesDirty();
            }
        }

        /// 额外旋转角度。
        public float RotationAngle
        {
            get => rotationAngle;
            set
            {
                if (Mathf.Approximately(rotationAngle, value)) return;
                rotationAngle = value;
                SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper.currentVertCount == 0) return;

            float angle = Mathf.Repeat(rotationAngle, 360f);
            bool hasRotation = !Mathf.Approximately(angle, 0f);
            if (!flipHorizontal && !flipVertical && !hasRotation) return;

            Vector2 center = graphic.rectTransform.rect.center;
            float radians = angle * Mathf.Deg2Rad;
            float sin = hasRotation ? Mathf.Sin(radians) : 0f;
            float cos = hasRotation ? Mathf.Cos(radians) : 1f;
            UIVertex vertex = default;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                Vector3 position = vertex.position;
                float x = position.x - center.x;
                float y = position.y - center.y;
                if (flipHorizontal) x = -x;
                if (flipVertical) y = -y;
                if (hasRotation)
                {
                    (x, y) = (x * cos - y * sin, x * sin + y * cos);
                }

                position.x = center.x + x;
                position.y = center.y + y;
                vertex.position = position;
                helper.SetUIVertex(vertex, i);
            }
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
