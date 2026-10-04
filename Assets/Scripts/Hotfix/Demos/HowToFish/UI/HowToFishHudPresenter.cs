using System.Text;
using TMPro;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 独立页面保存的业务显示与绑定，生命周期由对应 View 管理。
    public sealed class HowToFishHudPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI stats;
        [SerializeField] private TextMeshProUGUI focus;
        [SerializeField] private TextMeshProUGUI notice;
        [SerializeField] private TextMeshProUGUI controls;
        [SerializeField] private TextMeshProUGUI equipmentSlots;
        [SerializeField] private HowToFishRadarHudPresenter radarHud;
        [SerializeField] private HowToFishBossHudPresenter bossHud;
        [SerializeField] private HowToFishFishingHudPresenter fishingHud;
        private HowToFishWorld world;
        private float refreshAt;
        private readonly StringBuilder equipmentText = new();
        /// <summary>显示前交付当前世界。</summary>
        /// <param name="owner">当前世界。</param>
        public void Bind(HowToFishWorld owner) { Unbind(); world = owner; world.Changed += RefreshGameplay; RefreshGameplay(); }
        /// 隐藏时释放订阅。
        public void Unbind() { if (world != null) world.Changed -= RefreshGameplay; world = null; }
        private void Update()
        { if (world == null || Time.unscaledTime < refreshAt) return; refreshAt = Time.unscaledTime + .05f; RefreshGameplay(); }
        private void RefreshGameplay()
        {
            if (world == null) return;
            notice.text = world.Notice ?? "";
            radarHud.Refresh(world);
            bossHud.Refresh(world);
            fishingHud.Refresh(world);
            if (!world.HasSession)
            { stats.text = ""; focus.text = ""; controls.text = ""; equipmentSlots.text = ""; return; }
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
                    (heldFood.IsDrip ? "<color=#FF7777>D</color><color=#FFDD66>r</color><color=#77EE99>i</color><color=#77BBFF>p</color> · " : "") +
                    (heldFood.IsBurnt ? "烧焦" : heldFood.Cooking >= .45f ? "熟成" : heldFood.IsCooked ? "加热中" : "生") +
                    (heldFood.Creature == null ? "" : $" · {heldFood.Weight:0.##} kg  ${heldFood.SaleValue}");
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
            if (!world.IsPaused && (world.Player.IsDriving || HowToFishSkinCatalog.Supports(world.Player.Equipment?.Id)))
                controls.text += "   " + world.Input.BindingLabel("ChangeSkin") + " 更换皮肤";
            if (!world.IsPaused && !world.Player.IsDriving && world.Player.CanEat)
                controls.text = "按住 " + world.Input.BindingLabel("Use") + " 进食   " + controls.text;
            if (!world.IsPaused && !world.Player.IsDriving && world.Player.HeldItem == null && world.Player.Equipment?.Kind == HowToFishItemKind.Explosive)
                controls.text = world.Input.BindingLabel("Use") + " 点燃投出（3秒）   " + controls.text;

        }

        private void OnDestroy() => Unbind();
    }
}
