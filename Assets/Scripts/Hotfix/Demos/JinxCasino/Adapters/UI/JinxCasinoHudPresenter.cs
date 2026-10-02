using Core.Runtime.Networking;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.Adapters.UI
{
    /// P0 接入界面的序列化绑定；正式游戏界面在后续阶段替换表现，不改变网络语义。
    public sealed class JinxCasinoHudPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private TMP_Text roomText;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private TMP_InputField stakeInput;
        [SerializeField] private TMP_InputField choiceInput;
        [SerializeField] private Button offlineButton;
        [SerializeField] private Button createButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button slotsButton;
        [SerializeField] private Button rouletteButton;
        [SerializeField] private Button coinButton;
        [SerializeField] private Button backButton;
        [SerializeField] private JinxCasinoTouchPad movePad;
        [SerializeField] private JinxCasinoTouchPad lookPad;
        [SerializeField] private JinxCasinoAdventurePresenter adventurePresenter;
        [SerializeField] private GameObject[] legacyPanels;
        private JinxCasinoController controller;
        private RectTransform safeRoot;
        private Rect previousSafeArea;

        /// <summary>绑定当前场景会话；重新绑定前完整解绑旧会话。</summary>
        /// <param name="owner">拥有 UI 生命周期的场景宿主。</param>
        public void Bind(JinxCasinoController owner)
        {
            Unbind(); controller = owner;
            controller.Changed += Refresh;
            controller.BindTouchPads(movePad, lookPad);
            adventurePresenter?.Bind(owner);
            offlineButton.onClick.AddListener(OnOffline); createButton.onClick.AddListener(OnCreate);
            joinButton.onClick.AddListener(OnJoin); leaveButton.onClick.AddListener(OnLeave);
            slotsButton.onClick.AddListener(OnSlots); rouletteButton.onClick.AddListener(OnRoulette);
            coinButton.onClick.AddListener(OnCoin); backButton.onClick.AddListener(OnBack);
            safeRoot = transform as RectTransform;
            previousSafeArea = Rect.zero;
            Refresh();
        }

        /// 移除当前会话及按钮订阅，清空双指输入。
        public void Unbind()
        {
            if (controller == null) return;
            adventurePresenter?.Unbind();
            controller.Changed -= Refresh; controller.BindTouchPads(null, null);
            offlineButton.onClick.RemoveListener(OnOffline); createButton.onClick.RemoveListener(OnCreate);
            joinButton.onClick.RemoveListener(OnJoin); leaveButton.onClick.RemoveListener(OnLeave);
            slotsButton.onClick.RemoveListener(OnSlots); rouletteButton.onClick.RemoveListener(OnRoulette);
            coinButton.onClick.RemoveListener(OnCoin); backButton.onClick.RemoveListener(OnBack);
            controller = null;
        }

        private void Refresh()
        {
            if (controller == null) return;
            bool legacy = controller.IsLegacySession;
            if (legacyPanels != null) foreach (var panel in legacyPanels) if (panel != null) panel.SetActive(legacy);
            if (adventurePresenter != null) adventurePresenter.gameObject.SetActive(!legacy);
            statusText.text = controller.Status;
            balanceText.text = "团队筹码  " + controller.Balance;
            roomText.text = !controller.TransportKind.HasValue ? "房间：尚未连接"
                : controller.TransportKind == NetworkTransportKind.Internet
                    ? "房间码：" + controller.RoomCode : "离线模式没有可分享的互联网房间";
            var receipt = controller.LastReceipt;
            resultText.text = receipt == null ? "P0规则：水果机选项0；轮盘单号0–36；硬币0正/1反。"
                : !receipt.Accepted ? "请求未结算：" + receipt.Error
                : "结果 " + (receipt.Symbols.Length > 0 ? string.Join(" / ", receipt.Symbols) : receipt.Outcome.ToString())
                    + "  · 返还 " + receipt.Payout + "  · 余额 " + receipt.BalanceAfter;
            bool idle = !controller.IsBusy;
            offlineButton.interactable = createButton.interactable = joinButton.interactable = backButton.interactable = idle;
            leaveButton.interactable = idle && controller.TransportKind.HasValue;
            slotsButton.interactable = rouletteButton.interactable = coinButton.interactable = idle && controller.HasRun;
        }

        private void OnOffline() => controller.StartOffline(nameInput.text);
        private void OnCreate() => controller.CreateRoom(nameInput.text);
        private void OnJoin() => controller.JoinRoom(codeInput.text, nameInput.text);
        private void OnLeave() => controller.LeaveRoom();
        private void OnBack() => controller.RequestExit();
        private void OnSlots() => Submit(CasinoGameKind.Slots, 0);
        private void OnRoulette() => Submit(CasinoGameKind.Roulette, ParseChoice());
        private void OnCoin() => Submit(CasinoGameKind.CoinFlip, ParseChoice());
        private int ParseChoice() => int.TryParse(choiceInput.text, out int value) ? value : -1;
        private void Submit(CasinoGameKind game, int choice)
        {
            if (!long.TryParse(stakeInput.text, out long stake) || stake <= 0)
            { resultText.text = "请输入正整数投入。"; return; }
            controller.Bet(game, stake, choice);
        }

        private void Update()
        {
            if (safeRoot == null || Screen.width == 0 || Screen.height == 0 || Screen.safeArea == previousSafeArea) return;
            previousSafeArea = Screen.safeArea;
            safeRoot.anchorMin = previousSafeArea.min / new Vector2(Screen.width, Screen.height);
            safeRoot.anchorMax = previousSafeArea.max / new Vector2(Screen.width, Screen.height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }
        private void OnDestroy() => Unbind();
    }
}
