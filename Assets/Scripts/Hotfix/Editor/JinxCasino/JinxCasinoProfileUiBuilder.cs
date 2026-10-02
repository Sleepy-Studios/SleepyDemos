using Hotfix.JinxCasino.Adapters.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        private static JinxCasinoProfilePresenter BuildProfilePanel(Transform parent, TMP_FontAsset font)
        {
            var panel = AdventurePanel("Profile", parent, new Vector2(1320, 950));
            var presenter = panel.gameObject.AddComponent<JinxCasinoProfilePresenter>();
            var serialized = new SerializedObject(presenter);
            Label("Title", panel, font, "俱乐部档案", new Vector2(30, -22), new Vector2(800, 70), 42);
            var back = AdventureBackButton(panel, font); SetReference(serialized, "backButton", back);
            SetReference(serialized, "summary", Label("Summary", panel, font, "声望与正式旅程", new Vector2(30, -105), new Vector2(1230, 92), 27));
            var tabs = serialized.FindProperty("tabs"); tabs.arraySize = 3;
            string[] labels = { "生涯记录", "装扮衣橱", "荒诞图鉴" };
            for (int index = 0; index < labels.Length; index++)
                tabs.GetArrayElementAtIndex(index).objectReferenceValue = Button("Tab" + index, panel, font, labels[index], new Vector2(30 + index * 420, -211), new Vector2(400, 72));
            var wardrobe = new GameObject("WardrobeControls", typeof(RectTransform)).GetComponent<RectTransform>(); wardrobe.SetParent(panel, false);
            wardrobe.anchorMin = new Vector2(0, 1); wardrobe.anchorMax = new Vector2(1, 1); wardrobe.pivot = new Vector2(0, 1);
            wardrobe.anchoredPosition = new Vector2(30, -308); wardrobe.sizeDelta = new Vector2(-60, 85);
            SetReference(serialized, "wardrobeControls", wardrobe.gameObject);
            var selectors = serialized.FindProperty("selectors"); selectors.arraySize = 4;
            for (int index = 0; index < 4; index++)
            {
                var selector = AdventureExchangeDropdown(wardrobe, font);
                selector.name = "Cosmetic" + index;
                var rect = selector.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(index * 313, 0); rect.sizeDelta = new Vector2(293, 76);
                selectors.GetArrayElementAtIndex(index).objectReferenceValue = selector;
            }
            SetReference(serialized, "content", AdventureScrollableText("ProfileContent", panel, font, "档案与图鉴", new Vector2(30, -414), new Vector2(1260, 424), 27));
            SetReference(serialized, "closeButton", Button("CloseProfile", panel, font, "回到旅程", new Vector2(30, -862), new Vector2(1260, 70)));
            serialized.ApplyModifiedPropertiesWithoutUndo(); panel.gameObject.SetActive(false); return presenter;
        }
    }
}
