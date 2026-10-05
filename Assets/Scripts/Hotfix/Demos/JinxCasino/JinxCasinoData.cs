using System.Collections.Generic;
using Core.Runtime;
using Hotfix.JinxCasino.Interaction;
using Hotfix.JinxCasino.UI;
using Hotfix.JinxCasino.Persistence;

namespace Hotfix.JinxCasino
{
    public enum JinxCasinoPage { None = -1, MainMenu, Pause, Field, TutorialReady, TutorialChoice, TutorialConfirm, SaveSlots, SaveConfirm, Ending, Settings }
    public sealed class JinxCasinoData : IData
    {
        public List<IHandler> Handlers { get; }
        internal JinxCasinoHandler Handler { get; }
        internal JinxCasinoController Scene { get; }
        public JinxCasinoGame Game { get; }
        public JinxCasinoPlayerInteraction Player { get; }
        public JinxCasinoLocalSettings Settings { get; }
        internal JinxCasinoSaveWindowState Save { get; }
        internal JinxCasinoTutorialWindowState Tutorial { get; }
        public JinxCasinoPage Page { get; internal set; } = JinxCasinoPage.None;
        public bool IsExiting { get; internal set; }
        internal bool SettingsOpen;
        internal CasinoLocalPreferences SettingsSaved, SettingsDraft;
        internal bool SettingsPreviewing;
        internal JinxCasinoData(JinxCasinoController scene)
        {
            Scene = scene; Game = scene.Game; Player = scene.Player; Settings = scene.Settings;
            Save = new(scene); Tutorial = new(scene);
            Handler = new(scene); Handlers = new() { Handler };
        }
        public void ClearData() { Game.ClearAdventure(); Page = JinxCasinoPage.MainMenu; }
    }
}
