#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;

namespace Tests.Demo
{
    // 仅修改 Editor Game View 临时分辨率，Dispose 恢复用户原选择并移除临时尺寸。
    internal sealed class GameViewResolution : IDisposable
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        private readonly object group;
        private readonly EditorWindow window;
        private readonly PropertyInfo selected;
        private readonly int previous;
        private readonly int customIndex;

        internal GameViewResolution(int width, int height)
        {
            var assembly = typeof(EditorWindow).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var instance = sizesType.GetProperty("instance", Flags).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
            group = sizesType.GetMethod("GetGroup", Flags).Invoke(instance, new[] { Enum.Parse(groupType, "Standalone") });
            int total = (int)group.GetType().GetMethod("GetTotalCount", Flags).Invoke(group, null);
            int builtin = (int)group.GetType().GetMethod("GetBuiltinCount", Flags).Invoke(group, null);
            customIndex = total - builtin;
            var sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            var kind = assembly.GetType("UnityEditor.GameViewSizeType", true);
            var size = Activator.CreateInstance(sizeType, Flags, null,
                new[] { Enum.Parse(kind, "FixedResolution"), (object)width, height, "Streamline temporary test" }, null);
            group.GetType().GetMethod("AddCustomSize", Flags).Invoke(group, new[] { size });
            var viewType = assembly.GetType("UnityEditor.GameView", true);
            window = EditorWindow.GetWindow(viewType);
            selected = viewType.GetProperty("selectedSizeIndex", Flags);
            previous = (int)selected.GetValue(window);
            selected.SetValue(window, total);
        }

        public void Dispose()
        {
            selected.SetValue(window, previous);
            group.GetType().GetMethod("RemoveCustomSize", Flags).Invoke(group, new object[] { customIndex });
        }
    }
}
#endif
