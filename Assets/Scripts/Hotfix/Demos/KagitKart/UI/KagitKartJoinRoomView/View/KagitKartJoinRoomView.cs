namespace Hotfix
{
    using System;
    using Core.Runtime;
    using Cysharp.Threading.Tasks;
    using TMPro;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.EventSystems;

    [Module("KagitKart")]
    [UIBind("KagitKartJoinRoomView")]
    public partial class KagitKartJoinRoomView : View
    {
        private KagitKartWorld world;
        private string validationMessage;
        private TMP_Text backLabel;

        protected override void OnGameObjectInitialize()
        {
            backLabel = Button_Back.GetComponentInChildren<TMP_Text>(true);
            Button_Confirm.onClick.AddListener(Submit);
            Button_Back.onClick.AddListener(Back);
            TMP_InputField_Code.onValueChanged.AddListener(ClearValidation);
            TMP_InputField_DisplayName.onValueChanged.AddListener(ClearValidation);
            UIMenuScope_Sheet.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>打开联机码表单，不启动网络连接。</summary>
        /// <param name="owner">当前 Demo 宿主；连接仅在用户确认时提交。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            validationMessage = null;
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
            float fit = Mathf.Min(1, (RectTransform_Viewport.rect.width - 48) / 1120, (RectTransform_Viewport.rect.height - 48) / 920);
            RectTransform_SheetGroup.localScale = Vector3.one * Mathf.Max(0.01f, fit);
            KagitKartMenuPaperMotion_Viewport.Play(owner.MenuEntranceDelay, owner.ReducedMotion);
            Render();
        }

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(TMP_InputField_DisplayName.gameObject);

        private void ClearValidation(string value)
        {
            validationMessage = null;
            Render();
        }

        private void Submit()
        {
            if (world == null || world.Busy)
            {
                return;
            }
            string code = TMP_InputField_Code.text.Trim().Replace(" ", "");
            string displayName = TMP_InputField_DisplayName.text.Trim();
            if (code.Length != 6 || !System.Linq.Enumerable.All(code, value => value >= '0' && value <= '9') || displayName.Length is < 1 or > 32)
            {
                validationMessage = "请输入车手名字和6位数字联机码。";
                Render();
                return;
            }
            validationMessage = null;
            GlobalData.Dispatch(new KagitKartJoinRoomAction(world, code, displayName));
        }

        private void Render()
        {
            if (world == null)
            {
                return;
            }
            KagitKartMenuPaperMotion_Viewport.SetReducedMotion(world.ReducedMotion);
            TMP_InputField_Code.interactable = TMP_InputField_DisplayName.interactable
                = Button_Confirm.interactable = !world.Busy;
            Button_Back.interactable = !world.Busy || world.ConnectingOnlineRoom && !world.CancelingOnlineConnection;
            if (backLabel != null)
            {
                backLabel.text = world.CancelingOnlineConnection ? "正在取消…" : world.ConnectingOnlineRoom ? "取消连接" : "返回";
            }
            TextMeshProUGUI_Status.text = validationMessage ?? world.Feedback ?? "输入好友分享的6位联机码。";
        }

        private void Back()
        {
            if (world == null)
            {
                return;
            }
            if (world.ConnectingOnlineRoom)
            {
                world.CancelOnlineRoomConnectionAsync().Forget(Debug.LogException);
            }
            else if (!world.Busy)
            {
                world.Dispatch(KagitKartCommand.Online);
            }
        }

        protected override void OnHide() => world = null;

        protected override void OnDestroy()
        {
            UIMenuScope_Sheet.Canceled -= Back;
            world = null;
        }
    }
}
