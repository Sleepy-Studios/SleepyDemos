namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;

    [Module("KagitKart")]
    [UIBind("KagitKartPauseView")]
    public partial class KagitKartPauseView : View
    {
        private KagitKartWorld world;

        protected override void OnGameObjectInitialize()
        {
            Button_Resume.onClick.AddListener(Resume);
            Button_Restart.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Restart));
            Button_Hub.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Hub));
            Button_Settings.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Settings));
            Button_Controls.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Controls));
            UIMenuScope_Sheet.Canceled += Resume;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>在显示前绑定当前场景。</summary>
        /// <param name="owner">当前场景来源。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            Render();
        }

        private void Resume() => world?.Dispatch(KagitKartCommand.Resume);

        private void Render()
        {
            if (world?.Session == null)
            {
                return;
            }
            Button_Resume.interactable = !world.Busy && world.Session.Pause.CanResume;
            Button_Restart.interactable = Button_Hub.interactable = Button_Settings.interactable = Button_Controls.interactable = !world.Busy;
            TextMeshProUGUI_Detail.text = world.Session.Pause.CanResume ? "纸景和比赛已暂停。" : "请先返回游戏窗口或连接操作设备。";
        }

        protected override void OnDestroy()
        {
            UIMenuScope_Sheet.Canceled -= Resume;
            world = null;
        }
    }
}
