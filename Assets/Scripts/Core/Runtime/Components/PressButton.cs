using UnityEngine;
using UnityEngine.EventSystems;

namespace Core.Runtime
{
    public sealed class PressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float pressedScale = 0.95f;
        private Vector3 originScale;

        private void Awake()
        {
            originScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.localScale = originScale * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = originScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = originScale;
        }
    }
}
