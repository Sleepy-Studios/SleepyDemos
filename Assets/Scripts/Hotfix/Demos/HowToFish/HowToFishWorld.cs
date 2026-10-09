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
        [SerializeField]
        private HowToFishCatalog catalog;
        [SerializeField]
        private InputActionAsset inputTemplate;
        [SerializeField]
        private HowToFishPlayer player;
        [SerializeField]
        private HowToFishBoat boat;
        [SerializeField]
        private HowToFishVolcanoCrater crater;
        [SerializeField]
        private ParticleSystem explosionFire;
        [SerializeField]
        private ParticleSystem explosionSmoke;
        [SerializeField]
        private Transform startPoint;
        [SerializeField]
        private Transform[] clamPoints;
        [SerializeField]
        private Transform[] leechPoints = Array.Empty<Transform>();
        [SerializeField]
        private Transform[] snailPoints = Array.Empty<Transform>();
        [SerializeField]
        private float clamRegrowSeconds = 30;

        private readonly List<HowToFishWorldItem> items = new List<HowToFishWorldItem>();

        private readonly List<Light> suspendedLights = new List<Light>();

        private readonly List<HowToFishStation> deliveryStations = new List<HowToFishStation>();

        private HowToFishInput input;

        private View hud;

        internal HowToFishData Data { get; private set; }

        internal CancellationToken Lifetime => lifetime?.Token ?? default;

        private HowToFishSession session => Data?.Session;

        private HowToFishSaveStore saves;

        private bool settingsPauseGate;

        private int settingsClosedFrame;

        private CancellationTokenSource lifetime;

        private float previousTimeScale;

        private CursorLockMode previousCursor;

        private bool previousCursorVisible;

        private float clamRegrowAt;

        private bool initialized;

        private bool exiting => Data?.IsExiting == true;

        private HowToFishIsland[] islands;

        private IHowToFishBoss currentBoss;

        private HowToFishWorldItem tunaBait;

        private float tunaBaitSeconds;

        private bool IsCurrent => this != null && lifetime?.IsCancellationRequested == false && ReferenceEquals(GlobalData.Get<HowToFishData>(), Data);

        internal HowToFishBoat Boat => boat;

        internal Transform StartPoint => startPoint;

        internal HowToFishSaveStore SaveStore => saves;

        internal IReadOnlyList<HowToFishWorldItem> Items => items;

        internal void RequestSound(HowToFishSound sound, Vector3 position) => SoundRequested?.Invoke(sound, position);

        internal void ApplyPauseState()
        {
            Time.timeScale = IsPaused ? 0 : previousTimeScale;
            player.SetControls(!IsPaused);
            input?.SetMode(player.IsDriving, IsPaused);
            Changed?.Invoke();
        }

        internal void SettingsClosedGate()
        {
            settingsPauseGate = true;
            settingsClosedFrame = Time.frameCount;
        }

        public HowToFishSession Session => session;

        public HowToFishCatalog Catalog => catalog;

        public HowToFishPlayer Player => player;

        public HowToFishInput Input => input;

        /// 本机跨存档共享的当前人物服装。
        public string SelectedOutfitId => Data?.SelectedOutfitId ?? HowToFishOutfitCatalog.DefaultId;

        /// 已装配区域；雷达只显示进度已解锁的坐标。
        public IReadOnlyList<HowToFishIsland> Islands => islands;

        public bool IsPaused => Data?.IsPaused ?? true;

        public bool ShowJournal => Data?.ShowJournal == true;

        public bool ShowEnding => Data?.ShowEnding == true;

        public bool HasSession => session != null;

        /// 设置草稿占用菜单时，世界不处理暂停与图鉴快捷键。
        public bool IsEditingSettings => Data?.IsEditingSettings == true;

        public string Notice => Data?.Notice;

        /// 当前有效首领遭遇；兼容首领死亡和 Unity 对象已销毁的状态。
        public IHowToFishBoss ActiveBoss => currentBoss?.Item != null && currentBoss.IsFighting ? currentBoss : null;

        public event Action Changed;

        /// 已提交的世界动作音效，由本场景音源协调器播放。
        public event Action<HowToFishSound, Vector3> SoundRequested;

        /// 已保存 HUD 按钮使用的界面确认音，暂停时仍可播放。
        public void PlayUiSound() => SoundRequested?.Invoke(HowToFishSound.UiClick, player.transform.position);

        private void OnExplosion(Vector3 position)
        {
            // 发射数量和外观尺度为自制反馈，不代表精确伤害范围；静音不阻止视觉。
            EmitExplosion(explosionFire, position, 10);
            EmitExplosion(explosionSmoke, position, 14);
            SoundRequested?.Invoke(HowToFishSound.Explosion, position);
        }

        private static void EmitExplosion(ParticleSystem source, Vector3 position, int count)
        {
            if (source == null)
                return;
            source.transform.position = position;
            // 保存为世界空间，复用发射器时已发出的粒子不会被移到下一处爆点。
            source.Play(false);
            source.Emit(count);
        }

        private void OnBossDefeatedSound(HowToFishWorldItem item) => SoundRequested?.Invoke(HowToFishSound.BossDefeated, item.transform.position);

        private void OnCreatureHurtSound(HowToFishWorldItem item, float damage)
        {
            if (damage > 0)
                SoundRequested?.Invoke(HowToFishSound.Hit, item.transform.position);
        }

        private void Awake()
        {
            var previous = GlobalData.Get<HowToFishData>();
            if (previous != null)
            {
                previous.Handler.Dispose();
                GlobalData.Remove<HowToFishData>();
            }

            Data = GlobalData.Add(new HowToFishData(this));
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
                if (navigator == null)
                    throw new InvalidOperationException("请从 AppEntrance 的 Hub 进入渔力全开。");
                await navigator.WaitUntilStableAsync(GameSceneId.HowToFish, lifetime.Token);
                catalog.Validate();
                if (crater != null)
                    crater.WhaleOffered += OfferWhale;
                islands = FindObjectsByType<HowToFishIsland>(FindObjectsSortMode.None).Where(island => island.gameObject.scene == gameObject.scene).OrderBy(island => island.Index).ToArray();
                input = new HowToFishInput(inputTemplate);
                try
                {
                    var preferences = LocalDataManager.LoadData(LocalDataKeys.HowToFishPreferences, new HowToFishLocalPreferences(), out var warning, HowToFishLocalPreferences.Validate);
                    ApplyPreferences(preferences);
                    if (!string.IsNullOrEmpty(warning))
                        Notify(warning);
                }
                catch (ArgumentException)
                {
                    ApplyPreferences(new HowToFishLocalPreferences());
                    Notify("输入绑定无法加载，已使用默认值；原设置保留，请在设置中重新确认。");
                }

                saves = new HowToFishSaveStore(Path.Combine(Application.persistentDataPath, LocalDataKeys.HowToFishDirectory));
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.enabled && light.gameObject.scene != gameObject.scene)
                    {
                        suspendedLights.Add(light);
                        light.enabled = false;
                    }

                Data.Handler.Initialize();
                var shown = await UIManager.Instance.ShowAsync<HowToFishHudView>(view => view.SetData(this), new UIShowOptions(animated: false), lifetime.Token);
                if (shown.Status == UIOperationStatus.Failed)
                    throw shown.Exception;
                hud = shown.View;
                initialized = true;
                SetPaused(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        /// <summary>读取某槽的状态，供菜单决定继续、新建或恢复。</summary>
        /// <param name="index">范围 0–2 的槽号。</param>
        public HowToFishLoadResult InspectSlot(int index) => saves.Load(index);

        /// <summary>从选定槽开始。已有存档的重开必须由界面确认后调用。</summary>
        /// <param name="index">槽号。</param>
        /// <param name="newGame">是否新建进度。</param>
        public void StartSlot(int index, bool newGame) => GlobalData.Dispatch(new HowToFishStartSlotAction(this, index, newGame));

        internal void BuildSession(bool newGame, HowToFishSaveData state, string profileNotice)
        {
            boat.BindIslands(islands);
            player.Initialize(session, catalog, input, Spawn);
            player.SetOutfit(catalog.FindOutfit(SelectedOutfitId));
            player.Message += Notify;
            player.InteractRequested += Interact;
            player.Died += Respawn;
            player.ChangeSkinRequested += ChangeSkin;
            player.CreatureEaten += OnCreatureEaten;
            foreach (var machine in FindObjectsByType<HowToFishSlotMachine>(FindObjectsSortMode.None))
                if (machine.gameObject.scene == gameObject.scene)
                    machine.Initialize(this);
            foreach (var roulette in FindObjectsByType<HowToFishRoulette>(FindObjectsSortMode.None))
                if (roulette.gameObject.scene == gameObject.scene)
                    roulette.Initialize(this);
            foreach (var station in FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None))
                if (station.gameObject.scene == gameObject.scene && (station.Kind == HowToFishStationKind.GrillMaster || station.Kind == HowToFishStationKind.Keeper || station.Kind == HowToFishStationKind.ForestLady || station.Kind == HowToFishStationKind.Tourist || station.Kind == HowToFishStationKind.Islander || station.Kind == HowToFishStationKind.Scientist))
                {
                    deliveryStations.Add(station);
                    station.DeliveryRequested += DeliverToStation;
                }

            foreach (var grill in FindObjectsByType<HowToFishGrill>(FindObjectsSortMode.None))
                if (grill.gameObject.scene == gameObject.scene)
                    grill.Initialize(session, player);
            RestorePositions(state);
            if (newGame)
            {
                foreach (var point in clamPoints)
                    Spawn("Clam", point.position, false);
                foreach (var point in leechPoints)
                    Spawn("Leech", point.position, false);
            }
            else
                foreach (var item in state.worldItems)
                    Spawn(item.definitionId, item.position, item.isDrip).Restore(item);
            clamRegrowAt = Time.time + clamRegrowSeconds;
            SetPaused(false);
            if (newGame)
                Save();
            Notify((newGame ? "拾起岸边的蛤蜊，交给看守人换钱，再到灯塔木门购买钓竿。" : "已恢复到最近的安全位置。") + (string.IsNullOrEmpty(profileNotice) ? "" : "\n" + profileNotice));
        }

        /// <summary>生成一个保存好的实体 Prefab 并登记其生命周期。</summary>
        /// <param name="id">生物或物品定义 ID。</param>
        /// <param name="position">生成位置。</param>
        /// <param name="drip">珍稀变体。</param>
        public HowToFishWorldItem Spawn(string id, Vector3 position, bool drip)
        {
            var prefab = catalog.FindCreature(id)?.Prefab ?? catalog.FindItem(id)?.Prefab;
            if (prefab == null)
                throw new InvalidOperationException("缺少实体 Prefab：" + id);
            var item = Instantiate(prefab, position, Quaternion.identity).GetComponent<HowToFishWorldItem>();
            item.Initialize(session, catalog, id, drip);
            var dynamite = item.GetComponent<HowToFishDynamite>();
            if (dynamite != null)
            {
                dynamite.Initialize(player, SpawnUnderwaterExplosionCatch);
                dynamite.Exploded += OnExplosion;
            }

            if (item.Creature != null)
                item.Damaged += OnCreatureHurtSound;
            if (item.Creature?.IsBoss == true)
            {
                item.Defeated += OnBossDefeatedSound;
                if (!IsPaused)
                    SoundRequested?.Invoke(HowToFishSound.BossEncounter, position);
            }

            var fishMotion = item.GetComponent<HowToFishFishMotion>();
            if (fishMotion != null)
                fishMotion.Initialize(player);
            var crab = item.GetComponent<HowToFishSpiderCrab>();
            if (crab != null)
            {
                crab.Initialize(player);
                item.Defeated += DropCrabMeat;
            }

            var piranha = item.GetComponent<HowToFishGiantPiranha>();
            if (piranha != null)
            {
                piranha.Initialize(player, Spawn);
                item.Defeated += DropPiranhaSkeleton;
            }

            var pufferfish = item.GetComponent<HowToFishPufferfish>();
            if (pufferfish != null)
            {
                pufferfish.Initialize(player);
                item.Defeated += DropPufferfish;
            }

            var shark = item.GetComponent<HowToFishBlueShark>();
            if (shark != null)
                shark.Initialize(player);
            var tuna = item.GetComponent<HowToFishTuna>();
            if (tuna != null)
                tuna.Initialize(player);
            var miniBoss = item.GetComponent<HowToFishJumpingFish>();
            if (miniBoss != null)
                miniBoss.Initialize(player);
            var seagull = item.GetComponent<HowToFishSeagull>();
            if (seagull != null)
            {
                var home = position;
                home.y = 0;
                var ground = Physics.RaycastAll(position + Vector3.up * 80, Vector3.down, 160, ~0, QueryTriggerInteraction.Ignore).Where(hit => hit.collider.GetComponentInParent<HowToFishWorldItem>() == null && hit.collider.GetComponentInParent<HowToFishPlayer>() == null).OrderBy(hit => hit.distance).FirstOrDefault();
                if (ground.collider != null)
                    home.y = ground.point.y;
                seagull.Initialize(home, items);
            }

            var albatross = item.GetComponent<HowToFishAlbatross>();
            if (albatross != null)
            {
                albatross.Initialize(player);
                item.Defeated += DropAlbatrossHead;
            }

            var whale = item.GetComponent<HowToFishWhale>();
            if (whale != null)
            {
                whale.Initialize(player);
                if (id == "MutatedBowheadWhale")
                    item.Defeated += DropWhaleFin;
            }

            var boss = item.GetComponent<IHowToFishBoss>();
            if (boss != null && item.Creature?.IsBoss == true)
                currentBoss = boss;
            if (item.Creature?.IsBoss == true)
                player.Fishing.TrackBoss(item);
            items.Add(item);
            if (id == "SpiderCrab" || id == "GiantPiranha" || id == "Pufferfish" || id == "Albatross" || id == "MutatedBowheadWhale")
                item.Defeated += SaveAfterOutfitBoss;
            return item;
        }

        private void SpawnUnderwaterExplosionCatch(Vector3 center)
        {
            if (session == null || exiting || center.y >= 0 || islands == null || islands.Any(value => value.DistanceToShore(center) <= 0))
                return;
            var island = islands.Where(value => value.Index <= session.State.unlockedIsland).OrderBy(value => value.DistanceToShore(center)).FirstOrDefault();
            if (island == null)
                return;
            // 原作生成表未知：按最近已解锁岛，自制等概率普通鱼池；不沿用杆钓权重、Drip 或耗饵。
            var pool = catalog.Creatures.Where(creature => creature.Island == island.Index && !creature.IsBoss && !creature.IsGroundPickup && creature.Health > 0 && creature.Baits.Any(bait => bait == "FreeLure" || bait == "HotDog" || bait == "BeginnerLure" || bait == "StandardLure" || bait == "ProfessionalLure" || bait == "ScientificLure")).ToArray();
            if (pool.Length == 0)
                return;
            // 一条活体、水平两米内和水下 0.3 米均为项目推定；近岸偏移落入岛内时回退到爆心水平位置。
            Vector2 offset = UnityEngine.Random.insideUnitCircle * 2;
            var position = new Vector3(center.x + offset.x, -.3f, center.z + offset.y);
            if (islands.Any(value => value.DistanceToShore(position) <= 0))
                position = new Vector3(center.x, -.3f, center.z);
            Spawn(pool[UnityEngine.Random.Range(0, pool.Length)].Id, position, false);
        }

        /// 保存最近安全点和全部非活动首领实体。
        public void Save() => GlobalData.Dispatch(new HowToFishSaveAction(this));

        /// 创建当前实际参数与绑定副本，包含交互式重绑刚刚应用的覆盖。
        public HowToFishLocalPreferences GetPreferences() => new HowToFishLocalPreferences
        {
            MouseSensitivity = player.MouseSensitivity,
            GamepadSensitivity = player.GamepadSensitivity,
            DeadZone = player.DeadZone,
            InvertY = player.InvertY,
            Bindings = input.SaveBindings()
        };

        /// <summary>预览完整输入配置；非法参数或绑定不改变原设置，不写盘。</summary>
        /// <param name="value">经过校验的草稿或取消时的原快照。</param>
        public void ApplyPreferences(HowToFishLocalPreferences value) => GlobalData.Dispatch(new HowToFishApplyPreferencesAction(this, value));

        /// 保存当前实际输入配置；失败保留草稿供设置面板处理。
        public bool SavePreferences()
        {
            var request = new HowToFishSavePreferencesAction(this);
            GlobalData.Dispatch(request);
            return request.Result;
        }

        /// <summary>进入或退出设置编辑；关闭帧与尚未松开的退出键不能穿透为游戏暂停。</summary>
        /// <param name="editing">是否由设置面板接管快捷键。</param>
        public void SetEditingSettings(bool editing) => GlobalData.Dispatch(new HowToFishSetEditingSettingsAction(this, editing));

        /// <summary>验证已解锁服装，先原子保存共享选择，再更新当前人物表现。</summary>
        /// <param name="id">服装目录ID；未知、锁定或缺资源时保留原选择。</param>
        public bool TrySelectOutfit(string id)
        {
            var request = new HowToFishTrySelectOutfitAction(this, id);
            GlobalData.Dispatch(request);
            return request.Result;
        }

        private void SaveAfterOutfitBoss(HowToFishWorldItem item) => SaveOutfitProgress();

        private void SaveOutfitProgress() => GlobalData.Dispatch(new HowToFishSaveOutfitProgressAction(this));

        private void OnCreatureEaten(HowToFishWorldItem item) => GlobalData.Dispatch(new HowToFishOnCreatureEatenAction(this, item));

        /// <summary>消费一个已由玩家持握后放手的死 Drip 生物，开奖并保存；重复奖励不退款。</summary>
        /// <param name="island">实际老虎机所在岛屿，0至4。</param>
        /// <param name="item">本世界登记的交付实体。</param>
        /// <param name="result">已经保存的中奖公告，供机台动画结束时显示。</param>
        public bool TryPlaySlotMachine(int island, HowToFishWorldItem item, out string result)
        {
            var request = new HowToFishTryPlaySlotMachineAction(this, island, item);
            GlobalData.Dispatch(request);
            result = request.Message;
            return request.Result;
        }

        /// 驾驶时切换船皮肤，否则优先切换物理手持装备，再切换当前背包装备。
        public void ChangeSkin() => GlobalData.Dispatch(new HowToFishChangeSkinAction(this));

        /// <summary>预检整组潜在中奖金额，先保存本轮未来快照，再兑现物品倍率和销毁输掉的实体。</summary>
        /// <param name="roulette">本世界中已开放的轮盘台。</param>
        /// <param name="bets">开局时各实体的下注颜色，同一实体只允许一个颜色。</param>
        /// <param name="result">本轮结果；轮盘先抽签，保存成功才开始演出。</param>
        /// <param name="announcement">动画结束后显示的结果。</param>
        public bool TrySettleRoulette(HowToFishRoulette roulette, IReadOnlyDictionary<HowToFishWorldItem, HowToFishRouletteColor> bets, HowToFishRouletteColor result, out string announcement)
        {
            var request = new HowToFishTrySettleRouletteAction(this, roulette, bets, result);
            GlobalData.Dispatch(request);
            announcement = request.Announcement;
            return request.Result;
        }

        /// <summary>暂停或恢复当前世界。</summary>
        /// <param name="paused">是否暂停。</param>
        public void SetPaused(bool paused) => GlobalData.Dispatch(new HowToFishSetPausedAction(this, paused));

        /// 返回 Hub，先关闭本 Demo 的界面，再由现有导航卸载场景。
        public void ReturnToHub() => GlobalData.Dispatch(new HowToFishUiAction(this, HowToFishUiCommand.Exit));

        internal void ExitScene() => ReturnToHubAsync().Forget();

        private async UniTaskVoid ReturnToHubAsync()
        {
            GlobalData.Dispatch(new HowToFishExitingAction(this, true));
            SetPaused(true);
            try
            {
                await Data.Handler.CloseWindowsAsync();
                await UIManager.Instance.CloseAsync(hud);
                hud = null;
                Time.timeScale = previousTimeScale;
                var result = await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
                if (IsCurrent && (result.Status == GameSceneSwitchStatus.Failed || result.Status == GameSceneSwitchStatus.Busy))
                {
                    var restored = await UIManager.Instance.ShowAsync<HowToFishHudView>(view => view.SetData(this), new UIShowOptions(animated: false));
                    hud = restored.View;
                    if (!ReferenceEquals(GlobalData.Get<HowToFishData>(), Data))
                        GlobalData.Add(Data);
                    GlobalData.Dispatch(new HowToFishExitingAction(this, false));
                    Data.Handler.Publish();
                    SetPaused(true);
                    Notify("返回失败：" + result.Error);
                }
            }
            catch (Exception exception)
            {
                if (IsCurrent)
                    GlobalData.Dispatch(new HowToFishExitingAction(this, false));
                Notify(exception.Message);
                Debug.LogException(exception, this);
            }
        }

        public string FocusText()
        {
            if (!HasSession || IsPaused)
                return "";
            if (player.IsDriving)
                return "[" + input.BindingLabel("Interact") + "] 离开驾驶位";
            var focus = player.Focus;
            if (focus == null)
                return "";
            var roulette = focus.GetComponentInParent<HowToFishRoulette>();
            if (roulette != null && focus.GetComponentInParent<HowToFishWorldItem>() == null)
                return "[" + input.BindingLabel("Interact") + "] 开始轮盘\n" + roulette.StakeText();
            var station = focus.GetComponentInParent<HowToFishStation>();
            if (station != null)
            {
                if (station.Kind == HowToFishStationKind.InventoryUpgrade)
                    return session.NextSlotCost == 0 ? "装备栏已扩至上限" : "[" + input.BindingLabel("Interact") + "] 扩充装备栏  $" + session.NextSlotCost + $"\n{session.EquipmentCapacity} → {session.EquipmentCapacity + 1} 格";
                if (station.Kind == HowToFishStationKind.MotorUpgrade)
                    return session.State.boatMotorTier >= station.MotorTier ? station.Label + " · 已拥有更高或相同级别" : "[" + input.BindingLabel("Interact") + "] " + station.Label + "  $" + (station.MotorTier == 1 ? 230 : 860);
                if (station.Kind == HowToFishStationKind.BoatRadar)
                    return session.State.hasBoatRadar ? "船载雷达 · 已安装" : "[" + input.BindingLabel("Interact") + "] 船载雷达  $200";
                if (station.Kind == HowToFishStationKind.Attachment)
                {
                    var weapon = player.Equipment;
                    if (!session.CanBuyAttachment(weapon?.Id, station.Attachment, station.Island, out var reason))
                        return station.Label + " · " + reason;
                    return "[" + input.BindingLabel("Interact") + "] " + station.Label + "  $" + weapon.AttachmentPrice(station.Attachment) + "\n" + weapon.DisplayName;
                }

                if (station.Kind == HowToFishStationKind.Anvil || station.Kind == HowToFishStationKind.AmmoUpgrade)
                {
                    var weapon = player.Equipment;
                    var kind = station.Kind == HowToFishStationKind.Anvil ? HowToFishItemKind.Melee : HowToFishItemKind.Gun;
                    if (weapon == null || weapon.Kind != kind)
                        return station.Label + " · 请手持" + (kind == HowToFishItemKind.Melee ? "近战武器" : "枪械");
                    int level = session.UpgradeLevel(weapon.Id), cost = weapon.NextUpgradeCost(level);
                    if (cost == 0)
                        return weapon.DisplayName + " · 已升满";
                    if (level >= (station.Island + (kind == HowToFishItemKind.Melee ? 1 : 0)) * 3)
                        return weapon.DisplayName + " · 本岛升级已达上限";
                    return "[" + input.BindingLabel("Interact") + "] " + station.Label + "  $" + cost + $"\n{weapon.DisplayName} · 伤害 {weapon.DamageAtLevel(level)} → {weapon.DamageAtLevel(level + 1)}";
                }

                var product = catalog.FindItem(station.ItemId);
                return "[" + input.BindingLabel("Interact") + "] " + station.Label + (product == null ? "" : "  $" + product.Price);
            }

            var item = focus.GetComponentInParent<HowToFishWorldItem>();
            if (item != null && item.Creature?.IsMainBoss == true)
                return item.Creature.DisplayName + " · 领取掉落的战利品继续任务";
            if (item != null && item.Creature?.IsBoss == true && item.IsAlive && item.DefinitionId != "Tuna")
                return item.Creature.DisplayName + " · 击败后才能拾取";
            return item == null ? "" : "[" + input.BindingLabel("Interact") + "] 拾取 " + (item.Creature?.DisplayName ?? catalog.FindItem(item.DefinitionId)?.DisplayName) + (item.IsDrip ? " · <color=#FF7777>D</color><color=#FFDD66>r</color><color=#77EE99>i</color><color=#77BBFF>p</color>" : "") + (item.Creature != null ? $" · {item.Weight:0.##} kg" : "") + (item.Creature != null && !item.IsAlive ? $"  ${item.SaleValue} · 受热 {item.Cooking:P0}" : "");
        }

        public void Notify(string message) => GlobalData.Dispatch(new HowToFishNotifyAction(this, message));

        internal void ApplyRulePresentation()
        {
            boat.ApplyUpgrades(session.State);
            Changed?.Invoke();
        }

        private void Update()
        {
            if (!initialized || exiting)
                return;
            if (settingsPauseGate && Time.frameCount > settingsClosedFrame && !input.Held("Pause") && !input.Held("Journal"))
                settingsPauseGate = false;
            if (!ShowEnding && session != null && !IsEditingSettings && !settingsPauseGate && input.Pressed("Pause"))
                SetPaused(!IsPaused);
            if (!ShowEnding && session != null && !IsEditingSettings && !settingsPauseGate && input.Pressed("Journal"))
                GlobalData.Dispatch(new HowToFishUiAction(this, HowToFishUiCommand.ToggleJournal));
            GlobalData.Dispatch(new HowToFishTickAction(this, Time.deltaTime, Time.unscaledDeltaTime));
            if (session == null)
                return;
            if (IsPaused)
                return;
            foreach (var island in islands)
                if (island.Index <= session.State.unlockedIsland && island.Index != player.Island && island.DistanceToShore(player.transform.position) < 15)
                {
                    player.Island = island.Index;
                    Notify("抵达 " + island.DisplayName);
                    break;
                }

            items.RemoveAll(item => item == null);
            UpdateTunaBait();
            if (Time.time >= clamRegrowAt)
            {
                clamRegrowAt = Time.time + clamRegrowSeconds;
                foreach (var point in clamPoints)
                    if (!items.Any(item => !item.IsConsumed && item.DefinitionId == "Clam" && (item.transform.position - point.position).sqrMagnitude < 4))
                        Spawn("Clam", point.position, false);
                foreach (var point in leechPoints)
                    if (!items.Any(item => !item.IsConsumed && item.DefinitionId == "Leech" && (item.transform.position - point.position).sqrMagnitude < 4))
                        Spawn("Leech", point.position, false);
                if (session.State.unlockedIsland >= 4 && player.Island == 4)
                    foreach (var point in snailPoints)
                        if (!items.Any(item => !item.IsConsumed && item.DefinitionId == "FootSnail" && (item.transform.position - point.position).sqrMagnitude < 4))
                            Spawn("FootSnail", point.position, false);
                var island = islands.First(value => value.Index == player.Island);
                if (items.Count(item => item.DefinitionId == "Seagull" && item.IsAlive && island.DistanceToShore(item.transform.position) < 15) < 2)
                    Spawn("Seagull", player.transform.position + new Vector3(8, 8, 8), false);
            }
        }

        private void Interact(Collider collider) => GlobalData.Dispatch(new HowToFishInteractAction(this, collider));

        private void DeliverToStation(HowToFishStation station, HowToFishWorldItem item) => GlobalData.Dispatch(new HowToFishDeliverToStationAction(this, station, item));

        /// 离开结局面板，保留当前进度和装备继续探索。
        public void ContinueAfterEnding() => GlobalData.Dispatch(new HowToFishContinueAfterEndingAction(this));

        private void OfferWhale(HowToFishWorldItem item)
        {
            if (session == null || IsPaused || exiting || session.State.unlockedIsland < 4 || item == null || item.IsConsumed || item.IsAlive || item.IsHeld || item.IsCooked || item.DefinitionId != "BowheadWhale" || items.Any(value => value != null && !value.IsConsumed && value.Creature?.IsBoss == true && value.IsAlive))
                return;
            if (item.TryConsume(() => Spawn("MutatedBowheadWhale", crater.BossSpawnPosition, item.IsDrip)))
                Notify("鲸尸沉入熔岩，变异弓头鲸出现！躲开跃击与地面熔岩。");
        }

        private void DropWhaleFin(HowToFishWorldItem whale)
        {
            whale.TryConsume(() =>
            {
                Spawn("WhaleFin", whale.transform.position + Vector3.up, false);
                for (int i = 0; i < 4; i++)
                    Spawn("FishMeat", whale.transform.position + new Vector3(i * .5f - .75f, .5f, 1), false);
            });
            Notify("变异弓头鲸已击败！把鲸鱼鳍交给科学家。");
        }

        private void UpdateTunaBait()
        {
            if (session.State.unlockedIsland < 3 || player.Island != 3 || items.Any(item => !item.IsConsumed && item.Creature?.IsBoss == true && item.IsAlive))
            {
                tunaBait = null;
                tunaBaitSeconds = 0;
                return;
            }

            var candidate = items.FirstOrDefault(item => item.DefinitionId == "Tuna" && !item.IsConsumed && !item.IsAlive && !item.IsHeld && !item.IsCooked && item.transform.position.y > .2f && Physics.Raycast(item.transform.position + Vector3.up * .2f, Vector3.down, out var ground, 2, ~0, QueryTriggerInteraction.Ignore) && ground.collider.GetComponentInParent<HowToFishIsland>()?.Index == 3);
            if (candidate != tunaBait)
            {
                tunaBait = candidate;
                tunaBaitSeconds = 0;
            }

            if (candidate == null)
                return;
            tunaBaitSeconds += Time.deltaTime;
            if (tunaBaitSeconds < 4)
                return;
            var position = candidate.transform.position + Vector3.up * 18;
            candidate.TryConsume(() => Spawn("Albatross", position, false));
            tunaBait = null;
            tunaBaitSeconds = 0;
            Notify("信天翁被金枪鱼吸引来了！利用商店屋顶掩护。");
        }

        private void DropAlbatrossHead(HowToFishWorldItem bird)
        {
            bird.TryConsume(() =>
            {
                Spawn("AlbatrossHead", bird.transform.position, false);
                for (int i = 0; i < 3; i++)
                    Spawn("FishMeat", bird.transform.position + new Vector3(i * .4f - .4f, 0, .4f), false);
            });
            Notify("信天翁已击败，带鸟头去商店找岛民。");
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
                for (int i = 0; i < 3; i++)
                    Spawn("FishMeat", fish.transform.position + new Vector3(i * .4f - .4f, .6f, .4f), false);
            });
            Notify("河豚被打败了！把鱼鳍交给游客，肉块可以吃下或烹饪。");
        }

        private void Respawn() => GlobalData.Dispatch(new HowToFishRespawnAction(this));

        internal void RespawnPresentation()
        {
            var remains = Spawn("PlayerRemains", player.transform.position + Vector3.up, false);
            remains.SetOutfit(SelectedOutfitId);
            remains.Body.rotation = Quaternion.Euler(0, player.transform.eulerAngles.y, 90);
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
            if (state.isDriving)
            {
                player.Board(boat.Seat);
                boat.SetDriver(input);
            }
            else if (state.isOnBoat)
                player.Teleport(state.boatPosition + Quaternion.Euler(0, state.boatYaw, 0) * state.boatLocalPosition, state.boatYaw + state.boatLocalYaw);
        }

        private void OnDestroy()
        {
            if (hud != null)
                UIManager.Instance.CloseAsync(hud, false).Forget();
            lifetime?.Cancel();
            lifetime?.Dispose();
            if (crater != null)
                crater.WhaleOffered -= OfferWhale;
            foreach (var station in deliveryStations)
                if (station != null)
                    station.DeliveryRequested -= DeliverToStation;
            if (player != null)
            {
                player.Message -= Notify;
                player.InteractRequested -= Interact;
                player.Died -= Respawn;
                if (!exiting)
                    player.Drop(false);
            }

            if (player != null)
                player.ChangeSkinRequested -= ChangeSkin;
            if (player != null)
                player.CreatureEaten -= OnCreatureEaten;
            Data?.Handler.Dispose();
            Data?.ClearData();
            if (ReferenceEquals(GlobalData.Get<HowToFishData>(), Data))
                GlobalData.Remove<HowToFishData>();
            input?.Dispose();
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursor;
            Cursor.visible = previousCursorVisible;
            foreach (var light in suspendedLights)
                if (light != null)
                    light.enabled = true;
        }
    }
}
