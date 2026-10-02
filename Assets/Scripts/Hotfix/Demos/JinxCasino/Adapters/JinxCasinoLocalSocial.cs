using System.Linq;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    public sealed partial class JinxCasinoController
    {
        public string LocalSocialStatus => GetComponent<JinxCasinoLocalSocialFeedback>()?.Status ?? "暂时无法使用快捷交流。";
        public string LocalSocialMessage => GetComponent<JinxCasinoLocalSocialFeedback>()?.VisibleMessage ?? string.Empty;

        /// <summary>仅本机显示固定快捷消息，不触发钱包或伪装网络回执。</summary>
        /// <param name="id">六种固定消息的稳定ID。</param>
        /// <returns>本地消费者实际展示时为true。</returns>
        public bool SendLocalMessage(string id) => GetComponent<JinxCasinoLocalSocialFeedback>()?.Send(id) ?? false;
        /// 标记真实三米内机台，即使交流模态打开也能读取当前位置，无法找到时明确拒绝。
        public bool MarkNearbyStation() => GetComponent<JinxCasinoLocalSocialFeedback>()?.Mark(FindNearbyStation()) ?? false;
        /// 用户明确清除、界面释放、新局和恢复均清理本地标记。
        public void ClearLocalSocialFeedback() => GetComponent<JinxCasinoLocalSocialFeedback>()?.Clear();

        internal JinxCasinoStation GetNearbyLocalSocialStation() => FindNearbyStation();

        private JinxCasinoStation FindNearbyStation()
        {
            if (body == null) return null;
            return FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None)
                .Where(station => station.isActiveAndEnabled && (station.InteractionPosition - body.transform.position).sqrMagnitude <= 9)
                .OrderBy(station => (station.InteractionPosition - body.transform.position).sqrMagnitude).FirstOrDefault();
        }
    }
}
