using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class ViewList : MonoBehaviour
    {
        [SerializeField] private List<Button> itemButtons = new List<Button>();
        private Action<int> onClick;

        private void Awake()
        {
            for (int i = 0; i < itemButtons.Count; i++)
            {
                var index = i;
                if (itemButtons[i] != null)
                {
                    itemButtons[i].onClick.AddListener(() => onClick?.Invoke(index));
                }
            }
        }

        public void Register(Action<int> action)
        {
            onClick += action;
        }
    }
}
