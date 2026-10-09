using Hotfix.JinxCasino;
using Hotfix.JinxCasino.UI;
using System;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Inputs;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hotfix
{
    /// 页面持有控件和显示逻辑，业务命令通过 Flux 派发。
    [Module("JinxCasino")]
    [UIBind("JinxCasinoMainMenuView")]
    public sealed partial class JinxCasinoMainMenuView : View
    {
        private GameObject mainMenu;

        private Button quitGameButton;

        private Button start;

        private Button practice;

        private Button tutorialStartButton;

        private TMP_Text tutorialMainFeedbackText;

        private Button saveMainLoadButton;

        private Button settingsMainButton;

        private JinxCasinoController owner;

        private void OnData(JinxCasinoData value)
        {
            owner = value.Scene;
            Refresh();
        }

        protected override void OnHide()
        {
            owner = null;
            base.OnHide();
        }

        private JinxCasinoPage shownState = JinxCasinoPage.None;

        protected override void OnGameObjectInitialize()
        {
            mainMenu = RectTransform_MainMenu.gameObject;
            quitGameButton = Button_QuitGame;
            start = Button_Start;
            practice = Button_Practice;
            tutorialStartButton = Button_Tutorial;
            tutorialMainFeedbackText = TextMeshProUGUI_Footer;
            saveMainLoadButton = Button_LoadAdventure;
            settingsMainButton = Button_Settings;
            BindData<JinxCasinoData>(OnData);
            var relay = UICancelRelay_JinxCasinoMainMenuView;
            Action cancel = () => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CancelWindow));
            relay.Canceled += cancel;
            AddBinding(() => relay.Canceled -= cancel);
            mainMenuSize = ((RectTransform)mainMenu.transform).sizeDelta;
            start.onClick.AddListener(() => owner?.StartAdventure(CasinoAdventureMode.Standard));
            practice.onClick.AddListener(() => owner?.StartAdventure(CasinoAdventureMode.Practice));
            quitGameButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.Quit)));
            tutorialStartButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.StartTeaching)));
            saveMainLoadButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.OpenSaveLoad)));
            settingsMainButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.OpenSettings)));
        }

        /// <summary>显示前交付当前场景。</summary>
        /// <param name="controller">当前场景宿主。</param>
        public void SetData(JinxCasinoController controller)
        {
            owner = controller;
            shownState = JinxCasinoPage.None;
        }

        /// 隐藏时释放场景引用。
        private void Refresh()
        {
            if (owner == null)
                return;
            JinxCasinoPage state = owner.Data.Page;
            quitGameButton.gameObject.SetActive(owner.IsStandalonePlayer);
            ((RectTransform)mainMenu.transform).sizeDelta = new Vector2(mainMenuSize.x, mainMenuSize.y + (owner.IsStandalonePlayer ? 84 : 0));
            tutorialMainFeedbackText.text = owner.Data.Tutorial.Feedback ?? "水果维修  /  发条牌桌  /  合拍拉杆";
            var buttons = new List<Button>
            {
                start,
                practice,
                tutorialStartButton,
                saveMainLoadButton,
                settingsMainButton
            };
            if (owner.IsStandalonePlayer)
                buttons.Add(quitGameButton);
            foreach (var button in buttons)
                button.interactable = !owner.IsBusy;
            if (shownState != state)
                JinxCasinoMenuNavigation.SaveNavigation(buttons);
            if (shownState != state)
            {
                shownState = state;
                if (state == JinxCasinoPage.MainMenu)
                    owner.SetFirstSelection(RestoreMenuSelection(start.gameObject));
            }
        }

        private Vector2 mainMenuSize;

        /// <summary>接收公共取消事件并只退出当前窗口层。</summary>
        /// <param name="value">公共UI模块的取消事件。</param>
    }
}
