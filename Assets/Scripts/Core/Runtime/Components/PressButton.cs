using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Linq;
using System.Collections.Generic;

namespace Core.Runtime
{
    public sealed class PressButton : MonoBehaviour, IPointerDownHandler, IPointerMoveHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float pressedScale = 0.95f;
        [Tooltip("长按触发时间（秒）")]
        public float pressDuration = 0.4f;
        [Tooltip("拖动触发阈值（像素），过滤点击抖动")]
        public float dragThreshold = 10f;
        private Vector3 originScale;
        private static readonly Dictionary<int, PressButton> ActivePointers = new();
        private readonly Dictionary<int, PressButton> boundPointers = new();

        private readonly UnityEvent onMouseDown = new();
        private readonly UnityEvent onMouseMove = new();
        private readonly UnityEvent onMouseUp = new();
        private readonly UnityEvent onLongPress = new();

        private bool isPressed;
        private bool isDragged;
        private float pressTime;
        private Vector2 downPosition;
        private int activePointerId = int.MinValue;

        public bool IsPressed
        {
            get => isPressed;
            set => isPressed = value;
        }

        public bool IsDragged
        {
            get => isDragged;
            set => isDragged = value;
        }

        public UnityEvent OnMouseDown => onMouseDown;
        public UnityEvent OnMouseMove => onMouseMove;
        public UnityEvent OnMouseUp => onMouseUp;
        public UnityEvent OnLongPress => onLongPress;

        public Vector2 MovePosition { get; private set; }
        public Vector2 MoveLocalPosition { get; private set; }

        public float PressDuration
        {
            get => pressDuration;
            set => pressDuration = Mathf.Max(0f, value);
        }

        public float DragThreshold
        {
            get => dragThreshold;
            set => dragThreshold = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            originScale = transform.localScale;
        }

        private void OnEnable()
        {
            transform.localScale = originScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || activePointerId != int.MinValue || !CanControlPointer(eventData.pointerId))
            {
                return;
            }

            activePointerId = eventData.pointerId;
            BindPointer(activePointerId, this);
            isPressed = true;
            isDragged = false;
            pressTime = Time.time;
            downPosition = eventData.position;
            MovePosition = eventData.position;
            MoveLocalPosition = GetLocalPosition(eventData.position);
            transform.localScale = Core.Runtime.Inputs.InputDeviceState.ActiveKind == Core.Runtime.Inputs.InputDeviceKind.Touch
                ? originScale : originScale * pressedScale;
            onMouseDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || activePointerId != eventData.pointerId)
            {
                return;
            }

            UnbindPointer(activePointerId, this);
            activePointerId = int.MinValue;
            isPressed = false;
            isDragged = false;
            transform.localScale = originScale;
            MovePosition = eventData.position;
            MoveLocalPosition = GetLocalPosition(eventData.position);
            onMouseUp?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData == null || activePointerId != eventData.pointerId)
            {
                return;
            }

            OnPointerUp(eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!isPressed || activePointerId != eventData.pointerId)
            {
                return;
            }

            MovePosition = eventData.position;
            MoveLocalPosition = GetLocalPosition(eventData.position);
            if (!isDragged)
            {
                if (dragThreshold <= 0f || (eventData.position - downPosition).sqrMagnitude >= dragThreshold * dragThreshold)
                {
                    isDragged = true;
                }
                else
                {
                    return;
                }
            }

            onMouseMove?.Invoke();
        }

        private void Update()
        {
            if (!isPressed)
            {
                return;
            }

            if (pressDuration > 0f && Time.time - pressTime >= pressDuration)
            {
                onLongPress?.Invoke();
                pressTime = Time.time;
            }
        }

        private void OnDisable()
        {
            if (activePointerId != int.MinValue)
            {
                UnbindPointer(activePointerId, this);
                activePointerId = int.MinValue;
            }

            if (isPressed)
            {
                transform.localScale = originScale;
                onMouseUp?.Invoke();
            }

            isPressed = false;
            isDragged = false;
        }

        private void OnDestroy()
        {
            if (activePointerId != int.MinValue)
            {
                UnbindPointer(activePointerId, this);
                activePointerId = int.MinValue;
            }
            if (boundPointers.Count <= 0)
            {
                return;
            }

            var pointerIds = boundPointers.Keys.ToArray();
            foreach (var pointerId in pointerIds)
            {
                UnbindPointer(pointerId, this);
            }
        }

        private Vector2 GetLocalPosition(Vector2 screenPosition)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gameObject.transform as RectTransform,
                screenPosition,
                null,
                out localPoint);
            return localPoint;
        }

        private bool CanControlPointer(int pointerId)
        {
            if (ActivePointers.TryGetValue(pointerId, out var owner))
            {
                return owner == this;
            }

            return true;
        }

        private void BindPointer(int pointerId, PressButton button)
        {
            boundPointers.TryAdd(pointerId, button);
            ActivePointers.TryAdd(pointerId, button);
        }

        private void UnbindPointer(int pointerId, PressButton button)
        {
            if (boundPointers.ContainsKey(pointerId) && boundPointers[pointerId] == button)
            {
                boundPointers.Remove(pointerId);
            }

            if (ActivePointers.ContainsKey(pointerId) && ActivePointers[pointerId] == button)
            {
                ActivePointers.Remove(pointerId);
            }
        }
    }
}
