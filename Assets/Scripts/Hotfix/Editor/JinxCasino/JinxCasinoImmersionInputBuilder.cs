using System;
using System.IO;
using Hotfix.JinxCasino.Adapters.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.Editor.JinxCasino
{
    /// 单独保存Demo输入配置，不覆盖人工修改的动作资产或全局Player/UI配置。
    public static class JinxCasinoImmersionInputBuilder
    {
        /// 沉浸机台共用的独立输入资产地址。
        public const string AssetPath = "Assets/LoadResources/Demos/jinx_casino/Data/JinxCasinoImmersion.inputactions";

        /// 首次创建输入资产；已存在时校验并复用，不重写手工键位。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/创建或检查输入配置")]
        public static void EnsureInputAsset()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式装配输入配置。");
            if (!File.Exists(AssetPath))
            {
                var temporary = JinxCasinoInputAsset.Create();
                try { File.WriteAllText(AssetPath, temporary.ToJson()); }
                finally { UnityEngine.Object.DestroyImmediate(temporary); }
                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
            }
            var saved = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            if (saved == null) throw new InvalidOperationException("输入配置导入失败：" + AssetPath);
            using (var validation = new JinxCasinoInputRouter(saved)) { }
            Debug.Log("[JinxCasino] 独立输入配置已保存并验证：" + AssetPath);
        }
    }
}
