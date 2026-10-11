using System.Collections.Generic;
using Core.Runtime;
using Hotfix.SceneManagement;

namespace Hotfix
{
    /// 大厅的入口集合、选中态、导航状态和反馈。
    public sealed class MainMenuData : IData
    {
        public List<IHandler> Handlers { get; } = new()
        {
            new MainMenuHandler()
        };

        /// 当前展示的 Demo 卡片集合；卡片配置在构造时创建。
        public List<MainMenuDemoEntry> Entries { get; } = new();

        /// 当前选中卡片索引，从零开始。
        public int SelectedIndex { get; internal set; }

        /// 是否正在等待场景进入结果。
        public bool IsEntering { get; internal set; }

        /// 入口是否允许进入或切换选择。
        public bool CanEnter { get; internal set; }

        /// 当前用户可见反馈；null 表示无反馈。
        public string Feedback { get; internal set; }

        /// 常规入口反馈的稳定文案键；与任意诊断文本 Feedback 互斥。
        public string FeedbackKey { get; internal set; }

        internal int Version;

        public MainMenuData()
        {
            const string art = "LoadResources/UI/Hall/Art/Gallery/";
            Entries.Add(new MainMenuDemoEntry("drone_flight", "无人机飞行", "在训练场探索飞行手感。\n选择机型，开始你的飞行体验。", art + "DroneFlight", GameSceneId.DroneFlight, "起飞、穿越与精准操控"));
            Entries.Add(new MainMenuDemoEntry("dlss", "DLSS 实验室", "切换画质模式，比较画面与性能。\n体验实时渲染技术带来的差异。", art + "Dlss", GameSceneId.Dlss, "探索画质与性能的平衡"));
            Entries.Add(new MainMenuDemoEntry("ui_validation", "UI 交互展台", "界面与导航的交互体验。\n展台正在准备中，敬请期待。", art + "UiValidation", null, "新的体验，即将开放"));
            Entries[0].IsSelected = true;
            Entries.Add(new MainMenuDemoEntry("wall_squeeze", "夹爆它", "拖动滑墙，把红怪赶拢夹爆。\n移开蓝色住户，留出安全空间。", "LoadResources/Demos/wall_squeeze/Art/UI/HubPreview", GameSceneId.WallSqueeze, "推墙夹击，保护住户"));
            Entries.Add(new MainMenuDemoEntry("kagit_kart", "纸艺卡丁车", "驾驶剪纸赛车，穿过立体书赛道。\n中文单人移植开发中。", "LoadResources/Demos/kagit_kart/Art/UI/HubPreview", GameSceneId.KagitKart, "翻开纸页，开始比赛"));
        }

        /// 重置业务状态，使上一轮请求结果失效。
        public void ClearData()
        {
            Version++;
            SelectedIndex = 0;
            IsEntering = CanEnter = false;
            Feedback = null;
            FeedbackKey = null;
            for (int i = 0; i < Entries.Count; i++)
            {
                Entries[i].IsSelected = i == 0;
                Entries[i].CanEnter = Entries[i].CanBrowse = false;
            }
        }
    }
}
