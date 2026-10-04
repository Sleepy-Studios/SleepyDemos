using System;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 单人第一人称移动、持物和装备使用；场景交互交给世界协调器。
    [RequireComponent(typeof(CharacterController))]
    public sealed class HowToFishPlayer : MonoBehaviour
    {
        [SerializeField] private Camera eye;
        [SerializeField] private Transform equipmentRoot;
        [SerializeField] private HowToFishFishingRig fishing;
        [SerializeField] private float walkSpeed = 4.2f;
        [SerializeField] private float sprintSpeed = 6.8f;
        [SerializeField] private float eatingSeconds = 1.5f;
        private CharacterController motor;
        private HowToFishSession session;
        private HowToFishCatalog catalog;
        private HowToFishInput input;
        private Func<string, Vector3, bool, HowToFishWorldItem> spawnItem;
        private HowToFishWorldItem heldItem;
        private HowToFishEquipmentView equipmentView;
        private HowToFishItemDefinition equipment;
        private HowToFishOutfitVisual outfit;
        private float verticalVelocity;
        private float pitch;
        private float attackReadyTime;
        private readonly HowToFishKillScore killScore = new HowToFishKillScore();
        private float reloadRemaining;
        private float normalFieldOfView;
        private float eatingElapsed;
        private float footstepDistance;
        private bool eatingNeedsRelease;
        private bool controlsEnabled;
        private Transform drivingSeat;
        private readonly RaycastHit[] focusHits = new RaycastHit[16];
        private static readonly string[] slotActions = { "Slot1", "Slot2", "Slot3", "Slot4", "Slot5", "Slot6", "Slot7", "Slot8" };

        public Camera Eye => eye;
        public HowToFishWorldItem HeldItem => heldItem != null && !heldItem.IsConsumed && heldItem.IsHeld ? heldItem : null;
        public HowToFishFishingRig Fishing => fishing;
        public HowToFishItemDefinition Equipment => equipment;
        public bool IsDriving => drivingSeat != null;
        public int Island { get; set; }
        public float MouseSensitivity { get; set; } = 0.1f;
        public float GamepadSensitivity { get; set; } = 135;
        public float DeadZone { get; set; } = 0.15f;
        public bool InvertY { get; set; }
        public Collider Focus { get; private set; }
        public bool IsReloading => reloadRemaining > 0;
        public int Ammo => equipment?.Kind == HowToFishItemKind.Gun ? GunState?.ammo ?? 0 : 0;
        public int AmmoCapacity => equipment?.Kind != HowToFishItemKind.Gun ? 0 :
            GunState?.hasExtendedMag == true ? Mathf.Max(equipment.MagazineSize, equipment.ExtendedMagazineSize) : equipment.MagazineSize;
        public bool IsAiming { get; private set; }
        public float EatingProgress => Mathf.Clamp01(eatingElapsed / eatingSeconds);
        public bool CanEat => HeldItem == null ? equipment?.Kind == HowToFishItemKind.Food :
            !HeldItem.IsAlive && (HeldItem.Creature != null || catalog.FindItem(HeldItem.DefinitionId)?.Kind == HowToFishItemKind.Food);
        public Vector3 EquipmentCenter => equipmentView == null ? transform.position : equipmentView.CookingCenter;
        private HowToFishOwnedItem GunState => session.State.inventory.Find(item => item.id == equipment.Id);
        public event Action<Collider> InteractRequested;
        /// 将当前手持装备或驾驶船的换肤请求交给世界会话。
        public event Action ChangeSkinRequested;
        public event Action<string> Message;
        public event Action Died;
        /// 已发生的玩法动作；声音只消费此通知，不反向影响规则。
        public event Action<HowToFishSound, Vector3> SoundRequested;
        /// 已消费的生物仍可读取其烧焦状态；消费回调期间实体已从保存快照排除。
        public event Action<HowToFishWorldItem> CreatureEaten;

        /// <summary>设置后续装备也会继承的服装，并立即更新当前手袖。</summary>
        /// <param name="value">已成功保存的全局服装对应资源。</param>
        public void SetOutfit(HowToFishOutfitVisual value)
        {
            outfit = value ?? throw new ArgumentNullException(nameof(value));
            equipmentView?.SetOutfit(outfit);
        }

        private void Awake() { motor = GetComponent<CharacterController>(); normalFieldOfView = eye == null ? 75 : eye.fieldOfView; }

        /// <summary>将玩家绑定到当前单人会话。</summary>
        /// <param name="owner">业务会话。</param>
        /// <param name="definitions">内容目录。</param>
        /// <param name="controls">统一输入。</param>
        /// <param name="spawnCatch">世界鱼获生成入口。</param>
        public void Initialize(HowToFishSession owner, HowToFishCatalog definitions, HowToFishInput controls,
            Func<string, Vector3, bool, HowToFishWorldItem> spawnCatch)
        {
            if (eye == null || equipmentRoot == null || fishing == null) throw new InvalidOperationException("玩家 Prefab 缺少镜头、装备根或钓竿绑定。");
            session = owner; catalog = definitions; input = controls;
            spawnItem = spawnCatch;
            fishing.Initialize(owner, definitions, eye.transform, spawnCatch);
            fishing.Message += ForwardMessage;
            session.Changed += RefreshTools;
            RefreshTools();
        }

        /// <summary>菜单或场景过渡期间交还光标并禁用玩法输入。</summary>
        /// <param name="enabled">是否允许控制玩家。</param>
        public void SetControls(bool enabled)
        {
            controlsEnabled = enabled;
            input?.SetMode(IsDriving, !enabled);
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }

        /// <summary>在安全检查点恢复玩家位置，给脚底保留碰撞间隙，避免贴面存档点穿地。</summary>
        /// <param name="position">安全的世界位置。</param>
        /// <param name="yaw">水平朝向。</param>
        public void Teleport(Vector3 position, float yaw)
        {
            if (drivingSeat != null) transform.SetParent(null, true);
            drivingSeat = null;
            motor.enabled = false;
            float clearance = motor.skinWidth + .02f;
            position.y += clearance;
            if (Physics.Raycast(position + Vector3.up * .5f, Vector3.down, out var ground, 1.5f, ~0, QueryTriggerInteraction.Ignore) && ground.normal.y > .5f)
                position.y = Mathf.Max(position.y, ground.point.y + clearance - motor.center.y + motor.height * .5f);
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            Physics.SyncTransforms();
            motor.enabled = true;
            pitch = 0; verticalVelocity = 0; reloadRemaining = 0;
            eye.transform.localRotation = Quaternion.identity;
            IsAiming = false; eye.fieldOfView = normalFieldOfView;
            input?.SetMode(false, !controlsEnabled);
        }

        /// <summary>进入驾驶位，禁用角色碰撞，由船体带动角色。</summary>
        /// <param name="seat">驾驶位挂点。</param>
        public void Board(Transform seat)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            fishing.Cancel(); Drop(false); reloadRemaining = 0;
            IsAiming = false; eye.fieldOfView = normalFieldOfView;
            motor.enabled = false;
            drivingSeat = seat;
            transform.SetParent(seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            input.SetMode(true, !controlsEnabled);
        }

        /// <summary>对玩家造成伤害，死亡事件每次归零只发出一次。</summary>
        /// <param name="amount">正数伤害。</param>
        public void Damage(float amount)
        {
            if (session == null || session.State.health <= 0 || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            session.State.health = Mathf.Max(0, session.State.health - amount);
            if (session.State.health > 0) return;
            session.State.poisonSeconds = session.State.burningSeconds = 0;
            session.State.poisonDamagePerSecond = session.State.burningDamagePerSecond = 0;
            fishing.Cancel(); Drop(false); SetControls(false); Died?.Invoke();
        }

        /// <summary>刷新独立的毒火残留；同类不按每个区域累加，保留较强伤害与较长时间。</summary>
        /// <param name="burning">true 为燃烧，false 为中毒。</param>
        /// <param name="seconds">此次施加的持续秒数，范围(0,60]。</param>
        /// <param name="damagePerSecond">每秒伤害，范围(0,1000]。</param>
        public void ApplyDamageStatus(bool burning, float seconds, float damagePerSecond)
        {
            if (session == null || !(seconds > 0 && seconds <= 60) || !(damagePerSecond > 0 && damagePerSecond <= 1000)) return;
            var state = session.State;
            if (burning)
            {
                state.burningSeconds = Mathf.Max(state.burningSeconds, seconds);
                state.burningDamagePerSecond = Mathf.Max(state.burningDamagePerSecond, damagePerSecond);
            }
            else
            {
                state.poisonSeconds = Mathf.Max(state.poisonSeconds, seconds);
                state.poisonDamagePerSecond = Mathf.Max(state.poisonDamagePerSecond, damagePerSecond);
            }
        }

        private bool StepDamageStatus()
        {
            var state = session.State;
            float delta = Time.deltaTime;
            float damage = Mathf.Min(delta, state.poisonSeconds) * state.poisonDamagePerSecond +
                Mathf.Min(delta, state.burningSeconds) * state.burningDamagePerSecond;
            state.poisonSeconds = Mathf.Max(0, state.poisonSeconds - delta);
            state.burningSeconds = Mathf.Max(0, state.burningSeconds - delta);
            if (state.poisonSeconds == 0) state.poisonDamagePerSecond = 0;
            if (state.burningSeconds == 0) state.burningDamagePerSecond = 0;
            bool lethal = damage >= state.health;
            Damage(damage);
            return lethal;
        }

        /// <summary>尝试拾取物品或生物，必要时将装备收进背包。</summary>
        /// <param name="item">玩家指向的实体。</param>
        public bool PickUp(HowToFishWorldItem item)
        {
            if (item == null || item.IsConsumed) return false;
            var dynamite = item.GetComponent<HowToFishDynamite>();
            if (dynamite != null && dynamite.IsArmed)
            {
                Drop(false);
                if (!item.TryHold(eye.transform, motor)) return false;
                heldItem = item;
                return true;
            }
            var definition = catalog.FindItem(item.DefinitionId);
            if (definition?.IsEquipment == true)
            {
                if (!definition.IsConsumable && session.Count(definition.Id) > 0)
                { Message?.Invoke("已经拥有这件装备。"); return false; }
                if (session.Count(definition.Id) == 0 && session.UnstoredEquipment != null)
                { Message?.Invoke("请先收纳或放下手中未收纳的装备。"); return false; }
                var snapshot = item.EquipmentState;
                return item.TryConsume(() =>
                {
                    if (definition.IsConsumable) session.GrantItem(definition.Id);
                    else session.GrantEquipment(snapshot);
                    Message?.Invoke("拾回 " + definition.DisplayName);
                });
            }
            if (definition != null && item.Creature == null && definition.Kind != HowToFishItemKind.Quest && definition.Kind != HowToFishItemKind.Food)
            {
                if (!definition.IsConsumable && session.Count(definition.Id) > 0)
                { Message?.Invoke("已经拥有这件装备。"); return false; }
                return item.TryConsume(() => { session.GrantItem(definition.Id); Message?.Invoke("获得 " + definition.DisplayName); });
            }
            Drop(false);
            if (!item.TryHold(eye.transform, motor)) return false;
            heldItem = item;
            return true;
        }

        /// <summary>释放当前手持物。</summary>
        /// <param name="throwForward">是否向前投掷。</param>
        public void Drop(bool throwForward)
        {
            if (eatingElapsed > 0) eatingNeedsRelease = true;
            eatingElapsed = 0;
            HeldItem?.SetEatingProgress(0);
            if (heldItem == null && throwForward && equipment?.IsEquipment == true && session.Count(equipment.Id) > 0)
            {
                DropEquipment(equipment.Id, true);
                return;
            }
            if (heldItem != null && !heldItem.IsConsumed)
                heldItem.Release(throwForward ? eye.transform.forward * 9 + Vector3.up * 1.5f : Vector3.zero);
            heldItem = null;
        }

        private HowToFishWorldItem DropEquipment(string id, bool throwForward)
        {
            var snapshot = session.State.inventory.Find(item => item.id == id)?.Copy();
            if (snapshot == null) return null;
            snapshot.count = 1;
            var dropped = spawnItem(id, eye.transform.position + eye.transform.forward, false);
            try
            {
                if (catalog.FindItem(id)?.Kind == HowToFishItemKind.Explosive && dropped.GetComponent<HowToFishDynamite>() == null)
                    throw new InvalidOperationException("炸药 Prefab 缺少 HowToFishDynamite。");
                dropped.SetEquipmentState(snapshot);
            }
            catch { Destroy(dropped.gameObject); throw; }
            if (!session.TryConsume(id)) { Destroy(dropped.gameObject); return null; }
            dropped.Body.linearVelocity = throwForward ? eye.transform.forward * 9 + Vector3.up * 1.5f : Vector3.zero;
            return dropped;
        }

        /// <summary>选择一个装备栏；未收纳装备先尝试放入空栏，满栏则落地。</summary>
        /// <param name="slot">从零开始的装备栏索引。</param>
        public void SelectEquipmentSlot(int slot)
        {
            if (slot < 0 || slot >= session.EquipmentCapacity) { Message?.Invoke("请先购买此装备栏位。"); return; }
            var slots = session.State.equipmentSlots;
            if (equipment != null && !slots.Contains(equipment.Id))
            {
                int empty = string.IsNullOrEmpty(slots[slot]) ? slot : slots.FindIndex(string.IsNullOrEmpty);
                if (empty >= 0) session.TryStoreEquipment(equipment.Id, empty);
                else DropEquipment(equipment.Id, false);
            }
            session.State.selectedEquipmentSlot = slot;
            Equip(string.IsNullOrEmpty(slots[slot]) ? null : catalog.FindItem(slots[slot]));
        }

        private void Holster()
        {
            if (equipment == null) { SelectEquipmentSlot(session.State.selectedEquipmentSlot); return; }
            if (!session.State.equipmentSlots.Contains(equipment.Id))
            {
                int empty = session.State.equipmentSlots.FindIndex(string.IsNullOrEmpty);
                if (empty >= 0) session.TryStoreEquipment(equipment.Id, empty);
                else DropEquipment(equipment.Id, false);
            }
            Equip(null);
        }

        private void Update()
        {
            if (session == null || !controlsEnabled || Time.timeScale <= 0) return;
            if (StepDamageStatus()) return;
            IsAiming = !IsDriving && !IsReloading && equipment?.Kind == HowToFishItemKind.Gun &&
                input.Held("Alternate") && !CanEat;
            killScore.RecordAim(IsAiming, Time.time);
            eye.fieldOfView = Mathf.MoveTowards(eye.fieldOfView, IsAiming ? equipmentView.AimFieldOfView : normalFieldOfView, Time.deltaTime * 180);
            var look = input.ReadLook(MouseSensitivity, GamepadSensitivity, DeadZone, InvertY, Time.deltaTime);
            look *= eye.fieldOfView / normalFieldOfView;
            killScore.RecordLook(look.x, Time.time);
            transform.Rotate(0, look.x, 0, Space.Self);
            pitch = Mathf.Clamp(pitch - look.y, -82, 82);
            eye.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            var movement = input.ReadMove(DeadZone);
            if (!IsDriving)
            {
                if (motor.isGrounded && verticalVelocity < 0) verticalVelocity = -2;
                if (motor.isGrounded && input.Pressed("Jump")) verticalVelocity = 5.5f;
                verticalVelocity -= 18 * Time.deltaTime;
                var beforeMove = transform.position;
                motor.Move(((transform.right * movement.x + transform.forward * movement.y) *
                    (input.Held("Sprint") ? sprintSpeed : walkSpeed) + Vector3.up * verticalVelocity) * Time.deltaTime);
                if (beforeMove.y >= 0 && transform.position.y < 0)
                    SoundRequested?.Invoke(HowToFishSound.Splash, transform.position);
                if (motor.isGrounded)
                {
                    footstepDistance += Vector3.ProjectOnPlane(transform.position - beforeMove, Vector3.up).magnitude;
                    // 步距 1.6 米为音效节奏推定；撞墙和原地转向不会积累距离。
                    if (footstepDistance >= 1.6f) { footstepDistance = 0; SoundRequested?.Invoke(HowToFishSound.Footstep, transform.position); }
                }
                else footstepDistance = 0;
                if (transform.position.y < -2.5f) Damage(100);
            }
            Focus = FindFocus();
            if (input.Pressed("Interact")) InteractRequested?.Invoke(Focus);
            if (input.Pressed("ChangeSkin")) ChangeSkinRequested?.Invoke();
            if (IsDriving) return;
            if (IsReloading)
            {
                reloadRemaining = Mathf.Max(0, reloadRemaining - Time.deltaTime);
                if (!IsReloading && equipment?.Kind == HowToFishItemKind.Gun) GunState.ammo = AmmoCapacity;
            }
            if (input.Pressed("Throw")) Drop(true);
            if (input.Pressed("Next")) CycleTool(1);
            if (input.Pressed("Previous")) CycleTool(-1);
            if (input.Pressed("Holster")) Holster();
            for (int slot = 0; slot < slotActions.Length; slot++) if (input.Pressed(slotActions[slot])) SelectEquipmentSlot(slot);
            if (input.Pressed("Reload") && equipment?.Kind == HowToFishItemKind.Gun && !IsReloading && Ammo < AmmoCapacity)
            {
                reloadRemaining = equipment.ReloadSeconds;
                SoundRequested?.Invoke(HowToFishSound.Reload, eye.transform.position);
                Message?.Invoke("换弹中……");
            }
            if (input.Pressed("Style") && equipmentView != null && !equipmentView.IsSpinning)
                equipmentView.Spin();
            bool eating = StepEating();
            if (!eating && equipment?.Kind == HowToFishItemKind.Rod && HeldItem == null) fishing.Step(input, Island);
            else if (!eating && (equipment?.Automatic == true ? input.Held("Use") : input.Pressed("Use")) &&
                equipment?.Kind != HowToFishItemKind.Radar && equipment?.Kind != HowToFishItemKind.Food) Attack();
            if (equipmentView != null)
            {
                var owned = session.State.inventory.Find(item => item.id == equipment.Id);
                if (owned != null)
                {
                    if (owned.cooking > 0 && equipmentView.CookingCenter.y < 0) owned.cooking = 0;
                    equipmentView.SetSkin(owned.skinId);
                    equipmentView.SetCooking(owned.cooking);
                }
                equipmentView.Animate(movement.magnitude, IsReloading ? 1 - reloadRemaining / equipment.ReloadSeconds : 0, IsAiming);
                if (equipment.Kind == HowToFishItemKind.Gun) equipmentView.UpdateLaser(eye, equipment.Range);
            }
        }

        internal void HeatEquipment(float amount)
        {
            if (equipment == null || equipmentView == null) return;
            var owned = session.State.inventory.Find(item => item.id == equipment.Id);
            if (owned == null) return;
            owned.cooking = Mathf.Clamp01(owned.cooking + amount);
            equipmentView.SetCooking(owned.cooking);
        }

        private bool StepEating()
        {
            var food = HeldItem;
            var definition = food != null ? catalog.FindItem(food.DefinitionId) : equipment;
            bool edible = CanEat;
            if (!edible || !input.Held("Use"))
            {
                eatingElapsed = 0; food?.SetEatingProgress(0); eatingNeedsRelease = false;
                return edible;
            }
            if (eatingNeedsRelease) return true;
            eatingElapsed += Time.deltaTime;
            food?.SetEatingProgress(EatingProgress);
            if (eatingElapsed < eatingSeconds) return true;
            float cooking = food != null ? food.Cooking : session.State.inventory.Find(item => item.id == definition.Id)?.cooking ?? 0;
            void RestoreFood()
            {
                float nutrition = HowToFishSession.CookingMultiplier(cooking);
                session.State.hunger = Mathf.Min(100, session.State.hunger + (food?.Creature?.FullnessRestored ?? definition?.Nourishment ?? 20) * nutrition);
                session.State.health = Mathf.Min(100, session.State.health + (food?.Creature?.HealthRestored ?? 12) * nutrition);
                Message?.Invoke("进食完成，恢复了一些体力。");
                if (food?.Creature != null) CreatureEaten?.Invoke(food);
            }
            if (food != null) food.TryConsume(RestoreFood);
            else if (session.TryConsume(definition.Id)) RestoreFood();
            eatingElapsed = 0; eatingNeedsRelease = true;
            return true;
        }

        private void Attack()
        {
            if (Time.time < attackReadyTime || IsReloading) return;
            if (equipment?.Kind == HowToFishItemKind.Explosive)
            {
                if (HeldItem != null) return;
                float interval = equipment.UseInterval;
                var thrown = DropEquipment(equipment.Id, true);
                if (thrown != null)
                {
                    thrown.GetComponent<HowToFishDynamite>().Ignite();
                    attackReadyTime = Time.time + interval;
                }
                return;
            }
            if (equipment?.Kind == HowToFishItemKind.Gun)
            {
                if (Ammo <= 0) { attackReadyTime = Time.time + .25f; Message?.Invoke("弹匣已空，按 " + input.BindingLabel("Reload") + " 换弹。"); return; }
                GunState.ammo--;
                SoundRequested?.Invoke(equipment.Id == "Shotgun" ? HowToFishSound.ShotgunShot :
                    equipment.Id == "Pistol" ? HowToFishSound.GunShot : HowToFishSound.RifleShot, eye.transform.position);
                killScore.RecordAttack(equipment.UseInterval, Time.time);
                attackReadyTime = Time.time + equipment.UseInterval;
                equipmentView?.Strike();
                for (int pellet = 0; pellet < equipment.Pellets; pellet++)
                {
                    var spread = UnityEngine.Random.insideUnitCircle * equipment.Spread;
                    var direction = eye.transform.rotation * Quaternion.Euler(spread.y, spread.x, 0) * Vector3.forward;
                    if (!Physics.Raycast(eye.transform.position, direction, out var shot, equipment.Range, ~0, QueryTriggerInteraction.Ignore)) continue;
                    float shotDamage = equipment.DamageAtLevel(session.UpgradeLevel(equipment.Id));
                    HitCreature(shot, shotDamage, direction * (1.4f / equipment.Pellets), HowToFishKillMethod.Ranged);
                }
                pitch = Mathf.Clamp(pitch - equipment.RecoilAngle * equipmentView.RecoilMultiplier, -82, 82);
                return;
            }
            attackReadyTime = Time.time + (equipment?.UseInterval ?? 0.5f);
            SoundRequested?.Invoke(HowToFishSound.MeleeSwing, eye.transform.position);
            killScore.RecordAttack(equipment?.UseInterval ?? .5f, Time.time);
            equipmentView?.Strike();
            float range = equipment?.Range ?? 2.4f;
            if (!Physics.SphereCast(eye.transform.position, 0.12f, eye.transform.forward, out var hit, range, ~0, QueryTriggerInteraction.Ignore)) return;
            float damage = equipment == null ? 2 : equipment.DamageAtLevel(session.UpgradeLevel(equipment.Id));
            HitCreature(hit, damage, eye.transform.forward * 1.4f, HowToFishKillMethod.Melee);
        }

        private void HitCreature(RaycastHit hit, float damage, Vector3 impulse, HowToFishKillMethod method)
        {
            var item = hit.collider.GetComponentInParent<HowToFishWorldItem>();
            if (item == null || !item.IsAlive) return;
            float multiplier = 1;
            string bonuses = null;
            if (item.DamageToApply(damage) >= item.Health)
            {
                bool targetAirborne = true;
                foreach (var floor in Physics.RaycastAll(item.Body.worldCenterOfMass, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                    if (!floor.collider.transform.IsChildOf(item.transform) && floor.normal.y > .7f) { targetAirborne = false; break; }
                var score = killScore.Score(new HowToFishKillHit
                {
                    Method = method, Health = item.Health, MaximumHealth = item.Creature.Health, Damage = damage,
                    Endangered = item.Creature.IsEndangered, Boss = item.Creature.IsBoss,
                    FirstPlayerHit = !item.HasBeenHitByPlayer, Headshot = item.IsHeadHit(hit.point),
                    PlayerAirborne = !motor.isGrounded, TargetAirborne = targetAirborne,
                    CameraDistance = Vector3.Distance(eye.transform.position, item.transform.position),
                    PlayerDistance = Vector3.Distance(transform.position, item.transform.position), LastBullet = Ammo == 0
                }, Time.time);
                multiplier = score.Multiplier; bonuses = score.Bonuses;
            }
            item.Hit(damage, impulse, multiplier, true);
            if (bonuses != null && !item.IsAlive) Message?.Invoke($"击杀奖励 ×{multiplier:0.##}：{bonuses}");
        }

        internal void HitByExplosion(HowToFishWorldItem item, float damage)
        {
            if (item == null || !item.IsAlive) return;
            float multiplier = 1;
            if (item.DamageToApply(damage) >= item.Health)
                multiplier = killScore.Score(new HowToFishKillHit
                {
                    Method = HowToFishKillMethod.Explosion, Health = item.Health,
                    MaximumHealth = item.Creature.Health, Damage = damage
                }, Time.time).Multiplier;
            item.Hit(damage, Vector3.zero, multiplier, true);
            if (!item.IsAlive) Message?.Invoke("击杀奖励 ×1.25：爆炸");
        }

        private Collider FindFocus()
        {
            int count = Physics.RaycastNonAlloc(eye.transform.position, eye.transform.forward, focusHits, 3.5f, ~0, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float distance = float.PositiveInfinity;
            var carried = HeldItem;
            for (int i = 0; i < count; i++)
            {
                var hit = focusHits[i];
                if (hit.collider.transform.IsChildOf(transform) ||
                    (carried != null && hit.collider.transform.IsChildOf(carried.transform)) || hit.distance >= distance) continue;
                nearest = hit.collider; distance = hit.distance;
            }
            return nearest;
        }

        private void RefreshTools()
        {
            string id = session.State.equippedItemId;
            var desired = string.IsNullOrEmpty(id) || session.Count(id) == 0 ? null : catalog.FindItem(id);
            if (equipment != desired) Equip(desired);
            if (equipment?.Kind == HowToFishItemKind.Gun) equipmentView?.SetAttachments(GunState);
        }

        private void CycleTool(int direction)
        {
            var slots = session.State.equipmentSlots;
            int index = session.State.selectedEquipmentSlot;
            bool unstored = equipment != null && !slots.Contains(equipment.Id);
            for (int step = 0; step < slots.Count; step++)
            {
                index = (index + direction + slots.Count) % slots.Count;
                if (unstored || !string.IsNullOrEmpty(slots[index])) { SelectEquipmentSlot(index); return; }
            }
        }

        private void Equip(HowToFishItemDefinition next)
        {
            if (equipment == next) { session.State.equippedItemId = next?.Id; return; }
            if (next != null && next.ViewPrefab == null) { Message?.Invoke("装备模型尚未配置：" + next.DisplayName); return; }
            if (eatingElapsed > 0) eatingNeedsRelease = true;
            eatingElapsed = 0; HeldItem?.SetEatingProgress(0);
            fishing.SetRod(null, null);
            if (equipmentView != null) Destroy(equipmentView.gameObject);
            equipment = next;
            reloadRemaining = 0;
            IsAiming = false; eye.fieldOfView = normalFieldOfView;
            if (equipment?.Kind == HowToFishItemKind.Gun)
                GunState.ammo = GunState.ammo < 0 ? AmmoCapacity : Mathf.Min(GunState.ammo, AmmoCapacity);
            session.State.equippedItemId = next?.Id;
            if (next != null)
            {
                int slot = session.State.equipmentSlots.IndexOf(next.Id);
                if (slot >= 0) session.State.selectedEquipmentSlot = slot;
            }
            equipmentView = null;
            if (next == null) return;
            equipmentView = Instantiate(next.ViewPrefab, equipmentRoot).GetComponent<HowToFishEquipmentView>();
            if (equipmentView == null) throw new InvalidOperationException("装备 Prefab 缺少 HowToFishEquipmentView。");
            if (outfit != null) equipmentView.SetOutfit(outfit);
            if (next.Kind == HowToFishItemKind.Gun) equipmentView.SetAttachments(GunState);
            fishing.SetRod(next.Kind == HowToFishItemKind.Rod ? equipmentView.Tip : null, next.Id);
        }

        private void ForwardMessage(string message) => Message?.Invoke(message);
        private void OnDestroy()
        {
            if (session != null) session.Changed -= RefreshTools;
            if (fishing != null) fishing.Message -= ForwardMessage;
            Drop(false);
        }
    }
}
