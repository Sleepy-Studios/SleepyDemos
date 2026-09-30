using System;
using System.Collections.Generic;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using UnityEngine;

namespace Hotfix.BlockPorters.Adapters
{
    /// 场景会话、对象池与集中搬运演出的唯一所有者。
    public sealed class BlockPortersController : MonoBehaviour
    {
        private float cellSize = 0.2f;
        private enum Motion { Waiting, Outbound, Lifting, Returning, ToPit, Jumping }
        private sealed class Actor
        {
            public PorterAvatar Avatar;
            public PorterTeam Team;
            public PorterJob Job;
            public Transform Brick;
            public Motion Motion;
            public int PathIndex;
            public int Member;
            public float Timer;
            public Vector3 JumpOrigin;
        }

        [SerializeField] private BlockPortersLevel[] levels;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Transform actorsRoot;
        [SerializeField] private Transform brickPrefab;
        [SerializeField] private PorterAvatar porterPrefab;
        [SerializeField] private Material[] colorMaterials;
        [SerializeField] private Transform pit;
        [SerializeField] private ParticleSystem pitParticles;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private AudioClip dropSound;
        private readonly Stack<Transform> brickPool = new();
        private readonly Stack<PorterAvatar> porterPool = new();
        private readonly List<Actor> actors = new();
        private Transform[] boardBricks;
        private BlockPortersHudView hud;
        private CancellationTokenSource lifetime;
        private CancellationTokenSource rewardLifetime;
        private IBlockPortersReward reward = new SimulatedBlockPortersReward();
        private bool isReady;
        private bool isExiting;
        private bool isRewardPending;
        private bool isApplicationPaused;
        private int sessionVersion;
        private int screenWidth;
        private int screenHeight;
        private float animationClock;

        public BlockPortersSession Session { get; private set; }
        public BlockPortersLevel CurrentLevel => levels[LevelIndex];
        public int LevelIndex { get; private set; }
        public int LevelCount => levels.Length;
        public int ActorCount => actors.Count;
        public bool IsPaused { get; private set; }
        public bool IsMuted { get; private set; }
        public bool IsRewardPending => isRewardPending;
        public bool IsExiting => isExiting;
        public event Action Changed;

        /// <summary>由编辑器保存场景引用；仅在非运行期装配。</summary>
        /// <param name="definitions">五个关卡资产。</param>
        /// <param name="camera">唯一玩法主相机。</param>
        /// <param name="board">棋盘实例挂点。</param>
        /// <param name="people">角色实例挂点。</param>
        /// <param name="brick">共享方块预制体。</param>
        /// <param name="porter">共享小人预制体。</param>
        /// <param name="materials">与关卡色表一致的共享材质。</param>
        /// <param name="hole">深坑中心。</param>
        /// <param name="particles">入坑特效。</param>
        /// <param name="source">场景音效播放器。</param>
        /// <param name="pickup">自制抬砖音效。</param>
        /// <param name="drop">自制入坑音效。</param>
        public void Configure(BlockPortersLevel[] definitions, Camera camera, Transform board, Transform people,
            Transform brick, PorterAvatar porter, Material[] materials, Transform hole, ParticleSystem particles,
            AudioSource source, AudioClip pickup, AudioClip drop)
        {
            levels = definitions; worldCamera = camera; boardRoot = board; actorsRoot = people;
            brickPrefab = brick; porterPrefab = porter; colorMaterials = materials;
            pit = hole; pitParticles = particles; audioSource = source; pickupSound = pickup; dropSound = drop;
        }

        private void Start() => InitializeAsync().Forget();

        private async UniTaskVoid InitializeAsync()
        {
            lifetime = new CancellationTokenSource();
            try
            {
                var navigator = GameSceneNavigator.Instance;
                if (navigator == null) throw new InvalidOperationException("请从 AppEntrance 的 Hub 进入小人搬砖 Demo。");
                await navigator.WaitUntilStableAsync(GameSceneId.BlockPorters, lifetime.Token);
                LoadLevel(0);
                var result = await UIManager.Instance.ShowAsync<BlockPortersHudView, BlockPortersController>(
                    this, new UIShowOptions(animated: false), lifetime.Token);
                if (result.Status == UIOperationStatus.Failed) throw result.Exception;
                hud = UIManager.Instance.Get<BlockPortersHudView>();
                isReady = result.Status == UIOperationStatus.Succeeded || result.Status == UIOperationStatus.Ignored;
                Changed?.Invoke();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void Update()
        {
            if (Session == null || !isReady || isExiting) return;
            FitCamera();
            if (IsPaused || isApplicationPaused || isRewardPending || Session.Status != BlockPortersStatus.Playing) return;
            float delta = Time.deltaTime;
            animationClock += delta;
            bool changed = false;
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                var actor = actors[i];
                changed |= AdvanceActor(actor, delta);
                if (actor.Avatar == null) actors.RemoveAt(i);
            }
            changed |= AssignAvailable();
            var previous = Session.Status;
            Session.EvaluateOutcome();
            if (changed || Session.Status != previous) Changed?.Invoke();
        }

        private bool AdvanceActor(Actor actor, float delta)
        {
            var avatar = actor.Avatar;
            bool carrying = actor.Motion >= Motion.Returning;
            avatar.Animate(animationClock * 12 + actor.Member, actor.Motion is Motion.Outbound or Motion.Returning or Motion.ToPit, carrying);
            switch (actor.Motion)
            {
                case Motion.Outbound:
                    actor.Timer += delta;
                    if (actor.Timer < 0) break;
                    if (MoveTo(avatar.transform, CellPosition(actor.Job.Path[actor.PathIndex]), delta))
                    {
                        actor.PathIndex++;
                        if (actor.PathIndex >= actor.Job.Path.Length) { actor.Motion = Motion.Lifting; actor.Timer = 0; }
                    }
                    break;
                case Motion.Lifting:
                    actor.Timer += delta;
                    Vector3 target = CellPosition(new PorterCell(actor.Job.CellIndex % Session.Width, actor.Job.CellIndex / Session.Width));
                    Face(avatar.transform, target);
                    avatar.Animate(0, false, actor.Timer > 0.12f);
                    if (actor.Timer >= 0.28f)
                    {
                        Session.PickUp(actor.Job.Id);
                        actor.Brick = boardBricks[actor.Job.CellIndex]; boardBricks[actor.Job.CellIndex] = null;
                        actor.Brick.SetParent(avatar.CarryAnchor, false);
                        actor.Brick.localPosition = Vector3.zero;
                        actor.Brick.localRotation = Quaternion.identity;
                        actor.PathIndex = actor.Job.Path.Length - 1;
                        actor.Motion = Motion.Returning;
                        PlaySound(pickupSound, 0.15f);
                        return true;
                    }
                    break;
                case Motion.Returning:
                    if (MoveTo(avatar.transform, CellPosition(actor.Job.Path[actor.PathIndex]), delta))
                    {
                        actor.PathIndex--;
                        if (actor.PathIndex < 0) actor.Motion = Motion.ToPit;
                    }
                    break;
                case Motion.ToPit:
                    Vector3 lip = pit.position + new Vector3((actor.Member % 4 - 1.5f) * 0.15f, 0, 0.7f);
                    if (MoveTo(avatar.transform, lip, delta))
                    {
                        actor.JumpOrigin = avatar.transform.position; actor.Timer = 0; actor.Motion = Motion.Jumping;
                    }
                    break;
                case Motion.Jumping:
                    actor.Timer += delta;
                    float t = Mathf.Clamp01(actor.Timer / 0.5f);
                    avatar.transform.position = Vector3.Lerp(actor.JumpOrigin, pit.position + Vector3.down * 0.9f, t)
                        + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.55f);
                    avatar.transform.localScale = Vector3.one * Mathf.Lerp(1, 0.05f, t * t);
                    if (t >= 1)
                    {
                        Session.Deliver(actor.Job.Id);
                        ReturnBrick(actor.Brick); actor.Brick = null;
                        avatar.gameObject.SetActive(false); porterPool.Push(avatar); actor.Avatar = null;
                        pitParticles.Emit(6); PlaySound(dropSound, 0.3f);
                        return true;
                    }
                    break;
            }
            return false;
        }

        /// <summary>派出列头队伍；输入关闭时忽略，不支持派出后排队伍。</summary>
        /// <param name="column">0–3 的队列编号。</param>
        public void Dispatch(int column)
        {
            if (!isReady || IsPaused || isExiting || isRewardPending) return;
            var team = Session.Dispatch(column);
            if (team == null) return;
            AssignAvailable();
            Changed?.Invoke();
        }

        private bool AssignAvailable()
        {
            bool changed = false;
            foreach (var team in Session.Teams)
            {
                while (Session.TryAssign(team.Id, out var job))
                {
                    int member = team.Delivered + team.InFlight - 1;
                    var avatar = porterPool.Count > 0 ? porterPool.Pop() : Instantiate(porterPrefab, actorsRoot);
                    avatar.gameObject.SetActive(true);
                    avatar.ResetPose(colorMaterials[team.Color]);
                    avatar.transform.position = CellPosition(job.Path[0]);
                    avatar.transform.rotation = Quaternion.identity;
                    actors.Add(new Actor { Avatar = avatar, Team = team, Job = job, Member = member, Motion = Motion.Outbound });
                    changed = true;
                }
            }
            return changed;
        }

        /// 切换暂停，不修改全局 Time.timeScale。
        public void TogglePause() { if (!isExiting) { IsPaused = !IsPaused; Changed?.Invoke(); } }
        /// 切换当前场景音效开关。
        public void ToggleSound() { IsMuted = !IsMuted; audioSource.mute = IsMuted; Changed?.Invoke(); }
        /// 重置当前关卡与演出，取消旧会话奖励结果。
        public void Restart() { if (!isExiting) LoadLevel(LevelIndex); }
        /// 通关后进入下一关；最后一关回到第一关。
        public void NextLevel() { if (!isExiting && Session.Status == BlockPortersStatus.Won) LoadLevel((LevelIndex + 1) % levels.Length); }
        /// 请求本地模拟复活，结果完成后只生效一次。
        public void RequestRevive() => ReviveAsync().Forget();

        /// <summary>替换当前会话奖励适配器，不更改核心玩法规则。</summary>
        /// <param name="provider">不可为 null；正式平台适配器必须返回真实完成结果。</param>
        public void SetRewardProvider(IBlockPortersReward provider) => reward = provider ?? throw new ArgumentNullException(nameof(provider));

        private async UniTaskVoid ReviveAsync()
        {
            if (!isReady || isExiting || isRewardPending || Session.Status != BlockPortersStatus.Failed || Session.HasRevived) return;
            int version = sessionVersion;
            isRewardPending = true; Changed?.Invoke();
            try
            {
                var result = await reward.RequestReviveAsync(rewardLifetime.Token);
                if (version == sessionVersion && !isExiting && result == PorterRewardResult.Completed) Session.Revive();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception, this); }
            finally { if (version == sessionVersion) { isRewardPending = false; Changed?.Invoke(); } }
        }

        /// 关闭本会话 HUD 后通过导航返回 Hub。
        public void ReturnToHub() => ExitAsync().Forget();

        private async UniTaskVoid ExitAsync()
        {
            if (isExiting) return;
            isExiting = true; sessionVersion++; rewardLifetime?.Cancel(); Changed?.Invoke();
            try
            {
                if (hud != null)
                {
                    var closed = await UIManager.Instance.CloseAsync(hud, animated: false);
                    if (closed.Status == UIOperationStatus.Failed) throw closed.Exception;
                }
                var result = await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
                if (result.Status != GameSceneSwitchStatus.Succeeded)
                {
                    isExiting = false; isRewardPending = false;
                    var restored = await UIManager.Instance.ShowAsync<BlockPortersHudView, BlockPortersController>(this, new UIShowOptions(animated: false), lifetime.Token);
                    if (restored.Status == UIOperationStatus.Failed) throw restored.Exception;
                    hud = UIManager.Instance.Get<BlockPortersHudView>();
                    ResetRewardLifetime();
                    Changed?.Invoke();
                    if (result.Status == GameSceneSwitchStatus.Failed) Debug.LogError(result.Error, this);
                }
            }
            catch (Exception exception)
            {
                isExiting = false; isRewardPending = false; ResetRewardLifetime();
                Debug.LogException(exception, this);
                var restored = await UIManager.Instance.ShowAsync<BlockPortersHudView, BlockPortersController>(this, new UIShowOptions(animated: false), lifetime.Token);
                if (restored.Status == UIOperationStatus.Failed) Debug.LogException(restored.Exception, this);
                hud = UIManager.Instance.Get<BlockPortersHudView>();
                Changed?.Invoke();
            }
        }

        internal void LoadLevel(int index)
        {
            sessionVersion++; isRewardPending = false; IsPaused = false; LevelIndex = index;
            ResetRewardLifetime();
            foreach (var actor in actors)
            {
                if (actor.Brick != null) ReturnBrick(actor.Brick);
                actor.Avatar.gameObject.SetActive(false); porterPool.Push(actor.Avatar);
            }
            actors.Clear();
            if (boardBricks != null) foreach (var brick in boardBricks) if (brick != null) ReturnBrick(brick);
            Session = new BlockPortersSession(CurrentLevel.CreateData());
            cellSize = Mathf.Min(0.4f, 6.4f / Mathf.Max(Session.Width, Session.Height));
            boardBricks = new Transform[Session.Width * Session.Height];
            for (int i = 0; i < boardBricks.Length; i++)
            {
                int color = Session.GetCell(i);
                if (color < 0) continue;
                var brick = brickPool.Count > 0 ? brickPool.Pop() : Instantiate(brickPrefab, boardRoot);
                brick.SetParent(boardRoot, false); brick.gameObject.SetActive(true);
                brick.localScale = new Vector3(cellSize * 0.91f, cellSize * 0.8f, cellSize * 0.91f);
                brick.position = CellPosition(new PorterCell(i % Session.Width, i / Session.Width)) + Vector3.up * cellSize * 0.4f;
                brick.localRotation = Quaternion.identity;
                brick.GetComponent<Renderer>().sharedMaterial = colorMaterials[color];
                boardBricks[i] = brick;
            }
            if (pitParticles != null) pitParticles.Clear();
            FitCamera(); Changed?.Invoke();
        }

        private Vector3 CellPosition(PorterCell cell) => new(
            (cell.X - (Session.Width - 1) * 0.5f) * cellSize, 0,
            cell.Y * cellSize + (6.4f - Session.Height * cellSize) * 0.5f);
        private void ReturnBrick(Transform brick) { brick.SetParent(boardRoot, false); brick.gameObject.SetActive(false); brickPool.Push(brick); }
        private static bool MoveTo(Transform actor, Vector3 target, float delta)
        {
            Face(actor, target);
            actor.position = Vector3.MoveTowards(actor.position, target, delta * 3.4f);
            return (actor.position - target).sqrMagnitude < 0.0001f;
        }
        private static void Face(Transform actor, Vector3 target)
        {
            Vector3 direction = target - actor.position; direction.y = 0;
            if (direction.sqrMagnitude > 0.0001f) actor.rotation = Quaternion.LookRotation(direction);
        }
        private void PlaySound(AudioClip clip, float volume) { if (!IsMuted && clip != null) audioSource.PlayOneShot(clip, volume); }
        private void FitCamera()
        {
            if (screenWidth == Screen.width && screenHeight == Screen.height) return;
            screenWidth = Screen.width; screenHeight = Screen.height;
            float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float width = Mathf.Min(1, (9f / 16) / screenAspect);
            worldCamera.rect = new Rect((1 - width) * 0.5f, 0, width, 1);
            worldCamera.orthographicSize = Mathf.Max(7.6f, 4.3f / Mathf.Min(screenAspect, 9f / 16));
        }
        private void OnApplicationPause(bool paused) => isApplicationPaused = paused;
        private void ResetRewardLifetime()
        {
            rewardLifetime?.Cancel(); rewardLifetime?.Dispose();
            rewardLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        }
        private void OnDisable() { isReady = false; }
        private void OnDestroy()
        {
            rewardLifetime?.Cancel(); rewardLifetime?.Dispose();
            lifetime?.Cancel(); lifetime?.Dispose(); Changed = null;
        }
    }
}
