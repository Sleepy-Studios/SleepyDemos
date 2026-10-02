using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.Adapters.UI
{
    /// 档案、衣橱与图鉴使用保存控件；解锁和装备由宿主事务提交。
    public sealed class JinxCasinoProfilePresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text summary;
        [SerializeField] private TMP_Text content;
        [SerializeField] private GameObject wardrobeControls;
        [SerializeField] private TMP_Dropdown[] selectors = Array.Empty<TMP_Dropdown>();
        [SerializeField] private Button[] tabs = Array.Empty<Button>();
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backButton;
        private readonly List<Action> removeListeners = new List<Action>();
        private readonly List<string>[] choices = { new List<string>(), new List<string>(), new List<string>(), new List<string>() };
        private JinxCasinoController owner;
        private int page;
        private bool refreshing;

        /// <summary>绑定当前宿主，不保留旧场景订阅。</summary>
        /// <param name="controller">当前Demo宿主。</param>
        /// <param name="close">回到父界面的场地或主菜单。</param>
        public void Bind(JinxCasinoController controller, Action close)
        {
            Unbind(); owner = controller; page = 0; owner.Changed += Refresh;
            Listen(closeButton, close); Listen(backButton, owner.RequestExit);
            for (int index = 0; index < tabs.Length; index++)
            { int selected = index; Listen(tabs[index], () => { page = selected; Refresh(); }); }
            for (int index = 0; index < selectors.Length; index++)
            {
                int selected = index;
                UnityAction<int> handler = value => Select(selected, value);
                selectors[index].onValueChanged.AddListener(handler);
                var selector = selectors[index]; removeListeners.Add(() => { if (selector != null) selector.onValueChanged.RemoveListener(handler); });
            }
            Refresh();
        }

        /// 释放本实例拥有的监听，不修改用户档案。
        public void Unbind()
        {
            if (owner != null) owner.Changed -= Refresh;
            foreach (var remove in removeListeners) remove(); removeListeners.Clear(); owner = null;
        }

        private void OnEnable() => Refresh();
        private void OnDestroy() => Unbind();

        private void Refresh()
        {
            if (owner == null || !gameObject.activeInHierarchy || refreshing) return;
            refreshing = true;
            try
            {
                var data = owner.Game.ProfileData;
                summary.text = data == null ? owner.Game.ProfileStatus : "声望 " + data.Fame + " · 等级 " + owner.Game.ProfileLevel + " · 正式旅程 " + data.FinishedRuns + "\n" + owner.Game.ProfileStatus;
                wardrobeControls.SetActive(page == 1 && data != null);
                if (data == null) { content.text = "档案读取失败时保留已有文件，旅程存档与档案分开。"; return; }
                if (page == 0) content.text = Statistics(data);
                else if (page == 1) { RefreshSelectors(data); content.text = Wardrobe(data); }
                else content.text = Discoveries(data);
            }
            finally { refreshing = false; }
        }

        private void RefreshSelectors(CasinoProfileData data)
        {
            string[] equipped = { data.EquippedColor, data.EquippedHat, data.EquippedEmote, data.EquippedTitle };
            var definitions = CasinoProfileCatalog.Definitions;
            for (int index = 0; index < selectors.Length; index++)
            {
                choices[index].Clear(); var labels = new List<string>();
                foreach (var item in definitions)
                {
                    if ((int)item.Kind != index || !data.UnlockedIds.Contains(item.Id)) continue;
                    choices[index].Add(item.Id); labels.Add(item.Name);
                }
                selectors[index].ClearOptions(); selectors[index].AddOptions(labels);
                selectors[index].SetValueWithoutNotify(Math.Max(0, choices[index].IndexOf(equipped[index])));
            }
        }

        private void Select(int kind, int value)
        {
            if (refreshing || owner == null || kind < 0 || kind >= choices.Length || value < 0 || value >= choices[kind].Count) return;
            string id = choices[kind][value];
            owner.Game.EquipProfile(kind == 0 ? id : null, kind == 1 ? id : null, kind == 2 ? id : null, kind == 3 ? id : null);
            Refresh();
        }

        private static string Statistics(CasinoProfileData data)
        {
            var text = new StringBuilder("你的俱乐部故事\n\n");
            text.Append("确认投入 ").Append(data.SubmittedBets).Append(" 局 · 道具操作 ").Append(data.ItemActions).Append(" 次（含预备）\n")
                .Append("完成区域 ").Append(data.CompletedStages).Append(" · 场地任务 ").Append(data.Tasks).Append(" · 救场 ").Append(data.Rescues).Append("\n")
                .Append("成功整蛊 ").Append(data.Pranks).Append(" · 保护阻挡 ").Append(data.ProtectionBlocks).Append("\n")
                .Append("体面离场 ").Append(data.DignifiedExits).Append(" · 接管 ").Append(data.Takeovers).Append(" · 狼狈撤离 ").Append(data.Withdrawals).Append("\n")
                .Append("离场筹码纪录 ").Append(data.BestEndingCoins).Append("\n\n").Append(CasinoProfileCatalog.FameRule).Append("\n\n最近旅程\n");
            foreach (var run in data.FinishedRunRecords.AsEnumerable().Reverse().Take(8))
                text.Append(EndingName(run.Ending)).Append(" · ").Append(run.CompletedStages).Append("区 · ").Append(run.EndingCoins).Append("筹码 · ").Append(run.Fame).Append("声望\n");
            return text.ToString();
        }

        private static string Wardrobe(CasinoProfileData data)
        {
            var text = new StringBuilder("解锁条件\n\n");
            foreach (var item in CasinoProfileCatalog.Definitions)
                text.Append(data.UnlockedIds.Contains(item.Id) ? "已解锁 · " : "未解锁 · ").Append(item.Name).Append("：").Append(item.Requirement).Append('\n');
            return text.ToString();
        }

        private static string Discoveries(CasinoProfileData data)
        {
            var text = new StringBuilder("机台 ").Append(data.DiscoveredGames.Count).Append("/17\n");
            foreach (var item in CasinoContentCatalog.Games) text.Append(data.DiscoveredGames.Contains(item.Kind) ? "已发现 · " : "未发现 · ").Append(item.Name).Append('\n');
            text.Append("\n道具 ").Append(data.DiscoveredItems.Count).Append("/24\n");
            foreach (var item in CasinoContentCatalog.Items) text.Append(data.DiscoveredItems.Contains(item.Id) ? "已发现 · " : "未发现 · ").Append(item.Name).Append('\n');
            text.Append("\n事件 ").Append(data.DiscoveredEvents.Count).Append("/20\n");
            foreach (var item in CasinoContentCatalog.Events) text.Append(data.DiscoveredEvents.Contains(item.Id) ? "已发现 · " : "未发现 · ").Append(item.Name).Append('\n');
            text.Append("\n结局 ").Append(data.DiscoveredEndings.Count).Append("/3\n");
            foreach (var ending in new[] { CasinoAdventureEnding.Withdraw, CasinoAdventureEnding.LeaveWithDignity, CasinoAdventureEnding.TakeOver })
                text.Append(data.DiscoveredEndings.Contains(ending) ? "已发现 · " : "未发现 · ").Append(EndingName(ending)).Append('\n');
            return text.ToString();
        }

        private static string EndingName(CasinoAdventureEnding ending) => ending == CasinoAdventureEnding.TakeOver ? "接管狂欢城" : ending == CasinoAdventureEnding.LeaveWithDignity ? "体面离场" : "狼狈撤离";
        private void Listen(Button button, Action callback)
        {
            UnityAction handler = () => callback?.Invoke(); button.onClick.AddListener(handler);
            removeListeners.Add(() => { if (button != null) button.onClick.RemoveListener(handler); });
        }
    }
}
