using System;
using Core.Runtime;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Rules;
using UnityEngine;
namespace Hotfix.JinxCasino.UI
{
    /// 存档窗口持有槽快照与二次确认，保留原盘面恢复和备份规则。
    internal sealed class JinxCasinoSaveWindowState
    {
        private readonly JinxCasinoController owner;
        private readonly CasinoSaveSlotInfo[] saveInfos = new CasinoSaveSlotInfo[3];
        private bool saveBrowserOpen, saveWriting;
        private int savePendingSlot;
        private string saveSourceRun, saveUiFeedback;
        private int lastSaveActionFrame = -1, lastSaveCancelFrame = -1;
        internal bool IsOpen => saveBrowserOpen;
        internal bool Writing => saveWriting;
        internal int PendingSlot => savePendingSlot;
        internal string Feedback => saveUiFeedback;
        internal CasinoSaveSlotInfo Info(int slot) => saveInfos[slot - 1];
        internal JinxCasinoSaveWindowState(JinxCasinoController controller) => owner = controller;
        private void Refresh() => owner.UI.Refresh();
        internal void Open(bool writing)
        {
            if (owner.IsBusy || writing && !owner.Game.HasAdventure) return;
            saveWriting = writing; saveSourceRun = owner.Game.State?.RunId; saveBrowserOpen = true;
            savePendingSlot = 0; saveUiFeedback = null; ReloadSaveInfos(); Refresh();
        }
        internal void ReloadSaveInfos()
        {
            for (int slot = 1; slot <= 3; slot++)
            {
                try { saveInfos[slot - 1] = owner.Game.GetSaveSlotInfo(slot); }
                catch (Exception) { saveInfos[slot - 1] = new CasinoSaveSlotInfo { Slot = slot, Error = "暂时无法读取此槽，请稍后再试。" }; }
            }
        }

        internal bool CanSelectSaveSlot(int slot)
        {
            var info = saveInfos[slot - 1];
            return saveWriting || info != null && !info.IsEmpty && string.IsNullOrEmpty(info.Error);
        }

        internal void ChooseSaveSlot(int slot)
        {
            if (!saveBrowserOpen || savePendingSlot > 0 || owner.IsBusy || !CanSelectSaveSlot(slot)) return;
            ReloadSaveInfos();
            if (!CanSelectSaveSlot(slot)) { saveUiFeedback = "此槽暂时无法恢复，请选择另一个存档。"; Refresh(); return; }
            var info = saveInfos[slot - 1]; saveUiFeedback = null;
            if (saveWriting && !info.IsEmpty || !saveWriting && owner.Game.HasAdventure) { savePendingSlot = slot; Refresh(); }
            else ExecuteSaveOperation(slot);
        }

        internal void ConfirmSaveOperation() { if (savePendingSlot > 0) ExecuteSaveOperation(savePendingSlot); }

        internal void ExecuteSaveOperation(int slot)
        {
            if (owner.IsBusy || lastSaveActionFrame == Time.frameCount) return;
            lastSaveActionFrame = Time.frameCount;
            if (saveSourceRun != owner.Game.State?.RunId)
            { saveUiFeedback = "当前旅程已改变，请返回后重新选择。"; Refresh(); return; }
            bool success = saveWriting ? owner.Game.SaveAdventure(slot) : owner.LoadAdventure(slot);
            saveUiFeedback = owner.Game.Status;
            if (!success) { Refresh(); return; }
            savePendingSlot = 0;
            if (saveWriting)
            { ReloadSaveInfos(); saveSourceRun = owner.Game.State?.RunId; Refresh(); }
            else
            {
                saveBrowserOpen = false;
                if (owner.Player.IsPaused && !owner.Player.Resume()) owner.UI.Tutorial.SetFeedback("请先回到游戏窗口或接回手柄，再点击继续。");
                Refresh();
            }
        }

        internal void CancelSaveWindow()
        {
            if (owner == null || lastSaveCancelFrame == Time.frameCount) return;
            lastSaveCancelFrame = Time.frameCount;
            if (!saveBrowserOpen) { owner.UI.Tutorial.CancelTutorialWindow(); return; }
            if (savePendingSlot > 0) savePendingSlot = 0;
            else saveBrowserOpen = false;
            saveUiFeedback = null; Refresh();
        }
    }
}
