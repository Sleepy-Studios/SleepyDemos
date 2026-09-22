using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix
{
    /// 正式启动和 Editor 直启共用的公共画质设置入口。
    public static class GraphicsSettingsUI
    {
        public static async UniTaskVoid Initialize()
        {
            await UniTask.Yield();
            var result = await UIManager.Instance.ShowAsync<DlssSettingsView>(
                new UIShowOptions(animated: false, hidePrevious: false));
            if (result.Status == UIOperationStatus.Failed) Debug.LogError("画质设置入口加载失败：" + result.Exception);
        }
    }
}
