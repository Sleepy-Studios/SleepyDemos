using Hotfix.JinxCasino.Presentation;
using System;
using UnityEngine;

namespace Hotfix.JinxCasino.Interaction
{
    /// 机台物件提交的语义操作；金额及结果仍由规则验证。
    public enum JinxCasinoTableAction { ChipAdd, ChipClear, Commit, Primary, Secondary, Help, SelectProduct, PurchaseProduct, UseProduct }

    /// 独立实体操作点；碰撞命中和手柄焦点共用相同目标。
    public sealed class JinxCasinoTableTarget : MonoBehaviour
    {
        private static readonly Color FocusedFeedbackColor = new Color(0.65f, 1f, 0.88f);
        [SerializeField] private string targetId;
        [SerializeField] private JinxCasinoTableAction action;
        [SerializeField] private int value;
        [SerializeField] private int navigationOrder;
        [SerializeField] private Renderer[] feedbackRenderers = Array.Empty<Renderer>();
        [SerializeField] private Transform pressVisual;
        private bool isAvailable = true;
        private bool isFocused;
        private Vector3 restPosition;
        private float pressRemaining;
        private JinxCasinoPresentationClock presentationClock;

        /// <summary>绑定所属Demo的按压反馈时间；暂停不清除已经按下的视觉。</summary>
        /// <param name="clock">机台宿主共享时钟；null保持旧原型时间。</param>
        public void BindPresentationClock(JinxCasinoPresentationClock clock) => presentationClock = clock;
        private MaterialPropertyBlock feedback;
        private MaterialPropertyBlock[] originalBlocks;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// 稳定的机台内目标标识。
        public string TargetId => targetId;
        /// 设备无关的玩法动作。
        public JinxCasinoTableAction Action => action;
        /// 面额、玩家索引等由动作解释的参数。
        public int Value => value;
        /// 手柄导航顺序；同序号按机台保存顺序决定。
        public int NavigationOrder => navigationOrder;
        /// 当前允许交互且位于激活层级中。
        public bool IsAvailable => isAvailable && isActiveAndEnabled;
        /// 不可用原因，供机台附近提示展示。
        public string UnavailableReason { get; private set; }
        /// 通过目标验证后触发一次请求，不直接操作资金。
        public event Action<JinxCasinoTableTarget> Invoked;

        /// <summary>装配实体目标；只能在未开始交互时调用，保留既有材质属性块。</summary>
        /// <param name="id">机台内唯一标识。</param>
        /// <param name="command">设备无关操作。</param>
        /// <param name="parameter">动作对应的整数参数。</param>
        /// <param name="order">手柄焦点顺序。</param>
        /// <param name="renderers">高亮渲染器，不创建材质实例。</param>
        /// <param name="visual">按下时小幅移动的视觉节点，不移动Collider。</param>
        public void Configure(string id, JinxCasinoTableAction command, int parameter, int order,
            Renderer[] renderers, Transform visual)
        {
            RestoreFeedback();
            targetId = id; action = command; value = parameter; navigationOrder = order;
            feedbackRenderers = renderers ?? Array.Empty<Renderer>(); pressVisual = visual;
            CacheFeedback();
        }

        private void Awake() => CacheFeedback();

        private void CacheFeedback()
        {
            if (pressVisual != null) restPosition = pressVisual.localPosition;
            feedback ??= new MaterialPropertyBlock();
            originalBlocks = new MaterialPropertyBlock[feedbackRenderers.Length];
            for (int i = 0; i < feedbackRenderers.Length; i++)
            {
                originalBlocks[i] = new MaterialPropertyBlock();
                if (feedbackRenderers[i] != null) feedbackRenderers[i].GetPropertyBlock(originalBlocks[i]);
            }
        }

        /// <summary>更新目标的业务可操作性；禁用时立即清除焦点。</summary>
        /// <param name="available">是否接受请求。</param>
        /// <param name="reason">不能操作时的简短中文原因。</param>
        public void SetAvailable(bool available, string reason = null)
        {
            isAvailable = available;
            UnavailableReason = available ? null : reason;
            if (!available) SetFocused(false);
        }

        /// <summary>更新实体高亮；不可用物件不接受焦点。</summary>
        /// <param name="focused">鼠标、触屏或手柄当前是否指向此物件。</param>
        public void SetFocused(bool focused)
        {
            isFocused = focused && IsAvailable;
            if (!isFocused) { RestoreFeedback(); return; }
            if (originalBlocks == null) CacheFeedback();
            for (int i = 0; i < feedbackRenderers.Length; i++)
            {
                var renderer = feedbackRenderers[i];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(feedback);
                feedback.SetColor(BaseColor, FocusedFeedbackColor);
                renderer.SetPropertyBlock(feedback);
            }
        }

        /// 请求一次实体操作；未启用时无副作用，资金校验由订阅的规则适配完成。
        public bool TryInvoke()
        {
            if (!IsAvailable || (presentationClock?.IsPaused ?? false)) return false;
            pressRemaining = 0.12f;
            Invoked?.Invoke(this);
            return true;
        }

        private void Update()
        {
            if (pressRemaining <= 0 || (presentationClock?.IsPaused ?? false)) return;
            pressRemaining = Mathf.Max(0, pressRemaining - (presentationClock?.GetDeltaSeconds(Time.frameCount) ?? Time.unscaledDeltaTime));
            if (pressVisual != null)
                pressVisual.localPosition = restPosition + Vector3.down * (pressRemaining > 0 ? 0.008f : 0);
        }

        private void RestoreFeedback()
        {
            if (originalBlocks == null) return;
            for (int i = 0; i < feedbackRenderers.Length && i < originalBlocks.Length; i++)
                if (feedbackRenderers[i] != null) feedbackRenderers[i].SetPropertyBlock(originalBlocks[i]);
        }

        private void OnDisable()
        {
            isFocused = false; pressRemaining = 0;
            if (pressVisual != null) pressVisual.localPosition = restPosition;
            RestoreFeedback();
        }
    }
}
