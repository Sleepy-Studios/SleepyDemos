using Core.Runtime;

namespace Hotfix
{
    /// Demo 卡片使用普通数据刷新，虚拟列表身份和导航由公共组件维护。
    [Module("Main")]
    [Mvc("MainMenuDemoItemView")]
    public sealed partial class MainMenuDemoItemView : ItemView
    {
        /// <summary>更新当前卡片；列表保证控件已初始化，回收复用时不重复绑定事件。</summary>
        /// <param name="data">本次展示数据；点击由页面按当前索引处理，卡片无需另存数据。</param>
        public void SetData(MainMenuDemoEntry data)
        {
            UIImageLoader_Preview.SetImage(data.PreviewAddress, setNativeSize: false, isAsync: true);
            TextMeshProUGUI_Title.text = data.Title;
            TextMeshProUGUI_Description.text = data.Description;
            TextMeshProUGUI_Action.text = !data.SceneId.HasValue ? "未开放" : data.CanEnter ? "进入体验" : "加载中…";
            LoopScrollMenuButton_Enter.interactable = data.CanEnter && data.SceneId.HasValue;
        }

        private void OnEnterClick() => TriggerClick();

        /// 回收时清除图片，并取消旧图片写入。
        public void Clear() => UIImageLoader_Preview.Clear();
    }
}
