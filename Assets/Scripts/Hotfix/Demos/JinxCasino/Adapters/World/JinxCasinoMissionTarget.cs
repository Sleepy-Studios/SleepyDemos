using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 独立任务触发目标；只接受当前本地玩家进入，助手碰撞不能代领奖励。
    public sealed class JinxCasinoMissionTarget : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        private JinxCasinoController owner;
        private string missionId;
        private string eventId;
        private int index;
        private bool submitted;
        private Vector3 homePosition;
        private float startedAt;

        /// <summary>给实例绑定当前任务的稳定身份。</summary>
        /// <param name="controller">验证玩家身份和距离的宿主。</param>
        /// <param name="id">当前任务ID。</param>
        /// <param name="kind">事件稳定ID，决定提示和动作。</param>
        /// <param name="pointIndex">唯一交互点序号。</param>
        public void Configure(JinxCasinoController controller, string id, string kind, int pointIndex)
        {
            owner = controller; missionId = id; eventId = kind; index = pointIndex; submitted = false;
            homePosition = transform.position; startedAt = Time.unscaledTime;
            if (label != null) label.text = kind == "chip_rain" ? "筹码 " + (index + 1) : kind == "gold_delivery" ? (index == 0 ? "搬起金箱" : "交付金箱")
                : kind == "mascot_chase" ? "吉祥物 " + (index + 1) : kind == "power_relay" ? "送电 " + (index + 1) : kind == "power_repair" ? "抢修 " + (index + 1) : "同步按钮";
        }

        private void Update()
        {
            if (eventId != "mascot_chase" || submitted) return;
            // 当前区前庭内的固定短巡游路线上追逐；移动只表现，不由物理决定奖励或随机结果。
            float phase = (Time.unscaledTime - startedAt) * 0.7f;
            transform.position = homePosition + new Vector3(Mathf.Sin(phase) * 1.4f, 0, Mathf.Cos(phase) * 0.7f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (submitted || owner == null || !owner.IsLocalAdventureActor(other.transform)) return;
            var mission = owner.Game.State?.ActiveMission;
            if (mission == null || mission.Id != missionId) return;
            CasinoTaskAction action = mission.Action;
            if (eventId == "gold_delivery") action = index == 0 ? CasinoTaskAction.Carry : CasinoTaskAction.Deliver;
            var result = owner.InteractWithMission(missionId, action, index, transform.position);
            if (result.Success) { submitted = true; gameObject.SetActive(false); }
        }
    }
}
