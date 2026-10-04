using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.HowToFish
{
    /// 独立页面保存的业务显示与绑定，生命周期由对应 View 管理。
    public sealed class HowToFishMainMenuPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI menuTitle;
        [SerializeField] private Button[] slots;
        [SerializeField] private TextMeshProUGUI[] slotLabels;
        [SerializeField] private Button[] newGames;
        [SerializeField] private Button back;
        [SerializeField] private Button settingsButton;
        private HowToFishWorld world;
        private int confirmNewSlot = -1;
        private UIMenuScope menu;
        private void Awake()
        {
            menu = GetComponent<UIMenuScope>();
            for (int i = 0; i < slots.Length; i++)
            { int index = i; slots[i].onClick.AddListener(() => ContinueSlot(index)); newGames[i].onClick.AddListener(() => NewSlot(index)); }
            back.onClick.AddListener(Back);
            settingsButton.onClick.AddListener(() => world?.UI.OpenSettings());
            foreach (var button in GetComponentsInChildren<Button>(true)) button.onClick.AddListener(() => world?.PlayUiSound());
        }
        /// <summary>显示前绑定三槽菜单。</summary>
        /// <param name="owner">当前世界。</param>
        public void Bind(HowToFishWorld owner)
        { Unbind(); world = owner; world.Changed += Refresh; menu.Canceled += Back; Refresh(); }
        /// 隐藏时释放订阅。
        public void Unbind()
        { if (world != null) world.Changed -= Refresh; if (menu != null) menu.Canceled -= Back; world = null; }
        private void Back() => world?.ReturnToHub();
        private void Refresh()
        {
            if (world == null) return;
            menuTitle.text = "渔力全开\n<size=22>单人航程</size>";
            for (int i = 0; i < slots.Length; i++)
            {
                var saved = world.InspectSlot(i);
                slotLabels[i].text = saved.Status switch
                {
                    HowToFishLoadStatus.Empty => $"存档 {i + 1} · 新的航程",
                    HowToFishLoadStatus.Ready => $"存档 {i + 1} · 继续  ${saved.Data.money}",
                    HowToFishLoadStatus.RecoveryAvailable => $"存档 {i + 1} · 恢复备份",
                    HowToFishLoadStatus.UnsupportedVersion => $"存档 {i + 1} · 版本不支持",
                    _ => $"存档 {i + 1} · 数据损坏"
                };
                slots[i].interactable = saved.Status != HowToFishLoadStatus.Corrupt && saved.Status != HowToFishLoadStatus.UnsupportedVersion;
                newGames[i].interactable = saved.Status == HowToFishLoadStatus.Ready || saved.Status == HowToFishLoadStatus.Empty;
            }
        }
        private void ContinueSlot(int index)
        {
            var saved = world.InspectSlot(index);
            world.StartSlot(index, saved.Status == HowToFishLoadStatus.Empty, saved.Status == HowToFishLoadStatus.RecoveryAvailable);
        }

        private void NewSlot(int index)
        {
            if (world.InspectSlot(index).Status == HowToFishLoadStatus.Ready && confirmNewSlot != index)
            { confirmNewSlot = index; world.Notify("再次点击此槽的“重开”，确认覆盖当前航程；上一份进度会保留为备份。"); return; }
            world.StartSlot(index, true);
        }

        private void OnDestroy() => Unbind();
    }
}
