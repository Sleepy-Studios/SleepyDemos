using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class UITab : MonoBehaviour
    {
        [SerializeField] private List<Button> buttons = new List<Button>();
        [SerializeField] private List<GameObject> selectedStates = new List<GameObject>();
        [SerializeField] private int currentIndex;

        private Action<int> onSelected;

        private void Awake()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                var index = i;
                if (buttons[i] != null)
                {
                    buttons[i].onClick.AddListener(() => Select(index));
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
            for (int i = 0; i < selectedStates.Count; i++)
            {
                if (selectedStates[i] != null)
                {
                    selectedStates[i].SetActive(i == currentIndex);
                }
            }
        }
    }
}
