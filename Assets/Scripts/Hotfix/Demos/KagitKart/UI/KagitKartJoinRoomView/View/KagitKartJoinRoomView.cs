namespace Hotfix
{
    using System;
    using Core.Runtime;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.EventSystems;

    [Module("KagitKart")]
    [UIBind("KagitKartJoinRoomView")]
    public partial class KagitKartJoinRoomView : View
    {
        private KagitKartWorld world;
        private string validationMessage;

        protected override void OnGameObjectInitialize()
        {
            Button_Confirm.onClick.AddListener(Submit);
            Button_Back.onClick.AddListener(Back);
            TMP_InputField_Address.onValueChanged.AddListener(ClearValidation);
            TMP_InputField_Port.onValueChanged.AddListener(ClearValidation);
            TMP_InputField_DisplayName.onValueChanged.AddListener(ClearValidation);
            UIMenuScope_Sheet.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>打开地址表单，不启动网络连接。</summary>
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
            string address = TMP_InputField_Address.text.Trim();
            string displayName = TMP_InputField_DisplayName.text.Trim();
            bool validPort = ushort.TryParse(TMP_InputField_Port.text, out ushort port) && port > 0;
            // 复制房间地址包含端口，允许整段粘贴到地址框。
            if (Uri.TryCreate("udp://" + address, UriKind.Absolute, out var endpoint) && endpoint.Port > 0 && endpoint.Port <= ushort.MaxValue)
            {
                address = endpoint.Host;
                port = (ushort)endpoint.Port;
                validPort = true;
                TMP_InputField_Port.SetTextWithoutNotify(port.ToString());
            }
            if (address.Length == 0 || displayName.Length == 0 || !validPort)
            {
                validationMessage = "请输入车手名字、房主地址和 1-65535 的端口。";
                Render();
                return;
            }
            validationMessage = null;
            GlobalData.Dispatch(new KagitKartJoinRoomAction(world, address, port, displayName));
        }

        private void Render()
        {
            if (world == null)
            {
                return;
            }
            KagitKartMenuPaperMotion_Viewport.SetReducedMotion(world.ReducedMotion);
            TMP_InputField_Address.interactable = TMP_InputField_Port.interactable = TMP_InputField_DisplayName.interactable
                = Button_Confirm.interactable = Button_Back.interactable = !world.Busy;
            TextMeshProUGUI_Status.text = validationMessage ?? world.Feedback ?? "输入房主地址，也可粘贴包含端口的完整地址。";
        }

        private void Back() => world?.Dispatch(KagitKartCommand.Online);

        protected override void OnHide() => world = null;

        protected override void OnDestroy()
        {
            UIMenuScope_Sheet.Canceled -= Back;
            world = null;
        }
    }
}
