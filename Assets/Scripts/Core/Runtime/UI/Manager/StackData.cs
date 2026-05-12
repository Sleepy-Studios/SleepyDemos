using System.Collections.Generic;

namespace Core.Runtime
{
    internal sealed class StackData
    {
        public readonly List<View> showList = new List<View>();
        public readonly Dictionary<UILayer, List<View>> layerData = new Dictionary<UILayer, List<View>>();
        public string customName;

        public int Count => showList.Count;

        public void Clear()
        {
            showList.Clear();
            layerData.Clear();
            customName = null;
        }
    }
}
