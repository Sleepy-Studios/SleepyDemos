using System;
using Core.Runtime;
using Core.Runtime.Inputs;
using Cysharp.Threading.Tasks;
using Hotfix.DroneFlight;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hotfix
{
    [Module("DroneFlight")]
    [UIBind("DroneFlightVehicleSelectView")]
    public partial class DroneFlightVehicleSelectView : View
    {
        private static readonly (string Title, string Description, string Equipment, string Operation, string Purpose)[] Models =
        {
            ("纯无人机", "自由飞行与精准操控", "无附加模块", "飞行 · 视角", "飞行练习"),
            ("四爪抓斗无人机", "夹取、搬运与精准投放", "四爪抓斗", "开合 · 升降", "载荷搬运"),
            ("渔叉无人机", "发射、命中与绳索回收", "渔叉模块", "发射 · 回收", "目标牵引")
        };
        private Button[] cards;
        private Image[] previews;
        private RectTransform[] badges;
        private DroneFlightData data;
        private UIState[] cardStates;
        private bool isBusy => data.Mode is DroneFlightSessionMode.Loading or DroneFlightSessionMode.Leaving;

        protected override void OnGameObjectInitialize()
        {
            BindData<DroneFlightData>(OnData);
            cardStates = new[] { UIState_PlainButton, UIState_GrappleButton, UIState_HarpoonButton };
            cards = new[] { Button_PlainButton, Button_GrappleButton, Button_HarpoonButton };
            previews = new[] { Image_PlainPreview, Image_GrapplePreview, Image_HarpoonPreview };
            badges = new[] { RectTransform_PlainSelected, RectTransform_GrappleSelected, RectTransform_HarpoonSelected };
            UIMenuScope_DroneFlightVehicleSelectView.Canceled += OnBackButtonClick;
        }

        /// <summary>在正式显示前交付当前选择与场景操作。</summary>
        /// <param name="data">本次选择会话，失败恢复时保留机型与提示。</param>
        /// <returns>当前页面。</returns>
        private void OnData(DroneFlightData value)
        {
            data = value;
            RefreshSelection();
            SetBusy(isBusy, data.Feedback);
            RefreshHints();
        }

        protected override void OnShow()
        {
            base.OnShow();
            RefreshHints();
            EventSystem.current?.SetSelectedGameObject(string.IsNullOrEmpty(data.Feedback)
                ? cards[(int)data.SelectedKind].gameObject : Button_StartButton.gameObject);
            FollowMenuFocusAsync().Forget();
        }

        protected override void OnHide()
        {
            ReleasePresentation();
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            ReleasePresentation();
            if (UIMenuScope_DroneFlightVehicleSelectView != null)
                UIMenuScope_DroneFlightVehicleSelectView.Canceled -= OnBackButtonClick;
            base.OnDestroy();
        }

        private async UniTask FollowMenuFocusAsync()
        {
            while (IsEnable)
            {
                var focused = EventSystem.current?.currentSelectedGameObject;
                if (!isBusy && data != null)
                    for (int i = 0; i < cards.Length; i++)
                        if (focused == cards[i].gameObject && (int)data.SelectedKind != i) Select((DroneVehicleKind)i);
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            }
        }

        private void OnPlainButtonClick() => Choose(DroneVehicleKind.Plain);
        private void OnGrappleButtonClick() => Choose(DroneVehicleKind.Grapple);
        private void OnHarpoonButtonClick() => Choose(DroneVehicleKind.Harpoon);

        private void Choose(DroneVehicleKind kind)
        {
            if (isBusy || data == null) return;
            Select(kind);
            // 方向导航已经选择预览；确认键提交，指针与触屏只切换卡片。
            if (InputDeviceState.ActiveDevice is Keyboard || InputDeviceState.ActiveDevice is Gamepad)
                OnStartButtonClick();
        }

        private void Select(DroneVehicleKind kind) => GlobalData.Dispatch(new DroneFlightSelectAction(kind));

        private void RefreshSelection()
        {
            int index = (int)data.SelectedKind;
            var model = Models[index];
            TextMeshProUGUI_Title.text = model.Title;
            TextMeshProUGUI_Description.text = model.Description;
            TextMeshProUGUI_Equipment.text = model.Equipment;
            TextMeshProUGUI_Operation.text = model.Operation;
            TextMeshProUGUI_Purpose.text = model.Purpose;
            Image_Hero.sprite = previews[index].sprite;
            for (int i = 0; i < cards.Length; i++)
            {
                cardStates[i].SetState(i == index ? "Selected" : "Normal");
                badges[i].gameObject.SetActive(i == index);
            }
        }

        internal void SetBusy(bool busy, string message = null)
        {
            foreach (var card in cards) card.interactable = !busy;
            Button_StartButton.interactable = Button_BackButton.interactable = !busy;
            TextMeshProUGUI_StartLabel.text = busy ? "准备中…" : "开始飞行  »";
            TextMeshProUGUI_Status.text = message ?? string.Empty;
            if (!busy && !string.IsNullOrEmpty(message) && IsEnable && InputDeviceState.ActiveKind != InputDeviceKind.Touch)
                EventSystem.current?.SetSelectedGameObject(Button_StartButton.gameObject);
        }

        private void OnStartButtonClick()
        {
            if (isBusy || data == null || !IsEnable) return;
            GlobalData.Dispatch(new DroneFlightStartAction());
        }

        private void OnBackButtonClick()
        {
            if (isBusy || data == null || !IsEnable) return;
            GlobalData.Dispatch(new DroneFlightExitAction());
        }

        private void RefreshHints()
        {
            bool touch = Application.isMobilePlatform || InputDeviceState.ActiveKind == InputDeviceKind.Touch;
            RectTransform_Hints.gameObject.SetActive(!touch);
            TextMeshProUGUI_TouchHint.gameObject.SetActive(touch);
            TextMeshProUGUI_MoveHint.text = InputDeviceState.PromptKind == InputDeviceKind.Gamepad ? "方向键 / 摇杆  选择" : "← →  选择";
        }

        private void ReleasePresentation()
        {
        }
    }
}
