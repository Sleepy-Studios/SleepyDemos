using System;
using Hotfix.JinxCasino.Adapters.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        /// <summary>分步保存标准结果卡；不重建现有菜单、场景或教学/存档控件。</summary>
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/装配标准结局HUD")]
        public static void UpdateImmersionStandardEndingHud()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式装配结局HUD。");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == ImmersionHudPath && stage.scene.isDirty) throw new InvalidOperationException("请先保存HUD的人工修改。");
            var root = PrefabUtility.LoadPrefabContents(ImmersionHudPath);
            try
            {
                var presenter = root.GetComponent<JinxCasinoImmersionHudPresenter>();
                if (presenter == null) throw new InvalidOperationException("保存HUD缺少宿主。");
                var saved = new SerializedObject(presenter);
                var font = root.GetComponentInChildren<TMP_Text>(true)?.font;
                if (font == null) throw new InvalidOperationException("保存HUD缺少字体引用。");
                var card = root.transform.Find("StandardEnding") as RectTransform;
                if (card == null) card = Panel("StandardEnding", root.transform, Vector2.zero, new Vector2(840, 690), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Color(.055f, .09f, .15f, .97f));
                SetReference(saved, "standardEndingPanel", card.gameObject);
                SetReference(saved, "standardEndingTitleText", EndingLabel(card, "Title", font, "", new Vector2(40, -28), new Vector2(760, 64), 36));
                SetReference(saved, "standardEndingMessageText", EndingLabel(card, "Message", font, "", new Vector2(40, -108), new Vector2(760, 106), 26));
                SetReference(saved, "standardEndingStatsText", EndingLabel(card, "Stats", font, "", new Vector2(40, -238), new Vector2(760, 166), 30));
                SetReference(saved, "standardEndingProfileText", EndingLabel(card, "Profile", font, "", new Vector2(40, -432), new Vector2(760, 106), 24));
                var save = EndingButton(card, "Save", font, "保存旅程", new Vector2(40, -568), new Vector2(370, 84));
                var leave = EndingButton(card, "ReturnToHub", font, "返回大厅", new Vector2(430, -568), new Vector2(370, 84));
                SetReference(saved, "standardEndingSaveButton", save); SetReference(saved, "standardEndingReturnButton", leave);
                foreach (var button in new[] { save, leave })
                {
                    var other = button == save ? leave : save;
                    var navigation = button.navigation; navigation.mode = Navigation.Mode.Explicit;
                    navigation.selectOnLeft = navigation.selectOnRight = navigation.selectOnUp = navigation.selectOnDown = other;
                    button.navigation = navigation;
                    var relay = button.GetComponent<JinxCasinoEndingCancelRelay>() ?? button.gameObject.AddComponent<JinxCasinoEndingCancelRelay>();
                    relay.Configure(presenter);
                }
                saved.ApplyModifiedPropertiesWithoutUndo(); card.gameObject.SetActive(false);
                if (PrefabUtility.SaveAsPrefabAsset(root, ImmersionHudPath) == null) throw new InvalidOperationException("保存结局HUD失败。");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static TMP_Text EndingLabel(Transform parent, string name, TMP_FontAsset font, string text, Vector2 position, Vector2 size, float fontSize)
        { var current = parent.Find(name); return current != null ? current.GetComponent<TMP_Text>() ?? throw new InvalidOperationException("结局文字缺少TMP：" + name) : Label(name, parent, font, text, position, size, fontSize); }
        private static Button EndingButton(Transform parent, string name, TMP_FontAsset font, string text, Vector2 position, Vector2 size)
        { var current = parent.Find(name); return current != null ? current.GetComponent<Button>() ?? throw new InvalidOperationException("结局控件缺少Button：" + name) : Button(name, parent, font, text, position, size); }
    }
}
