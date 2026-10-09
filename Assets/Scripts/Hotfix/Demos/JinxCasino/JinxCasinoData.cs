using System.Collections.Generic;
using Core.Runtime;
using Hotfix.JinxCasino.Interaction;
using Hotfix.JinxCasino.UI;
using Hotfix.JinxCasino.Persistence;

namespace Hotfix.JinxCasino
{
    public enum JinxCasinoPage
    {
        None = -1,
        MainMenu,
        Pause,
        Field,
        TutorialReady,
        TutorialChoice,
        TutorialConfirm,
        SaveSlots,
        SaveConfirm,
        Ending,
        Settings
    }

    /// 本场规则状态、页面状态及派生查询的读取入口。
    public sealed class JinxCasinoData : IData
    {
        public List<IHandler> Handlers { get; }

        internal JinxCasinoHandler Handler { get; }

        internal JinxCasinoController Scene { get; }

        /// 当前冒险规则与存档真源，和场景使用同一实例。
        public JinxCasinoGame Game { get; }

        /// 当前交互及物理表现对象；页面读取其实际状态。
        public JinxCasinoPlayerInteraction Player { get; }

        /// 当前本地设置服务与真实生效偏好。
        public JinxCasinoLocalSettings Settings { get; }

        internal JinxCasinoSaveWindowState Save { get; }

        internal JinxCasinoTutorialWindowState Tutorial { get; }

        /// 当前业务请求的页面类型，实际导航由 Handler 执行。
        public JinxCasinoPage Page { get; internal set; } = JinxCasinoPage.None;

        /// 是否正在离场并阻断新的玩法命令。
        public bool IsExiting { get; internal set; }

        internal bool SettingsOpen;

        internal CasinoLocalPreferences SettingsSaved, SettingsDraft;

        internal bool SettingsPreviewing;

        internal int LastSettingsCancelFrame = -1;

        internal JinxCasinoData(JinxCasinoController scene)
        {
            Scene = scene;
            Game = scene.Game;
            Player = scene.Player;
            Settings = scene.Settings;
            Save = new();
            Tutorial = new();
            Handler = new(scene);
            Handlers = new()
            {
                Handler
            };
        }

        internal JinxCasinoPage ResolvePage()
        {
            if (SettingsOpen)
                return JinxCasinoPage.Settings;
            if (Save.IsOpen)
                return Save.PendingSlot > 0 ? JinxCasinoPage.SaveConfirm : JinxCasinoPage.SaveSlots;
            if (Player.Exit.HasEnding && !Game.HasActiveRound && !Player.HasFocus)
                return JinxCasinoPage.Ending;
            return Tutorial.ResolveState(this);
        }

        /// 清理窗口和规则状态，不请求场景复位、存档或导航。
        public void ClearData()
        {
            Game.ResetAdventureState();
            Save.ClearData();
            Tutorial.ClearData();
            Page = JinxCasinoPage.MainMenu;
            IsExiting = SettingsOpen = SettingsPreviewing = false;
            SettingsSaved = SettingsDraft = null;
            LastSettingsCancelFrame = -1;
        }
    }
}
