using Core.Runtime;

namespace Hotfix
{
    /// Demo 卡片使用普通数据刷新，虚拟列表身份和导航由公共组件维护。
    [Module("Main")]
    [UIBind("MainMenuDemoItemView")]
    public sealed partial class MainMenuDemoItemView : ItemView
    {
        private string previewAddress;
        /// <summary>更新当前卡片；列表保证控件已初始化，回收复用时不重复绑定事件。</summary>
        /// <param name="data">本次展示数据；点击由页面按当前索引处理，卡片无需另存数据。</param>
        public void SetData(MainMenuDemoEntry data)
        {
            if (previewAddress != data.PreviewAddress)
            {
                previewAddress = data.PreviewAddress;
                UIImageLoader_Preview.SetImage(previewAddress, setNativeSize: false, isAsync: true);
            }
            TextMeshProUGUI_Title.text = data.Title;
            TextMeshProUGUI_Description.text = data.Subtitle;
            TextMeshProUGUI_Action.text = !data.SceneId.HasValue ? "未开放" : "当前选择";
            Image_Badge.gameObject.SetActive(data.IsSelected || !data.SceneId.HasValue);
            Image_Badge.color = data.SceneId.HasValue ? new UnityEngine.Color(.12f, .69f, .57f) : new UnityEngine.Color(.48f, .50f, .47f);
            Image_Selection.enabled = data.IsSelected;
            LoopScrollMenuButton_Enter.interactable = data.CanBrowse;
        }

        private void OnEnterClick() => TriggerClick();

        /// 回收时清除图片，并取消旧图片写入。
        public void Clear()
        {
            previewAddress = null;
            UIImageLoader_Preview.Clear();
        }
    }
}
