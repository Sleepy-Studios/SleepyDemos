using UnityEngine;
using UnityEngine.EventSystems;

namespace Hotfix.JinxCasino.Adapters.UI
{
    /// <summary>结局按钮的 Core 取消只保留结果，不代替明确离场操作。</summary>
    public sealed class JinxCasinoEndingCancelRelay : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private JinxCasinoImmersionHudPresenter owner;

        /// <summary>装配已保存按钮的所属结果卡。</summary>
        /// <param name="presenter">当前 HUD 实例。</param>
        public void Configure(JinxCasinoImmersionHudPresenter presenter) => owner = presenter;

        /// <summary>保留结局卡。</summary>
        /// <param name="eventData">Core 派发的取消事件。</param>
        public void OnCancel(BaseEventData eventData)
        { eventData.Use(); owner?.CancelStandardEndingWindow(); }
    }
}
