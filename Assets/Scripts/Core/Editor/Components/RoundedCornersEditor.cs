using Core.Runtime;
using UnityEditor;

namespace Core.Editor
{
    [CustomEditor(typeof(RoundedCorners))]
    [CanEditMultipleObjects]
    public sealed class RoundedCornersEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox(
                "圆角依赖 Canvas 的 TexCoord1/TexCoord2 通道，组件会自动开启。作为遮罩时会自动添加 Mask。",
                MessageType.Info);
        }
    }
}
