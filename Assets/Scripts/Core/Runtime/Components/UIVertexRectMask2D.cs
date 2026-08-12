using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 通过裁剪子 Graphic 的顶点和 UV 实现硬边矩形遮罩，不修改材质裁剪状态。
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("UI/Effects/UI Vertex Rect Mask 2D")]
    public sealed class UIVertexRectMask2D : UIBehaviour, ICanvasRaycastFilter
    {
        [SerializeField, Tooltip("顺序为 Left、Bottom、Right、Top。")]
        private Vector4 padding;
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private bool autoTrackTransformChanges;

        [NonSerialized] private RectTransform cachedRectTransform;
        [NonSerialized] private List<Graphic> foundGraphics = new List<Graphic>(8);
        [NonSerialized] private List<UIVertexRectClipTarget> targets = new List<UIVertexRectClipTarget>(8);
        [NonSerialized] private List<Matrix4x4> targetMatrices = new List<Matrix4x4>(8);
        [NonSerialized] private Rect lastClipRect;
        [NonSerialized] private bool hierarchyDirty;

        /// 裁剪区域内缩值。
        public Vector4 Padding
        {
            get => padding;
            set
            {
                if (padding == value) return;
                padding = value;
                MarkAllVerticesDirty();
            }
        }

        /// 是否自动跟踪子 Graphic 变换。
        public bool AutoTrackTransformChanges
        {
            get => autoTrackTransformChanges;
            set
            {
                if (autoTrackTransformChanges == value) return;
                autoTrackTransformChanges = value;
                RefreshTargetMatrices();
                MarkAllVerticesDirty();
            }
        }

        /// 当前裁剪节点。
        public RectTransform RectTransform => cachedRectTransform != null
            ? cachedRectTransform
            : cachedRectTransform = (RectTransform)transform;

        /// 本地坐标系中的有效裁剪区域。
        public Rect ClipRect
        {
            get
            {
                Rect rect = RectTransform.rect;
                return Rect.MinMaxRect(
                    rect.xMin + padding.x,
                    rect.yMin + padding.y,
                    rect.xMax - padding.z,
                    rect.yMax - padding.w);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshTargets();
            Canvas.preWillRenderCanvases += OnPreWillRenderCanvases;
        }

        protected override void OnDisable()
        {
            Canvas.preWillRenderCanvases -= OnPreWillRenderCanvases;
            UnregisterTargets();
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            MarkAllVerticesDirty();
        }

        private void OnTransformChildrenChanged() => hierarchyDirty = true;

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            MarkAllVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            hierarchyDirty = true;
            MarkAllVerticesDirty();
        }
#endif

        /// 重新收集全部子 Graphic 并安装内部裁剪修改器。
        [ContextMenu("Refresh Clip Targets")]
        public void RefreshTargets()
        {
            if (!isActiveAndEnabled) return;

            EnsureCollections();
            UnregisterTargets();
            foundGraphics.Clear();
            GetComponentsInChildren(includeInactive, foundGraphics);
            for (int i = 0; i < foundGraphics.Count; i++)
            {
                Graphic childGraphic = foundGraphics[i];
                if (childGraphic == null) continue;

                UIVertexRectClipTarget target = childGraphic.GetComponent<UIVertexRectClipTarget>();
                if (target == null)
                {
                    target = childGraphic.gameObject.AddComponent<UIVertexRectClipTarget>();
                    target.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor;
                }

                target.Register(this);
                targets.Add(target);
                targetMatrices.Add(GetGraphicToClipMatrix(target));
            }

            lastClipRect = ClipRect;
            hierarchyDirty = false;
        }

        /// <summary>判断屏幕坐标是否位于有效裁剪区域。</summary>
        /// <param name="screenPoint">屏幕坐标。</param>
        /// <param name="eventCamera">事件相机。</param>
        /// <returns>组件未启用或坐标位于裁剪区域内时为 true。</returns>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            return !isActiveAndEnabled || RectTransformUtility.RectangleContainsScreenPoint(
                RectTransform,
                screenPoint,
                eventCamera,
                padding);
        }

        internal void MarkAllVerticesDirty()
        {
            EnsureCollections();
            lastClipRect = ClipRect;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null) targets[i].SetVerticesDirty();
            }
        }

        private void OnPreWillRenderCanvases()
        {
            if (!isActiveAndEnabled) return;
            if (hierarchyDirty || targets.Count != targetMatrices.Count)
            {
                RefreshTargets();
                return;
            }

            if (!autoTrackTransformChanges) return;
            Rect clipRect = ClipRect;
            bool clipRectChanged = clipRect != lastClipRect;
            lastClipRect = clipRect;
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                UIVertexRectClipTarget target = targets[i];
                if (target == null)
                {
                    hierarchyDirty = true;
                    continue;
                }

                Matrix4x4 currentMatrix = GetGraphicToClipMatrix(target);
                if (clipRectChanged || !Approximately(currentMatrix, targetMatrices[i]))
                {
                    targetMatrices[i] = currentMatrix;
                    target.SetVerticesDirty();
                }
            }
        }

        private Matrix4x4 GetGraphicToClipMatrix(UIVertexRectClipTarget target)
        {
            return RectTransform.worldToLocalMatrix * target.TargetGraphic.rectTransform.localToWorldMatrix;
        }

        private void RefreshTargetMatrices()
        {
            EnsureCollections();
            if (targets.Count != targetMatrices.Count) return;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null) targetMatrices[i] = GetGraphicToClipMatrix(targets[i]);
            }
        }

        private void UnregisterTargets()
        {
            EnsureCollections();
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null) targets[i].Unregister(this);
            }

            targets.Clear();
            targetMatrices.Clear();
        }

        private void EnsureCollections()
        {
            foundGraphics ??= new List<Graphic>(8);
            targets ??= new List<UIVertexRectClipTarget>(8);
            targetMatrices ??= new List<Matrix4x4>(8);
        }

        private static bool Approximately(Matrix4x4 left, Matrix4x4 right)
        {
            const float epsilon = 0.00001f;
            for (int i = 0; i < 16; i++)
            {
                if (Mathf.Abs(left[i] - right[i]) > epsilon) return false;
            }

            return true;
        }
    }

    /// UIVertexRectMask2D 自动添加的内部顶点修改器，不应手动挂载。
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("")]
    public sealed class UIVertexRectClipTarget : BaseMeshEffect
    {
        [NonSerialized] private List<UIVertexRectMask2D> owners = new List<UIVertexRectMask2D>(1);
        [NonSerialized] private List<UIVertex> vertexStreamA = new List<UIVertex>(16);
        [NonSerialized] private List<UIVertex> vertexStreamB = new List<UIVertex>(16);
        [NonSerialized] private UIVertex[] polygonA = new UIVertex[8];
        [NonSerialized] private UIVertex[] polygonB = new UIVertex[8];

        /// 被修改的 Graphic。
        public Graphic TargetGraphic => graphic;

        internal void Register(UIVertexRectMask2D owner)
        {
            EnsureCollections();
            if (!owners.Contains(owner)) owners.Add(owner);
            enabled = true;
            SetVerticesDirty();
        }

        internal void Unregister(UIVertexRectMask2D owner)
        {
            EnsureCollections();
            owners.Remove(owner);
            SetVerticesDirty();
            if (owners.Count == 0) enabled = false;
        }

        internal void SetVerticesDirty()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            EnsureCollections();
            if (!IsActive() || owners.Count == 0 || vertexHelper.currentVertCount == 0) return;

            vertexStreamA.Clear();
            vertexStreamB.Clear();
            vertexHelper.GetUIVertexStream(vertexStreamA);
            List<UIVertex> source = vertexStreamA;
            List<UIVertex> destination = vertexStreamB;
            for (int ownerIndex = 0; ownerIndex < owners.Count; ownerIndex++)
            {
                UIVertexRectMask2D owner = owners[ownerIndex];
                if (owner == null || !owner.isActiveAndEnabled) continue;
                destination.Clear();
                ClipTriangleStream(source, destination, owner);
                (source, destination) = (destination, source);
                if (source.Count == 0) break;
            }

            vertexHelper.Clear();
            if (source.Count > 0) vertexHelper.AddUIVertexTriangleStream(source);
        }

        private void ClipTriangleStream(List<UIVertex> source, List<UIVertex> destination, UIVertexRectMask2D owner)
        {
            Rect clipRect = owner.ClipRect;
            if (clipRect.width <= 0f || clipRect.height <= 0f) return;

            Matrix4x4 graphicToClip = owner.RectTransform.worldToLocalMatrix * graphic.rectTransform.localToWorldMatrix;
            Matrix4x4 clipToGraphic = default;
            bool hasInverse = false;
            int completeVertexCount = source.Count - source.Count % 3;
            for (int i = 0; i < completeVertexCount; i += 3)
            {
                polygonA[0] = TransformPosition(source[i], graphicToClip);
                polygonA[1] = TransformPosition(source[i + 1], graphicToClip);
                polygonA[2] = TransformPosition(source[i + 2], graphicToClip);
                int code0 = GetOutCode(polygonA[0].position, clipRect);
                int code1 = GetOutCode(polygonA[1].position, clipRect);
                int code2 = GetOutCode(polygonA[2].position, clipRect);
                if ((code0 | code1 | code2) == 0)
                {
                    destination.Add(source[i]);
                    destination.Add(source[i + 1]);
                    destination.Add(source[i + 2]);
                    continue;
                }

                if ((code0 & code1 & code2) != 0) continue;
                int count = 3;
                count = ClipEdge(polygonA, count, polygonB, 0, clipRect.xMin, true);
                count = ClipEdge(polygonB, count, polygonA, 0, clipRect.xMax, false);
                count = ClipEdge(polygonA, count, polygonB, 1, clipRect.yMin, true);
                count = ClipEdge(polygonB, count, polygonA, 1, clipRect.yMax, false);
                if (count < 3) continue;

                if (!hasInverse)
                {
                    clipToGraphic = graphicToClip.inverse;
                    hasInverse = true;
                }

                UIVertex first = TransformPosition(polygonA[0], clipToGraphic);
                for (int vertexIndex = 1; vertexIndex < count - 1; vertexIndex++)
                {
                    destination.Add(first);
                    destination.Add(TransformPosition(polygonA[vertexIndex], clipToGraphic));
                    destination.Add(TransformPosition(polygonA[vertexIndex + 1], clipToGraphic));
                }
            }
        }

        private static int GetOutCode(Vector3 position, Rect clipRect)
        {
            int code = 0;
            if (position.x < clipRect.xMin) code |= 1;
            else if (position.x > clipRect.xMax) code |= 2;
            if (position.y < clipRect.yMin) code |= 4;
            else if (position.y > clipRect.yMax) code |= 8;
            return code;
        }

        private static int ClipEdge(UIVertex[] input, int inputCount, UIVertex[] output, int axis, float boundary, bool keepGreater)
        {
            if (inputCount == 0) return 0;
            int outputCount = 0;
            UIVertex previous = input[inputCount - 1];
            float previousCoordinate = GetCoordinate(previous.position, axis);
            bool previousInside = keepGreater ? previousCoordinate >= boundary : previousCoordinate <= boundary;
            for (int i = 0; i < inputCount; i++)
            {
                UIVertex current = input[i];
                float currentCoordinate = GetCoordinate(current.position, axis);
                bool currentInside = keepGreater ? currentCoordinate >= boundary : currentCoordinate <= boundary;
                if (currentInside != previousInside)
                {
                    float denominator = currentCoordinate - previousCoordinate;
                    float t = Mathf.Abs(denominator) > 0.000001f
                        ? (boundary - previousCoordinate) / denominator
                        : 0f;
                    output[outputCount++] = Lerp(previous, current, t);
                }

                if (currentInside) output[outputCount++] = current;
                previous = current;
                previousCoordinate = currentCoordinate;
                previousInside = currentInside;
            }

            return outputCount;
        }

        private static float GetCoordinate(Vector3 position, int axis) => axis == 0 ? position.x : position.y;

        private static UIVertex TransformPosition(UIVertex vertex, Matrix4x4 matrix)
        {
            vertex.position = matrix.MultiplyPoint3x4(vertex.position);
            return vertex;
        }

        private static UIVertex Lerp(UIVertex from, UIVertex to, float t)
        {
            UIVertex result = from;
            result.position = Vector3.LerpUnclamped(from.position, to.position, t);
            result.normal = Vector3.LerpUnclamped(from.normal, to.normal, t);
            result.tangent = Vector4.LerpUnclamped(from.tangent, to.tangent, t);
            result.color = (Color32)Color.LerpUnclamped(from.color, to.color, t);
            result.uv0 = Vector4.LerpUnclamped(from.uv0, to.uv0, t);
            result.uv1 = Vector4.LerpUnclamped(from.uv1, to.uv1, t);
            result.uv2 = Vector4.LerpUnclamped(from.uv2, to.uv2, t);
            result.uv3 = Vector4.LerpUnclamped(from.uv3, to.uv3, t);
            return result;
        }

        private void EnsureCollections()
        {
            owners ??= new List<UIVertexRectMask2D>(1);
            vertexStreamA ??= new List<UIVertex>(16);
            vertexStreamB ??= new List<UIVertex>(16);
            if (polygonA == null || polygonA.Length < 8) polygonA = new UIVertex[8];
            if (polygonB == null || polygonB.Length < 8) polygonB = new UIVertex[8];
        }
    }
}
