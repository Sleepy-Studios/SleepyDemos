using Core.Runtime;
using System;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 将钓鱼状态与实际浮漂、鱼线、鱼获刚体连接起来。
    public sealed class HowToFishFishingRig : MonoBehaviour
    {
        [SerializeField]
        private Rigidbody bobberPrefab;
        [SerializeField]
        private LineRenderer line;

        private readonly HowToFishFishingState state = new HowToFishFishingState();

        private HowToFishSession session;

        private HowToFishCatalog catalog;

        private Func<string, Vector3, bool, HowToFishWorldItem> spawn;

        private Rigidbody bobber;

        private HowToFishWorldItem fish;

        private HowToFishWorldItem activeBoss;

        private HowToFishCreatureDefinition catchDefinition;

        private Transform tip;

        private Transform eye;

        private bool wasUseHeld;

        private string selectedBait = "FreeLure";

        private string selectedRod;

        private bool catchSpawned;

        private bool baitAttached;

        /// 当前钓鱼阶段及张力，供 HUD 读取。
        public HowToFishFishingState State => state;

        /// 当前选用的鱼饵 ID。
        public string SelectedBait => selectedBait;

        public string BaitName => selectedBait == "FreeLure" ? selectedRod == "CrabRod" ? "火腿" : "薯条" : catalog.FindItem(selectedBait)?.DisplayName;

        /// 对玩家显示的鱼讯或失败原因。
        public event Action<string> Message;

        /// 真实抛竿和浮漂入水事件；由场景音源消费。
        public event Action<HowToFishSound, Vector3> SoundRequested;

        /// 收线音跟随有效收线阶段与输入，不使用界面提示文字推断。
        public bool IsReelingSoundActive => state.Phase == HowToFishFishingPhase.Reeling && wasUseHeld;

        /// <summary>绑定当前会话和世界生成入口。</summary>
        /// <param name="owner">单人会话。</param>
        /// <param name="definitions">内容目录。</param>
        /// <param name="cameraTransform">第一人称视角。</param>
        /// <param name="spawnCatch">由世界所有者创建并登记物品。</param>
        public void Initialize(HowToFishSession owner, HowToFishCatalog definitions, Transform cameraTransform, Func<string, Vector3, bool, HowToFishWorldItem> spawnCatch)
        {
            if (bobberPrefab == null || line == null)
                throw new InvalidOperationException("钓竿缺少保存的浮漂或鱼线引用。");
            session = owner;
            catalog = definitions;
            eye = cameraTransform;
            spawn = spawnCatch;
            line.useWorldSpace = true;
            line.positionCount = 18;
            line.enabled = false;
        }

        /// <summary>切换到当前鱼竿的真实竿尖；切换装备时收回旧线。</summary>
        /// <param name="rodTip">新竿尖，没有鱼竿时传 null。</param>
        /// <param name="rodId">鱼竿定义 ID；清空装备时传 null。</param>
        public void SetRod(Transform rodTip, string rodId)
        {
            Cancel();
            tip = rodTip;
            selectedRod = rodId;
        }

        /// <summary>追踪世界生成的首领，包含诱饵触发而非直接钓获的遭遇。</summary>
        /// <param name="boss">世界已初始化的首领实体。</param>
        public void TrackBoss(HowToFishWorldItem boss) => activeBoss = boss;

        /// 循环选择已拥有的鱼饵，不凭空生成消耗品。
        public void CycleBait()
        {
            if (state.IsActive)
            {
                Message?.Invoke("先收回鱼线再更换鱼饵。");
                return;
            }

            int start = -1;
            for (int i = 0; i < catalog.Items.Count; i++)
                if (catalog.Items[i].Id == selectedBait)
                    start = i;
            for (int step = 1; step <= catalog.Items.Count; step++)
            {
                var item = catalog.Items[(start + step) % catalog.Items.Count];
                if ((item.Kind != HowToFishItemKind.Bait && item.Id != "EmptyBeerCan") || session.Count(item.Id) == 0)
                    continue;
                selectedBait = item.Id;
                Message?.Invoke("鱼饵：" + BaitName);
                return;
            }
        }

        /// <summary>读取玩家操作并推进世界中的钓鱼表现。</summary>
        /// <param name="input">当前会话输入。</param>
        /// <param name="island">所处鱼池。</param>
        public void Step(HowToFishInput input, int island)
        {
            if (session == null || tip == null)
                return;
            bool use = input.Held("Use");
            if (input.Pressed("Alternate"))
                Cancel();
            if (input.Pressed("Bait"))
                CycleBait();
            if (input.Pressed("Use"))
            {
                if (state.Phase == HowToFishFishingPhase.Bite)
                    state.Hook(selectedRod == "FishingRod");
                else if (!state.IsActive)
                    state.BeginCharge();
                if (selectedRod == "FishingRod")
                    state.Pull();
            }

            if (state.Phase == HowToFishFishingPhase.Charging && wasUseHeld && !use)
                Cast(island);
            wasUseHeld = use;
            var previous = state.Phase;
            bool needsMovement = selectedBait == "FreeLure" ? selectedRod == "FishingRod" : catalog.FindItem(selectedBait).BaitRequiresMovement;
            bool retrieving = needsMovement && state.Phase == HowToFishFishingPhase.Waiting;
            state.Tick(retrieving && !use ? 0 : Time.deltaTime, use);
            if (retrieving && use && bobber != null)
            {
                var destination = new Vector3(eye.position.x, .025f, eye.position.z);
                bobber.position = Vector3.MoveTowards(bobber.position, destination, Time.deltaTime * .6f);
            }

            if (state.Phase == HowToFishFishingPhase.Flying && bobber != null && bobber.position.y <= 0.025f)
            {
                bobber.position = new Vector3(bobber.position.x, 0.025f, bobber.position.z);
                bobber.linearVelocity = Vector3.zero;
                bobber.isKinematic = true;
                var bait = catalog.FindItem(selectedBait);
                float minimum = selectedBait == "FreeLure" ? selectedRod == "CrabRod" ? 1 : 2 : bait.MinimumBiteSeconds;
                float maximum = selectedBait == "FreeLure" ? selectedRod == "CrabRod" ? 4 : 3 : bait.MaximumBiteSeconds;
                state.EnterWater(UnityEngine.Random.Range(minimum, maximum), catchDefinition.Strength);
                SoundRequested?.Invoke(HowToFishSound.Splash, bobber.position);
            }

            if (state.Phase == HowToFishFishingPhase.Bite && previous != state.Phase)
            {
                baitAttached = true;
                Message?.Invoke(selectedRod == "FishingRod" ? "咬钩了！松开后再次按下收线。" : "咬钩了！按下收线。");
            }

            if (state.Phase == HowToFishFishingPhase.Reeling && !catchSpawned)
            {
                catchSpawned = true;
                fish = spawn(catchDefinition.Id, bobber.position + Vector3.down * 0.2f, !catchDefinition.IsBoss && UnityEngine.Random.value < catalog.DripChance);
                if (fish != null)
                    fish.Body.isKinematic = true;
            }

            if (state.Phase == HowToFishFishingPhase.Reeling && fish != null)
            {
                var target = eye.position + eye.forward * 1.5f;
                var point = Vector3.Lerp(bobber.position, target, state.Progress);
                point.y = Mathf.Lerp(0.08f, target.y, state.Progress);
                fish.transform.position = point;
                fish.transform.rotation = Quaternion.Euler(0, Time.time * 160, Mathf.Sin(Time.time * 20) * 25);
            }

            if (state.Phase == HowToFishFishingPhase.Landed && previous != state.Phase)
            {
                if (fish != null)
                {
                    fish.Body.isKinematic = false;
                    fish.Body.linearVelocity = eye.forward * 2 + Vector3.up * 2;
                }

                fish = null;
                ReleaseBait();
                RemoveBobber();
                Message?.Invoke(catchDefinition.IsBoss ? "首领已上岸，准备战斗！" : "鱼获已上岸，抓住它！");
            }

            if (state.Phase == HowToFishFishingPhase.Escaped && previous != state.Phase)
            {
                ReleaseCatch();
                RemoveBobber();
                Message?.Invoke("鱼逃走了。收线时注意张力，松手可以放线。");
            }

            UpdateLine();
        }

        /// 收回浮漂并放弃未完成鱼获。
        public void Cancel()
        {
            ReleaseCatch();
            RemoveBobber();
            state.Reset();
            wasUseHeld = false;
            catchSpawned = false;
        }

        private void Cast(int island)
        {
            if (session.Count(selectedBait) == 0)
                selectedBait = "FreeLure";
            catchDefinition = catalog.RollCatch(island, selectedBait, Mathf.Min(UnityEngine.Random.value, .9999999f), selectedRod);
            if (catchDefinition == null)
            {
                Message?.Invoke("当前鱼竿和鱼饵不适合这片水域。");
                state.Reset();
                return;
            }

            if (catchDefinition.IsBoss && activeBoss != null && activeBoss.IsAlive)
            {
                if (selectedBait == "BeginnerLure" || selectedBait == "StandardLure" || selectedBait == "ProfessionalLure")
                    catchDefinition = catalog.FindCreature("Cod");
                else
                {
                    Message?.Invoke("先结束当前首领战斗。");
                    state.Reset();
                    return;
                }
            }

            float distance = state.Cast();
            SoundRequested?.Invoke(HowToFishSound.Cast, tip.position);
            catchSpawned = false;
            bobber = Instantiate(bobberPrefab, tip.position, Quaternion.identity);
            var forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
            bobber.linearVelocity = (forward + Vector3.up * 0.55f).normalized * Mathf.Sqrt(distance * 9.81f / 0.85f);
            line.enabled = true;
        }

        private void UpdateLine()
        {
            if (line == null || tip == null || bobber == null)
                return;
            var end = fish != null ? fish.HookPosition : bobber.position;
            float sag = state.Phase == HowToFishFishingPhase.Reeling ? (1 - state.Tension) * 0.3f : 0.5f;
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (line.positionCount - 1f);
                line.SetPosition(i, Vector3.Lerp(tip.position, end, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * sag);
            }
        }

        private void ReleaseCatch()
        {
            ReleaseBait();
            if (fish != null)
                Destroy(fish.gameObject);
            fish = null;
        }

        private void ReleaseBait()
        {
            if (!baitAttached)
                return;
            baitAttached = false;
            var bait = catalog.FindItem(selectedBait);
            if (bait.BaitLossChance >= 1 || bait.BaitLossChance > 0 && UnityEngine.Random.value < bait.BaitLossChance)
                GlobalData.Dispatch(new HowToFishTryConsumeAction(session, selectedBait));
            if (session.Count(selectedBait) == 0)
                selectedBait = "FreeLure";
        }

        private void RemoveBobber()
        {
            if (bobber != null)
                Destroy(bobber.gameObject);
            bobber = null;
            if (line != null)
                line.enabled = false;
        }

        private void OnDestroy()
        {
            ReleaseCatch();
            RemoveBobber();
        }
    }
}
