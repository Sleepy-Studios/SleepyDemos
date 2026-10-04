using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 巨型食人鱼的鱼群围攻与跃击；时长、速度和伤害为可调推定值。
    [RequireComponent(typeof(HowToFishWorldItem))]
    public sealed class HowToFishGiantPiranha : MonoBehaviour, IHowToFishBoss
    {
        [SerializeField] private float escapeSeconds = 120;
        [SerializeField] private float summonSeconds = 15;
        [SerializeField] private float secondSummonSeconds = 20;
        [SerializeField] private int maximumSummons = 6;
        [SerializeField] private int secondMaximumSummons = 12;
        [SerializeField] private float perSummonDelay = .2f;
        [Tooltip("来源只说明独立的转阶段鱼群，数量暂按6条校准。")]
        [SerializeField] private int transitionSummons = 6;
        [SerializeField] private float leapSeconds = 4;
        [SerializeField] private float damage = 15;
        private readonly List<HowToFishWorldItem> minions = new List<HowToFishWorldItem>();
        private readonly List<HowToFishWorldItem> extraMinions = new List<HowToFishWorldItem>();
        private HowToFishBossTransition transition;
        private int pendingSummons;
        private int pendingExtraSummons;
        private bool secondPhaseStarted;
        private float nextFishAt;
        private HowToFishWorldItem item;
        private HowToFishPlayer player;
        private Func<string, Vector3, bool, HowToFishWorldItem> spawn;
        private float remaining;
        private float summonAt;
        private float leapAt;
        private float hitAt;
        private RigidbodyConstraints originalConstraints;

        public bool IsFighting => player != null && item.IsAlive && !item.Body.isKinematic;
        public float EscapeFraction => Mathf.Clamp01(remaining / escapeSeconds);
        public HowToFishWorldItem Item => item;
        public string Hint => transition.IsProtected ? "转阶段保护 · 保持移动，避开新鱼群" : "留意鱼群与跃击";

        private void Awake()
        {
            item = GetComponent<HowToFishWorldItem>();
            transition = GetComponent<HowToFishBossTransition>();
            originalConstraints = item.GetComponent<Rigidbody>().constraints;
            remaining = escapeSeconds;
            item.Defeated += OnDefeated;
        }

        /// <summary>绑定玩家与世界生成入口，召唤物仍由世界登记和持久化。</summary>
        /// <param name="target">当前玩家。</param>
        /// <param name="spawnItem">世界实体生成入口。</param>
        public void Initialize(HowToFishPlayer target, Func<string, Vector3, bool, HowToFishWorldItem> spawnItem)
        { player = target; spawn = spawnItem; }

        private void FixedUpdate()
        {
            if (!IsFighting) return;
            remaining -= Time.fixedDeltaTime;
            if (remaining <= 0) { Destroy(gameObject); return; }
            item.Body.constraints = originalConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            minions.RemoveAll(fish => fish == null || fish.IsConsumed || !fish.IsAlive);
            extraMinions.RemoveAll(fish => fish == null || fish.IsConsumed || !fish.IsAlive);
            if (transition.IsSecondPhase && !secondPhaseStarted)
            {
                secondPhaseStarted = true;
                pendingExtraSummons = transitionSummons;
            }
            if (pendingSummons == 0 && Time.time >= summonAt)
            {
                pendingSummons = Mathf.Max(0, (secondPhaseStarted ? secondMaximumSummons : maximumSummons) - minions.Count);
                summonAt = Time.time + (secondPhaseStarted ? secondSummonSeconds : summonSeconds);
            }
            if (Time.time >= nextFishAt && (pendingExtraSummons > 0 || pendingSummons > 0))
            {
                nextFishAt = Time.time + perSummonDelay;
                var direction = Quaternion.AngleAxis(UnityEngine.Random.Range(0, 360), Vector3.up) * Vector3.forward;
                var fish = spawn("Piranha", item.transform.position + direction * 2 + Vector3.up, false);
                fish.Body.AddForce(direction * 2, ForceMode.VelocityChange);
                if (pendingExtraSummons > 0) { extraMinions.Add(fish); pendingExtraSummons--; }
                else
                {
                    minions.Add(fish); pendingSummons--;
                    if (pendingSummons == 0) summonAt = Time.time + (secondPhaseStarted ? secondSummonSeconds : summonSeconds);
                }
            }
            if (Time.time >= leapAt)
            {
                leapAt = Time.time + leapSeconds;
                var offset = player.transform.position - item.Body.position;
                var horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
                if (horizontal.sqrMagnitude > .01f) item.Body.MoveRotation(Quaternion.LookRotation(horizontal));
                item.Body.linearVelocity = Vector3.ClampMagnitude(horizontal, 10) + Vector3.up * 5;
            }
            if (Vector3.Distance(item.Body.worldCenterOfMass, player.transform.position + Vector3.up) < 1.8f && Time.time >= hitAt)
            { player.Damage(damage); hitAt = Time.time + 1; }
        }

        private void OnDefeated(HowToFishWorldItem defeated)
        {
            item.Body.constraints = originalConstraints;
            ReleaseMinions();
        }

        private void ReleaseMinions()
        {
            foreach (var fish in minions) if (fish != null && fish.IsAlive && !fish.IsHeld) Destroy(fish.gameObject);
            foreach (var fish in extraMinions) if (fish != null && fish.IsAlive && !fish.IsHeld) Destroy(fish.gameObject);
            minions.Clear();
            extraMinions.Clear();
        }

        private void OnDestroy()
        {
            if (item != null) item.Defeated -= OnDefeated;
            ReleaseMinions();
        }
    }
}
