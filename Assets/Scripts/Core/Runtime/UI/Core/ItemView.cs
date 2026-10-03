using System;
using UnityEngine;

namespace Core.Runtime
{
    public class ItemView
    {
        public int Index { get; private set; }
        public GameObject gameObject { get; private set; }
        public Transform transform { get; private set; }
        public bool IsInitialized { get; private set; }
        public Action<int> onClick;

        /// <summary>绑定物理对象和控件，再通知初始化完成；提前接收的数据此后才能刷新 UI。</summary>
        /// <param name="target">包含 MvcBind 引用的物理 Item 对象。</param>
        /// <param name="index">当前数据索引；回收复用时由 SetIndex 更新。</param>
        public virtual void Init(GameObject target, int index)
        {
            IsInitialized = false;
            gameObject = target;
            transform = target != null ? target.transform : null;
            Index = index;
            InitComponent();
            IsInitialized = true;
            OnInitialized();
        }

        public virtual void SetIndex(int index)
        {
            Index = index;
        }

        public void TriggerClick()
        {
            onClick?.Invoke(Index);
            OnClick();
        }

        protected virtual void InitComponent()
        {
        }

        // 此时 MvcBind 控件引用已就绪；初始化过程中不要在 InitComponent 内刷新业务数据。
        protected virtual void OnInitialized()
        {
        }

        protected virtual void OnClick()
        {
        }
    }

    public class ItemView<T> : ItemView
    {
        protected T params1;
        private bool hasData;

        /// <summary>接收数据，可以早于 Init；UI 更新统一在控件绑定完成后调用 RefreshUI。</summary>
        /// <param name="data">当前数据，可以为空；初始化前重复调用只保留最后一次数据。</param>
        /// <returns>当前 Item，供调用方链式配置。</returns>
        public virtual ItemView<T> SetData(T data)
        {
            params1 = data;
            hasData = true;
            if (IsInitialized)
                RefreshUI();
            return this;
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            if (hasData)
                RefreshUI();
        }

        // 子类只在这里访问绑定控件，不在 SetData 中直接刷新，兼顾提前配置与回收重绑。
        protected virtual void RefreshUI()
        {
        }
    }
}
