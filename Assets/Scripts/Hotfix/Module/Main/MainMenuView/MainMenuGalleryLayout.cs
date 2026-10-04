using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;

namespace Hotfix
{
    /// 大厅的安全区与横屏布局；只调整本页内容，不改变公共 Canvas 或输入识别。
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class MainMenuGalleryLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform hero;
        [SerializeField] private RectTransform gallery;
        [SerializeField] private RectTransform title;
        [SerializeField] private RectTransform subtitle;
        [SerializeField] private RectTransform description;
        [SerializeField] private RectTransform startButton;
        [SerializeField] private RectTransform settingsButton;
        [SerializeField] private RectTransform footer;
        [SerializeField] private RectTransform hints;

        private Vector2 lastSize;
        private Rect lastSafeArea;
        private bool lastTouch;

        private void OnEnable() => lastSize = Vector2.zero;

        private void LateUpdate()
        {
            if (content == null) return;
            var size = ((RectTransform)transform).rect.size;
            var safe = Screen.safeArea;
            bool touch = Application.isMobilePlatform || Application.isPlaying && InputDeviceState.ActiveKind == InputDeviceKind.Touch;
            if (size == lastSize && safe == lastSafeArea && touch == lastTouch) return;
            lastSize = size;
            lastSafeArea = safe;
            lastTouch = touch;
            ApplyLayout(new Rect(safe.x / Mathf.Max(1, Screen.width), safe.y / Mathf.Max(1, Screen.height),
                safe.width / Mathf.Max(1, Screen.width), safe.height / Mathf.Max(1, Screen.height)), touch);
        }

        /// <summary>按宿主尺寸重排大厅，编辑期预览和运行时使用同一套布局。</summary>
        /// <param name="normalizedSafeArea">以宿主左下角为原点的归一化安全区。</param>
        /// <param name="useTouchLayout">触屏使用更大的卡片及操作区域，并收起长说明。</param>
        public void ApplyLayout(Rect normalizedSafeArea, bool useTouchLayout)
        {
            if (content == null) return;
            var size = ((RectTransform)transform).rect.size;
            float safeWidth = size.x * normalizedSafeArea.width;
            float safeHeight = size.y * normalizedSafeArea.height;
            if (safeWidth <= 0 || safeHeight <= 0) return;
            float scale = Mathf.Min(safeHeight / 1080f, safeWidth / 1660f);
            float width = Mathf.Min(2080f, safeWidth / scale);
            float height = safeHeight / scale;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            content.anchoredPosition = Vector2.Scale(normalizedSafeArea.center - new Vector2(.5f, .5f), size);
            content.sizeDelta = new Vector2(width, height);
            content.localScale = Vector3.one * scale;

            Place(hero, width * .335f, 108, width * .65f, 568);
            Place(title, 64, useTouchLayout ? 185 : 156, width * .34f, 118);
            var heading = title.GetComponent<TMP_Text>();
            heading.fontSize = heading.fontSizeMax = useTouchLayout ? 78 : 88;
            Place(subtitle, 68, useTouchLayout ? 320 : 294, width * .32f, 62);
            Place(description, 68, 382, width * .30f, 102);
            description.gameObject.SetActive(!useTouchLayout);
            Place(startButton, 68, useTouchLayout ? 432 : 520, 280, useTouchLayout ? 104 : 88);
            Place(settingsButton, 360, useTouchLayout ? 432 : 520, 128, useTouchLayout ? 104 : 88);

            float cardScale = useTouchLayout ? 1.4f : 1f;
            float galleryHeight = useTouchLayout ? 330 : 306;
            Place(gallery, 40, height - galleryHeight - 112, (width - 80) / cardScale, galleryHeight / cardScale);
            gallery.localScale = Vector3.one * cardScale;
            Place(footer, 64, height - 73, width * .48f, 42);
            Place(hints, width * .55f, height - 73, width * .41f, 42);
        }

        private static void Place(RectTransform target, float x, float y, float width, float height)
        {
            target.anchorMin = target.anchorMax = target.pivot = new Vector2(0, 1);
            target.anchoredPosition = new Vector2(x, -y);
            target.sizeDelta = new Vector2(width, height);
        }
    }
}
