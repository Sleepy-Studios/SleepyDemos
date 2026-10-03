using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// 机型选择页使用固定设计比例；背景铺满，内容整体避让安全区。
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DroneVehicleSelectionLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text[] cardTitles;
        [SerializeField] private TMP_Text[] cardDescriptions;
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

        /// <summary>统一应用本页的设计尺寸、安全区和触控文字大小。</summary>
        /// <param name="safeArea">以宿主左下角为原点的归一化安全区。</param>
        /// <param name="touch">触屏增大卡片文字。</param>
        public void ApplyLayout(Rect safeArea, bool touch)
        {
            if (content == null) return;
            var size = ((RectTransform)transform).rect.size;
            float scale = Mathf.Min(size.x * safeArea.width / 1920f, size.y * safeArea.height / 1080f);
            if (scale <= 0) return;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            content.anchoredPosition = Vector2.Scale(safeArea.center - new Vector2(.5f, .5f), size);
            content.sizeDelta = new Vector2(1920, 1080);
            content.localScale = Vector3.one * scale;
            foreach (var label in cardTitles) label.fontSize = touch ? 30 : 26;
            foreach (var label in cardDescriptions) label.fontSize = touch ? 25 : 21;
        }
    }
}
