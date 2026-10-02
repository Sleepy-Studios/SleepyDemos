using System.Collections.Generic;
using Hotfix.JinxCasino.Adapters.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        private static void BuildLocalSettingsUi(RectTransform adventure, TMP_FontAsset font, SerializedObject main, List<Button> panelBacks)
        {
            var root = new GameObject("LocalPreferences", typeof(RectTransform)); var rect = root.GetComponent<RectTransform>();
            rect.SetParent(adventure, false); Stretch(rect);
            var presenter = root.AddComponent<JinxCasinoLocalSettingsPresenter>(); var saved = new SerializedObject(presenter);
            SetReference(main, "localSettingsPresenter", presenter);
            SetReference(saved, "movePad", adventure.parent.Find("MovePad")?.GetComponent<JinxCasinoTouchPad>());
            SetReference(saved, "lookPad", adventure.parent.Find("LookPad")?.GetComponent<JinxCasinoTouchPad>());

            var settings = AdventurePanel("Settings", rect, new Vector2(1140, 900)); SetReference(saved, "settingsPanel", settings.gameObject);
            Label("Title", settings, font, "本地设置", new Vector2(32, -24), new Vector2(800, 64), 40);
            panelBacks.Add(AdventureBackButton(settings, font));
            SetReference(saved, "pcLabel", Label("PcLabel", settings, font, "鼠标视角灵敏度", new Vector2(36, -120), new Vector2(1050, 46), 28));
            SetReference(saved, "pcSensitivity", LocalSettingsSlider("MouseSensitivity", settings, new Vector2(36, -177), 0.25f, 3, 1));
            SetReference(saved, "touchLabel", Label("TouchLabel", settings, font, "触控视角灵敏度", new Vector2(36, -275), new Vector2(1050, 46), 28));
            SetReference(saved, "touchSensitivity", LocalSettingsSlider("TouchSensitivity", settings, new Vector2(36, -332), 0.25f, 3, 1));
            SetReference(saved, "volumeLabel", Label("VolumeLabel", settings, font, "本场景音量", new Vector2(36, -430), new Vector2(1050, 46), 28));
            SetReference(saved, "volume", LocalSettingsSlider("Volume", settings, new Vector2(36, -487), 0, 1, 0.7f));
            SetReference(saved, "muted", LocalSettingsToggle("Mute", settings, font, "静音", new Vector2(36, -595)));
            SetReference(saved, "leftHanded", LocalSettingsToggle("LeftHanded", settings, font, "左手视角 · 右侧移动", new Vector2(574, -595)));
            SetReference(saved, "settingsFeedback", Label("Feedback", settings, font, "只保存本机偏好。", new Vector2(36, -688), new Vector2(1050, 70), 24));
            SetReference(saved, "defaultsButton", Button("Defaults", settings, font, "预览默认设置", new Vector2(36, -800), new Vector2(330, 68)));
            SetReference(saved, "saveButton", Button("Save", settings, font, "保存本地设置", new Vector2(397, -800), new Vector2(330, 68)));
            SetReference(saved, "settingsCloseButton", Button("Close", settings, font, "关闭 · 撤销未保存预览", new Vector2(758, -800), new Vector2(346, 68)));

            var emotes = AdventurePanel("Emotes", rect, new Vector2(1100, 650)); SetReference(saved, "emotePanel", emotes.gameObject);
            Label("Title", emotes, font, "表情动作", new Vector2(32, -24), new Vector2(760, 64), 40);
            panelBacks.Add(AdventureBackButton(emotes, font));
            Label("Hint", emotes, font, "选择已解锁动作，让自己的纸片角色动起来。\n这里播放动作；装备与解锁进度仍由档案管理。", new Vector2(32, -120), new Vector2(1030, 100), 28);
            var dropdown = AdventureExchangeDropdown(emotes, font); ((RectTransform)dropdown.transform).anchoredPosition = new Vector2(32, -260);
            dropdown.name = "EmoteChoice"; SetReference(saved, "emoteDropdown", dropdown);
            SetReference(saved, "emoteFeedback", Label("Feedback", emotes, font, "选择一种动作。", new Vector2(32, -370), new Vector2(1030, 110), 26));
            SetReference(saved, "playEmoteButton", Button("Play", emotes, font, "播放表情", new Vector2(32, -538), new Vector2(500, 76)));
            SetReference(saved, "emoteCloseButton", Button("Close", emotes, font, "回到场地", new Vector2(568, -538), new Vector2(500, 76)));
            saved.ApplyModifiedPropertiesWithoutUndo(); root.SetActive(false);

            var shortcuts = Panel("LocalShortcuts", adventure, new Vector2(0, 28), new Vector2(366, 68), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Color.clear);
            shortcuts.GetComponent<Image>().raycastTarget = false; SetReference(main, "localShortcutPanel", shortcuts.gameObject);
            SetReference(main, "settingsButton", Button("Settings", shortcuts, font, "本地设置", new Vector2(0, 0), new Vector2(174, 68)));
            SetReference(main, "emoteButton", Button("Emotes", shortcuts, font, "表情动作", new Vector2(192, 0), new Vector2(174, 68)));
        }

        private static Slider LocalSettingsSlider(string name, Transform parent, Vector2 position, float minimum, float maximum, float value)
        {
            var rect = Panel(name, parent, position, new Vector2(1068, 68), new Vector2(0, 1), new Vector2(0, 1), new Color(0.08f, 0.13f, 0.18f));
            rect.GetComponent<Image>().raycastTarget = true;
            var track = Panel("Track", rect, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, new Color(0.15f, 0.24f, 0.31f));
            track.anchorMin = new Vector2(0, 0.35f); track.anchorMax = new Vector2(1, 0.65f); track.offsetMin = new Vector2(24, 0); track.offsetMax = new Vector2(-24, 0);
            var fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>(); fillArea.SetParent(rect, false); Stretch(fillArea);
            fillArea.offsetMin = new Vector2(24, 24); fillArea.offsetMax = new Vector2(-24, -24);
            var fill = Panel("Fill", fillArea, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, new Color(0.12f, 0.74f, 0.72f)); Stretch(fill); fill.GetComponent<Image>().raycastTarget = false;
            var handleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>(); handleArea.SetParent(rect, false); Stretch(handleArea);
            handleArea.offsetMin = new Vector2(24, 0); handleArea.offsetMax = new Vector2(-24, 0);
            var handle = Panel("Handle", handleArea, Vector2.zero, new Vector2(48, 60), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.68f, 0.96f, 0.96f));
            handle.GetComponent<Image>().raycastTarget = true;
            var slider = rect.gameObject.AddComponent<Slider>(); slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = minimum; slider.maxValue = maximum; slider.SetValueWithoutNotify(value);
            return slider;
        }

        private static Toggle LocalSettingsToggle(string name, Transform parent, TMP_FontAsset font, string text, Vector2 position)
        {
            var rect = Panel(name, parent, position, new Vector2(530, 68), new Vector2(0, 1), new Vector2(0, 1), new Color(0.1f, 0.17f, 0.23f));
            rect.GetComponent<Image>().raycastTarget = true;
            var mark = Panel("Check", rect, new Vector2(16, -15), new Vector2(38, 38), new Vector2(0, 1), new Vector2(0, 1), new Color(0.2f, 0.85f, 0.7f));
            mark.GetComponent<Image>().raycastTarget = false;
            Label("Label", rect, font, text, new Vector2(74, -10), new Vector2(448, 48), 27).raycastTarget = false;
            var toggle = rect.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = rect.GetComponent<Image>(); toggle.graphic = mark.GetComponent<Image>(); toggle.SetIsOnWithoutNotify(false);
            return toggle;
        }
    }
}
