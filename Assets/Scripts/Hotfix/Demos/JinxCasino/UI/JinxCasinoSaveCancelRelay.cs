using UnityEngine;
using UnityEngine.EventSystems;

namespace Hotfix.JinxCasino.UI
{
    /// <summary>保存的存档按钮将 Core 取消事件交回所属菜单。</summary>
    public sealed class JinxCasinoSaveCancelRelay : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private JinxCasinoImmersionHudPresenter owner;

        /// <summary>由保存 Prefab 装配指定所属菜单。</summary>
        /// <param name="presenter">当前 HUD；不在运行时搜索另一菜单。</param>
        public void Configure(JinxCasinoImmersionHudPresenter presenter) => owner = presenter;

        /// <summary>返回当前存档菜单层级。</summary>
        /// <param name="eventData">Core UI 模块派发的取消事件。</param>
        public void OnCancel(BaseEventData eventData)
        { eventData.Use(); owner?.CancelSaveWindow(); }
    }
}
