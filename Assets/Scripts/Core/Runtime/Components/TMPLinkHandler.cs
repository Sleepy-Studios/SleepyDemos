using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Core.Runtime
{
    /// TextMeshPro UGUI 超链接点击处理器。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class TMPLinkHandler : MonoBehaviour, IPointerClickHandler
    {
        private TextMeshProUGUI tmpText;

        /// 链接点击事件，参数为 TMP linkId。
        public event Action<string> LinkClicked;

        private void Awake() => tmpText = GetComponent<TextMeshProUGUI>();

        /// <summary>检查点击位置对应的 TMP link，并发布 linkId。</summary>
        /// <param name="eventData">当前指针点击数据。</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            tmpText ??= GetComponent<TextMeshProUGUI>();
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(
                tmpText,
                eventData.position,
                eventData.pressEventCamera);
            if (linkIndex < 0) return;

            LinkClicked?.Invoke(tmpText.textInfo.linkInfo[linkIndex].GetLinkID());
        }

        private void OnDestroy()
        {
            LinkClicked = null;
            tmpText = null;
        }
    }
}
