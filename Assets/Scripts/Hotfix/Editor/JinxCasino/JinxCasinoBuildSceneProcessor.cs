using Core.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace Hotfix.Editor.JinxCasino
{
    /// 仅替换本构建事务里的场景副本；原 AppEntrance 与默认 HotfixConfig 不保存、不改写。
    public sealed class JinxCasinoBuildSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 1000;

        /// <summary>为本次 P0 Player 的内存启动场景替换平台专属配置。</summary>
        /// <param name="scene">Unity 构建中的场景副本；不对场景资源执行保存。</param>
        /// <param name="report">非空的 Player 构建报告；普通编辑器打开场景时忽略。</param>
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || scene.path != JinxCasinoPlayerBuildPipeline.EntranceScene ||
                !JinxCasinoPlayerBuildPipeline.TryGetBuildConfig(out var config)) return;
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var entrance in root.GetComponentsInChildren<CoreEntrance>(true))
            {
                var serialized = new SerializedObject(entrance);
                var property = serialized.FindProperty("hotfixConfig");
                if (property == null) throw new BuildFailedException("CoreEntrance 的 hotfixConfig 序列化契约发生变化。");
                property.objectReferenceValue = config;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }
            if (count != 1) throw new BuildFailedException("P0 构建要求 AppEntrance 恰好包含一个 CoreEntrance。");
        }
    }
}
