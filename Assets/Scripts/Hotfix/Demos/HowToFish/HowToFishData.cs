using System.Collections.Generic;
using Core.Runtime;
namespace Hotfix.HowToFish
{
    public enum HowToFishPage { None, MainMenu, Pause, Journal, Ending, Settings, Outfits }
    public sealed class HowToFishData : IData
    {
        public List<IHandler> Handlers { get; }
        internal HowToFishHandler Handler { get; }
        internal HowToFishWorld Scene { get; }
        public HowToFishSession Session { get; internal set; }
        public bool IsPaused { get; internal set; } = true;
        public bool ShowJournal { get; internal set; }
        public bool ShowEnding { get; internal set; }
        public bool IsEditingSettings { get; internal set; }
        public bool IsExiting { get; internal set; }
        public string SelectedOutfitId { get; internal set; } = HowToFishOutfitCatalog.DefaultId;
        public string Notice { get; internal set; }
        internal float NoticeUntil;
        internal int Slot, Version, ConfirmNewSlot = -1;
        internal readonly HowToFishLoadResult[] SlotInfos = new HowToFishLoadResult[3];
        internal HowToFishPage RequestedOverlay;
        internal bool SettingsOpen;
        internal HowToFishLocalPreferences SettingsSnapshot;
        public HowToFishPage MainPage => Session == null ? HowToFishPage.MainMenu : ShowEnding ? HowToFishPage.Ending : ShowJournal ? HowToFishPage.Journal : IsPaused ? HowToFishPage.Pause : HowToFishPage.None;
        internal HowToFishData(HowToFishWorld scene)
        { Scene = scene; Handler = new(scene); Handlers = new() { Handler }; }
        public void ClearData() => Handler.Clear();
    }
}
