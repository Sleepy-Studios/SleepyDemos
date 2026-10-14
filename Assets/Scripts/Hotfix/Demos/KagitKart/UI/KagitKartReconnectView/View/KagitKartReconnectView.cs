namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;

    [Module("KagitKart")]
    [UIBind("KagitKartReconnectView")]
    public partial class KagitKartReconnectView : View
    {
        private KagitKartWorld world;

        protected override void OnGameObjectInitialize()
        {
            Button_Cancel.onClick.AddListener(Cancel);
            UIMenuScope_Sheet.Canceled += Cancel;
        }

        /// <summary>显示当前连接恢复，不持有网络连接。</summary>
        /// <param name="owner">本场宿主。</param>
        public void SetData(KagitKartWorld owner) => world = owner;

        private void Update()
        {
            if (world != null) TextMeshProUGUI_Seconds.text = "剩余 " + (world.Network?.ReconnectSeconds ?? 0) + " 秒";
        }

        private void Cancel() => world?.Dispatch(KagitKartCommand.LeaveRoom);

        protected override void OnDestroy()
        {
            UIMenuScope_Sheet.Canceled -= Cancel;
            world = null;
        }
    }
}
