using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hotfix.HowToFish
{
    /// 保存的 HUD Prefab 上的显示与按钮绑定，不创建运行时 UI 层级。
    public sealed class HowToFishHudPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI stats;
        [SerializeField] private TextMeshProUGUI focus;
        [SerializeField] private TextMeshProUGUI notice;
        [SerializeField] private TextMeshProUGUI fishingStatus;
        [SerializeField] private TextMeshProUGUI controls;
        [SerializeField] private TextMeshProUGUI equipmentSlots;
        [SerializeField] private Image tension;
        [SerializeField] private GameObject fishingPanel;
        [SerializeField] private GameObject bossPanel;
        [SerializeField] private TextMeshProUGUI bossStatus;
        [SerializeField] private Image bossHealth;
        [SerializeField] private Image bossEscape;
        [SerializeField] private GameObject radarPanel;
        [SerializeField] private TextMeshProUGUI radarStatus;
        [SerializeField] private TextMeshProUGUI[] radarIslands;
        [SerializeField] private GameObject menu;
        [SerializeField] private TextMeshProUGUI menuTitle;
        [SerializeField] private Button[] slots;
        [SerializeField] private TextMeshProUGUI[] slotLabels;
        [SerializeField] private Button[] newGames;
        [SerializeField] private Button resume;
        [SerializeField] private Button save;
        [SerializeField] private Button back;
        [SerializeField] private TextMeshProUGUI journal;
        private HowToFishWorld world;
        private float refreshAt;
        private int confirmNewSlot = -1;
        private bool menuWasVisible;
        private readonly StringBuilder journalText = new StringBuilder();
        private readonly StringBuilder equipmentText = new StringBuilder();

        private void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].onClick.AddListener(() => ContinueSlot(index));
                newGames[i].onClick.AddListener(() => NewSlot(index));
            }
            resume.onClick.AddListener(() => { if (world?.ShowEnding == true) world.ContinueAfterEnding(); else world?.SetPaused(false); });
            save.onClick.AddListener(() => world?.Save());
            back.onClick.AddListener(() => world?.ReturnToHub());
        }

        /// <summary>绑定当前世界，重复显示不重复注册监听。</summary>
        /// <param name="owner">当前 Demo 世界。</param>
        public void Bind(HowToFishWorld owner)
        {
            Unbind();
            world = owner;
            world.Changed += OnChanged;
            menuWasVisible = false;
            confirmNewSlot = -1;
            Refresh();
        }

        /// 界面隐藏或销毁时解除世界引用。
        public void Unbind()
        {
            if (world != null) world.Changed -= OnChanged;
            world = null;
        }

        private void Update()
        {
            if (world == null || Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 0.05f;
            RefreshGameplay();
        }

        private void OnChanged() => Refresh();

        private void Refresh()
        {
            if (world == null) return;
            bool show = world.IsPaused || !world.HasSession;
            menu.SetActive(show);
            menuTitle.text = world.ShowEnding ? "航程完成\n<size=22>已经返回大陆 · 可继续探索</size>" : world.HasSession ? "已暂停" : "渔力全开\n<size=22>单人航程</size>";
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].gameObject.SetActive(!world.HasSession);
                newGames[i].gameObject.SetActive(!world.HasSession);
                if (world.HasSession) continue;
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
            resume.gameObject.SetActive(world.HasSession);
            save.gameObject.SetActive(world.HasSession && !world.ShowEnding);
            journal.gameObject.SetActive(world.ShowJournal);
            if (world.ShowJournal)
            {
                menuTitle.text = "鱼类图鉴";
                journalText.Clear();
                foreach (var creature in world.Catalog.Creatures)
                {
                    if (!creature.IsJournalEntry) continue;
                    bool found = world.Session.State.discoveredCreatures.Contains(creature.Id);
                    bool defeated = world.Session.State.defeatedCreatures.Contains(creature.Id);
                    journalText.Append(found ? creature.DisplayName : "未知生物").Append(defeated ? "  ✓" : "  —");
                    if (world.Session.State.defeatedDripCreatures.Contains(creature.Id)) journalText.Append("  珍品");
                    journalText.AppendLine();
                }
                journal.text = journalText.ToString();
            }
            if (show && !menuWasVisible && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(world.HasSession ? resume.gameObject : slots[0].gameObject);
            menuWasVisible = show;
            RefreshGameplay();
        }

        private void RefreshGameplay()
        {
            notice.text = world.Notice ?? "";
            bool radarVisible = world.HasSession && !world.IsPaused && world.Player.Equipment?.Kind == HowToFishItemKind.Radar;
            radarPanel.SetActive(radarVisible);
            if (radarVisible)
            {
                foreach (var dot in radarIslands) dot.gameObject.SetActive(false);
                radarStatus.text = "雷达 · 朝向 " + world.Player.transform.eulerAngles.y.ToString("0") + "°";
                foreach (var island in world.Islands)
                {
                    if (island.Index > world.Session.State.unlockedIsland || island.Index >= radarIslands.Length) continue;
                    var offset = island.Position - world.Player.transform.position;
                    var local = Quaternion.Euler(0, -world.Player.transform.eulerAngles.y, 0) * offset;
                    var dot = radarIslands[island.Index];
                    dot.gameObject.SetActive(true);
                    dot.rectTransform.anchoredPosition = Vector2.ClampMagnitude(new Vector2(local.x, local.z) * .055f, 70);
                    dot.text = "● " + island.DisplayName;
                    if (island.Index == world.Session.State.unlockedIsland)
                        radarStatus.text += $"\n{island.DisplayName} {new Vector2(offset.x, offset.z).magnitude:0}m";
                }
            }
            var boss = world.ActiveBoss;
            bool fighting = boss != null && !world.IsPaused;
            bossPanel.SetActive(fighting);
            if (fighting)
            {
                var creature = boss.Item;
                bossHealth.fillAmount = creature.Health / creature.Creature.Health;
                bossEscape.fillAmount = boss.EscapeFraction;
                bossStatus.text = $"{creature.Creature.DisplayName}  {creature.Health:0} / {creature.Creature.Health:0} · {boss.Hint}";
            }
            if (!world.HasSession)
            { stats.text = ""; focus.text = ""; controls.text = ""; equipmentSlots.text = ""; fishingPanel.SetActive(false); return; }
            var state = world.Session.State;
            stats.text = $"${state.money}\n生命 {state.health:0}   饱食 {state.hunger:0}";
            if (state.poisonSeconds > 0) stats.text += $"  <color=#B1D75B>中毒 {state.poisonSeconds:0.0}s</color>";
            if (state.burningSeconds > 0) stats.text += $"  <color=#FFA45B>燃烧 {state.burningSeconds:0.0}s</color>";
            if (world.Player.Equipment?.Kind == HowToFishItemKind.Rod) stats.text += "\n鱼饵：" + world.Player.Fishing.BaitName;
            var heldFood = world.Player.HeldItem;
            var heldDynamite = heldFood != null ? heldFood.GetComponent<HowToFishDynamite>() : null;
            if (heldDynamite != null && heldDynamite.IsArmed)
                stats.text += $"\n<color=#FF815B>炸药引信 {heldDynamite.RemainingFuse:0.0}s · 立即投掷</color>";
            else if (world.Player.Equipment?.Kind == HowToFishItemKind.Explosive)
                stats.text += $"\n炸药 ×{world.Session.Count(world.Player.Equipment.Id)}";
            if (heldFood != null && world.Player.CanEat)
                stats.text += $"\n{heldFood.Creature?.DisplayName ?? world.Catalog.FindItem(heldFood.DefinitionId)?.DisplayName} · " +
                    (heldFood.IsBurnt ? "烧焦" : heldFood.Cooking >= .45f ? "熟成" : heldFood.IsCooked ? "加热中" : "生") +
                    (heldFood.Creature == null ? "" : $"  ${heldFood.SaleValue}");
            if (world.Player.EatingProgress > 0) stats.text += $"\n进食 {world.Player.EatingProgress:P0}";
            if (world.Player.Equipment?.Kind == HowToFishItemKind.Gun)
                stats.text += $"\n{world.Player.Equipment.DisplayName}  {world.Player.Ammo}/{world.Player.AmmoCapacity}" +
                    (world.Player.IsReloading ? " · 换弹中" : "");
            focus.text = world.FocusText();
            equipmentText.Clear();
            for (int i = 0; i < world.Session.EquipmentCapacity; i++)
            {
                string id = world.Session.State.equipmentSlots[i];
                bool selected = !string.IsNullOrEmpty(id) && world.Player.Equipment?.Id == id;
                if (selected) equipmentText.Append("<color=#FFD98B>");
                equipmentText.Append('[').Append(i + 1).Append("] ").Append(world.Catalog.FindItem(id)?.DisplayName ?? "空").Append("   ");
                if (selected) equipmentText.Append("</color>");
            }
            var unstored = world.Session.UnstoredEquipment;
            if (unstored != null) equipmentText.Append("手持未收纳：").Append(world.Catalog.FindItem(unstored.id).DisplayName);
            else if (world.Player.Equipment == null) equipmentText.Append("空手");
            equipmentSlots.text = world.IsPaused ? "" : equipmentText.ToString();
            controls.text = world.IsPaused ? "" : world.Player.IsDriving
                ? $"{world.Input.BindingLabel("Move")} 航行   {(world.Input.IsGamepad ? "右摇杆" : "鼠标")} 视角   {world.Input.BindingLabel("Interact")} 离开驾驶位   {world.Input.BindingLabel("Pause")} 暂停"
                : $"{world.Input.BindingLabel("Interact")} 交互   {world.Input.BindingLabel("Throw")} 投掷   " +
                  $"{world.Input.BindingLabel("Next")} 换装备   {world.Input.BindingLabel("Journal")} 图鉴   {world.Input.BindingLabel("Pause")} 暂停";
            if (!world.IsPaused && !world.Player.IsDriving && !world.Player.CanEat && world.Player.Equipment?.Kind == HowToFishItemKind.Gun)
                controls.text = $"{world.Input.BindingLabel("Use")} 开火   {world.Input.BindingLabel("Alternate")} 瞄准   {world.Input.BindingLabel("Reload")} 换弹   " + controls.text;
            if (!world.IsPaused && !world.Player.IsDriving) controls.text += "   " + world.Input.BindingLabel("Holster") + " 收纳/空手";
            if (!world.IsPaused && !world.Player.IsDriving && world.Player.CanEat)
                controls.text = "按住 " + world.Input.BindingLabel("Use") + " 进食   " + controls.text;
            if (!world.IsPaused && !world.Player.IsDriving && world.Player.HeldItem == null && world.Player.Equipment?.Kind == HowToFishItemKind.Explosive)
                controls.text = world.Input.BindingLabel("Use") + " 点燃投出（3秒）   " + controls.text;
            var fishing = world.Player.Fishing.State;
            fishingPanel.SetActive(!world.IsPaused && fishing.IsActive);
            bool pullBack = world.Player.Equipment?.Id == "FishingRod";
            tension.fillAmount = fishing.Phase == HowToFishFishingPhase.Charging ? fishing.Charge : pullBack ? fishing.Progress : fishing.Tension;
            tension.color = fishing.Tension > 0.75f ? new Color(0.95f, 0.24f, 0.12f) : new Color(0.94f, 0.76f, 0.24f);
            fishingStatus.text = fishing.Phase switch
            {
                HowToFishFishingPhase.Charging => "松手抛竿",
                HowToFishFishingPhase.Flying => "抛竿",
                HowToFishFishingPhase.Waiting => world.Player.Equipment?.Id == "FishingRod"
                    ? "按住 " + world.Input.BindingLabel("Use") + " 慢收，吸引鱼咬钩" : "等待鱼讯…",
                HowToFishFishingPhase.Bite => world.Player.Equipment?.Id == "FishingRod" ? "咬钩！松开后再按下收线" : "咬钩！按下收线",
                HowToFishFishingPhase.Reeling => pullBack ? $"连按收线 {fishing.Progress:P0}" : $"收线 {fishing.Progress:P0} · 松手降低张力",
                _ => ""
            };
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
