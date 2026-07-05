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

        private readonly UnityEvent _onMouseDown = new();
        private readonly UnityEvent _onMouseMove = new();
        private readonly UnityEvent _onMouseUp = new();
        private readonly UnityEvent _onLongPress = new();

        private bool _isPressed;
        private bool _isDragged;
        private float _pressTime;
        private Vector2 _downPosition;
        private int _activePointerId = -1;

        public bool IsPressed
        {
            get => _isPressed;
            set => _isPressed = value;
        }

        public bool IsDragged
        {
            get => _isDragged;
            set => _isDragged = value;
        }

        public UnityEvent OnMouseDown => _onMouseDown;
        public UnityEvent OnMouseMove => _onMouseMove;
        public UnityEvent OnMouseUp => _onMouseUp;
        public UnityEvent OnLongPress => _onLongPress;

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
            if (eventData == null || _activePointerId != -1 || !CanControlPointer(eventData.pointerId))
            {
                return;
            }

            _activePointerId = eventData.pointerId;
            BindPointer(_activePointerId, this);
            _isPressed = true;
            _isDragged = false;
            _pressTime = Time.time;
            _downPosition = eventData.position;
            MovePosition = eventData.position;
            MoveLocalPosition = GetLocalPosition(eventData.position);
            transform.localScale = originScale * pressedScale;
            _onMouseDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || _activePointerId != eventData.pointerId)
            {
                return;
            }

            UnbindPointer(_activePointerId, this);
            _activePointerId = -1;
            _isPressed = false;
            _isDragged = false;
            transform.localScale = originScale;
            MovePosition = eventData.position;
            MoveLocalPosition = GetLocalPosition(eventData.position);
            _onMouseUp?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData == null || _activePointerId != eventData.pointerId)
            {
                return;
            }

            OnPointerUp(eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!_isPressed || _activePointerId != eventData.pointerId)
            {
                return;
            }

            MovePosition = eventData.position;
            MoveLocalPosition = GetLocalPosition(eventData.position);
            if (!_isDragged)
            {
                if (dragThreshold <= 0f || (eventData.position - _downPosition).sqrMagnitude >= dragThreshold * dragThreshold)
                {
                    _isDragged = true;
                }
                else
                {
                    return;
                }
            }

            _onMouseMove?.Invoke();
        }

        private void Update()
        {
            if (!_isPressed)
            {
                return;
            }

            if (pressDuration > 0f && Time.time - _pressTime >= pressDuration)
            {
                _onLongPress?.Invoke();
                _pressTime = Time.time;
            }
        }

        private void OnDisable()
        {
            if (_activePointerId != -1)
            {
                UnbindPointer(_activePointerId, this);
                _activePointerId = -1;
            }

            if (_isPressed)
            {
                transform.localScale = originScale;
                _onMouseUp?.Invoke();
            }

            _isPressed = false;
            _isDragged = false;
        }

        private void OnDestroy()
        {
            if (_activePointerId != -1)
            {
                UnbindPointer(_activePointerId, this);
                _activePointerId = -1;
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
