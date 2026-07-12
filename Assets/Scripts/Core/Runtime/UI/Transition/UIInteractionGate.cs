using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    internal interface IUIInteractionGate
    {
        void Acquire();
        void Release();
    }

    public sealed class UIInteractionGate : IUIInteractionGate
    {
        private Graphic graphic;

        /// 当前尚未释放的交互锁数量。
        public int Count { get; private set; }

        /// 当前是否正在拦截 UI 射线。
        public bool IsBlocking => Count > 0;

        /// <summary>
        /// 绑定用于拦截射线的透明 Graphic，并同步当前计数状态。
        /// </summary>
        /// <param name="graphic">Tip 层的全屏透明 Graphic。</param>
        public void Initialize(Graphic graphic)
        {
            EnsureMainThread();
            if (this.graphic != null && this.graphic != graphic)
            {
                this.graphic.raycastTarget = false;
            }

            this.graphic = graphic;
            ApplyBlockingState();
        }

        /// 获取一份交互锁。
        public void Acquire()
        {
            EnsureMainThread();
            checked
            {
                Count++;
            }

            ApplyBlockingState();
        }

        /// 将交互锁恢复到所属层级的最上方；仅在层级结构发生变化后调用。
        public void EnsureOnTop()
        {
            EnsureMainThread();
            if (graphic != null)
            {
                graphic.transform.SetAsLastSibling();
            }
        }

        /// 释放一份交互锁；多余释放会记录错误并保持为零。
        public void Release()
        {
            EnsureMainThread();
            if (Count <= 0)
            {
                Count = 0;
                Debug.LogError("UIInteractionGate 收到多余的 Release，计数已保持为 0。");
                ApplyBlockingState();
                return;
            }

            Count--;
            ApplyBlockingState();
        }

        private void ApplyBlockingState()
        {
            if (graphic == null)
            {
                return;
            }

            graphic.raycastTarget = IsBlocking;
            if (IsBlocking)
            {
                EnsureOnTop();
            }
        }

        private static void EnsureMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                throw new InvalidOperationException("UIInteractionGate 只能在 Unity 主线程操作。");
            }
        }
    }
}
