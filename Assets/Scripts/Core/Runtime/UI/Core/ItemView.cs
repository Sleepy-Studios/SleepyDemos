using System;
using UnityEngine;

namespace Core.Runtime
{
    public class ItemView
    {
        public int Index { get; private set; }
        public GameObject gameObject { get; private set; }
        public Transform transform { get; private set; }
        public Action<int> onClick;

        /// <summary>绑定物理对象和控件；列表桥接在本方法完成后才交付数据，复用时只更新索引。</summary>
        /// <param name="target">包含 MvcBind 引用的物理 Item 对象。</param>
        /// <param name="index">当前数据索引；回收复用时由 SetIndex 更新。</param>
        public virtual void Init(GameObject target, int index)
        {
            gameObject = target;
            transform = target != null ? target.transform : null;
            Index = index;
            InitComponent();
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

        protected virtual void OnClick()
        {
        }
    }

}
