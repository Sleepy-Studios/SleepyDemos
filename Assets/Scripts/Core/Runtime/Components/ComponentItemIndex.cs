using UnityEngine;

namespace Core.Runtime
{
    public sealed class ComponentItemIndex : MonoBehaviour
    {
        [SerializeField]
        private Component[] components;
        [SerializeField, HideInInspector]
        private string[] componentTypes;
        [SerializeField, HideInInspector]
        private string[] bindingKeys;
        [SerializeField, HideInInspector]
        private string[] bindingMethods;

        public Component[] Components { get => components; set => components = value; }

        public string[] ComponentTypes { get => componentTypes; set => componentTypes = value; }

        public string[] BindingKeys { get => bindingKeys; set => bindingKeys = value; }

        public string[] BindingMethods { get => bindingMethods; set => bindingMethods = value; }

        public T Get<T>(int index) where T : Component
        {
            if (components == null || index < 0 || index >= components.Length)
            {
                return null;
            }

            return components[index] as T;
        }
    }
}
