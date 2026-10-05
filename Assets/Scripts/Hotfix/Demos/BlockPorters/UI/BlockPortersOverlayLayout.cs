using UnityEngine;
namespace Hotfix.BlockPorters
{
    /// 独立弹窗共享的竖屏安全区布局，不持有玩法数据。
    public sealed class BlockPortersOverlayLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform card;
        private float age;
        private void OnEnable() => age = 0;
        private void LateUpdate()
        {
            age += Time.unscaledDeltaTime;
            var size = ((RectTransform)transform).rect.size;
            var layout = BlockPortersScreenLayout.Calculate(Screen.width, Screen.height, Screen.safeArea);
            float scale = layout.Scale * size.x / Mathf.Max(1, Screen.width);
            float t = Mathf.Clamp01(age / .18f);
            card.localScale = Vector3.one * scale * Mathf.Lerp(.94f, 1, 1 - Mathf.Pow(1 - t, 3));
            card.anchoredPosition = new Vector2((Screen.safeArea.center.x / Mathf.Max(1, Screen.width) - .5f) * size.x,
                (Screen.safeArea.center.y / Mathf.Max(1, Screen.height) - .5f) * size.y);
        }
    }
}
