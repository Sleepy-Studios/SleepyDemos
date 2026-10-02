using System;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix
{
    /// 正式启动和Editor直启共用的公共画质入口；页面只借用控件显隐，不改变View状态或画质。
    public static class GraphicsSettingsUI
    {
        private static int suppressionCount;
        public static bool IsEntrySuppressed => suppressionCount > 0;
        /// 仅首个借用者与最后一个释放者通知，已显示入口自行管理拥有的子控件。
        public static event Action<bool> EntrySuppressionChanged;

        public static async UniTaskVoid Initialize()
        {
            await UniTask.Yield();
            var result = await UIManager.Instance.ShowAsync<DlssSettingsView>(
                new UIShowOptions(animated: false, hidePrevious: false));
            if (result.Status == UIOperationStatus.Failed) Debug.LogError("画质设置入口加载失败：" + result.Exception);
        }

        /// Unity主线程页面同步借用入口显隐；返回幂等释放的作用域，不进入另一UI导航队列。
        public static IDisposable SuppressEntry()
        {
            var lease = new EntrySuppression();
            if (++suppressionCount == 1) EntrySuppressionChanged?.Invoke(true);
            return lease;
        }

        private sealed class EntrySuppression : IDisposable
        {
            private bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (--suppressionCount == 0) EntrySuppressionChanged?.Invoke(false);
            }
        }
    }
}
