using Core.Runtime;

namespace Hotfix
{
    /// Demo 卡片使用普通数据刷新，虚拟列表身份和导航由公共组件维护。
    [Module("Main")]
    [Mvc("MainMenuDemoItemView")]
    public sealed partial class MainMenuDemoItemView : ItemView<MainMenuDemoEntry>
    {
        protected override void RefreshUI()
        {
            UIImageLoader_Preview.SetImage(params1.PreviewAddress, setNativeSize: false, isAsync: true);
            TextMeshProUGUI_Title.text = params1.Title;
            TextMeshProUGUI_Description.text = params1.Description;
            TextMeshProUGUI_Action.text = !params1.SceneId.HasValue ? "未开放" : params1.CanEnter ? "进入体验" : "加载中…";
            LoopScrollMenuButton_Enter.interactable = params1.CanEnter && params1.SceneId.HasValue;
        }

        private void OnEnterClick() => TriggerClick();

        /// 回收时清除图片，并取消旧图片写入。
        public void Clear() => UIImageLoader_Preview.Clear();
    }
}
