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
        /// 只装配保存HUD的教学控件，不重建场景、MVC字段或其它已保存控件。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/装配互动教学HUD")]
        public static void UpdateImmersionTutorialHud()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式装配教学HUD。");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == ImmersionHudPath && stage.scene.isDirty) throw new InvalidOperationException("请先保存HUD的人工修改。");
            var root = PrefabUtility.LoadPrefabContents(ImmersionHudPath);
            try
            {
                var presenter = root.GetComponent<JinxCasinoImmersionHudPresenter>();
                if (presenter == null) throw new InvalidOperationException("保存HUD缺少宿主。");
                var saved = new SerializedObject(presenter);
                var menu = RequiredTutorialRect(root.transform, "MainMenu"); var paused = RequiredTutorialRect(root.transform, "PauseMenu");
                var hud = RequiredTutorialRect(root.transform, "FieldHud");
                var font = root.GetComponentInChildren<TMP_Text>(true)?.font;
                if (font == null) throw new InvalidOperationException("保存HUD缺少字体引用。");
                var start = menu.Find("Start").GetComponent<Button>(); var practice = menu.Find("Practice").GetComponent<Button>();
                var savedLoad = menu.Find("LoadAdventure")?.GetComponent<Button>();
                // 三槽入口已经占有四按钮布局时，教学更新不能把它改回三按钮布局。
                if (savedLoad == null)
                {
                    PlaceTutorialRect((RectTransform)start.transform, new Vector2(48, -440), new Vector2(550, 76));
                    PlaceTutorialRect((RectTransform)practice.transform, new Vector2(48, -532), new Vector2(550, 76));
                }
                var tutorial = EnsureTutorialButton(menu, "Tutorial", font, "互动教学", new Vector2(48, -624), new Vector2(550, 76));
                SetReference(saved, "tutorialStartButton", tutorial);
                if (savedLoad != null) TutorialNavigation(start, practice, tutorial, savedLoad);
                else TutorialNavigation(start, practice, tutorial);
                SetReference(saved, "tutorialMainFeedbackText", menu.Find("Footer").GetComponent<TMP_Text>());
                SetReference(saved, "tutorialPauseFeedbackText", paused.Find("Hint").GetComponent<TMP_Text>());
                var skip = EnsureTutorialButton(paused, "TutorialSkip", font, "跳过教学", new Vector2(48, -452), new Vector2(554, 74));
                var review = EnsureTutorialButton(paused, "TutorialReview", font, "教学后选择", new Vector2(48, -452), new Vector2(554, 74));
                var retry = EnsureTutorialButton(paused, "TutorialRetry", font, "重新教学", new Vector2(48, -542), new Vector2(554, 74));
                SetReference(saved, "tutorialSkipButton", skip); SetReference(saved, "tutorialReviewButton", review); SetReference(saved, "tutorialRetryButton", retry);
                var strip = EnsureTutorialPanel(hud, "TutorialStrip", new Vector2(36, -98), new Vector2(960, 156), new Vector2(0, 1), new Vector2(0, 1));
                SetReference(saved, "tutorialStrip", strip.gameObject);
                SetReference(saved, "tutorialHintText", EnsureTutorialLabel(strip, "Hint", font, "互动教学", new Vector2(20, -12), new Vector2(920, 48), 26));
                SetReference(saved, "tutorialDirectionText", EnsureTutorialLabel(strip, "Direction", font, "", new Vector2(20, -64), new Vector2(920, 34), 23));
                SetReference(saved, "tutorialFeedbackText", EnsureTutorialLabel(strip, "Feedback", font, "", new Vector2(20, -106), new Vector2(920, 38), 22));
                // 只调整教学条自身，保留机台和其它HUD的人工布局。
                strip.sizeDelta = new Vector2(700, 112);
                PlaceTutorialRect(RequiredTutorialRect(strip, "Hint"), new Vector2(20, -12), new Vector2(660, 64));
                PlaceTutorialRect(RequiredTutorialRect(strip, "Direction"), new Vector2(20, -76), new Vector2(660, 30));
                PlaceTutorialRect(RequiredTutorialRect(strip, "Feedback"), new Vector2(20, -112), new Vector2(660, 34));
                var ready = EnsureTutorialPanel(root.transform, "TutorialReady", Vector2.zero, new Vector2(640, 360), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
                SetReference(saved, "tutorialReadyPanel", ready.gameObject);
                EnsureTutorialLabel(ready, "Title", font, "三台机台都学会了", new Vector2(36, -24), new Vector2(568, 62), 36);
                EnsureTutorialLabel(ready, "Hint", font, "练习不会计入正式成长。准备好后明确完成教学。", new Vector2(36, -94), new Vector2(568, 70), 25);
                var complete = EnsureTutorialButton(ready, "Complete", font, "完成教学", new Vector2(36, -188), new Vector2(568, 64));
                var back = EnsureTutorialButton(ready, "Back", font, "稍后再说", new Vector2(36, -272), new Vector2(568, 64));
                SetReference(saved, "tutorialCompleteButton", complete); SetReference(saved, "tutorialReadyBackButton", back); TutorialNavigation(complete, back);
                var choice = EnsureTutorialPanel(root.transform, "TutorialChoice", Vector2.zero, new Vector2(680, 430), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
                SetReference(saved, "tutorialChoicePanel", choice.gameObject);
                EnsureTutorialLabel(choice, "Title", font, "下一步由你选择", new Vector2(36, -24), new Vector2(608, 62), 36);
                SetReference(saved, "tutorialChoiceMessageText", EnsureTutorialLabel(choice, "Hint", font, "", new Vector2(36, -96), new Vector2(608, 110), 25));
                var keep = EnsureTutorialButton(choice, "Continue", font, "继续自由练习", new Vector2(36, -240), new Vector2(608, 70));
                var standard = EnsureTutorialButton(choice, "Standard", font, "开始正式冒险", new Vector2(36, -332), new Vector2(608, 70));
                SetReference(saved, "tutorialContinueButton", keep); SetReference(saved, "tutorialStandardButton", standard); TutorialNavigation(keep, standard);
                var confirm = EnsureTutorialPanel(root.transform, "TutorialConfirm", Vector2.zero, new Vector2(680, 440), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
                SetReference(saved, "tutorialConfirmPanel", confirm.gameObject);
                SetReference(saved, "tutorialConfirmTitleText", EnsureTutorialLabel(confirm, "Title", font, "", new Vector2(36, -24), new Vector2(608, 62), 36));
                SetReference(saved, "tutorialConfirmMessageText", EnsureTutorialLabel(confirm, "Hint", font, "", new Vector2(36, -98), new Vector2(608, 104), 25));
                SetReference(saved, "tutorialConfirmFeedbackText", EnsureTutorialLabel(confirm, "Feedback", font, "", new Vector2(36, -212), new Vector2(608, 46), 22));
                var yes = EnsureTutorialButton(confirm, "Confirm", font, "确认开始", new Vector2(36, -282), new Vector2(286, 92));
                var no = EnsureTutorialButton(confirm, "Cancel", font, "返回", new Vector2(358, -282), new Vector2(286, 92));
                SetReference(saved, "tutorialConfirmButton", yes); SetReference(saved, "tutorialCancelButton", no); TutorialNavigation(no, yes);
                foreach (var button in new[] { start, practice, tutorial, paused.Find("Resume").GetComponent<Button>(), paused.Find("Leave").GetComponent<Button>(), skip, review, retry, complete, back, keep, standard, yes, no })
                {
                    var relay = button.GetComponent<JinxCasinoTutorialCancelRelay>() ?? button.gameObject.AddComponent<JinxCasinoTutorialCancelRelay>();
                    relay.Configure(presenter);
                }
                saved.ApplyModifiedPropertiesWithoutUndo();
                foreach (var item in new[] { strip.gameObject, ready.gameObject, choice.gameObject, confirm.gameObject, skip.gameObject, review.gameObject, retry.gameObject }) item.SetActive(false);
                if (PrefabUtility.SaveAsPrefabAsset(root, ImmersionHudPath) == null) throw new InvalidOperationException("保存教学HUD失败。");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static RectTransform RequiredTutorialRect(Transform parent, string name)
        { var child = parent.Find(name) as RectTransform; return child != null ? child : throw new InvalidOperationException("保存HUD缺少：" + name); }
        private static void PlaceTutorialRect(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static RectTransform EnsureTutorialPanel(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchor, Vector2 pivot)
        {
            var current = parent.Find(name) as RectTransform;
            return current != null ? current : Panel(name, parent, position, size, anchor, pivot, new Color(.055f, .09f, .15f, .94f));
        }
        private static Button EnsureTutorialButton(Transform parent, string name, TMP_FontAsset font, string caption, Vector2 position, Vector2 size)
        { var current = parent.Find(name); return current != null ? current.GetComponent<Button>() ?? throw new InvalidOperationException("保存按钮缺少Button：" + name) : Button(name, parent, font, caption, position, size); }
        private static TMP_Text EnsureTutorialLabel(Transform parent, string name, TMP_FontAsset font, string caption, Vector2 position, Vector2 size, float fontSize)
        { var current = parent.Find(name); return current != null ? current.GetComponent<TMP_Text>() ?? throw new InvalidOperationException("保存文字缺少TMP：" + name) : Label(name, parent, font, caption, position, size, fontSize); }
        private static void TutorialNavigation(params Button[] buttons)
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
