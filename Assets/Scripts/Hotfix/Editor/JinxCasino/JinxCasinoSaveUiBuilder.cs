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
        /// <summary>为现有沉浸 HUD 分步保存三槽菜单，保留 Prefab 与场景 GUID。</summary>
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/装配三槽存档HUD")]
        public static void UpdateImmersionSaveHud()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式装配存档HUD。");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == ImmersionHudPath && stage.scene.isDirty) throw new InvalidOperationException("请先保存HUD的人工修改。");
            var root = PrefabUtility.LoadPrefabContents(ImmersionHudPath);
            try
            {
                var presenter = root.GetComponent<JinxCasinoImmersionHudPresenter>();
                if (presenter == null) throw new InvalidOperationException("保存HUD缺少宿主。");
                var saved = new SerializedObject(presenter);
                var menu = RequiredSaveRect(root.transform, "MainMenu");
                var paused = RequiredSaveRect(root.transform, "PauseMenu");
                var font = root.GetComponentInChildren<TMP_Text>(true)?.font;
                if (font == null) throw new InvalidOperationException("保存HUD缺少字体引用。");
                var start = menu.Find("Start").GetComponent<Button>(); var practice = menu.Find("Practice").GetComponent<Button>();
                var tutorial = menu.Find("Tutorial")?.GetComponent<Button>();
                if (tutorial == null) throw new InvalidOperationException("请先装配互动教学HUD，再装配存档HUD。");
                SavePlace((RectTransform)start.transform, new Vector2(48, -418), new Vector2(550, 64));
                SavePlace((RectTransform)practice.transform, new Vector2(48, -496), new Vector2(550, 64));
                SavePlace((RectTransform)tutorial.transform, new Vector2(48, -574), new Vector2(550, 64));
                var mainLoad = SaveButton(menu, "LoadAdventure", font, "继续存档", new Vector2(48, -652), new Vector2(550, 64));
                SetReference(saved, "saveMainLoadButton", mainLoad);
                SavePlace(RequiredSaveRect(menu, "Footer"), new Vector2(48, -734), new Vector2(560, 36));
                SaveButtonNavigation(start, practice, tutorial, mainLoad);
                var resume = paused.Find("Resume").GetComponent<Button>(); var leave = paused.Find("Leave").GetComponent<Button>();
                SavePlace((RectTransform)resume.transform, new Vector2(48, -244), new Vector2(554, 64));
                SavePlace((RectTransform)leave.transform, new Vector2(48, -410), new Vector2(554, 64));
                var write = SaveButton(paused, "SaveAdventure", font, "保存旅程", new Vector2(48, -326), new Vector2(268, 64));
                var load = SaveButton(paused, "LoadAdventure", font, "读取存档", new Vector2(334, -326), new Vector2(268, 64));
                SetReference(saved, "savePauseSaveButton", write); SetReference(saved, "savePauseLoadButton", load);
                // 只调整本次菜单按钮的占位，保留已经装配的教学引用、样式及所有场景资源。
                foreach (string name in new[] { "TutorialSkip", "TutorialReview" })
                    SavePlace(RequiredSaveRect(paused, name), new Vector2(48, -490), new Vector2(554, 64));
                SavePlace(RequiredSaveRect(paused, "TutorialRetry"), new Vector2(48, -570), new Vector2(554, 64));
                SaveButtonNavigation(resume, write, load, leave);
                var slots = SavePanel(root.transform, "SaveSlots", new Vector2(800, 740));
                SetReference(saved, "saveSlotsPanel", slots.gameObject);
                SetReference(saved, "saveTitleText", SaveLabel(slots, "Title", font, "继续一段旅程", new Vector2(36, -24), new Vector2(728, 62), 34));
                var slotButtons = new Button[3];
                for (int slot = 1; slot <= 3; slot++)
                {
                    var button = SaveButton(slots, "Slot" + slot, font, "存档 " + slot, new Vector2(36, -108 - (slot - 1) * 150), new Vector2(728, 132));
                    var caption = button.GetComponentInChildren<TMP_Text>(true);
                    caption.fontSize = 25; caption.alignment = TextAlignmentOptions.Center;
                    SetReference(saved, "saveSlot" + slot + "Button", button); SetReference(saved, "saveSlot" + slot + "Text", caption);
                    slotButtons[slot - 1] = button;
                }
                SetReference(saved, "saveFeedbackText", SaveLabel(slots, "Feedback", font, "", new Vector2(36, -554), new Vector2(728, 48), 22));
                var back = SaveButton(slots, "Back", font, "返回", new Vector2(36, -616), new Vector2(728, 76));
                SetReference(saved, "saveBackButton", back); SaveButtonNavigation(slotButtons[0], slotButtons[1], slotButtons[2], back);
                var confirm = SavePanel(root.transform, "SaveConfirm", new Vector2(780, 440));
                SetReference(saved, "saveConfirmPanel", confirm.gameObject);
                SetReference(saved, "saveConfirmTitleText", SaveLabel(confirm, "Title", font, "", new Vector2(36, -24), new Vector2(708, 62), 34));
                SetReference(saved, "saveConfirmMessageText", SaveLabel(confirm, "Message", font, "", new Vector2(36, -100), new Vector2(708, 138), 25));
                SetReference(saved, "saveConfirmFeedbackText", SaveLabel(confirm, "Feedback", font, "", new Vector2(36, -244), new Vector2(708, 46), 22));
                var yes = SaveButton(confirm, "Confirm", font, "确认", new Vector2(36, -312), new Vector2(338, 84));
                var no = SaveButton(confirm, "Cancel", font, "返回", new Vector2(406, -312), new Vector2(338, 84));
                SetReference(saved, "saveConfirmButton", yes); SetReference(saved, "saveCancelButton", no); SaveButtonNavigation(no, yes);
                foreach (var button in new[] { mainLoad, write, load, slotButtons[0], slotButtons[1], slotButtons[2], back, yes, no })
                {
                    var relay = button.GetComponent<JinxCasinoSaveCancelRelay>() ?? button.gameObject.AddComponent<JinxCasinoSaveCancelRelay>();
                    relay.Configure(presenter);
                }
                saved.ApplyModifiedPropertiesWithoutUndo();
                slots.gameObject.SetActive(false); confirm.gameObject.SetActive(false); write.gameObject.SetActive(false); load.gameObject.SetActive(false);
                if (PrefabUtility.SaveAsPrefabAsset(root, ImmersionHudPath) == null) throw new InvalidOperationException("保存三槽HUD失败。");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static RectTransform RequiredSaveRect(Transform parent, string name)
        { var child = parent.Find(name) as RectTransform; return child != null ? child : throw new InvalidOperationException("保存HUD缺少：" + name); }
        private static void SavePlace(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static RectTransform SavePanel(Transform parent, string name, Vector2 size)
        { var current = parent.Find(name) as RectTransform; return current != null ? current : Panel(name, parent, Vector2.zero, size, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Color(.055f, .09f, .15f, .97f)); }
        private static Button SaveButton(Transform parent, string name, TMP_FontAsset font, string caption, Vector2 position, Vector2 size)
        { var current = parent.Find(name); return current != null ? current.GetComponent<Button>() ?? throw new InvalidOperationException("保存按钮缺少Button：" + name) : Button(name, parent, font, caption, position, size); }
        private static TMP_Text SaveLabel(Transform parent, string name, TMP_FontAsset font, string caption, Vector2 position, Vector2 size, float fontSize)
        { var current = parent.Find(name); return current != null ? current.GetComponent<TMP_Text>() ?? throw new InvalidOperationException("保存文字缺少TMP：" + name) : Label(name, parent, font, caption, position, size, fontSize); }
        private static void SaveButtonNavigation(params Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var navigation = buttons[i].navigation; navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = navigation.selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length];
                navigation.selectOnDown = navigation.selectOnRight = buttons[(i + 1) % buttons.Length]; buttons[i].navigation = navigation;
            }
        }
    }
}
