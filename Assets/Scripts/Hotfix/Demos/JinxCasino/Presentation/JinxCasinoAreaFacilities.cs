using System;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Presentation
{
    /// 保存某一区的环境设施引用，独立恢复灯光与捷径，不操作冒险规则。
    public sealed class JinxCasinoAreaFacilities : MonoBehaviour
    {
        [SerializeField, Range(0, 3)] private int areaIndex;
        [SerializeField] private Transform safeSpawn;
        [SerializeField] private Light[] lights = Array.Empty<Light>();
        [SerializeField] private GameObject shortcutGate;
        [SerializeField] private JinxCasinoMovingTable[] tables = Array.Empty<JinxCasinoMovingTable>();
        [SerializeField] private Transform[] radarStations = Array.Empty<Transform>();
        private LightState[] lightStates;
        private bool powerFaulted;
        private bool shortcutOpen;
        private bool originalGateActive;
        private JinxCasinoPresentationClock presentationClock;

        /// <summary>把宿主共享时钟传给保存的移动桌，不改变灯光或门的领域状态。</summary>
        /// <param name="clock">当前Demo表现时钟；null保持旧原型时间。</param>
        public void BindPresentationClock(JinxCasinoPresentationClock clock)
        {
            presentationClock = clock;
            foreach (var table in tables) if (table != null) table.BindPresentationClock(clock);
        }

        public int AreaIndex => areaIndex;
        public Transform SafeSpawn => safeSpawn;
        public Transform[] RadarStations => radarStations == null ? Array.Empty<Transform>() : (Transform[])radarStations.Clone();
        public bool IsPowerFaulted => powerFaulted;
        public bool IsShortcutOpen => shortcutOpen;

        /// <summary>绑定本区真实设施，安全点始终位于已验证可站立区域。</summary>
        /// <param name="index">0..3的区域索引。</param>
        /// <param name="spawn">本区安全集合点。</param>
        /// <param name="managedLights">只属于本区的灯光，不能把全场景主光交给多个区域。</param>
        /// <param name="gate">只阻挡本区内部捷径的门，不是跨区解锁门。</param>
        /// <param name="movingTables">本区沿安全短轨道移动的桌。</param>
        /// <param name="stations">当前区可雷达标记的机台锚点。</param>
        public void Configure(int index, Transform spawn, Light[] managedLights, GameObject gate, JinxCasinoMovingTable[] movingTables, Transform[] stations)
        {
            ClearRuntimeState(); areaIndex = Mathf.Clamp(index, 0, 3); safeSpawn = spawn;
            lights = managedLights ?? Array.Empty<Light>(); shortcutGate = gate;
            tables = movingTables ?? Array.Empty<JinxCasinoMovingTable>(); radarStations = stations ?? Array.Empty<Transform>();
            BindPresentationClock(presentationClock);
        }

        /// <summary>同步停电任务；首次停电保存原状态，结束精确恢复，不永久改灯光资产。</summary>
        /// <param name="faulted">任务未完成为true，结束、超时或离区为false。</param>
        public void SetPowerFaulted(bool faulted)
        {
            if (faulted == powerFaulted) return;
            if (faulted)
            {
                lightStates = new LightState[lights.Length];
                for (int index = 0; index < lights.Length; index++)
                {
                    var light = lights[index]; if (light == null) continue;
                    lightStates[index] = new LightState { Light = light, Intensity = light.intensity, Enabled = light.enabled };
                    light.intensity *= 0.15f;
                }
            }
            else if (lightStates != null)
                foreach (var saved in lightStates) if (saved?.Light != null) { saved.Light.intensity = saved.Intensity; saved.Light.enabled = saved.Enabled; }
            powerFaulted = faulted;
            if (!faulted) lightStates = null;
        }

        /// <summary>开启或恢复本区捷径门，永不改变跨区进度。</summary>
        /// <param name="open">当前完整StageIndex内已使用钥匙时为true。</param>
        public void SetShortcutOpen(bool open)
        {
            if (open == shortcutOpen) return;
            if (shortcutGate != null)
            {
                if (open) { originalGateActive = shortcutGate.activeSelf; shortcutGate.SetActive(false); }
                else shortcutGate.SetActive(originalGateActive);
            }
            shortcutOpen = open;
        }

        /// <summary>只移动当前区的空闲桌，不影响正在操作的机台。</summary>
        /// <param name="duration">领域提供的最多三秒演出时间。</param>
        /// <param name="activeGame">当前已提交机台类型；无活动局传null。</param>
        /// <param name="layers">物理碰撞与地面校验层。</param>
        public void MoveIdleTables(float duration, CasinoGameKind? activeGame, int layers)
        {
            foreach (var table in tables)
                if (table != null && table.gameObject.activeInHierarchy && (!activeGame.HasValue || table.Game != activeGame.Value)) table.BeginMotion(duration, layers);
        }

        /// <summary>同步已提交局，正在操作的桌体冻结，不能继续被环境机关移动。</summary>
        /// <param name="activeGame">本区正在操作的类型，无活动局为null。</param>
        public void SetOccupiedGame(CasinoGameKind? activeGame)
        {
            if (tables != null) foreach (var table in tables) if (table != null) table.SetOccupied(activeGame.HasValue && table.Game == activeGame.Value);
        }

        /// 恢复所有本区环境变化；桌体遇角色占位时会安全等待归位。
        public void ClearRuntimeState()
        {
            SetPowerFaulted(false); SetShortcutOpen(false);
            if (tables != null) foreach (var table in tables) if (table != null) { table.SetOccupied(false); table.EndMotion(); }
        }

        private void OnDisable() => ClearRuntimeState();
        private sealed class LightState { public Light Light; public float Intensity; public bool Enabled; }
    }
}
