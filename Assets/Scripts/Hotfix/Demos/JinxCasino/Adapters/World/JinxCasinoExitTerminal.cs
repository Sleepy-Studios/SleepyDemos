using System;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    public enum JinxCasinoExitAction { Verify = 0, Leave = 1 }

    /// 入口的检票与离场物件；只显示原领域状态，不支付、开奖或改变区域碰撞。
    public sealed class JinxCasinoExitTerminal : MonoBehaviour
    {
        [SerializeField] private JinxCasinoController owner;
        [SerializeField] private string terminalId;
        [SerializeField] private JinxCasinoExitAction action;
        [SerializeField] private Transform approach;
        [SerializeField] private Transform control;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private TMP_Text status;
        public string TerminalId => terminalId;
        public JinxCasinoExitAction Action => action;
        public Vector3 InteractionPosition => approach != null ? approach.position : transform.position;
        public Vector3 ControlPosition => control != null ? control.position : transform.position;

        /// <summary>保存入口引用，运行时不寻找或创建界面、相机和材质。</summary>
        /// <param name="controller">当前单机场景宿主。</param>
        /// <param name="id">固定物件ID，与机台ID分离。</param>
        /// <param name="command">验票或明确离场；不是小游戏类型。</param>
        /// <param name="nearby">位于大厅内部的实际接近位置。</param>
        /// <param name="button">物件操作中心，有自身或子节点Collider。</param>
        /// <param name="label">短动作铭牌。</param>
        /// <param name="description">两行状态铭牌。</param>
        public void Configure(JinxCasinoController controller, string id, JinxCasinoExitAction command, Transform nearby,
            Transform button, TMP_Text label, TMP_Text description)
        {
            if (controller == null || string.IsNullOrWhiteSpace(id) || !Enum.IsDefined(typeof(JinxCasinoExitAction), command) ||
                nearby == null || !nearby.IsChildOf(transform) || button == null || !button.IsChildOf(transform) || label == null || description == null)
                throw new ArgumentException("检票物件需要保存宿主、ID、操作中心和两块铭牌。");
            Unbind(); owner = controller; terminalId = id; action = command; approach = nearby;
            control = button; caption = label; status = description;
            if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void Bind()
        {
            if (owner == null) return;
            owner.Changed -= Refresh; owner.ImmersionInputChanged -= Refresh;
            owner.Changed += Refresh; owner.ImmersionInputChanged += Refresh; Refresh();
        }
        private void Unbind()
        {
            if (owner == null) return;
            owner.Changed -= Refresh; owner.ImmersionInputChanged -= Refresh;
        }
        private void OnDisable() => Unbind();
        private void Refresh()
        {
            if (caption == null || status == null || owner == null) return;
            var state = owner.AdventureState;
            if (state == null || state.Mode != CasinoAdventureMode.Standard || state.Config.StageCount != 1)
            { caption.text = action == JinxCasinoExitAction.Verify ? "验票口" : "离场口"; status.text = "标准冒险结束后\n在这里离场"; return; }
            if (state.Phase == CasinoAdventurePhase.Ended)
            { caption.text = "返回大厅"; status.text = state.Ending == CasinoAdventureEnding.LeaveWithDignity ? "见好就收\n体面离场" : "带着剩余筹码\n下次再来"; return; }
            if (owner.HasActiveAdventureRound)
            { caption.text = action == JinxCasinoExitAction.Verify ? "验票口" : "离场口"; status.text = "先回原机台\n完成这一局"; return; }
            if (action == JinxCasinoExitAction.Verify)
            {
                caption.text = state.Phase == CasinoAdventurePhase.Finale ? "验票通过" : "核验筹码";
                status.text = state.Phase == CasinoAdventurePhase.Finale ? "前往另一侧离场口\n领取离场券" :
                    state.Phase == CasinoAdventurePhase.Failed ? "时间已到\n前往离场口" : "持有 " + state.Coins + " / " + owner.AdventureTarget + "\n达标后在此核验";
            }
            else
            {
                caption.text = owner.IsExitWithdrawalArmed(this) ? "确认撤离" : state.Phase == CasinoAdventurePhase.Finale ? "领取离场券" : "提前离场";
                status.text = owner.IsExitWithdrawalArmed(this) ? "再次交互确认\n本次旅程将结束" :
                    state.Phase == CasinoAdventurePhase.Finale ? "核验已通过\n带着筹码离开" : "保留剩余筹码\n结束本次旅程";
            }
        }
    }
}
