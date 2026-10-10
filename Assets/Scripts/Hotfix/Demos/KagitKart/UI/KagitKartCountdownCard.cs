using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.KagitKart
{
    /// 原作倒计时剪纸字形；只变形自身四个顶点，不改变Canvas或比赛。
    public sealed class KagitKartCountdownCard : Image
    {
        [SerializeField] private Sprite[] numbers;
        [SerializeField] private Vector2[] sizesEm;
        private float angle;
        private float fontSize;
        private int index;

        /// <summary>数字复用源印刷图，中文“出发”继续使用项目字体。</summary>
        /// <param name="number">原倒计时数字。</param>
        public void ShowNumber(int number)
        {
            gameObject.SetActive(number > 0);
            if (number > 0)
            {
                index = 3 - number;
                sprite = numbers[index];
                rectTransform.sizeDelta = sizesEm[index] * fontSize;
            }
        }

        /// <summary>复用源 perspective(40em) 和88%折卡原点。</summary>
        /// <param name="degrees">当前rotateX角度。</param>
        /// <param name="size">原字体em的实际尺寸。</param>
        public void SetFold(float degrees, float size)
        {
            if (Mathf.Approximately(angle, degrees) && Mathf.Approximately(fontSize, size)) return;
            angle = degrees;
            fontSize = size;
            rectTransform.sizeDelta = sizesEm[index] * size;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);
            float sin = Mathf.Sin(angle * Mathf.Deg2Rad);
            float cos = Mathf.Cos(angle * Mathf.Deg2Rad);
            float offsetY = rectTransform.localPosition.y;
            var vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Vector3 point = vertex.position;
                float y = point.y + offsetY;
                float perspective = 1 + y * sin / Mathf.Max(1, fontSize * 40);
                point.x /= perspective;
                point.y = y * cos / perspective - offsetY;
                vertex.position = point;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
