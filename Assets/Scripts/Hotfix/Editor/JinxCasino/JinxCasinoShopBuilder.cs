using System;
using System.Collections.Generic;
using System.Linq;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        /// 为现有售票柜台装配商品实物、报价牌与购买/库存按钮；已有柜台不重建。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/装配实体补给柜台")]
        public static void BuildImmersionShop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式装配柜台。");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ImmersionScenePath); bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("请先保存场景人工修改。");
            if (opened) scene = EditorSceneManager.OpenScene(ImmersionScenePath, OpenSceneMode.Additive);
            try
            {
                if (SceneManager.GetActiveScene() != scene) SceneManager.SetActiveScene(scene);
                var owner = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<JinxCasinoController>(true)).Single();
                var counter = owner.GetComponentInChildren<JinxCasinoShopCounter>(true);
                if (counter == null) counter = CreateShopCounter(owner.transform);
                owner.ConfigureShopCounter(counter);
                var settings = AssetDatabase.LoadAssetAtPath<JinxCasinoGameSettings>(Root + "/Data/ImmersionSettings.asset");
                var saved = new SerializedObject(settings); var items = saved.FindProperty("adventure").FindPropertyRelative("ShopItemIds");
                string[] ids = { "duo_wrench", "redraw_card", "stop_loss" }; items.arraySize = ids.Length;
                for (int i = 0; i < ids.Length; i++) items.GetArrayElementAtIndex(i).stringValue = ids[i];
                saved.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(settings);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("柜台场景保存失败。");
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// 只更新柜台商品缩放、报价牌及聚焦挂点，保留大厅和其它人工布局。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/调整柜台可读布局")]
        public static void UpdateShopReadability()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式调整柜台。");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ImmersionScenePath); bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("请先保存场景人工修改。");
            if (opened) scene = EditorSceneManager.OpenScene(ImmersionScenePath, OpenSceneMode.Additive);
            try
            {
                var counter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<JinxCasinoShopCounter>(true)).Single();
                ApplyShopReadability(counter.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("柜台场景保存失败。");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ApplyShopReadability(Transform root)
        {
            foreach (string product in new[] { "Product0", "Product1", "Product2" })
            {
                var shelf = root.Find(product);
                foreach (Transform child in shelf) if (child.name.StartsWith("Item_", StringComparison.Ordinal)) child.localScale = Vector3.one * .5f;
            }
            root.Find("QuoteFrame").localPosition = new Vector3(0, 2.10f, .65f);
            root.Find("QuoteFace").localPosition = new Vector3(0, 2.10f, .689f);
            root.Find("Quote").localPosition = new Vector3(0, 2.10f, .708f);
            root.Find("QuoteFrame").localScale = new Vector3(1.65f, .30f, .055f);
            root.Find("QuoteFace").localScale = new Vector3(1.58f, .25f, .025f);
            var quote = root.Find("Quote").GetComponent<TMP_Text>();
            quote.rectTransform.sizeDelta = new Vector2(1.48f, .23f); quote.fontSize = .34f;
            var focus = root.Find("FocusPose"); focus.localPosition = new Vector3(0, 1.95f, 1.9f);
            focus.rotation = Quaternion.LookRotation(root.TransformPoint(new Vector3(0, 1.66f, 0)) - focus.position, Vector3.up);
        }

        private static JinxCasinoShopCounter CreateShopCounter(Transform parent)
        {
            foreach (string id in new[] { "duo_wrench", "redraw_card", "stop_loss" })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/Item_" + id + ".fbx") == null)
                    throw new InvalidOperationException("缺少柜台商品模型：" + id);
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) == null) throw new InvalidOperationException("缺少柜台字体。");
            foreach (string material in new[] { "CopperGold", "InkBlue", "Mint" })
                if (AssetDatabase.LoadAssetAtPath<Material>(JinxCasinoImmersionArtBuilder.Root + "/Materials/" + material + ".mat") == null)
                    throw new InvalidOperationException("缺少柜台材质：" + material);
            var root = new GameObject("SupplyCounter").transform; root.SetParent(parent, false);
            try
            {
                root.localPosition = new Vector3(-4.85f, 0, -3.6f); root.localRotation = Quaternion.Euler(0, 180, 0);
                var counter = root.gameObject.AddComponent<JinxCasinoShopCounter>();
                var approach = ShopAnchor(root, "Approach", new Vector3(0, 0, 1.1f));
                var focus = ShopAnchor(root, "FocusPose", new Vector3(0, 2.25f, 1.9f));
                focus.rotation = Quaternion.LookRotation(root.TransformPoint(new Vector3(0, 1.45f, 0)) - focus.position, Vector3.up);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                var gold = AssetDatabase.LoadAssetAtPath<Material>(JinxCasinoImmersionArtBuilder.Root + "/Materials/CopperGold.mat");
                var ink = AssetDatabase.LoadAssetAtPath<Material>(JinxCasinoImmersionArtBuilder.Root + "/Materials/InkBlue.mat");
                var mint = AssetDatabase.LoadAssetAtPath<Material>(JinxCasinoImmersionArtBuilder.Root + "/Materials/Mint.mat");
                var controls = new List<JinxCasinoTableTarget>();
                string[] products = { "duo_wrench", "redraw_card", "stop_loss" };
                for (int i = 0; i < products.Length; i++)
                {
                    float x = (1 - i) * .56f;
                    var shelf = ShopAnchor(root, "Product" + i, new Vector3(x, 1.31f, -.04f));
                    ShopBlock(shelf, "Stand", new Vector3(0, -.005f, 0), new Vector3(.46f, .035f, .38f), ink);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/Item_" + products[i] + ".fbx");
                    if (prefab == null) throw new InvalidOperationException("缺少商品模型：" + products[i]);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, shelf); model.transform.localPosition = new Vector3(0, .035f, 0);
                    var label = JinxCasinoS1PresentationBuilder.Label(root, "ProductName" + i, font, new Vector3(x, 1.405f, .22f), .51f, .075f, .34f);
                    var item = CasinoContentCatalog.FindItem(products[i]); label.text = item.Name + " " + item.Price;
                    controls.Add(ShopTarget(root, "product" + i, JinxCasinoTableAction.SelectProduct, i, i * 10,
                        new Vector3(x, 1.5f, -.04f), new Vector3(.46f, .37f, .40f), model.GetComponentsInChildren<Renderer>(true), model.transform));
                }
                ShopBlock(root, "QuoteFrame", new Vector3(0, 1.88f, -.36f), new Vector3(1.78f, .57f, .055f), gold);
                ShopBlock(root, "QuoteFace", new Vector3(0, 1.88f, -.321f), new Vector3(1.71f, .50f, .025f), ink);
                var quote = JinxCasinoS1PresentationBuilder.Label(root, "Quote", font, new Vector3(0, 1.88f, -.302f), 1.62f, .44f, .40f);
                quote.textWrappingMode = TextWrappingModes.Normal; quote.alignment = TextAlignmentOptions.TopLeft;
                quote.text = "好运补给柜台\n先选商品，再确认购买。";
                TMP_Text useText = null;
                for (int i = 0; i < 3; i++)
                {
                    float x = (1 - i) * .56f;
                    var button = ShopBlock(root, "CounterButton" + i, new Vector3(x, 1.33f, .40f), new Vector3(.48f, .11f, .18f), i == 0 ? gold : mint);
                    var label = JinxCasinoS1PresentationBuilder.Label(root, "CounterButtonText" + i, font, new Vector3(x, 1.33f, .497f), .46f, .085f, .37f);
                    label.text = i == 0 ? "确认购买" : i == 1 ? "使用库存" : "商品说明";
                    if (i == 1) useText = label;
                    controls.Add(ShopTarget(root, "action" + i, i == 0 ? JinxCasinoTableAction.PurchaseProduct : i == 1 ? JinxCasinoTableAction.UseProduct : JinxCasinoTableAction.Help,
                        0, 40 + i * 10, button.localPosition, new Vector3(.48f, .14f, .22f), new[] { button.GetComponent<Renderer>() }, button));
                    UnityEngine.Object.DestroyImmediate(button.GetComponent<Collider>());
                }
                var receipt = JinxCasinoS1PresentationBuilder.Label(root, "Receipt", font, new Vector3(0, 1.54f, .22f), 1.68f, .13f, .32f);
                receipt.textWrappingMode = TextWrappingModes.Normal;
                counter.Configure("s1.supply", approach, focus, products, controls.ToArray(), quote, receipt, useText);
                ApplyShopReadability(root);
                return counter;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
                throw;
            }
        }

        private static Transform ShopAnchor(Transform parent, string name, Vector3 position)
        { var root = new GameObject(name).transform; root.SetParent(parent, false); root.localPosition = position; return root; }
        private static Transform ShopBlock(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube).transform; root.name = name; root.SetParent(parent, false);
            root.localPosition = position; root.localScale = size; root.GetComponent<Renderer>().sharedMaterial = material; return root;
        }
        private static JinxCasinoTableTarget ShopTarget(Transform root, string id, JinxCasinoTableAction action, int value, int order,
            Vector3 position, Vector3 size, Renderer[] renderers, Transform press)
        {
            var target = ShopAnchor(root, id, position).gameObject.AddComponent<JinxCasinoTableTarget>();
            target.gameObject.AddComponent<BoxCollider>().size = size;
            target.Configure("s1.supply." + id, action, value, order, renderers, press); return target;
        }
    }
}
