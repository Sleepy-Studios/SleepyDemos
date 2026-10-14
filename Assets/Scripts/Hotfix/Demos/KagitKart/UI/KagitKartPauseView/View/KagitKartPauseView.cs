namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;

    [Module("KagitKart")]
    [UIBind("KagitKartPauseView")]
    public partial class KagitKartPauseView : View
    {
        private KagitKartWorld world;
        private bool quitArmed;
        private KagitKartChoiceCard[] resetCards;

        protected override void OnGameObjectInitialize()
        {
            Button_Resume.onClick.AddListener(Resume);
            Button_Restart.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Restart));
            Button_Title.onClick.AddListener(ReturnToTitle);
            Button_Hub.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Hub));
            Button_Settings.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Settings));
            Button_Controls.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Controls));
            UIMenuScope_Sheet.Canceled += Resume;
            var buttons = new[] { Button_Resume, Button_Restart, Button_Settings, Button_Controls, Button_Hub };
            resetCards = new KagitKartChoiceCard[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                resetCards[i] = buttons[i].GetComponent<KagitKartChoiceCard>();
                resetCards[i].Focused += ResetQuitConfirmation;
            }
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>在显示前绑定当前场景，重新显示暂停目录时取消退出确认。</summary>
        /// <param name="owner">当前场景来源。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            quitArmed = false;
            Render();
        }

        private void Resume() => world?.Dispatch(KagitKartCommand.Resume);

        private void ReturnToTitle()
        {
            if (world == null || world.Busy)
            {
                return;
            }
            if (world.GrandPrix != null && !quitArmed)
            {
                quitArmed = true;
                Render();
                return;
            }
            world.Dispatch(KagitKartCommand.Title);
        }

        private void ResetQuitConfirmation()
        {
            if (!quitArmed)
            {
                return;
            }
            quitArmed = false;
            Render();
        }

        private void Render()
        {
            if (world?.Session == null)
            {
                return;
            }
            Button_Resume.interactable = !world.Busy && world.Session.Pause.CanResume;
            Button_Restart.interactable = Button_Title.interactable = Button_Hub.interactable
                = Button_Settings.interactable = Button_Controls.interactable = !world.Busy;
            if (world.Network?.Connected == true) Button_Restart.interactable = false;
            TextMeshProUGUI_Detail.text = quitArmed ? "杯赛进度将丢失。确定退出吗？再次选择“返回菜单”确认。"
                : !world.Session.Pause.CanResume ? "请先返回游戏窗口或连接操作设备。"
                : world.Network?.Connected == true ? "已停止本机操作，联机比赛仍在继续。"
                : world.GrandPrix != null ? "大奖赛：重新开始后，本场不计入杯赛积分。" : "纸景和比赛已暂停。";
        }

        protected override void OnShow()
        {
            quitArmed = false;
            Render();
        }

        protected override void OnHide() => ResetQuitConfirmation();

        protected override void OnDestroy()
        {
            UIMenuScope_Sheet.Canceled -= Resume;
            foreach (var card in resetCards)
            {
                card.Focused -= ResetQuitConfirmation;
            }
            world = null;
        }
    }
}
