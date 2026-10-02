using UnityEngine;
using UnityEngine.EventSystems;

namespace Hotfix.JinxCasino.Adapters.UI
{
    /// Core UI模块只向当前Selected对象派发Cancel，保存到按钮上转给本HUD，不新增输入轮询。
    public sealed class JinxCasinoTutorialCancelRelay : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private JinxCasinoImmersionHudPresenter owner;
        /// <summary>Editor绑定保存的HUD宿主。</summary>
        /// <param name="presenter">所属HUD，不在运行时搜索或创建控件。</param>
        public void Configure(JinxCasinoImmersionHudPresenter presenter) => owner = presenter;
        /// <summary>将Core取消事件交回所属教学菜单。</summary>
        /// <param name="eventData">Core InputSystemUIInputModule提供的事件。</param>
        public void OnCancel(BaseEventData eventData)
        { if (owner == null) return; eventData.Use(); owner.CancelTutorialWindow(); }
    }
}
