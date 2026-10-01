using System;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.BlockPorters
{
    [CustomEditor(typeof(BlockPortersRecipe))]
    public sealed class BlockPortersRecipeEditor : UnityEditor.Editor
    {
        private string error;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var recipe = (BlockPortersRecipe)target;
            var previousSource = recipe.Source;
            EditorGUILayout.HelpBox("直接修改同样会使工作台分析失效。网格、色表和锁定区域请在工作台操作；修改尺寸后重新转换图片。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("关卡显示名"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("source"), new GUIContent("原图"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"), new GUIContent("处理与难度参数"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("columns"), new GUIContent("五列队伍（兼容旧四列）"), true);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(recipe, "修改搬砖配方");
                serializedObject.ApplyModifiedProperties();
                try
                {
                    if (recipe.Source != null && recipe.Source != previousSource)
                        recipe.SetSource(BlockPortersWorkbenchIO.CopySource(AssetDatabase.GetAssetPath(recipe.Source)));
                    error = null;
                }
                catch (Exception exception) { recipe.SetSource(previousSource); error = exception.Message; }
                recipe.Invalidate(); EditorUtility.SetDirty(recipe);
            }
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.LabelField($"有效格数组 {recipe.Cells.Length}，色号 {recipe.Palette.Length}，内容版本 {recipe.Revision}");
            if (GUILayout.Button("打开关卡工作台")) EditorApplication.ExecuteMenuItem("Tools/SleepyDemos/小小搬豆工/关卡编辑器");
        }
    }
}
