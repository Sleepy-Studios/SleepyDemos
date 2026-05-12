using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class ViewTab : MonoBehaviour
    {
        [Serializable]
        private sealed class ViewTabItem
        {
            public Button button;
            public GameObject viewRoot;
        }

        [SerializeField] private List<ViewTabItem> items = new List<ViewTabItem>();
        [SerializeField] private int currentIndex;
        private Action<int> onSelected;

        private void Awake()
        {
            for (int i = 0; i < items.Count; i++)
            {
                var index = i;
                if (items[i].button != null)
                {
                    items[i].button.onClick.AddListener(() => Select(index));
                }
            }
            Refresh();
        }

        public void Register(Action<int> action)
        {
            onSelected += action;
        }

        public void Select(int index)
        {
            currentIndex = index;
            Refresh();
            onSelected?.Invoke(index);
        }

        private void Refresh()
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].viewRoot != null)
                {
                    items[i].viewRoot.SetActive(i == currentIndex);
                }
            }
        }
    }
}
