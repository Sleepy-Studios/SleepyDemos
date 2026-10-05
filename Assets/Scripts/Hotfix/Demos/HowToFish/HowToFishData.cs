using System.Collections.Generic;
using Core.Runtime;

namespace Hotfix.HowToFish
{
    public enum HowToFishPage
    {
        None,
        MainMenu,
        Pause,
        Journal,
        Ending,
        Settings,
        Outfits
    }

    /// 本场规则状态、页面状态及派生查询的读取入口。
    public sealed class HowToFishData : IData
    {
        public List<IHandler> Handlers { get; }

        internal HowToFishHandler Handler { get; }

        internal HowToFishWorld Scene { get; }

        /// 当前航程的规则真源；未开始航程时为空。
        public HowToFishSession Session { get; internal set; }

        /// 玩法是否暂停；开始航程前默认为 true。
        public bool IsPaused { get; internal set; } = true;

        /// 是否请求显示本场图鉴。
        public bool ShowJournal { get; internal set; }

        /// 是否已经进入本航程结局展示。
        public bool ShowEnding { get; internal set; }

        /// 设置或服装页面是否正在阻断玩法输入。
        public bool IsEditingSettings { get; internal set; }

        /// 是否正在离开本场景。
        public bool IsExiting { get; internal set; }

        /// 当前共享档案选择的服装标识。
        public string SelectedOutfitId { get; internal set; } = HowToFishOutfitCatalog.DefaultId;

        /// 当前用户可见反馈；为空时不显示。
        public string Notice { get; internal set; }

        internal float NoticeUntil;

        internal int Slot, Version, ConfirmNewSlot = -1;

        internal readonly HowToFishLoadResult[] SlotInfos = new HowToFishLoadResult[3];

        internal HowToFishPage RequestedOverlay;

        internal bool SettingsOpen;

        internal HowToFishLocalPreferences SettingsSnapshot;

        /// 按规则与暂停状态确定主页面，弹层导航仍由协调器负责。
        public HowToFishPage MainPage
        {
            get
            {
                if (Session == null)
                    return HowToFishPage.MainMenu;
                if (ShowEnding)
                    return HowToFishPage.Ending;
                if (ShowJournal)
                    return HowToFishPage.Journal;
                return IsPaused ? HowToFishPage.Pause : HowToFishPage.None;
            }
        }

        internal HowToFishData(HowToFishWorld scene)
        {
            Scene = scene;
            Handler = new(scene);
            Handlers = new()
            {
                Handler
            };
        }

        /// 清理本场读取状态，先退订规则事件，再失效旧版本。
        public void ClearData()
        {
            Handler.DetachSession();
            Version++;
            Session = null;
            Slot = 0;
            ConfirmNewSlot = -1;
            System.Array.Clear(SlotInfos, 0, SlotInfos.Length);
            ShowJournal = ShowEnding = IsEditingSettings = IsExiting = SettingsOpen = false;
            IsPaused = true;
            SelectedOutfitId = HowToFishOutfitCatalog.DefaultId;
            Notice = null;
            NoticeUntil = 0;
            SettingsSnapshot = null;
            RequestedOverlay = HowToFishPage.None;
        }

        /// 当前航程的同一份持久状态；未开始航程时为空。
        public HowToFishSaveData SaveState => Session?.State;

        /// 是否已经进入航程。
        public bool HasSession => Session != null;

        /// <summary>查询服装是否已在当前航程解锁。</summary>
        /// <param name="id">当前服装目录的标识。</param>
        /// <returns>当前航程已解锁该服装时为 true。</returns>
        public bool IsOutfitUnlocked(string id) => Session != null && HowToFishOutfitCatalog.IsUnlocked(id, Session.State.unlockedOutfits);
    }
}
