using System;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 场景中的实体物品。抓取通过刚体追随，出售与任务提交只允许消费一次。
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HowToFishWorldItem : MonoBehaviour
    {
        [SerializeField] private string definitionId;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform hookPoint;
        [SerializeField] private float holdingDistance = 1.9f;
        [Tooltip("自制模型头部阈值：从重心到口部挂点的比例，按物种轮廓校准。")]
        [Range(0, 1)] [SerializeField] private float headThreshold = .6f;
        private Rigidbody body;
        private Collider[] colliders;
        private Collider holderCollider;
        private Transform holdTarget;
        private float activeHoldingDistance;
        private bool originalGravity;
        private bool consumed;
        private HowToFishSession session;
        private HowToFishCreatureDefinition creature;
        private HowToFishItemDefinition definition;
        private float health;
        private string instanceId;
        private bool isDrip;
        private float cooking;
        private float lastHeatStep = -1;
        private bool hasBeenHeld;
        private HowToFishOwnedItem equipmentState;
        private float styleMultiplier = 1;
        private bool hasBeenHitByPlayer;
        private HowToFishBossTransition bossTransition;

        /// 定义标识，关联目录中的物品或生物。
        public string DefinitionId => definitionId;
        /// 持久化实例标识，区别同种的多个散落物品。
        public string InstanceId => instanceId;
        /// 当前生命；普通非生物物品为零。
        public float Health => health;
        /// 生物定义，普通装备为 null。
        public HowToFishCreatureDefinition Creature => creature;
        /// 此物品是否已被消费。
        public bool IsConsumed => consumed;
        /// 是否正在手持。
        public bool IsHeld => holdTarget != null;
        internal bool IsHeldBy(Transform holder) => holdTarget == holder;
        /// 是否为活物。
        public bool IsAlive => creature != null && health > 0;
        /// 当前是否已烹饪。
        public bool IsCooked => cooking > 0;
        public float Cooking => cooking;
        public bool IsBurnt => cooking >= .9f;
        public bool HasBeenHeld => hasBeenHeld;
        public bool HasBeenHitByPlayer => hasBeenHitByPlayer;
        public float KillMultiplier => styleMultiplier;
        public int SaleValue => creature == null ? 0 : session.CatchValue(definitionId, cooking, isDrip, styleMultiplier);
        /// 珍稀变体标记。
        public bool IsDrip => isDrip;
        public HowToFishOwnedItem EquipmentState => equipmentState?.Copy();
        /// 物理刚体。
        public Rigidbody Body => body;
        /// 渲染与关节的根节点。
        public Transform VisualRoot => visualRoot;
        /// 鱼线与鱼体连接位置。
        public Vector3 HookPosition => hookPoint != null ? hookPoint.position : transform.position;
        /// 击杀完成事件，用于首领状态和演出。
        public event Action<HowToFishWorldItem> Defeated;
        /// 受击反馈，数值为实际伤害。
        public event Action<HowToFishWorldItem, float> Damaged;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            bossTransition = GetComponent<HowToFishBossTransition>();
            colliders = GetComponentsInChildren<Collider>();
            instanceId = Guid.NewGuid().ToString("N");
            originalGravity = body.useGravity;
        }

        /// <summary>绑定会话和定义；每个新实例只调用一次。</summary>
        /// <param name="owner">当前单人会话。</param>
        /// <param name="catalog">内容定义。</param>
        /// <param name="id">当前物品或生物 ID。</param>
        /// <param name="drip">是否使用珍稀变体。</param>
        public void Initialize(HowToFishSession owner, HowToFishCatalog catalog, string id, bool drip = false)
        {
            if (session != null) throw new InvalidOperationException("世界物品不能重复初始化。");
            session = owner ?? throw new ArgumentNullException(nameof(owner));
            definitionId = id;
            creature = catalog.FindCreature(id);
            definition = catalog.FindItem(id);
            if (creature == null && definition == null) throw new ArgumentException("世界物品定义不存在：" + id);
            health = creature?.Health ?? 0;
            isDrip = drip;
            if (definition?.IsEquipment == true) SetEquipmentState(new HowToFishOwnedItem { id = id, count = 1 });
            if (creature != null) session.RegisterCreature(id, false, drip);
        }

        /// <summary>恢复安全快照；不得用于活动首领。</summary>
        /// <param name="data">当前实例对应的持久化数据。</param>
        public void Restore(HowToFishWorldItemData data)
        {
            if (data == null || data.definitionId != definitionId) throw new ArgumentException("存档物品定义不匹配。");
            instanceId = data.instanceId;
            health = creature == null ? 0 : Mathf.Clamp(data.health, 0, creature.Health);
            cooking = data.cooking > 0 ? data.cooking : data.isCooked ? .5f : 0;
            hasBeenHeld = data.hasBeenHeld;
            styleMultiplier = data.styleMultiplier;
            hasBeenHitByPlayer = data.hasBeenHitByPlayer;
            isDrip = data.isDrip;
            if (data.HasEquipment) SetEquipmentState(data.equipment);
            HowToFishEquipmentView.ApplyCookingTint(visualRoot, cooking);
            transform.SetPositionAndRotation(data.position, Quaternion.Euler(data.eulerAngles));
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        /// 创建当前物品快照。
        public HowToFishWorldItemData Snapshot() => new HowToFishWorldItemData
        {
            instanceId = instanceId, definitionId = definitionId, health = health,
            position = transform.position, eulerAngles = transform.eulerAngles,
            isCooked = IsCooked, cooking = cooking, hasBeenHeld = hasBeenHeld, styleMultiplier = styleMultiplier,
            isDrip = isDrip, hasBeenHitByPlayer = hasBeenHitByPlayer, equipment = equipmentState?.Copy()
        };

        /// <summary>将背包装备状态转入这个物理实体。</summary>
        /// <param name="state">同一物品定义的一件装备快照。</param>
        public void SetEquipmentState(HowToFishOwnedItem state)
        {
            if (definition?.IsEquipment != true || state == null || !session.HasCompatibleEquipmentState(state) || state.id != definitionId || state.count != 1)
                throw new ArgumentException("落地装备状态无效或定义不匹配。");
            equipmentState = state.Copy();
            cooking = state.cooking;
            HowToFishEquipmentView.ApplyCookingTint(visualRoot, cooking);
            var view = visualRoot.GetComponent<HowToFishEquipmentView>();
            if (view != null) view.SetAttachments(equipmentState);
        }

        /// <summary>从相机方向物理抓取，忽略与持有者自身的碰撞。</summary>
        /// <param name="target">持有者相机或握持方向。</param>
        /// <param name="playerCollider">持有者碰撞体。</param>
        /// <param name="distance">沿持有者前方的距离；默认使用 Prefab 值，海鸥使用零距离挂点。</param>
        public bool TryHold(Transform target, Collider playerCollider, float? distance = null)
        {
            if (consumed || IsHeld || target == null || (creature != null &&
                (creature.IsMainBoss || creature.IsBoss && IsAlive && creature.Id != "Tuna"))) return false;
            float requestedDistance = distance ?? holdingDistance;
            if (float.IsNaN(requestedDistance) || float.IsInfinity(requestedDistance) || requestedDistance < 0) return false;
            holdTarget = target;
            hasBeenHeld |= target.GetComponentInParent<HowToFishPlayer>() != null;
            activeHoldingDistance = requestedDistance;
            holderCollider = playerCollider;
            body.isKinematic = false;
            originalGravity = body.useGravity;
            body.useGravity = false;
            if (holderCollider != null)
                foreach (var collider in colliders) Physics.IgnoreCollision(collider, holderCollider, true);
            return true;
        }

        /// <summary>松开或投掷物品并恢复重力与碰撞。</summary>
        /// <param name="velocity">释放时的世界速度。</param>
        public void Release(Vector3 velocity)
        {
            holdTarget = null;
            body.useGravity = creature != null && !IsAlive || originalGravity;
            body.linearVelocity = Vector3.ClampMagnitude(velocity, 30f);
            if (holderCollider != null)
                foreach (var collider in colliders) Physics.IgnoreCollision(collider, holderCollider, false);
            holderCollider = null;
        }

        /// <summary>施加伤害；只有第一次击杀产生奖励与击杀事件。</summary>
        /// <param name="damage">正数伤害。</param>
        /// <param name="impulse">击中产生的冲量。</param>
        /// <param name="style">致死命中的奖励倍率。</param>
        /// <param name="byPlayer">是否来自玩家攻击，用于首次命中规则。</param>
        public void Hit(float damage, Vector3 impulse, float style = 1, bool byPlayer = false)
        {
            if (consumed || !IsAlive || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage) ||
                !(style >= 1) || float.IsInfinity(style)) return;
            float dealt = DamageToApply(damage);
            if (dealt <= 0) return;
            hasBeenHitByPlayer |= byPlayer;
            health -= dealt;
            body.AddForce(Vector3.ClampMagnitude(impulse, 40), ForceMode.Impulse);
            if (health <= 0) styleMultiplier = style;
            Damaged?.Invoke(this, dealt);
            if (health > 0) return;
            session.RegisterCreature(definitionId, true, isDrip);
            Defeated?.Invoke(this);
        }

        /// <summary>预估当前命中实际扣血，计分与实际伤害共用首领阶段保护。</summary>
        /// <param name="damage">命中的原始伤害；无效值与受保护状态返回零。</param>
        public float DamageToApply(float damage)
        {
            if (consumed || !IsAlive || !(damage > 0) || float.IsInfinity(damage)) return 0;
            return Mathf.Min(health, bossTransition == null ? damage : bossTransition.LimitDamage(damage));
        }

        /// <summary>按自制模型的重心、口部挂点和可调阈值识别头部，不套用原作网格坐标。</summary>
        /// <param name="point">物理射线的实际命中点。</param>
        public bool IsHeadHit(Vector3 point)
        {
            if (creature == null || hookPoint == null) return false;
            var direction = hookPoint.position - body.worldCenterOfMass;
            return direction.sqrMagnitude > .0001f &&
                Vector3.Dot(point - body.worldCenterOfMass, direction) >= direction.sqrMagnitude * headThreshold;
        }

        /// <summary>对死鱼、可烹饪食物或工具加热；复合碰撞体每个物理步只加热一次。</summary>
        /// <param name="amount">本物理步增加的受热程度，正数；到1后不再增加。</param>
        public bool Heat(float amount)
        {
            if (consumed || IsAlive || cooking >= 1 || lastHeatStep == Time.fixedTime ||
                (creature == null && definition?.IsCookable != true && definition?.IsEquipment != true) ||
                !(amount > 0) || float.IsInfinity(amount)) return false;
            lastHeatStep = Time.fixedTime;
            cooking = Mathf.Min(1, cooking + amount);
            if (equipmentState != null) equipmentState.cooking = cooking;
            HowToFishEquipmentView.ApplyCookingTint(visualRoot, cooking);
            return true;
        }

        /// <summary>随进食进度将手持鱼获靠近嘴部，停止进食则恢复普通持物距离。</summary>
        /// <param name="progress">0至1的进食进度。</param>
        public void SetEatingProgress(float progress) => activeHoldingDistance = Mathf.Lerp(holdingDistance, .65f, Mathf.Clamp01(progress));

        /// <summary>出售死去的鱼获，防止同一物理实例重复出售。</summary>
        /// <param name="money">本次获得的金钱。</param>
        public bool TrySell(out int money)
        {
            money = 0;
            if (consumed || creature == null || creature.IgnoredBySeller || IsAlive || !hasBeenHeld) return false;
            consumed = true;
            try { money = session.SellCatch(definitionId, cooking, isDrip, styleMultiplier); }
            catch { consumed = false; throw; }
            FinishConsume();
            return true;
        }

        /// <summary>任务或进食消费当前物品；效果失败时保留实体。</summary>
        /// <param name="effect">由任务或进食规则执行的单次效果。</param>
        public bool TryConsume(Action effect)
        {
            if (consumed || effect == null) return false;
            consumed = true;
            try { effect(); }
            catch { consumed = false; throw; }
            FinishConsume();
            return true;
        }

        private void FixedUpdate()
        {
            if (consumed) return;
            if (equipmentState != null && cooking > 0 && body.worldCenterOfMass.y < 0)
            {
                cooking = equipmentState.cooking = 0;
                HowToFishEquipmentView.ApplyCookingTint(visualRoot, 0);
            }
            if (holdTarget != null)
            {
                var destination = holdTarget.position + holdTarget.forward * activeHoldingDistance;
                var offset = destination - body.worldCenterOfMass;
                if (offset.sqrMagnitude > 36f) { Release(Vector3.zero); return; }
                body.AddForce(Vector3.ClampMagnitude(offset * 60 - body.linearVelocity * 12, 90), ForceMode.Acceleration);
                body.angularVelocity *= 0.85f;
            }
            else if (transform.position.y < -0.15f)
            {
                float lift = Mathf.Clamp(-transform.position.y * 18, 0, 24);
                body.AddForce(Vector3.up * lift - body.linearVelocity * 1.5f, ForceMode.Acceleration);
            }
            if (body.linearVelocity.sqrMagnitude > 900) body.linearVelocity = body.linearVelocity.normalized * 30;
        }

        private void FinishConsume()
        {
            if (IsHeld) Release(Vector3.zero);
            foreach (var collider in colliders) collider.enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (holderCollider != null && colliders != null)
                foreach (var collider in colliders)
                    if (collider != null) Physics.IgnoreCollision(collider, holderCollider, false);
        }
    }
}
