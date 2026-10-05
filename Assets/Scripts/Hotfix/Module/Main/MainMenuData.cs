using System.Collections.Generic;
using Core.Runtime;
using Hotfix.SceneManagement;

namespace Hotfix
{
    /// 大厅的入口集合、选中态、导航状态和反馈。
    public sealed class MainMenuData : IData
    {
        public List<IHandler> Handlers { get; } = new() { new MainMenuHandler() };
        public List<MainMenuDemoEntry> Entries { get; } = new();
        public int SelectedIndex { get; internal set; }
        public bool IsEntering { get; internal set; }
        public bool CanEnter { get; internal set; }
        public string Feedback { get; internal set; }
        public MainMenuData()
        {
            const string art = "LoadResources/UI/Hall/Art/Gallery/";
            Entries.Add(new MainMenuDemoEntry("drone_flight", "无人机飞行", "在训练场探索飞行手感。\n选择机型，开始你的飞行体验。", art + "DroneFlight", GameSceneId.DroneFlight, "起飞、穿越与精准操控"));
            Entries.Add(new MainMenuDemoEntry("block_porters", "小小搬豆工", "操纵搬运机械，规划运送路线。\n在立体场地中完成搬运挑战。", art + "BlockPorters", GameSceneId.BlockPorters, "机械协作，巧妙搬运"));
            Entries.Add(new MainMenuDemoEntry("jinx_casino", "倒霉蛋俱乐部", "走进复古俱乐部，直接操作机台。\n选择你的玩法，挑战自己的运气。", art + "JinxCasino", GameSceneId.JinxCasino, "下一次，会有好运吗？"));
            Entries.Add(new MainMenuDemoEntry("dlss", "DLSS 实验室", "切换画质模式，比较画面与性能。\n体验实时渲染技术带来的差异。", art + "Dlss", GameSceneId.Dlss, "探索画质与性能的平衡"));
            Entries.Add(new MainMenuDemoEntry("ui_validation", "UI 交互展台", "界面与导航的交互体验。\n展台正在准备中，敬请期待。", art + "UiValidation", null, "新的体验，即将开放"));
            Entries.Add(new MainMenuDemoEntry("how_to_fish", "渔力全开", "驾船探索群岛，钓起奇异生物。\n单人冒险开发中。", "LoadResources/Demos/how_to_fish/Art/UI/HubPreview", GameSceneId.HowToFish, "出海垂钓，探索未知"));
            Entries[0].IsSelected = true;
        }
        public void ClearData()
        {
            SelectedIndex = 0; IsEntering = CanEnter = false; Feedback = null;
            for (int i = 0; i < Entries.Count; i++) { Entries[i].IsSelected = i == 0; Entries[i].CanEnter = Entries[i].CanBrowse = false; }
        }
    }
}
