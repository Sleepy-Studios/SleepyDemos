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
namespace Hotfix.JinxCasino.UI
{
    /// 独立窗口的显示与业务命令绑定，由对应 UIBind View 持有生命周期。
    public sealed class JinxCasinoMainMenuPresenter : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private GameObject mainMenu;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private Button start;
        [SerializeField] private Button practice;
        [SerializeField] private Button tutorialStartButton;
        [SerializeField] private TMP_Text tutorialMainFeedbackText;
        [SerializeField] private Button saveMainLoadButton;
        [SerializeField] private Button settingsMainButton;
        private JinxCasinoController owner;
        private int shownState = -1;
        private void Awake()
        {

            mainMenuSize = ((RectTransform)mainMenu.transform).sizeDelta;
            start.onClick.AddListener(() => owner?.StartAdventure(CasinoAdventureMode.Standard));
            practice.onClick.AddListener(() => owner?.StartAdventure(CasinoAdventureMode.Practice));
            quitGameButton.onClick.AddListener(() => owner?.QuitStandaloneApplication());
            tutorialStartButton.onClick.AddListener(() => owner?.UI.Tutorial.StartTeaching());
            saveMainLoadButton.onClick.AddListener(() => owner?.UI.OpenSaveLoad());
            settingsMainButton.onClick.AddListener(() => owner?.UI.OpenSettings());
        }
        /// <summary>显示前交付当前场景。</summary>
        /// <param name="controller">当前场景宿主。</param>
        public void Bind(JinxCasinoController controller) { Unbind(); owner = controller; shownState = -1; Refresh(); }
        /// 隐藏时释放场景引用。
        public void Unbind() => owner = null;
        private void Update() { if (owner != null) Refresh(); }
        private void Refresh()
        {
            if (owner == null) return;
            int state = owner.UI.State;
            quitGameButton.gameObject.SetActive(owner.IsStandalonePlayer);
            ((RectTransform)mainMenu.transform).sizeDelta = new Vector2(mainMenuSize.x, mainMenuSize.y + (owner.IsStandalonePlayer ? 84 : 0));
            tutorialMainFeedbackText.text = owner.UI.Tutorial.Feedback ?? "水果维修  /  发条牌桌  /  合拍拉杆";
            var buttons = new List<Button> { start, practice, tutorialStartButton, saveMainLoadButton, settingsMainButton };
            if (owner.IsStandalonePlayer) buttons.Add(quitGameButton);
            foreach (var button in buttons) button.interactable = !owner.IsBusy;
            if (shownState != state) JinxCasinoMenuNavigation.SaveNavigation(buttons);
            if (shownState != state) { shownState = state; owner.UI.SetFirstSelection(start.gameObject); }
        }
        private Vector2 mainMenuSize;
        /// <summary>接收公共取消事件并只退出当前窗口层。</summary>
        /// <param name="value">公共UI模块的取消事件。</param>
        public void OnCancel(BaseEventData value) { value.Use(); owner?.UI.CancelImmersionHudWindow(); }
        private void OnDestroy() => Unbind();
    }
}
