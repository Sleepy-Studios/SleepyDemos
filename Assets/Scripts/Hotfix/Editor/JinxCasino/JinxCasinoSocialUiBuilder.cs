using System;
using System.Collections.Generic;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Adapters.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        private static void BuildLocalSocialUi(RectTransform adventure, TMP_FontAsset font, SerializedObject main, List<Button> panelBacks)
        {
            var panel = AdventurePanel("Social", adventure, new Vector2(1100, 690));
            var presenter = panel.gameObject.AddComponent<JinxCasinoSocialPresenter>(); var saved = new SerializedObject(presenter);
            SetReference(main, "socialPresenter", presenter); panelBacks.Add(AdventureBackButton(panel, font));
            Label("Title", panel, font, "快捷交流", new Vector2(32, -24), new Vector2(800, 64), 40);
            Label("Description", panel, font, "单人提示，仅自己可见。\n靠近机台后可以标记，回到场地查看标记。", new Vector2(32, -124), new Vector2(1030, 100), 28);
            var dropdown = AdventureExchangeDropdown(panel, font); ((RectTransform)dropdown.transform).anchoredPosition = new Vector2(32, -264);
            dropdown.name = "QuickMessage"; SetReference(saved, "messageDropdown", dropdown);
            SetReference(saved, "feedback", Label("Feedback", panel, font, "单人提示，仅自己可见。", new Vector2(32, -372), new Vector2(1030, 88), 26));
            SetReference(saved, "sendButton", Button("Send", panel, font, "显示快捷消息", new Vector2(32, -482), new Vector2(332, 76)));
            SetReference(saved, "markButton", Button("Mark", panel, font, "标记附近机台", new Vector2(384, -482), new Vector2(332, 76)));
            SetReference(saved, "clearButton", Button("Clear", panel, font, "清除标记与消息", new Vector2(736, -482), new Vector2(332, 76)));
            SetReference(saved, "closeButton", Button("Close", panel, font, "回到场地查看", new Vector2(32, -582), new Vector2(1036, 76)));
            saved.ApplyModifiedPropertiesWithoutUndo(); panel.gameObject.SetActive(false);
            var shortcuts = adventure.Find("LocalShortcuts") as RectTransform;
            if (shortcuts == null) throw new InvalidOperationException("本地交流需要先保存本地设置快捷行。");
            shortcuts.sizeDelta = new Vector2(558, 68);
            SetReference(main, "socialButton", Button("Social", shortcuts, font, "快捷交流", new Vector2(384, 0), new Vector2(174, 68)));
        }

        private static void ConfigureLocalSocialFeedback(JinxCasinoController controller)
        {
            string directory = Root + "/Prefabs/World"; EnsureFolder(directory);
            string path = directory + "/LocalSocialMarker.prefab";
            var root = new GameObject("LocalSocialMarker");
            try
            {
                InstantiateFormalModel("Item_map_radar", root.transform, EnsureFormalPalette());
                if (root.GetComponentsInChildren<Collider>(true).Length > 0 || root.GetComponentsInChildren<Camera>(true).Length > 0 || root.GetComponentsInChildren<AudioListener>(true).Length > 0)
                    throw new InvalidOperationException("世界标记只能使用无物理体的正式雷达模型。");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            var feedback = controller.GetComponent<JinxCasinoLocalSocialFeedback>();
            if (feedback == null) feedback = controller.gameObject.AddComponent<JinxCasinoLocalSocialFeedback>();
            feedback.Setup(controller, AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
    }
}
