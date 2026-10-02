using System;
using System.Linq;
using Hotfix.JinxCasino.Adapters;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        /// 在入口两侧保存检票和离场物件，不重建Hall或已有机台。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/装配标准检票与离场入口")]
        public static void BuildImmersionExit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式装配离场入口。");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            string palette = JinxCasinoImmersionArtBuilder.Root + "/Materials/";
            var gold = AssetDatabase.LoadAssetAtPath<Material>(palette + "CopperGold.mat");
            var ink = AssetDatabase.LoadAssetAtPath<Material>(palette + "InkBlue.mat");
            var paper = AssetDatabase.LoadAssetAtPath<Material>(palette + "CreamYellow.mat");
            var ticket = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/Item_mystery_coupon.fbx");
            if (font == null || gold == null || ink == null || paper == null || ticket == null) throw new InvalidOperationException("离场入口缺少已有字库、palette或票券模型。");
            Scene previous = SceneManager.GetActiveScene(), scene = SceneManager.GetSceneByPath(ImmersionScenePath);
            bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("请先处理场景人工修改，不能覆盖未保存场景。");
            if (opened) scene = EditorSceneManager.OpenScene(ImmersionScenePath, OpenSceneMode.Additive);
            var created = new System.Collections.Generic.List<GameObject>();
            JinxCasinoController owner = null;
            UnityEngine.Object[] previousBindings = null;
            try
            {
                if (SceneManager.GetActiveScene() != scene) SceneManager.SetActiveScene(scene);
                owner = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<JinxCasinoController>(true)).Single();
                var bindings = new SerializedObject(owner).FindProperty("exitTerminals");
                previousBindings = Enumerable.Range(0, bindings.arraySize).Select(index => bindings.GetArrayElementAtIndex(index).objectReferenceValue).ToArray();
                var existing = owner.GetComponentsInChildren<JinxCasinoExitTerminal>(true);
                if (existing.Length != 0)
                {
                    if (existing.Length != 2 || existing.Select(terminal => terminal.TerminalId).Distinct().Count() != 2)
                        throw new InvalidOperationException("已有离场入口不完整，请先检查引用，不自动删除用户对象。");
                    owner.ConfigureExitTerminals(existing);
                }
                else
                {
                    var verify = CreateExitTerminal(owner, "s1.verify", JinxCasinoExitAction.Verify, new Vector3(2.2f, 0, -5.45f), font, gold, ink, paper, ticket, created);
                    var leave = CreateExitTerminal(owner, "s1.leave", JinxCasinoExitAction.Leave, new Vector3(-2.2f, 0, -5.45f), font, gold, ink, paper, ticket, created);
                    owner.ConfigureExitTerminals(new[] { verify, leave });
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("离场场景保存失败。");
            }
            catch
            {
                // 只清本次新物件；不把未配置的半入口留下供下次误认成功，也不删除用户原对象。
                foreach (var root in created) if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (owner != null && previousBindings != null)
                {
                    var saved = new SerializedObject(owner); var bindings = saved.FindProperty("exitTerminals");
                    bindings.arraySize = previousBindings.Length;
                    for (int index = 0; index < previousBindings.Length; index++) bindings.GetArrayElementAtIndex(index).objectReferenceValue = previousBindings[index];
                    saved.ApplyModifiedPropertiesWithoutUndo();
                }
                throw;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded && SceneManager.GetActiveScene() != previous) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// 只更新两处入口的操作高度和文字前后关系，保留其它场景布局。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/更新入口铭牌可读性")]
        public static void UpdateExitReadability()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式调整入口。");
            Scene scene = SceneManager.GetSceneByPath(ImmersionScenePath); bool opened = !scene.isLoaded;
            Scene previous = SceneManager.GetActiveScene();
            if (!opened && scene.isDirty) throw new InvalidOperationException("请先保存场景人工修改。");
            if (opened) scene = EditorSceneManager.OpenScene(ImmersionScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var terminal in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<JinxCasinoExitTerminal>(true)))
                    ApplyExitReadability(terminal.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("入口铭牌保存失败。");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
        private static void ApplyExitReadability(Transform root)
        {
            root.Find("Control").localPosition = new Vector3(0, 1.60f, .03f);
            // TMP使用RectTransform，显式写入保存的锚定位置，避免只改Transform后旧XY被重新应用。
            var actionText = (RectTransform)root.Find("ActionText");
            var stateText = (RectTransform)root.Find("StateText");
            actionText.anchoredPosition3D = new Vector3(0, 1.68f, .135f);
            stateText.anchoredPosition3D = new Vector3(0, 1.56f, .135f);
            EditorUtility.SetDirty(actionText); EditorUtility.SetDirty(stateText);
            root.Find("Button").localPosition = new Vector3(0, 1.34f, .04f);
            var ticket = root.Find("Item_mystery_coupon");
            if (ticket != null) ticket.localPosition = new Vector3(0, 1.17f, .035f);
        }

        private static JinxCasinoExitTerminal CreateExitTerminal(JinxCasinoController owner, string id, JinxCasinoExitAction action, Vector3 position,
            TMP_FontAsset font, Material gold, Material ink, Material paper, GameObject ticket, System.Collections.Generic.List<GameObject> created)
        {
            var root = new GameObject(id); created.Add(root); root.transform.SetParent(owner.transform, false);
            root.transform.localPosition = position;
            var component = root.AddComponent<JinxCasinoExitTerminal>();
            var approach = ExitChild(root.transform, "Approach", new Vector3(0, 0, .8f));
            var control = ExitChild(root.transform, "Control", new Vector3(0, 1.35f, .03f));
            ExitBlock(control, "CopperFrame", Vector3.zero, new Vector3(.68f, .32f, .12f), gold);
            ExitBlock(control, "Face", Vector3.forward * .075f, new Vector3(.60f, .25f, .035f), ink);
            var button = ExitBlock(root.transform, "Button", new Vector3(0, 1.09f, .04f), new Vector3(.34f, .13f, .12f), paper);
            var label = JinxCasinoS1PresentationBuilder.Label(root.transform, "ActionText", font, new Vector3(0, 1.43f, .098f), .56f, .07f, .45f);
            var status = JinxCasinoS1PresentationBuilder.Label(root.transform, "StateText", font, new Vector3(0, 1.31f, .098f), .56f, .15f, .32f);
            status.textWrappingMode = TextWrappingModes.Normal;
            var coupon = (GameObject)PrefabUtility.InstantiatePrefab(ticket, root.transform);
            coupon.transform.localPosition = new Vector3(0, .92f, .035f); coupon.transform.localScale = Vector3.one * .25f;
            component.Configure(owner, id, action, approach, control, label, status);
            // Button视觉保留固定Collider，其它Hall建筑/机台/玩家碰撞完全不触碰。
            ApplyExitReadability(root.transform);
            _ = button; return component;
        }
        private static Transform ExitChild(Transform parent, string name, Vector3 position)
        { var child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = position; return child; }
        private static Transform ExitBlock(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            child.name = name; child.SetParent(parent, false); child.localPosition = position; child.localScale = size;
            child.GetComponent<Renderer>().sharedMaterial = material; return child;
        }
    }
}
