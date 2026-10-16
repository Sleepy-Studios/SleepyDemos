using Core.Runtime;
using UnityEngine;

namespace Hotfix
{
    /// 公共 UI 与 Hub 的固定色板；各 Demo 在自身程序集维护专属色板。
    public static class UIColors
    {
        #region 公共 UI
        public static class Common
        {
            public static readonly Color LoadingBackground = ColorUtil.GetColor("#0F1214");
            public static readonly Color LoopFocus = ColorUtil.GetColor("#33D9FF");
            public static readonly Color LoopDisabled = ColorUtil.GetColor("#6B7078", alphaPercent: 60);
        }
        #endregion

        #region Hub
        // 首页状态与反馈；见 docs/architecture/ui-art-direction.md。
        public static class Hub
        {
            public static readonly Color Status = ColorUtil.GetColor("#636E6B");
            public static readonly Color EntryFeedback = ColorUtil.GetColor("#B83829");
        }
        #endregion
    }
}
