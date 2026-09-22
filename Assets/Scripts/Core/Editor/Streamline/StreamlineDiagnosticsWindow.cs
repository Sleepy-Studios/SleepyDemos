using Core.Runtime.Rendering.Streamline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Editor.Streamline
{
    internal sealed class StreamlineDiagnosticsWindow : EditorWindow
    {
        private string report = "尚未探测。";
        private Vector2 scroll;

        [MenuItem("Tools/Rendering/Streamline Diagnostics")]
        private static void Open()
        {
            GetWindow<StreamlineDiagnosticsWindow>("Streamline");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Streamline 2.14.1 · 能力诊断", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName);
            EditorGUILayout.HelpBox("此入口检查原生设备和 SDK 能力。查询成功不代表 DLSS 效果已启用。", MessageType.Info);
            if (GUILayout.Button("提交渲染线程探测"))
            {
                using (var commands = new CommandBuffer { name = "Streamline capability probe" })
                {
                    if (StreamlineDiagnostics.TryEnqueueProbe(commands, out string reason))
                        Graphics.ExecuteCommandBuffer(commands);
                    else report = reason;
                }
            }
            if (GUILayout.Button("读取最近结果"))
                report = StreamlineDiagnostics.TryGetReport(out string json, out string error) ? json : error;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }
}
