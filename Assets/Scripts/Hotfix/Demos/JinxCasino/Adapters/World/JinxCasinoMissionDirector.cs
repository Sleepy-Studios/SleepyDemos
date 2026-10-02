using System;
using System.Collections.Generic;
using System.Linq;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 将领域任务装配为可接近的场景目标；碰撞只提交交互，发奖仍由聚合处理。
    public sealed class JinxCasinoMissionDirector : MonoBehaviour
    {
        [SerializeField] private JinxCasinoController owner;
        [SerializeField] private JinxCasinoMissionTarget targetPrefab;
        [SerializeField] private JinxCasinoSceneEffects sceneEffects;
        [SerializeField] private CasinoMissionPrefabBinding[] templates = Array.Empty<CasinoMissionPrefabBinding>();
        private readonly List<JinxCasinoMissionTarget> targets = new List<JinxCasinoMissionTarget>();
        private string missionId;

        /// <summary>绑定保存的任务目标模板。</summary>
        /// <param name="controller">当前场景的唯一内容宿主。</param>
        /// <param name="prefab">带Trigger的保存目标，不在运行时新建模型或控件。</param>
        /// <param name="effects">当前场景的雷达接收器。</param>
        /// <param name="bindings">按事件及交互点区分的保存模板；空值保留P1筹码模板。</param>
        public void Configure(JinxCasinoController controller, JinxCasinoMissionTarget prefab, JinxCasinoSceneEffects effects = null, CasinoMissionPrefabBinding[] bindings = null)
        {
            owner = controller; targetPrefab = prefab; sceneEffects = effects; templates = bindings ?? Array.Empty<CasinoMissionPrefabBinding>();
        }

        private void OnEnable() { if (owner != null) owner.Changed += Refresh; }
        private void OnDisable() { if (owner != null) owner.Changed -= Refresh; ClearTargets(); }

        private void Refresh()
        {
            var state = owner.AdventureState;
            var mission = state?.ActiveMission;
            if (mission == null || string.IsNullOrEmpty(mission.Id) || mission.Completed || mission.Failed)
            { ClearTargets(); return; }
            // 同ID较早存档会回退Visited/Carrying，旧目标实例的submitted不能跨恢复沿用。
            if (missionId != mission.Id || owner.IsAdventureRestoreInProgress)
            {
                ClearTargets(); missionId = mission.Id;
                var origin = owner.CurrentAdventureSafePosition + new Vector3(0, 0.7f, 2);
                int count = mission.EventId == "gold_delivery" ? 2 : mission.TargetCount;
                for (int index = 0; index < count; index++)
                {
                    float angle = index * Mathf.PI * 2 / Mathf.Max(1, count);
                    var position = origin + new Vector3(Mathf.Cos(angle) * 3, 0, Mathf.Sin(angle) * 2);
                    var binding = Array.Find(templates, candidate => candidate != null && candidate.EventId == mission.EventId && (candidate.PointIndex < 0 || candidate.PointIndex == index));
                    var target = Instantiate(binding?.Prefab != null ? binding.Prefab : targetPrefab, position, Quaternion.identity, transform);
                    target.Configure(owner, mission.Id, mission.EventId, index);
                    target.gameObject.SetActive(true); targets.Add(target);
                }
            }
            for (int index = 0; index < targets.Count; index++)
            {
                bool available = !mission.Visited.Contains(index);
                if (mission.EventId == "gold_delivery") available = mission.Carrying ? index == 1 : index == 0;
                if (mission.EventId == "mascot_chase") available = index == mission.Progress;
                targets[index].gameObject.SetActive(available);
            }
            sceneEffects?.SetMissionMarkers(targets.Where(target => target != null && target.gameObject.activeInHierarchy).Select(target => target.transform).ToArray());
        }

        private void ClearTargets()
        {
            foreach (var target in targets) if (target != null) { target.gameObject.SetActive(false); Destroy(target.gameObject); }
            targets.Clear(); missionId = null;
            sceneEffects?.SetMissionMarkers(Array.Empty<Transform>());
        }
    }

    [Serializable]
    public sealed class CasinoMissionPrefabBinding
    {
        public string EventId;
        public int PointIndex = -1;
        public JinxCasinoMissionTarget Prefab;
    }
}
