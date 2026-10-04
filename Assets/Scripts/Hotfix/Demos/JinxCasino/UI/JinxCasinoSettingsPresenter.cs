using UnityEngine;
using UnityEngine.EventSystems;
namespace Hotfix.JinxCasino.UI
{
    /// 设置页只持有设置预览生命周期，触控布局由HUD长期持有。
    public sealed class JinxCasinoSettingsPresenter : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private JinxCasinoLocalSettingsPresenter localSettings;
        private JinxCasinoController owner;
        private GameObject first;
        /// <summary>显示前绑定本机设置。</summary>
        /// <param name="controller">当前场景宿主。</param>
        public void Bind(JinxCasinoController controller)
        { Unbind(); owner = controller; localSettings.Bind(owner, owner.UI.OnSettingsClosed); localSettings.ShowSettings(); first = null; Update(); }
        /// 隐藏时撤销未保存预览并释放订阅。
        public void Unbind() { if (localSettings != null) localSettings.Unbind(); owner = null; first = null; }
        /// 当前窗口的取消操作。
        public void CancelPreview() => localSettings.CloseSettings();
        private void Update()
        { if (owner != null && first != localSettings.FirstSelection) { first = localSettings.FirstSelection; owner.UI.SetFirstSelection(first); } }
        /// <summary>接收公共取消事件并只退出当前窗口层。</summary>
        /// <param name="value">公共UI模块的取消事件。</param>
        public void OnCancel(BaseEventData value) { value.Use(); owner?.UI.CancelImmersionHudWindow(); }
        private void OnDestroy() => Unbind();
    }
}
