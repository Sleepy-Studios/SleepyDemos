using System;
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
        private static bool buildAdventure;
        private static bool buildFullRegions;

        private static JinxCasinoAdventurePresenter BuildAdventureHud(Transform parent, TMP_FontAsset font)
        {
            var root = new GameObject("Adventure", typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>(); rect.SetParent(parent, false); Stretch(rect);
            var presenter = root.AddComponent<JinxCasinoAdventurePresenter>();
            var serialized = new SerializedObject(presenter);
            var edgeArray = serialized.FindProperty("inkEdges"); edgeArray.arraySize = 4;
            for (int side = 0; side < 4; side++)
            {
                var edge = new GameObject("InkEdge" + side, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                edge.SetParent(rect, false);
                bool vertical = side < 2;
                edge.anchorMin = side == 0 || side == 2 ? Vector2.zero : vertical ? new Vector2(1, 0) : new Vector2(0, 1);
                edge.anchorMax = side == 0 ? new Vector2(0, 1) : side == 1 ? Vector2.one : side == 2 ? new Vector2(1, 0) : Vector2.one;
                edge.pivot = side == 0 || side == 2 ? Vector2.zero : side == 1 ? new Vector2(1, 0) : new Vector2(0, 1);
                edge.sizeDelta = vertical ? new Vector2(145, 0) : new Vector2(0, 85);
                var image = edge.GetComponent<Image>(); image.raycastTarget = false; image.color = new Color(0.06f, 0.015f, 0.11f, 0);
                edgeArray.GetArrayElementAtIndex(side).objectReferenceValue = image;
            }
            var panelBacks = new List<Button>();
            var header = Panel("AdventureHeader", rect, new Vector2(26, -24), new Vector2(1080, 160), new Vector2(0, 1), new Vector2(0, 1), new Color(0.035f, 0.045f, 0.085f, 0.92f));
            SetReference(serialized, "hudText", Label("Progress", header, font, "倒霉蛋俱乐部", new Vector2(20, -10), new Vector2(1030, 78), 30));
            SetReference(serialized, "statusText", Label("Feedback", header, font, "选择你的旅程。", new Vector2(20, -98), new Vector2(1030, 58), 24));
            FitAdventurePanel(header, new Vector2(1080, 160));
            var mission = Panel("MissionProgress", rect, new Vector2(26, -194), new Vector2(1080, 130), new Vector2(0, 1), new Vector2(0, 1), new Color(0.035f, 0.045f, 0.085f, 0.85f));
            SetReference(serialized, "missionText", Label("MissionState", mission, font, "场地任务：留意突发事件。", new Vector2(20, -10), new Vector2(1030, 112), 26));
            FitAdventurePanel(mission, new Vector2(1080, 130));
            var radar = Panel("Radar", rect, new Vector2(26, -340), new Vector2(760, 430), new Vector2(0, 1), new Vector2(0, 1), new Color(0.035f, 0.045f, 0.085f, 0.95f));
            SetReference(serialized, "radarPanel", radar.gameObject);
            SetReference(serialized, "radarText", AdventureScrollableText("RadarDirections", radar, font, "地图雷达", new Vector2(20, -12), new Vector2(720, 404), 26));
            FitAdventurePanel(radar, new Vector2(760, 430));
            radar.gameObject.SetActive(false);

            var toolbar = Panel("Toolbar", rect, new Vector2(-30, -210), new Vector2(290, 806), new Vector2(1, 1), new Vector2(1, 1), new Color(0.035f, 0.045f, 0.085f, 0.9f));
            string[] fields = { "interactButton", "resumeRoundButton", "shopButton", "slotsButton", "finishStageButton", "nextStageButton", "refillButton", "eventButton", "profileButton", "backButton" };
            string[] labels = { "交互 · E", "继续机台", "商店 / 背包", "保存 / 继续", "提前结算本区", "确认进入下一站", "练习补充筹码", "事件选择", "档案 / 装扮", "返回园区入口" };
            for (int index = 0; index < fields.Length; index++) SetReference(serialized, fields[index], Button(fields[index], toolbar, font, labels[index], new Vector2(12, -12 - index * 78), new Vector2(266, 68)));
            FitAdventurePanel(toolbar, new Vector2(290, 806));

            var menu = AdventurePanel("Menu", rect, new Vector2(960, 840));
            SetReference(serialized, "menuPanel", menu.gameObject);
            Label("MenuTitle", menu, font, "倒霉蛋俱乐部", new Vector2(36, -30), new Vector2(610, 80), 48);
            Label("MenuDescription", menu, font, "攒够本区额度，再明确选择下一步。\n机台、随机事件和道具一起决定你的旅程。\n开放内容按当前场景配置，协作机台提供单人助手。", new Vector2(36, -140), new Vector2(880, 170), 30);
            panelBacks.Add(AdventureBackButton(menu, font));
            SetReference(serialized, "standardButton", Button("Standard", menu, font, "标准冒险", new Vector2(50, -380), new Vector2(850, 85)));
            SetReference(serialized, "practiceButton", Button("Practice", menu, font, "单人练习", new Vector2(50, -480), new Vector2(410, 85)));
            SetReference(serialized, "endlessButton", Button("Endless", menu, font, "无尽挑战", new Vector2(490, -480), new Vector2(410, 85)));
            Label("ControlGuide", menu, font, "键鼠：WASD移动，鼠标右键转向，E交互。\n触控：左侧移动，右侧转向，点击交互。", new Vector2(50, -590), new Vector2(850, 95), 26);
            SetReference(serialized, "profileMenuButton", Button("MenuProfile", menu, font, "个人档案与装扮", new Vector2(50, -712), new Vector2(850, 76)));

            var machine = AdventurePanel("Machine", rect, new Vector2(1250, 1060));
            SetReference(serialized, "machinePanel", machine.gameObject);
            SetReference(serialized, "machineTitle", Label("MachineTitle", machine, font, "机台", new Vector2(28, -20), new Vector2(900, 70), 42));
            panelBacks.Add(AdventureBackButton(machine, font));
            SetReference(serialized, "machineRules", AdventureScrollableText("Rules", machine, font, "投入前的规则与收益。", new Vector2(28, -108), new Vector2(750, 260), 25));
            SetReference(serialized, "machineResult", AdventureScrollableText("MachineState", machine, font, "确认投入后操作。", new Vector2(28, -390), new Vector2(750, 270), 29));
            Label("StakeHeading", machine, font, "最高投入", new Vector2(810, -115), new Vector2(380, 44), 25);
            SetReference(serialized, "stakeInput", Input("AdventureStake", machine, font, "100", "最高投入", new Vector2(810, -162), new Vector2(390, 68), true));
            SetReference(serialized, "choiceHeading", Label("ChoiceHeading", machine, font, "选择玩法", new Vector2(810, -240), new Vector2(380, 44), 25));
            SetReference(serialized, "choiceGroupDropdown", AdventureMachineDropdown("ChoiceGroup", machine, font, new Vector2(810, -288)));
            SetReference(serialized, "choiceOptionDropdown", AdventureMachineDropdown("ChoiceOption", machine, font, new Vector2(810, -368)));
            var choiceWire = Input("AdventureChoice", machine, font, "0", "机台参数", Vector2.zero, new Vector2(390, 68), true);
            SetReference(serialized, "choiceInput", choiceWire); choiceWire.gameObject.SetActive(false);
            SetReference(serialized, "confirmBetButton", Button("ConfirmBet", machine, font, "确认投入并开始", new Vector2(810, -452), new Vector2(390, 76)));
            SetReference(serialized, "actionHeading", Label("ActionHeading", machine, font, "选择操作目标", new Vector2(810, -558), new Vector2(380, 44), 25));
            SetReference(serialized, "actionOptionDropdown", AdventureMachineDropdown("ActionOption", machine, font, new Vector2(810, -608)));
            SetReference(serialized, "numberHeading", Label("NumberHeading", machine, font, "三位密码 / 出价", new Vector2(810, -706), new Vector2(390, 44), 24));
            SetReference(serialized, "numberInput", Input("NumericAction", machine, font, "", "输入密码或出价", new Vector2(810, -756), new Vector2(390, 68), true));
            var actionWire = Input("ActionValue", machine, font, "0", "动作参数", Vector2.zero, new Vector2(390, 64), true);
            SetReference(serialized, "actionInput", actionWire); actionWire.gameObject.SetActive(false);
            var actionArray = serialized.FindProperty("actionButtons"); actionArray.arraySize = 8;
            var actionTextArray = serialized.FindProperty("actionButtonTexts"); actionTextArray.arraySize = 8;
            for (int index = 0; index < 8; index++)
            {
                var button = Button("GameAction" + index, machine, font, "操作", new Vector2(28 + index % 2 * 382, -680 - index / 2 * 76), new Vector2(368, 68));
                var actionLabel = button.GetComponentInChildren<TMP_Text>(); actionLabel.fontSize = 26;
                actionArray.GetArrayElementAtIndex(index).objectReferenceValue = button;
                actionTextArray.GetArrayElementAtIndex(index).objectReferenceValue = actionLabel;
            }
            SetReference(serialized, "machineCloseButton", Button("MachineClose", machine, font, "回到场地 · 已提交的局继续保留", new Vector2(28, -984), new Vector2(750, 68)));

            var shop = AdventurePanel("Shop", rect, new Vector2(1290, 830));
            SetReference(serialized, "shopPanel", shop.gameObject);
            Label("ShopTitle", shop, font, "歪门道具铺 · 商店与背包", new Vector2(28, -20), new Vector2(950, 60), 38);
            panelBacks.Add(AdventureBackButton(shop, font));
            var viewport = Panel("Viewport", shop, new Vector2(26, -102), new Vector2(1238, 620), new Vector2(0, 1), new Vector2(0, 1), new Color(0.02f, 0.025f, 0.04f, 0.5f));
            viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
            var grid = content.GetComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(395, 266); grid.spacing = new Vector2(15, 15); grid.padding = new RectOffset(10, 10, 10, 10); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            SetReference(serialized, "itemContent", content);
            SetReference(serialized, "itemTemplate", BuildItemTemplate(shop, font));
            SetReference(serialized, "shopCloseButton", Button("ShopClose", shop, font, "回到场地", new Vector2(28, -742), new Vector2(1230, 65)));

            var slots = AdventurePanel("Slots", rect, new Vector2(1150, 730));
            SetReference(serialized, "slotsPanel", slots.gameObject);
            Label("SaveTitle", slots, font, "三个旅程存档", new Vector2(28, -24), new Vector2(800, 64), 40);
            panelBacks.Add(AdventureBackButton(slots, font));
            var slotTexts = serialized.FindProperty("slotTexts"); var saveButtons = serialized.FindProperty("saveButtons"); var loadButtons = serialized.FindProperty("loadButtons");
            slotTexts.arraySize = saveButtons.arraySize = loadButtons.arraySize = 3;
            for (int index = 0; index < 3; index++)
            {
                float y = -120 - index * 150;
                slotTexts.GetArrayElementAtIndex(index).objectReferenceValue = Label("Slot" + index, slots, font, "存档", new Vector2(28, y), new Vector2(590, 100), 29);
                saveButtons.GetArrayElementAtIndex(index).objectReferenceValue = Button("Save" + index, slots, font, "保存", new Vector2(640, y), new Vector2(205, 75));
                loadButtons.GetArrayElementAtIndex(index).objectReferenceValue = Button("Load" + index, slots, font, "继续", new Vector2(875, y), new Vector2(205, 75));
            }
            SetReference(serialized, "overwriteText", Label("OverwriteHint", slots, font, "选择存档槽。", new Vector2(28, -574), new Vector2(1080, 56), 25));
            SetReference(serialized, "slotsCloseButton", Button("SlotsClose", slots, font, "返回", new Vector2(28, -644), new Vector2(1080, 64)));

            var ending = AdventurePanel("Ending", rect, new Vector2(1090, 720));
            SetReference(serialized, "endingPanel", ending.gameObject);
            Label("FinaleTitle", ending, font, "最后的团队选择", new Vector2(28, -24), new Vector2(760, 68), 42);
            panelBacks.Add(AdventureBackButton(ending, font));
            SetReference(serialized, "endingText", Label("EndingStory", ending, font, "旅程结算", new Vector2(28, -132), new Vector2(1020, 290), 32));
            SetReference(serialized, "withdrawButton", Button("Withdraw", ending, font, "狼狈撤离", new Vector2(28, -480), new Vector2(1020, 76)));
            SetReference(serialized, "leaveEndingButton", Button("LeaveWithDignity", ending, font, "体面离场", new Vector2(28, -450), new Vector2(490, 80)));
            SetReference(serialized, "takeoverButton", Button("TakeOver", ending, font, "接管狂欢城", new Vector2(558, -450), new Vector2(490, 80)));
            SetReference(serialized, "vaultChallengeButton", Button("VaultChallenge", ending, font, "额外协作挑战 · 开启金库", new Vector2(28, -564), new Vector2(1020, 76)));

            var eventPanel = AdventurePanel("EventChoice", rect, new Vector2(1080, 740));
            SetReference(serialized, "eventPanel", eventPanel.gameObject);
            panelBacks.Add(AdventureBackButton(eventPanel, font));
            Label("EventHeading", eventPanel, font, "团队事件选择", new Vector2(28, -24), new Vector2(740, 68), 42);
            SetReference(serialized, "eventText", Label("EventStory", eventPanel, font, "请选择如何回应事件。", new Vector2(28, -120), new Vector2(1020, 240), 30));
            SetReference(serialized, "exchangeDropdown", AdventureExchangeDropdown(eventPanel, font));
            var eventButtons = serialized.FindProperty("eventButtons"); eventButtons.arraySize = 2;
            var eventTexts = serialized.FindProperty("eventButtonTexts"); eventTexts.arraySize = 2;
            for (int index = 0; index < 2; index++)
            {
                var button = Button("EventAction" + index, eventPanel, font, "事件选项", new Vector2(28, -470 - index * 90), new Vector2(1020, 78));
                eventButtons.GetArrayElementAtIndex(index).objectReferenceValue = button;
                eventTexts.GetArrayElementAtIndex(index).objectReferenceValue = button.GetComponentInChildren<TMP_Text>();
            }
            SetReference(serialized, "eventCloseButton", Button("EventClose", eventPanel, font, "稍后选择 · 回到场地", new Vector2(28, -652), new Vector2(1020, 68)));
            SetReference(serialized, "profilePresenter", BuildProfilePanel(rect, font));
            BuildLocalSettingsUi(rect, font, serialized, panelBacks);
            BuildLocalSocialUi(rect, font, serialized, panelBacks);
            var backArray = serialized.FindProperty("panelBackButtons"); backArray.arraySize = panelBacks.Count;
            for (int index = 0; index < panelBacks.Count; index++) backArray.GetArrayElementAtIndex(index).objectReferenceValue = panelBacks[index];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return presenter;
        }

        private static RectTransform AdventurePanel(string name, Transform parent, Vector2 size)
        {
            var panel = Panel(name, parent, Vector2.zero, size, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.04f, 0.055f, 0.09f, 0.985f));
            panel.GetComponent<Image>().raycastTarget = true;
            FitAdventurePanel(panel, size);
            return panel;
        }

        private static JinxCasinoItemCard BuildItemTemplate(Transform parent, TMP_FontAsset font)
        {
            var rect = Panel("ItemTemplate", parent, Vector2.zero, new Vector2(395, 266), new Vector2(0, 1), new Vector2(0, 1), new Color(0.1f, 0.14f, 0.2f));
            var card = rect.gameObject.AddComponent<JinxCasinoItemCard>();
            var serialized = new SerializedObject(card);
            SetReference(serialized, "titleText", Label("Title", rect, font, "道具名称", new Vector2(14, -12), new Vector2(365, 40), 26));
            SetReference(serialized, "descriptionText", Label("Description", rect, font, "道具说明", new Vector2(14, -55), new Vector2(365, 82), 20));
            SetReference(serialized, "countText", Label("Count", rect, font, "库存0", new Vector2(14, -137), new Vector2(365, 32), 22));
            SetReference(serialized, "purchaseButton", Button("Purchase", rect, font, "购买", new Vector2(14, -181), new Vector2(175, 68)));
            var useButton = Button("Use", rect, font, "使用", new Vector2(204, -181), new Vector2(175, 68));
            SetReference(serialized, "useButton", useButton);
            SetReference(serialized, "useButtonText", useButton.GetComponentInChildren<TMP_Text>());
            serialized.ApplyModifiedPropertiesWithoutUndo(); rect.gameObject.SetActive(false);
            return card;
        }

        private static void FitAdventurePanel(RectTransform panel, Vector2 size)
        {
            panel.gameObject.AddComponent<JinxCasinoPanelFit>().Configure(size, new Vector2(24, 24));
        }

        private static Button AdventureBackButton(RectTransform panel, TMP_FontAsset font)
        {
            var button = Button("ReturnToHub", panel, font, "返回园区入口", new Vector2(-28, -20), new Vector2(250, 68));
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
            return button;
        }

        private static TMP_Text AdventureScrollableText(string name, Transform parent, TMP_FontAsset font, string text,
            Vector2 position, Vector2 size, float fontSize)
        {
            var viewport = Panel(name + "Viewport", parent, position, size, new Vector2(0, 1), new Vector2(0, 1), new Color(0, 0, 0, 0.1f));
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var label = Label(name, viewport, font, text, Vector2.zero, size, fontSize);
            label.rectTransform.anchorMin = new Vector2(0, 1); label.rectTransform.anchorMax = new Vector2(1, 1);
            label.rectTransform.pivot = new Vector2(0.5f, 1); label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = new Vector2(0, size.y);
            label.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = label.rectTransform; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return label;
        }

        private static TMP_Dropdown AdventureExchangeDropdown(Transform parent, TMP_FontAsset font)
        {
            var rect = Panel("ExchangeInventory", parent, new Vector2(28, -380), new Vector2(1020, 68), new Vector2(0, 1), new Vector2(0, 1), new Color(0.13f, 0.16f, 0.21f));
            rect.GetComponent<Image>().raycastTarget = true;
            var dropdown = rect.gameObject.AddComponent<TMP_Dropdown>(); dropdown.targetGraphic = rect.GetComponent<Image>();
            var caption = Label("SelectedItem", rect, font, "选择交换道具", new Vector2(18, -8), new Vector2(970, 52), 28);
            var template = Panel("Template", rect, Vector2.zero, new Vector2(1020, 240), new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Color(0.10f, 0.14f, 0.20f));
            template.anchorMin = new Vector2(0, 0); template.anchorMax = new Vector2(1, 0); template.sizeDelta = new Vector2(0, 240);
            var viewport = Panel("Viewport", template, Vector2.zero, new Vector2(1020, 240), Vector2.zero, Vector2.one, Color.clear);
            Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>(); viewport.GetComponent<Image>().raycastTarget = true;
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.sizeDelta = new Vector2(0, 72);
            var item = Panel("Item", content, Vector2.zero, new Vector2(1020, 72), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Color(0.17f, 0.29f, 0.4f));
            item.anchorMin = new Vector2(0, 1); item.anchorMax = new Vector2(1, 1); item.sizeDelta = new Vector2(0, 72);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<Image>(); toggle.isOn = false;
            var itemLabel = Label("ItemLabel", item, font, "库存道具", new Vector2(18, -10), new Vector2(970, 52), 28);
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            dropdown.captionText = caption; dropdown.itemText = itemLabel; dropdown.template = template;
            dropdown.options = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData("没有可交换的库存道具") };
            template.gameObject.SetActive(false);
            return dropdown;
        }

        private static TMP_Dropdown AdventureMachineDropdown(string name, Transform parent, TMP_FontAsset font, Vector2 position)
        {
            // 与交换/装扮列表共用已保存的 Toggle + ScrollRect 模板，宽度按机台右栏配置。
            var dropdown = AdventureExchangeDropdown(parent, font);
            dropdown.name = name;
            var rect = (RectTransform)dropdown.transform;
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(390, 68);
            var caption = dropdown.captionText.rectTransform;
            caption.sizeDelta = new Vector2(354, 52); dropdown.captionText.fontSize = 24;
            var template = dropdown.template;
            template.sizeDelta = new Vector2(0, 288);
            var itemLabel = dropdown.itemText.rectTransform;
            itemLabel.sizeDelta = new Vector2(354, 62); dropdown.itemText.fontSize = 24;
            dropdown.itemText.textWrappingMode = TextWrappingModes.Normal;
            dropdown.options = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData("请选择") };
            return dropdown;
        }
    }
}
