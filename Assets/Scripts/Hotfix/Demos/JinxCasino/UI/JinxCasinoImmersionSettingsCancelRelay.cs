using UnityEngine;
using UnityEngine.EventSystems;

namespace Hotfix.JinxCasino.UI
{
    /// 保存设置控件的Core取消回调；不创建输入动作或持有全局EventSystem。
    public sealed class JinxCasinoImmersionSettingsCancelRelay : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private JinxCasinoImmersionHudPresenter presenter;

        /// <summary>绑定所属保存HUD的设置取消入口。</summary>
        /// <param name="owner">所属HUD；为空时忽略取消。</param>
        public void Configure(JinxCasinoImmersionHudPresenter owner) => presenter = owner;

        /// <summary>Core菜单取消只退设置层，不提交保存或恢复冒险。</summary>
        /// <param name="eventData">本次公共UI取消事件；已处理时标记消费。</param>
        public void OnCancel(BaseEventData eventData)
        {
            if (presenter == null) return;
            presenter.CloseSettingsWindow(); eventData.Use();
        }
    }
}
