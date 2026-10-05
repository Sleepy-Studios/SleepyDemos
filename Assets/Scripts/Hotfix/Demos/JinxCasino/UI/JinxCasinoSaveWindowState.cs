using System;
using Hotfix.JinxCasino.Persistence;

namespace Hotfix.JinxCasino.UI
{
    /// 存档窗口的槽快照和确认状态；命令与 IO 由 Handler 执行。
    internal sealed class JinxCasinoSaveWindowState
    {
        internal CasinoSaveSlotInfo[] Infos { get; } = new CasinoSaveSlotInfo[3];

        internal bool IsOpen { get; set; }

        internal bool Writing { get; set; }

        internal int PendingSlot { get; set; }

        internal string SourceRun { get; set; }

        internal string Feedback { get; set; }

        internal int LastActionFrame { get; set; } = -1;

        internal int LastCancelFrame { get; set; } = -1;

        internal CasinoSaveSlotInfo Info(int slot) => Infos[slot - 1];

        internal bool CanSelectSaveSlot(int slot)
        {
            var info = Info(slot);
            return Writing || info != null && !info.IsEmpty && string.IsNullOrEmpty(info.Error);
        }

        internal void ClearData()
        {
            Array.Clear(Infos, 0, Infos.Length);
            IsOpen = Writing = false;
            PendingSlot = 0;
            SourceRun = Feedback = null;
            LastActionFrame = LastCancelFrame = -1;
        }
    }
}
