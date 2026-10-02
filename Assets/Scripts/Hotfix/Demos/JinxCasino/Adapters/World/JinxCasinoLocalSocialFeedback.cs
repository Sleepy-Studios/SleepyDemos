using System;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 六句固定本地消息；未来传输可使用同一稳定ID，当前不会假称发给网络队友。
    public static class CasinoLocalMessages
    {
        private static readonly string[] ids = { "gather", "need_help", "my_turn", "your_turn", "stop_bet", "thanks" };
        private static readonly string[] labels = { "来这里集合", "我需要帮忙", "这一把我来", "轮到你了", "先别下注", "谢谢，干得漂亮" };
        public static string[] Ids => (string[])ids.Clone();
        public static string[] Labels => (string[])labels.Clone();
    }

    /// 只消费本地社交操作；世界标记复用保存Prefab，没有资金或网络副作用。
    public sealed class JinxCasinoLocalSocialFeedback : MonoBehaviour
    {
        [SerializeField] private JinxCasinoController owner;
        [SerializeField] private GameObject markerPrefab;
        private GameObject marker;
        private JinxCasinoStation markedStation;
        private string runId;
        private int stageIndex = -1;
        private float nextActionAt;
        private float messageUntil;
        private string message;
        private bool bound;
        public string Status { get; private set; } = "单人提示，仅自己可见。";
        public string VisibleMessage => Time.unscaledTime < messageUntil ? message ?? string.Empty : string.Empty;
        public Transform Marker => marker != null ? marker.transform : null;
        public JinxCasinoStation MarkedStation => markedStation;
        public event Action Changed;

        /// <summary>保存场景消费者及地图雷达模型制成的无物理标记Prefab。</summary>
        /// <param name="controller">唯一赌场宿主，绑定其新局/恢复/退出状态。</param>
        /// <param name="prefab">无Collider、Camera、AudioListener的保存模型。</param>
        public void Setup(JinxCasinoController controller, GameObject prefab)
        {
            Unbind(); Clear(); owner = controller; markerPrefab = prefab;
            if (Application.isPlaying && isActiveAndEnabled) Bind();
        }

        /// <summary>展示一条本地固定消息；有效操作间隔至少一秒。</summary>
        /// <param name="id">CasinoLocalMessages.Ids中的稳定ID。</param>
        /// <returns>确实在本机展示时为true。</returns>
        public bool Send(string id)
        {
            int index = Array.IndexOf(CasinoLocalMessages.Ids, id);
            if (owner == null || !owner.Game.HasAdventure || owner.Game.State.Phase == CasinoAdventurePhase.Ended || index < 0) return Reject("请开始旅程并选择消息。");
            if (!CanAct()) return false;
            nextActionAt = Time.unscaledTime + 1;
            message = "单人提示：" + CasinoLocalMessages.Labels[index]; messageUntil = Time.unscaledTime + 4;
            Status = message + " · 仅自己可见。"; Changed?.Invoke(); return true;
        }

        /// <summary>标记当前真实附近机台；调用方不能用远端坐标产生假标记。</summary>
        /// <param name="station">宿主按玩家距离找到的可交互机台，没有时为空。</param>
        /// <returns>生成保存标记实例时为true。</returns>
        public bool Mark(JinxCasinoStation station)
        {
            if (owner == null || !owner.Game.HasAdventure || owner.Game.State.Phase == CasinoAdventurePhase.Ended || station == null ||
                !station.isActiveAndEnabled || owner.GetNearbyLocalSocialStation() != station) return Reject("暂无附近机台，请靠近后再标记。");
            if (markerPrefab == null || markerPrefab.GetComponentsInChildren<Collider>(true).Length > 0 ||
                markerPrefab.GetComponentsInChildren<Camera>(true).Length > 0 || markerPrefab.GetComponentsInChildren<AudioListener>(true).Length > 0)
                return Reject("暂时无法显示标记，请稍后再试。");
            if (!CanAct()) return false;
            nextActionAt = Time.unscaledTime + 1; ClearMarker();
            markedStation = station; marker = Instantiate(markerPrefab, transform); marker.SetActive(true); FollowMarker();
            var game = Array.Find(CasinoContentCatalog.Games, definition => definition.Kind == station.Game);
            Status = "已标记：" + (game?.Name ?? "附近机台") + " · 仅自己可见。";
            Changed?.Invoke(); return true;
        }

        /// 用户清除始终允许，避免节流或面板关闭留下无法撤销的标记。
        public void Clear()
        {
            ClearMarker(); message = null; messageUntil = 0; nextActionAt = 0;
            Status = "单人提示，仅自己可见。"; Changed?.Invoke();
        }
        private bool CanAct() => Time.unscaledTime >= nextActionAt || Reject("请稍等一秒再发送或标记。");
        private bool Reject(string description) { Status = description; Changed?.Invoke(); return false; }
        private void FollowMarker()
        {
            if (marker == null || markedStation == null) return;
            // 早期三桌根共用原点，真正模型位置在FormalVisual或Interaction锚点，不能按根错误居中。
            var mount = markedStation.transform.Find("FormalVisual");
            marker.transform.position = (mount != null ? mount.position : markedStation.InteractionPosition) + Vector3.up * 2.2f;
            marker.transform.Rotate(Vector3.up, 45 * Time.unscaledDeltaTime, Space.World);
        }
        private void Update()
        {
            if (markedStation != null && !markedStation.isActiveAndEnabled) ClearMarker();
            FollowMarker();
        }
        private void ClearMarker()
        {
            if (marker != null) { marker.SetActive(false); Destroy(marker); }
            marker = null; markedStation = null;
        }
        private void ObserveRun()
        {
            var state = owner != null ? owner.Game.State : null;
            if (state == null || owner.Game.IsRestoring || state.RunId != runId || state.StageIndex != stageIndex || state.Phase == CasinoAdventurePhase.Ended)
                Clear();
            runId = state?.RunId; stageIndex = state?.StageIndex ?? -1;
        }
        private void Bind()
        { if (bound || owner == null) return; bound = true; owner.Changed += ObserveRun; ObserveRun(); }
        private void Unbind()
        { if (!bound) return; bound = false; if (owner != null) owner.Changed -= ObserveRun; }
        private void OnEnable() { if (Application.isPlaying) Bind(); }
        private void OnDisable() { Unbind(); Clear(); }
        private void OnDestroy() { Unbind(); Clear(); Changed = null; }
    }
}
