using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.HowToFish
{
    /// Demo 场景会话的所有者：资源、输入、存档、实体交互和 Hub 往返。
    public sealed class HowToFishWorld : MonoBehaviour
    {
        [SerializeField] private HowToFishCatalog catalog;
        [SerializeField] private InputActionAsset inputTemplate;
        [SerializeField] private HowToFishPlayer player;
        [SerializeField] private HowToFishBoat boat;
        [SerializeField] private HowToFishVolcanoCrater crater;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform[] clamPoints;
        [SerializeField] private Transform[] leechPoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] snailPoints = Array.Empty<Transform>();
        [SerializeField] private float clamRegrowSeconds = 30;
        private readonly List<HowToFishWorldItem> items = new List<HowToFishWorldItem>();
        private readonly List<Light> suspendedLights = new List<Light>();
        private readonly List<HowToFishStation> deliveryStations = new List<HowToFishStation>();
        private HowToFishInput input;
        private HowToFishSession session;
        private HowToFishSaveStore saves;
        private CancellationTokenSource lifetime;
        private float previousTimeScale;
        private CursorLockMode previousCursor;
        private bool previousCursorVisible;
        private int slot;
        private float messageUntil;
        private float clamRegrowAt;
        private bool initialized;
        private bool exiting;
        private HowToFishIsland[] islands;
        private IHowToFishBoss currentBoss;
        private HowToFishWorldItem tunaBait;
        private float tunaBaitSeconds;

        public HowToFishSession Session => session;
        public HowToFishCatalog Catalog => catalog;
        public HowToFishPlayer Player => player;
        public HowToFishInput Input => input;
        /// 已装配区域；雷达只显示进度已解锁的坐标。
        public IReadOnlyList<HowToFishIsland> Islands => islands;
        public bool IsPaused { get; private set; } = true;
        public bool ShowJournal { get; private set; }
        public bool ShowEnding { get; private set; }
        public bool HasSession => session != null;
        public string Notice { get; private set; }
        /// 当前有效首领遭遇；兼容首领死亡和 Unity 对象已销毁的状态。
        public IHowToFishBoss ActiveBoss => currentBoss?.Item != null && currentBoss.IsFighting ? currentBoss : null;
        public event Action Changed;

        private void Awake()
        {
            previousTimeScale = Time.timeScale;
            previousCursor = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
        }

        private void Start() => InitializeAsync().Forget();

        private async UniTaskVoid InitializeAsync()
        {
            lifetime = new CancellationTokenSource();
            try
            {
                if (catalog == null || inputTemplate == null || player == null || boat == null || startPoint == null)
                    throw new InvalidOperationException("渔力全开场景引用不完整，请运行专属场景装配工具。");
                var navigator = GameSceneNavigator.Instance;
                if (navigator == null) throw new InvalidOperationException("请从 AppEntrance 的 Hub 进入渔力全开。");
                await navigator.WaitUntilStableAsync(GameSceneId.HowToFish, lifetime.Token);
                catalog.Validate();
                if (crater != null) crater.WhaleOffered += OfferWhale;
                islands = FindObjectsByType<HowToFishIsland>(FindObjectsSortMode.None)
                    .Where(island => island.gameObject.scene == gameObject.scene).OrderBy(island => island.Index).ToArray();
                input = new HowToFishInput(inputTemplate);
                saves = new HowToFishSaveStore(Path.Combine(Application.persistentDataPath, "HowToFish"));
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.enabled && light.gameObject.scene != gameObject.scene) { suspendedLights.Add(light); light.enabled = false; }
                var shown = await UIManager.Instance.ShowAsync<HowToFishHudView, HowToFishWorld>(this,
                    new UIShowOptions(animated: false), lifetime.Token);
                if (shown.Status == UIOperationStatus.Failed) throw shown.Exception;
                initialized = true;
                SetPaused(true);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        /// <summary>读取某槽的状态，供菜单决定继续、新建或恢复。</summary>
        /// <param name="index">范围 0–2 的槽号。</param>
        public HowToFishLoadResult InspectSlot(int index) => saves.Load(index);

        /// <summary>从选定槽开始。已有存档的重开必须由界面确认后调用。</summary>
        /// <param name="index">槽号。</param>
        /// <param name="newGame">是否新建进度。</param>
        /// <param name="recoverBackup">是否明确选择了备份恢复。</param>
        public void StartSlot(int index, bool newGame, bool recoverBackup = false)
        {
            if (session != null) { Notify("请先返回 Hub，再切换存档。"); return; }
            try
            {
                if (recoverBackup) saves.RestoreBackup(index);
                var loaded = saves.Load(index);
                if (loaded.Status != HowToFishLoadStatus.Ready && loaded.Status != HowToFishLoadStatus.Empty)
                { Notify(loaded.Error); return; }
                if (!newGame && loaded.Status != HowToFishLoadStatus.Ready) { Notify("此槽还没有存档。"); return; }
                var state = newGame ? new HowToFishSaveData
                {
                    safePosition = startPoint.position, safeYaw = startPoint.eulerAngles.y,
                    boatPosition = boat.transform.position, boatYaw = boat.transform.eulerAngles.y
                } : loaded.Data;
                slot = index;
                session = new HowToFishSession(catalog, state);
                boat.BindIslands(islands);
                session.Changed += OnStateChanged;
                if (session.Count("FreeLure") == 0) session.GrantItem("FreeLure");
                player.Initialize(session, catalog, input, Spawn);
                player.Message += Notify;
                player.InteractRequested += Interact;
                player.Died += Respawn;
                foreach (var station in FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None))
                    if (station.gameObject.scene == gameObject.scene &&
                        (station.Kind == HowToFishStationKind.GrillMaster || station.Kind == HowToFishStationKind.Keeper || station.Kind == HowToFishStationKind.ForestLady ||
                        station.Kind == HowToFishStationKind.Tourist || station.Kind == HowToFishStationKind.Islander || station.Kind == HowToFishStationKind.Scientist))
                    { deliveryStations.Add(station); station.DeliveryRequested += DeliverToStation; }
                foreach (var grill in FindObjectsByType<HowToFishGrill>(FindObjectsSortMode.None))
                    if (grill.gameObject.scene == gameObject.scene) grill.Initialize(session, player);
                RestorePositions(state);
                if (newGame)
                {
                    foreach (var point in clamPoints) Spawn("Clam", point.position, false);
                    foreach (var point in leechPoints) Spawn("Leech", point.position, false);
                }
                else foreach (var item in state.worldItems) Spawn(item.definitionId, item.position, item.isDrip).Restore(item);
                clamRegrowAt = Time.time + clamRegrowSeconds;
                SetPaused(false);
                if (newGame) Save();
                Notify(newGame ? "拾起岸边的蛤蜊，交给看守人换钱，再到灯塔木门购买钓竿。" : "已恢复到最近的安全位置。");
            }
            catch (Exception exception) { Notify("无法开始游戏：" + exception.Message); Debug.LogException(exception, this); }
        }

        /// <summary>生成一个保存好的实体 Prefab 并登记其生命周期。</summary>
        /// <param name="id">生物或物品定义 ID。</param>
        /// <param name="position">生成位置。</param>
        /// <param name="drip">珍稀变体。</param>
        public HowToFishWorldItem Spawn(string id, Vector3 position, bool drip)
        {
            var prefab = catalog.FindCreature(id)?.Prefab ?? catalog.FindItem(id)?.Prefab;
            if (prefab == null) throw new InvalidOperationException("缺少实体 Prefab：" + id);
            var item = Instantiate(prefab, position, Quaternion.identity).GetComponent<HowToFishWorldItem>();
            item.Initialize(session, catalog, id, drip);
            var dynamite = item.GetComponent<HowToFishDynamite>();
            if (dynamite != null) dynamite.Initialize(player);
            var fishMotion = item.GetComponent<HowToFishFishMotion>();
            if (fishMotion != null) fishMotion.Initialize(player);
            var crab = item.GetComponent<HowToFishSpiderCrab>();
            if (crab != null) { crab.Initialize(player); item.Defeated += DropCrabMeat; }
            var piranha = item.GetComponent<HowToFishGiantPiranha>();
            if (piranha != null) { piranha.Initialize(player, Spawn); item.Defeated += DropPiranhaSkeleton; }
            var pufferfish = item.GetComponent<HowToFishPufferfish>();
            if (pufferfish != null) { pufferfish.Initialize(player); item.Defeated += DropPufferfish; }
            var shark = item.GetComponent<HowToFishBlueShark>();
            if (shark != null) shark.Initialize(player);
            var tuna = item.GetComponent<HowToFishTuna>();
            if (tuna != null) tuna.Initialize(player);
            var miniBoss = item.GetComponent<HowToFishJumpingFish>();
            if (miniBoss != null) miniBoss.Initialize(player);
            var seagull = item.GetComponent<HowToFishSeagull>();
            if (seagull != null)
            {
                var home = position; home.y = 0;
                var ground = Physics.RaycastAll(position + Vector3.up * 80, Vector3.down, 160, ~0, QueryTriggerInteraction.Ignore)
                    .Where(hit => hit.collider.GetComponentInParent<HowToFishWorldItem>() == null && hit.collider.GetComponentInParent<HowToFishPlayer>() == null)
                    .OrderBy(hit => hit.distance).FirstOrDefault();
                if (ground.collider != null) home.y = ground.point.y;
                seagull.Initialize(home, items);
            }
            var albatross = item.GetComponent<HowToFishAlbatross>();
            if (albatross != null) { albatross.Initialize(player); item.Defeated += DropAlbatrossHead; }
            var whale = item.GetComponent<HowToFishWhale>();
            if (whale != null)
            {
                whale.Initialize(player);
                if (id == "MutatedBowheadWhale") item.Defeated += DropWhaleFin;
            }
            var boss = item.GetComponent<IHowToFishBoss>();
            if (boss != null && item.Creature?.IsBoss == true) currentBoss = boss;
            if (item.Creature?.IsBoss == true) player.Fishing.TrackBoss(item);
            items.Add(item);
            return item;
        }

        /// 保存最近安全点和全部非活动首领实体。
        public void Save() => TrySave();

        private bool TrySave()
        {
            if (session == null) return false;
            if (items.Any(item => item != null && !item.IsConsumed && item.Creature?.IsBoss == true && item.IsAlive))
            { Notify("首领战斗结束后才能保存。"); return false; }
            try
            {
                var state = session.State;
                state.boatPosition = boat.transform.position; state.boatYaw = boat.transform.eulerAngles.y;
                state.isDriving = player.IsDriving;
                state.isOnBoat = player.IsDriving || (Physics.Raycast(player.transform.position + Vector3.up * .1f,
                    Vector3.down, out var floor, 2, ~0, QueryTriggerInteraction.Ignore) && floor.rigidbody == boat.GetComponent<Rigidbody>());
                state.boatLocalPosition = state.isOnBoat ? boat.transform.InverseTransformPoint(player.transform.position) : Vector3.zero;
                state.boatLocalYaw = state.isOnBoat ? Mathf.DeltaAngle(boat.transform.eulerAngles.y, player.transform.eulerAngles.y) : 0;
                if (!state.isOnBoat && player.GetComponent<CharacterController>().isGrounded && player.transform.position.y > .1f)
                { state.safePosition = player.transform.position; state.safeYaw = player.transform.eulerAngles.y; state.safeIsland = player.Island; }
                state.worldItems.Clear();
                foreach (var item in items)
                    if (item != null && !item.IsConsumed) state.worldItems.Add(item.Snapshot());
                saves.Save(slot, state);
                Notify("已保存到存档 " + (slot + 1));
                return true;
            }
            catch (Exception exception) { Notify("保存失败，原存档已保留：" + exception.Message); Debug.LogException(exception, this); return false; }
        }

        /// <summary>暂停或恢复当前世界。</summary>
        /// <param name="paused">是否暂停。</param>
        public void SetPaused(bool paused)
        {
            IsPaused = paused || session == null || ShowEnding;
            if (!IsPaused) ShowJournal = false;
            Time.timeScale = IsPaused ? 0 : previousTimeScale;
            player.SetControls(!IsPaused);
            input?.SetMode(player.IsDriving, IsPaused);
            Changed?.Invoke();
        }

        /// 返回 Hub，先关闭本 Demo 的界面，再由现有导航卸载场景。
        public void ReturnToHub() => ReturnToHubAsync().Forget();

        private async UniTaskVoid ReturnToHubAsync()
        {
            if (exiting) return;
            exiting = true;
            SetPaused(true);
            try
            {
                await UIManager.Instance.CloseAsync<HowToFishHudView>();
                Time.timeScale = previousTimeScale;
                var result = await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
                if (result.Status == GameSceneSwitchStatus.Failed || result.Status == GameSceneSwitchStatus.Busy)
                {
                    await UIManager.Instance.ShowAsync<HowToFishHudView, HowToFishWorld>(this, new UIShowOptions(animated: false));
                    exiting = false; SetPaused(true); Notify("返回失败：" + result.Error);
                }
            }
            catch (Exception exception) { exiting = false; Notify(exception.Message); Debug.LogException(exception, this); }
        }

        public string FocusText()
        {
            if (!HasSession || IsPaused) return "";
            if (player.IsDriving) return "[" + input.BindingLabel("Interact") + "] 离开驾驶位";
            var focus = player.Focus;
            if (focus == null) return "";
            var station = focus.GetComponentInParent<HowToFishStation>();
            if (station != null)
            {
                if (station.Kind == HowToFishStationKind.InventoryUpgrade)
                    return session.NextSlotCost == 0 ? "装备栏已扩至上限" :
                        "[" + input.BindingLabel("Interact") + "] 扩充装备栏  $" + session.NextSlotCost + $"\n{session.EquipmentCapacity} → {session.EquipmentCapacity + 1} 格";
                if (station.Kind == HowToFishStationKind.MotorUpgrade)
                    return session.State.boatMotorTier >= station.MotorTier ? station.Label + " · 已拥有更高或相同级别" :
                        "[" + input.BindingLabel("Interact") + "] " + station.Label + "  $" + (station.MotorTier == 1 ? 230 : 860);
                if (station.Kind == HowToFishStationKind.BoatRadar)
                    return session.State.hasBoatRadar ? "船载雷达 · 已安装" : "[" + input.BindingLabel("Interact") + "] 船载雷达  $200";
                if (station.Kind == HowToFishStationKind.Attachment)
                {
                    var weapon = player.Equipment;
                    if (!session.CanBuyAttachment(weapon?.Id, station.Attachment, station.Island, out var reason)) return station.Label + " · " + reason;
                    return "[" + input.BindingLabel("Interact") + "] " + station.Label + "  $" + weapon.AttachmentPrice(station.Attachment) + "\n" + weapon.DisplayName;
                }
                if (station.Kind == HowToFishStationKind.Anvil || station.Kind == HowToFishStationKind.AmmoUpgrade)
                {
                    var weapon = player.Equipment;
                    var kind = station.Kind == HowToFishStationKind.Anvil ? HowToFishItemKind.Melee : HowToFishItemKind.Gun;
                    if (weapon == null || weapon.Kind != kind) return station.Label + " · 请手持" + (kind == HowToFishItemKind.Melee ? "近战武器" : "枪械");
                    int level = session.UpgradeLevel(weapon.Id), cost = weapon.NextUpgradeCost(level);
                    if (cost == 0) return weapon.DisplayName + " · 已升满";
                    if (level >= (station.Island + (kind == HowToFishItemKind.Melee ? 1 : 0)) * 3) return weapon.DisplayName + " · 本岛升级已达上限";
                    return "[" + input.BindingLabel("Interact") + "] " + station.Label + "  $" + cost +
                        $"\n{weapon.DisplayName} · 伤害 {weapon.DamageAtLevel(level)} → {weapon.DamageAtLevel(level + 1)}";
                }
                var product = catalog.FindItem(station.ItemId);
                return "[" + input.BindingLabel("Interact") + "] " + station.Label + (product == null ? "" : "  $" + product.Price);
            }
            var item = focus.GetComponentInParent<HowToFishWorldItem>();
            if (item != null && item.Creature?.IsMainBoss == true) return item.Creature.DisplayName + " · 领取掉落的战利品继续任务";
            if (item != null && item.Creature?.IsBoss == true && item.IsAlive && item.DefinitionId != "Tuna") return item.Creature.DisplayName + " · 击败后才能拾取";
            return item == null ? "" : "[" + input.BindingLabel("Interact") + "] 拾取 " + (item.Creature?.DisplayName ?? catalog.FindItem(item.DefinitionId)?.DisplayName) +
                (item.Creature != null && !item.IsAlive ? $"  ${item.SaleValue} · 受热 {item.Cooking:P0}" : "");
        }

        public void Notify(string message) { Notice = message; messageUntil = Time.unscaledTime + 6; Changed?.Invoke(); }
        private void OnStateChanged()
        {
            boat.ApplyUpgrades(session.State);
            Changed?.Invoke();
        }

        private void Update()
        {
            if (!initialized || exiting) return;
            if (!ShowEnding && session != null && input.Pressed("Pause")) SetPaused(!IsPaused);
            if (!ShowEnding && session != null && input.Pressed("Journal")) { ShowJournal = !ShowJournal; SetPaused(ShowJournal); }
            if (Notice != null && Time.unscaledTime > messageUntil) { Notice = null; Changed?.Invoke(); }
            if (session == null || IsPaused) return;
            session.State.playedSeconds += Time.deltaTime;
            foreach (var island in islands)
                if (island.Index <= session.State.unlockedIsland && island.Index != player.Island &&
                    island.DistanceToShore(player.transform.position) < 15)
                {
                    player.Island = island.Index;
                    Notify("抵达 " + island.DisplayName);
                    break;
                }
            session.State.hunger = Mathf.Max(0, session.State.hunger - Time.deltaTime * 0.06f);
            if (session.State.hunger <= 0) player.Damage(Time.deltaTime);
            items.RemoveAll(item => item == null);
            UpdateTunaBait();
            if (Time.time >= clamRegrowAt)
            {
                clamRegrowAt = Time.time + clamRegrowSeconds;
                foreach (var point in clamPoints)
                    if (!items.Any(item => !item.IsConsumed && item.DefinitionId == "Clam" &&
                        (item.transform.position - point.position).sqrMagnitude < 4)) Spawn("Clam", point.position, false);
                foreach (var point in leechPoints)
                    if (!items.Any(item => !item.IsConsumed && item.DefinitionId == "Leech" &&
                        (item.transform.position - point.position).sqrMagnitude < 4)) Spawn("Leech", point.position, false);
                if (session.State.unlockedIsland >= 4 && player.Island == 4)
                    foreach (var point in snailPoints)
                        if (!items.Any(item => !item.IsConsumed && item.DefinitionId == "FootSnail" &&
                            (item.transform.position - point.position).sqrMagnitude < 4)) Spawn("FootSnail", point.position, false);
                var island = islands.First(value => value.Index == player.Island);
                if (items.Count(item => item.DefinitionId == "Seagull" && item.IsAlive && island.DistanceToShore(item.transform.position) < 15) < 2)
                    Spawn("Seagull", player.transform.position + new Vector3(8, 8, 8), false);
            }
        }

        private void Interact(Collider collider)
        {
            if (player.IsDriving) { boat.SetDriver(null); player.Teleport(boat.ExitPosition, boat.transform.eulerAngles.y); return; }
            if (collider == null) return;
            var station = collider.GetComponentInParent<HowToFishStation>();
            if (station == null) { player.PickUp(collider.GetComponentInParent<HowToFishWorldItem>()); return; }
            switch (station.Kind)
            {
                case HowToFishStationKind.Product:
                    Notify(session.TryBuy(station.ItemId, station.Island, out var reason) ? "已购买 " + catalog.FindItem(station.ItemId).DisplayName : reason);
                    break;
                case HowToFishStationKind.Sell:
                    if (player.HeldItem != null && player.HeldItem.TrySell(out int value)) Notify("售出鱼获 +$" + value);
                    else Notify("拿着处理好的鱼获来出售。");
                    break;
                case HowToFishStationKind.Keeper: TalkToKeeper(); break;
                case HowToFishStationKind.ForestLady:
                    if (!TryDeliverToForestLady(player.HeldItem))
                        Notify(session.State.unlockedIsland >= 2 ? "湖畔女士：沙漠在西北方，坐标已经给你。" :
                            $"湖畔女士：带给我三条草地上的水蛭，我给你改造鱼饵。已收到 {session.State.forestLeeches}/3；打败巨型食人鱼后，把骨架带回来。");
                    break;
                case HowToFishStationKind.BoatWheel:
                    if (!session.State.hasBoatKey) { Notify("先帮灯塔看守人拿到船钥匙。"); break; }
                    player.Board(boat.Seat); boat.SetDriver(input); break;
                case HowToFishStationKind.Grill:
                    Notify(session.State.hasGrill ? "把鱼获放到或拿在烤架上方持续加热；熟成后及时取走，继续加热会烧焦。" :
                        "先向烧烤师交付一条死蓝鲨或哥布林鲨，再领取打火机。"); break;
                case HowToFishStationKind.GrillMaster:
                    if (TryDeliverToGrillMaster(player.HeldItem)) break;
                    if (session.State.hasGrill) { Notify("烤炉已经可以使用，留意鱼获颜色和售价，别烤焦了。"); break; }
                    if (!session.State.completedQuests.Contains("GrillSharkDelivered")) { Notify("带来一条你拿过的死蓝鲨或哥布林鲨，我就给你打火机。"); break; }
                    if (session.Count("Lighter") >= 100000) { Notify("打火机已达到携带上限，奖励暂时保留。"); break; }
                    session.GrantItem("Lighter");
                    session.State.hasGrill = true;
                    if (!session.State.completedQuests.Contains("DesertGrill")) session.State.completedQuests.Add("DesertGrill");
                    if (TrySave()) Notify("获得打火机，烤炉已解锁。");
                    break;
                case HowToFishStationKind.Tourist:
                    if (!TryDeliverToTourist(player.HeldItem))
                        Notify(session.State.unlockedIsland >= 3 ? "游客：岩石岛的坐标已经给你，祝你一路顺风。" :
                            "游客：把处理好的濒危鱼带给我，颌针鱼、海马或鱼缸鱼都可以。我给你萝卜去钓河豚，之后请把鱼鳍带回来。");
                    break;
                case HowToFishStationKind.Anvil:
                case HowToFishStationKind.AmmoUpgrade:
                    var weaponKind = station.Kind == HowToFishStationKind.Anvil ? HowToFishItemKind.Melee : HowToFishItemKind.Gun;
                    if (player.Equipment?.Kind != weaponKind) { Notify(weaponKind == HowToFishItemKind.Melee ? "请先手持近战武器。" : "请先手持枪械。"); break; }
                    Notify(session.TryUpgrade(player.Equipment?.Id, station.Island, out var upgradeError) ? "武器升级完成。" : upgradeError); break;
                case HowToFishStationKind.Attachment:
                    Notify(session.TryBuyAttachment(player.Equipment?.Id, station.Attachment, station.Island, out var attachmentError)
                        ? "已安装" + station.Label : attachmentError); break;
                case HowToFishStationKind.InventoryUpgrade:
                    Notify(session.TryExpandInventory(station.Island, out var inventoryError) ? "装备栏已扩容。" : inventoryError); break;
                case HowToFishStationKind.MotorUpgrade:
                    Notify(session.TryBuyMotor(station.MotorTier, station.Island, out var motorError) ? "船只马达已升级。" : motorError); break;
                case HowToFishStationKind.BoatRadar:
                    Notify(session.TryBuyBoatRadar(station.Island, out var radarError) ? "船载雷达已安装。" : radarError); break;
                case HowToFishStationKind.Scientist: TalkToScientist(); break;
                case HowToFishStationKind.MilitaryDeparture:
                    if (!session.State.hasMilitaryBoatKey) { Notify("需要科学家交给你的军用船钥匙。"); break; }
                    session.State.hasFinished = true;
                    if (TrySave()) { ShowEnding = true; ShowJournal = false; SetPaused(true); Notify("你乘军用船回到了大陆。航程已保存，可以继续探索群岛。"); }
                    break;
                case HowToFishStationKind.Islander:
                    if (!TryDeliverToIslander(player.HeldItem))
                        Notify(session.State.unlockedIsland >= 4 ? "岛民：火山的坐标已经给你，准备好再出发。" :
                            "岛民：用专业首领饵钓金枪鱼，打倒后把完整生鱼放在岸上引鸟。屋顶能挡落物，请把鸟头带回来。");
                    break;
            }
        }

        private void TalkToKeeper()
        {
            if (TryDeliverToKeeper(player.HeldItem)) return;
            if (session.State.hasBoatKey) { Notify("看守人：钥匙交给你了，去探索下一座岛吧。"); return; }
            if (session.TryConsume("CrabMeat"))
            {
                GiveBoatKey(); return;
            }
            if (session.Count("CrabRod") == 0) { Notify("看守人：把蛤蜊交给我，攒够 $3 就去木门买钓竿。蛤蜊也能填饱肚子。"); return; }
            if (session.Count("EmptyBeerCan") > 0) { Notify("看守人：用空啤酒罐作饵，打败蜘蛛蟹，把蟹壳带回来。"); return; }
            if (session.TryConsume("Beer"))
            {
                GiveEmptyCan();
            }
            else Notify("看守人：先给我一罐啤酒，我有办法帮你离开这里。");
        }

        private void DeliverToStation(HowToFishStation station, HowToFishWorldItem item)
        {
            if (session == null || IsPaused || exiting) return;
            if (station.Kind == HowToFishStationKind.Keeper) TryDeliverToKeeper(item);
            else if (station.Kind == HowToFishStationKind.ForestLady) TryDeliverToForestLady(item);
            else if (station.Kind == HowToFishStationKind.Tourist) TryDeliverToTourist(item);
            else if (station.Kind == HowToFishStationKind.Islander) TryDeliverToIslander(item);
            else if (station.Kind == HowToFishStationKind.Scientist) TryDeliverToScientist(item);
            else if (station.Kind == HowToFishStationKind.GrillMaster) TryDeliverToGrillMaster(item);
        }

        private bool TryDeliverToGrillMaster(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || !item.HasBeenHeld || session.State.hasGrill ||
                session.State.completedQuests.Contains("GrillSharkDelivered") ||
                (item.DefinitionId != "BlueShark" && item.DefinitionId != "GoblinShark")) return false;
            return item.TryConsume(() =>
            {
                session.State.completedQuests.Add("GrillSharkDelivered");
                if (TrySave()) Notify("烧烤师收下了鲨鱼，再交谈领取打火机。");
            });
        }

        /// 离开结局面板，保留当前进度和装备继续探索。
        public void ContinueAfterEnding()
        {
            if (!ShowEnding) return;
            ShowEnding = false;
            SetPaused(false);
        }

        private void TalkToScientist()
        {
            if (TryDeliverToScientist(player.HeldItem)) return;
            if (session.State.hasMilitaryBoatKey) { Notify("科学家：军用船钥匙交给你了。登上黑色充气艇，在船舵处启程吧。"); return; }
            if (session.State.volcanoFish < 5)
            { Notify($"科学家：给我五条处理好的鱼，我来准备鱼桶。已收到 {session.State.volcanoFish}/5。"); return; }
            if (session.Count("FishBucket") > 0)
            { Notify("科学家：用鱼桶钓鲸鱼；把完整生鲸尸投入火山，再把变异鲸鱼鳍交给我。"); return; }
            session.GrantItem("FishBucket");
            if (!session.State.completedQuests.Contains("VolcanoFishBucket")) session.State.completedQuests.Add("VolcanoFishBucket");
            if (TrySave()) Notify("领到鱼桶。用普通鱼竿在火山周围水域钓出弓头鲸。");
        }

        private bool TryDeliverToScientist(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || session.State.unlockedIsland < 4) return false;
            if (item.DefinitionId == "WhaleFin" && !session.State.hasMilitaryBoatKey)
                return item.TryConsume(() =>
                {
                    session.GrantItem("MilitaryBoatKey");
                    session.State.hasMilitaryBoatKey = true;
                    if (!session.State.completedQuests.Contains("VolcanoWhale")) session.State.completedQuests.Add("VolcanoWhale");
                    if (TrySave()) Notify("科学家：拿好军用船钥匙，你可以回家了！");
                });
            var creature = item.Creature;
            if (session.State.volcanoFish >= 5 || creature == null || creature.IsBoss || creature.IsGroundPickup || creature.Island == 0) return false;
            return item.TryConsume(() =>
            {
                session.State.volcanoFish++;
                if (TrySave()) Notify(session.State.volcanoFish == 5 ? "科学家：鱼桶准备好了，再与我交谈领取。" : $"科学家：已经收到 {session.State.volcanoFish}/5 条鱼。");
            });
        }

        private void OfferWhale(HowToFishWorldItem item)
        {
            if (session == null || IsPaused || exiting || session.State.unlockedIsland < 4 || item == null ||
                item.IsConsumed || item.IsAlive || item.IsHeld || item.IsCooked || item.DefinitionId != "BowheadWhale" ||
                items.Any(value => value != null && !value.IsConsumed && value.Creature?.IsBoss == true && value.IsAlive)) return;
            if (item.TryConsume(() => Spawn("MutatedBowheadWhale", crater.BossSpawnPosition, item.IsDrip)))
                Notify("鲸尸沉入熔岩，变异弓头鲸出现！躲开跃击与地面熔岩。");
        }

        private void DropWhaleFin(HowToFishWorldItem whale)
        {
            whale.TryConsume(() =>
            {
                Spawn("WhaleFin", whale.transform.position + Vector3.up, false);
                for (int i = 0; i < 4; i++) Spawn("FishMeat", whale.transform.position + new Vector3(i * .5f - .75f, .5f, 1), false);
            });
            Notify("变异弓头鲸已击败！把鲸鱼鳍交给科学家。");
        }

        private void UpdateTunaBait()
        {
            if (session.State.unlockedIsland < 3 || player.Island != 3 ||
                items.Any(item => !item.IsConsumed && item.Creature?.IsBoss == true && item.IsAlive))
            { tunaBait = null; tunaBaitSeconds = 0; return; }
            var candidate = items.FirstOrDefault(item => item.DefinitionId == "Tuna" && !item.IsConsumed &&
                !item.IsAlive && !item.IsHeld && !item.IsCooked && item.transform.position.y > .2f &&
                Physics.Raycast(item.transform.position + Vector3.up * .2f, Vector3.down, out var ground, 2, ~0, QueryTriggerInteraction.Ignore) &&
                ground.collider.GetComponentInParent<HowToFishIsland>()?.Index == 3);
            if (candidate != tunaBait) { tunaBait = candidate; tunaBaitSeconds = 0; }
            if (candidate == null) return;
            tunaBaitSeconds += Time.deltaTime;
            if (tunaBaitSeconds < 4) return;
            var position = candidate.transform.position + Vector3.up * 18;
            candidate.TryConsume(() => Spawn("Albatross", position, false));
            tunaBait = null; tunaBaitSeconds = 0;
            Notify("信天翁被金枪鱼吸引来了！利用商店屋顶掩护。");
        }

        private bool TryDeliverToIslander(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || item.DefinitionId != "AlbatrossHead" ||
                session.State.unlockedIsland < 3) return false;
            return item.TryConsume(() =>
            {
                session.State.unlockedIsland = 4;
                if (!session.State.completedQuests.Contains("RocksAlbatross")) session.State.completedQuests.Add("RocksAlbatross");
                if (TrySave()) Notify("岛民：终于清静了！这是北方火山岛的坐标。");
            });
        }

        private void DropAlbatrossHead(HowToFishWorldItem bird)
        {
            bird.TryConsume(() =>
            {
                Spawn("AlbatrossHead", bird.transform.position, false);
                for (int i = 0; i < 3; i++) Spawn("FishMeat", bird.transform.position + new Vector3(i * .4f - .4f, 0, .4f), false);
            });
            Notify("信天翁已击败，带鸟头去商店找岛民。");
        }

        private bool TryDeliverToTourist(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive || session.State.unlockedIsland < 2) return false;
            if (item.DefinitionId == "PufferfishFin") return item.TryConsume(() =>
            {
                session.State.unlockedIsland = Mathf.Max(session.State.unlockedIsland, 3);
                if (!session.State.completedQuests.Contains("DesertPufferfish")) session.State.completedQuests.Add("DesertPufferfish");
                if (TrySave()) Notify("游客：谢谢！这是西北方向岩石岛的坐标。");
            });
            if (item.Creature?.IsEndangered != true) return false;
            if (session.Count("Carrot") >= 100000) { Notify("萝卜已达到携带上限。"); return true; }
            return item.TryConsume(() =>
            {
                session.GrantItem("Carrot");
                if (!session.State.completedQuests.Contains("DesertCarrot")) session.State.completedQuests.Add("DesertCarrot");
                if (TrySave()) Notify("游客：萝卜给你了。用普通鱼竿钓出河豚，记得把鱼鳍交回来。");
            });
        }

        private bool TryDeliverToForestLady(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || session.State.unlockedIsland < 1) return false;
            if (item.DefinitionId == "Leech" && session.State.forestLeeches == 2 && session.Count("ModifiedLeech") >= 100000)
            { Notify("改造水蛭已达到携带上限，请先使用一些。"); return true; }
            if (item.DefinitionId == "Leech") return item.TryConsume(() =>
            {
                if (session.State.forestLeeches == 2)
                {
                    session.GrantItem("ModifiedLeech");
                    session.State.forestLeeches = 0;
                    if (!session.State.completedQuests.Contains("ForestLeeches")) session.State.completedQuests.Add("ForestLeeches");
                }
                else session.State.forestLeeches++;
                if (TrySave()) Notify(session.State.forestLeeches == 0 ? "湖畔女士：改造水蛭给你了，在湖边用它钓出巨型食人鱼！" :
                    $"湖畔女士：已收到 {session.State.forestLeeches}/3 条水蛭。");
            });
            if (item.DefinitionId == "PiranhaSkeleton") return item.TryConsume(() =>
            {
                session.State.unlockedIsland = Mathf.Max(session.State.unlockedIsland, 2);
                if (!session.State.completedQuests.Contains("ForestPiranha")) session.State.completedQuests.Add("ForestPiranha");
                if (TrySave()) Notify("湖畔女士：谢谢！这是西北方沙漠岛的坐标。");
            });
            return false;
        }

        private bool TryDeliverToKeeper(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed || item.IsAlive) return false;
            if (item.DefinitionId == "CrabMeat" && !session.State.hasBoatKey)
                return item.TryConsume(GiveBoatKey);
            if (item.DefinitionId == "Beer") return item.TryConsume(GiveEmptyCan);
            if (item.Creature != null && !item.Creature.IsBoss && item.TrySell(out int money))
            { Notify("看守人收下鱼获，付给你 $" + money); return true; }
            return false;
        }

        private void GiveBoatKey()
        {
            session.State.hasBoatKey = true;
            session.State.unlockedIsland = Mathf.Max(session.State.unlockedIsland, 1);
            if (!session.State.completedQuests.Contains("KeeperCrab")) session.State.completedQuests.Add("KeeperCrab");
            if (TrySave()) Notify("看守人：船钥匙给你了。带上雷达，向西北寻找森林岛。");
        }

        private void GiveEmptyCan()
        {
            session.GrantItem("EmptyBeerCan");
            if (!session.State.completedQuests.Contains("KeeperBeer")) session.State.completedQuests.Add("KeeperBeer");
            if (TrySave()) Notify("看守人：谢了！空罐拿去钓蜘蛛蟹吧。");
        }

        private void DropCrabMeat(HowToFishWorldItem crab)
        {
            Spawn("CrabMeat", crab.transform.position + Vector3.up, false);
            Notify("蜘蛛蟹被打败了！拾起掉落的蟹壳，交给灯塔看守人。");
        }

        private void DropPiranhaSkeleton(HowToFishWorldItem fish)
        {
            Spawn("PiranhaSkeleton", fish.transform.position + Vector3.up, false);
            Notify("巨型食人鱼被打败了！把骨架交给湖畔女士。");
        }

        private void DropPufferfish(HowToFishWorldItem fish)
        {
            // 河豚分解为任务鱼鳍和可食肉块，不把完整尸体同时保留成重复战利品。
            fish.TryConsume(() =>
            {
                Spawn("PufferfishFin", fish.transform.position + Vector3.up, false);
                for (int i = 0; i < 3; i++) Spawn("FishMeat", fish.transform.position + new Vector3(i * .4f - .4f, .6f, .4f), false);
            });
            Notify("河豚被打败了！把鱼鳍交给游客，肉块可以吃下或烹饪。");
        }

        private void Respawn()
        {
            var remains = Spawn("PlayerRemains", player.transform.position + Vector3.up, false);
            remains.Body.rotation = Quaternion.Euler(0, player.transform.eulerAngles.y, 90);
            session.State.health = 100;
            session.State.hunger = Mathf.Max(35, session.State.hunger);
            RestorePositions(session.State);
            SetPaused(false);
            Notify("回到了最近的安全位置。散落的物品仍留在原处。");
        }

        private void RestorePositions(HowToFishSaveData state)
        {
            boat.ApplyUpgrades(state);
            boat.SetDriver(null);
            var body = boat.GetComponent<Rigidbody>();
            body.position = state.boatPosition;
            body.rotation = Quaternion.Euler(0, state.boatYaw, 0);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            player.Teleport(state.safePosition, state.safeYaw);
            player.Island = state.safeIsland;
            if (state.isDriving) { player.Board(boat.Seat); boat.SetDriver(input); }
            else if (state.isOnBoat) player.Teleport(state.boatPosition + Quaternion.Euler(0, state.boatYaw, 0) * state.boatLocalPosition,
                state.boatYaw + state.boatLocalYaw);
        }

        private void OnDestroy()
        {
            lifetime?.Cancel(); lifetime?.Dispose();
            if (crater != null) crater.WhaleOffered -= OfferWhale;
            if (session != null) session.Changed -= OnStateChanged;
            foreach (var station in deliveryStations) if (station != null) station.DeliveryRequested -= DeliverToStation;
            if (player != null) { player.Message -= Notify; player.InteractRequested -= Interact; player.Died -= Respawn; player.Drop(false); }
            input?.Dispose();
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursor; Cursor.visible = previousCursorVisible;
            foreach (var light in suspendedLights) if (light != null) light.enabled = true;
        }
    }
}
