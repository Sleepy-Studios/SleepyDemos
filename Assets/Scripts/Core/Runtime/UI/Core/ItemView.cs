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

    public class ItemView<T> : ItemView
    {
        protected T params1;

        public virtual ItemView<T> SetData(T data)
        {
            params1 = data;
            return this;
        }
    }
}
