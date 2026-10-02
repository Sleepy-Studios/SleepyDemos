using System;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// S1只消费已公开桌面副本；运行时不寻找Controller，不改变规则/钱包或构造视觉资源。
    public abstract class JinxCasinoS1Presentation : MonoBehaviour
    {
        [SerializeField] private string stationId;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text rulesText;
        [SerializeField] private TMP_Text resultText;
        private string runId;
        private int settlementSequence;
        private bool hasBaseline;
        private bool paused;
        private bool skipNextTick = true;
        protected JinxCasinoTableView Latest { get; private set; }
        protected bool IsPaused => paused;
        protected abstract CasinoGameKind Game { get; }
        /// 主机用于短暂输入互斥；暂停不会把正在进行的演出变成结束。
        public abstract bool IsAnimating { get; }
        public string StationId => stationId;
        /// 当前已静态呈现或完成演出的结算序号；无结果或仍演出返回0，Restore无需再次付款即可被观察。
        public int PresentedSettlementSequence => !IsAnimating && Latest?.Presentation?.IsComplete == true ? Latest.SettlementSequence : 0;

        protected void ConfigureLabels(string id, TMP_Text amount, TMP_Text rules, TMP_Text result)
        { stationId = id; amountText = amount; rulesText = rules; resultText = result; CacheBindings(); }

        /// <summary>显式开始新Run以建立结算去重基线；同ID重复通知无副作用。</summary>
        /// <param name="id">宿主已保存的RunId，不由表现层生成。</param>
        public void BeginRun(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("表现需要宿主RunId。", nameof(id));
            if (runId == id) return;
            runId = id; settlementSequence = 0; hasBaseline = true; Latest = null;
            ResetVisual(); SetResult(string.Empty); SetText(amountText, "筹码 0");
        }

        /// <summary>更新公开视图和同一暂停标志；null只保留桌面，不清牌或取消活动局。</summary>
        /// <param name="view">属于本具体Station的公开副本。</param>
        /// <param name="isPaused">宿主全局暂停，必须在暂停变化时立即通知所有机台。</param>
        public void Present(JinxCasinoTableView view, bool isPaused)
        {
            if (paused != isPaused) skipNextTick = true;
            paused = isPaused;
            if (view == null) return;
            Validate(view);
            bool first = !hasBaseline;
            bool older = view.SettlementSequence > 0 && view.SettlementSequence < settlementSequence;
            if (older) return; // 较早检查点只允许走显式Restore，不能使下一条正常快照再次演出旧结算。
            bool settled = !first && view.SettlementSequence > settlementSequence;
            if (view.SettlementSequence > 0) settlementSequence = view.SettlementSequence;
            hasBaseline = true; Latest = view; UpdateLabels(view);
            ApplyView(view, settled, first);
        }

        /// <summary>读取/接管较早快照时显式静态恢复，清掉演出队列，不能再次出币或发旧牌。</summary>
        /// <param name="view">宿主恢复后的公开视图。</param>
        public void Restore(JinxCasinoTableView view)
        {
            if (view == null) return;
            Validate(view); Latest = view; settlementSequence = view.SettlementSequence; hasBaseline = true;
            UpdateLabels(view); ApplyView(view, false, true);
        }
        private void Validate(JinxCasinoTableView view)
        {
            if (view.StationId != stationId || view.Game != Game)
                throw new ArgumentException("桌面视图不能串到其它机台：" + stationId, nameof(view));
        }
        private void Awake() => CacheBindings();
        private void LateUpdate()
        {
            if (paused || Latest == null) return;
            if (skipNextTick) { skipNextTick = false; return; }
            // 偶发长帧只延长演出，不把发牌/翻面整个压进一帧；规则时钟仍由宿主独立推进。
            Tick(Mathf.Clamp(Time.unscaledDeltaTime, 0, .08f));
        }
        private void OnDisable()
        {
            if (Latest != null) ApplyView(Latest, false, true);
        }
        private void UpdateLabels(JinxCasinoTableView view)
        {
            var round = view.Presentation;
            SetText(amountText, round?.IsComplete == true ? "投入 " + round.Cost : view.HasOwnActiveRound ? "已投入" :
                "筹码 " + view.DraftStake + (view.IsSlotsPrepared ? " · 已确认" : string.Empty));
            // 专属赔率牌保留完整收益与当前修正，不能截掉投入前必须看见的第二行。
            SetText(rulesText, view.RulesText ?? string.Empty);
        }
        protected void SetResult(string text) => SetText(resultText, text);
        protected static void SetText(TMP_Text label, string text) { if (label != null && label.text != text) label.text = text; }
        protected abstract void CacheBindings();
        protected abstract void ResetVisual();
        protected abstract void ApplyView(JinxCasinoTableView view, bool newSettlement, bool snap);
        protected abstract void Tick(float seconds);
    }
}
