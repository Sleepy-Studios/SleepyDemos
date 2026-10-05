using Core.Runtime;
using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Hotfix.BlockPorters
{
    /// 设置与暂停页；声音即时应用，暂停归属由当前会话 UI 协调器恢复。
    public sealed class BlockPortersSettingsPresenter : MonoBehaviour
    {
        [SerializeField] private Button sound;
        [SerializeField] private Button restart;
        [SerializeField] private Button exit;
        [SerializeField] private Button settingsContinue;
        [SerializeField] private Button settingsClose;
        [SerializeField] private TextMeshProUGUI soundLabel;
        private BlockPortersController owner;
        private UIMenuScope menu;
        private UIBtnSwitch soundSwitch;
        private void Awake()
        {
            menu = GetComponent<UIMenuScope>();
            soundSwitch = sound.GetComponent<UIBtnSwitch>();
            restart.onClick.AddListener(() => owner?.Restart());
            exit.onClick.AddListener(() => owner?.ReturnToHub());
            settingsContinue.onClick.AddListener(Close);
            settingsClose.onClick.AddListener(Close);
        }
        /// <summary>显示前绑定当前会话。</summary>
        /// <param name="controller">当前玩法宿主。</param>
        public void Bind(BlockPortersController controller)
        {
            Unbind(); owner = controller; owner.Changed += Refresh; menu.Canceled += Close;
            soundSwitch.SetAction(value => { if (owner != null && value == owner.IsMuted) owner.ToggleSound(); });
            Refresh();
        }
        /// 隐藏时释放订阅。
        public void Unbind()
        {
            if (owner != null) owner.Changed -= Refresh;
            if (menu != null) menu.Canceled -= Close;
            if (soundSwitch != null) soundSwitch.SetAction(null);
            owner = null;
        }
        private void Close() => owner?.UI.CloseSettings();
        private void Refresh()
        {
            if (owner == null) return;
            soundSwitch.SetStatus(!owner.IsMuted);
            soundLabel.text = owner.IsMuted ? "声音已关闭" : "声音已开启";
            sound.interactable = restart.interactable = exit.interactable = settingsContinue.interactable = settingsClose.interactable = !owner.IsExiting;
        }
        private void OnDestroy() => Unbind();
    }
}
