using System;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 机台轮换的实体展位；活动局保留当前外观，结算后才展示新的机台。
    public sealed class JinxCasinoRotationStand : MonoBehaviour
    {
        [SerializeField] private JinxCasinoController owner;
        [SerializeField] private JinxCasinoStation station;
        [SerializeField] private TMP_Text title;
        [SerializeField] private GameObject[] displays = Array.Empty<GameObject>();
        private int lastGame = -1;

        /// <summary>绑定区域内的轮换展位和已保存的模型，不影响普通17个固定机台。</summary>
        /// <param name="controller">场景宿主。</param>
        /// <param name="target">该区域独立交互机台。</param>
        /// <param name="label">展示本次开放规则的招牌。</param>
        /// <param name="models">按CasinoGameKind索引的显示模型，可空；不在运行时构造模型。</param>
        public void Configure(JinxCasinoController controller, JinxCasinoStation target, TMP_Text label, GameObject[] models = null)
        {
            owner = controller; station = target; title = label; displays = models ?? Array.Empty<GameObject>();
        }

        private void OnEnable() { if (owner != null) owner.Changed += Refresh; Refresh(); }
        private void OnDisable() { if (owner != null) owner.Changed -= Refresh; }

        private void Refresh()
        {
            if (owner == null || station == null) return;
            var state = owner.Game.State;
            bool available = state != null && state.StageIndex % 4 == station.AreaIndex && state.GameRotationOffset > 0;
            if (owner.Game.HasActiveRound && lastGame == (int)state.ActiveGame) return;
            station.enabled = available;
            int index = available ? (state.StageIndex * 5 + state.GameRotationOffset) % 17 : -1;
            if (index == lastGame && index >= 0) return;
            lastGame = index;
            if (available) station.Configure((CasinoGameKind)index, station.AreaIndex, station.transform.Find("Interaction"));
            if (title != null) title.text = available ? "轮换展位 · " + Array.Find(CasinoContentCatalog.Games, value => (int)value.Kind == index).Name : "轮换展位 · 等待事件";
            for (int display = 0; display < displays.Length; display++) if (displays[display] != null) displays[display].SetActive(display == index);
        }
    }
}
