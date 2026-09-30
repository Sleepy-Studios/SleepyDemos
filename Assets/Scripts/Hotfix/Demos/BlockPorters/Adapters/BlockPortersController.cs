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
        private sealed class Actor
        {
            public PorterAvatar Avatar;
            public BlockPortersScheduler.Transport Transport;
            public Transform Brick;
        }

        [SerializeField] private BlockPortersLevelCatalog catalog;
        private BlockPortersScheduler scheduler;
        private Material[] levelMaterials;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Transform actorsRoot;
        [SerializeField] private Transform brickPrefab;
        [SerializeField] private PorterAvatar porterPrefab;
        [SerializeField] private Material colorMaterialTemplate;
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
        private BlockPortersLevel[] Definitions => catalog.Levels;
        public BlockPortersLevel CurrentLevel => Definitions[LevelIndex];
        public int LevelIndex { get; private set; }
        public int LevelCount => Definitions.Length;
        public bool IsStable => scheduler != null && scheduler.IsStable;
        public int ActorCount => actors.Count;
        public bool IsPaused { get; private set; }
        public bool IsMuted { get; private set; }
        public bool IsRewardPending => isRewardPending;
        public bool IsExiting => isExiting;
        public event Action Changed;

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
            var previous = Session.Status;
            scheduler.AdvanceTo(scheduler.Time + delta);
            foreach (var actor in actors) PoseActor(actor);
            if (Session.Status != previous) Changed?.Invoke();
        }

        private void PoseActor(Actor actor)
        {
            var task = actor.Transport;
            double time = scheduler.Time;
            var avatar = actor.Avatar;
            bool carrying = task.Job.IsPickedUp;
            bool walking = time < task.Arrived || (time >= task.Pickup && time < task.Jump);
            avatar.Animate(animationClock * 12 + task.Job.Id, walking, carrying || time > task.Arrived + .12);
            Vector3 position;
            Vector3 facing;
            if (time < task.Arrived)
                position = PathPose(task.Job.Path, time - task.Started, false, out facing);
            else if (time < task.Pickup)
            {
                position = CellPosition(task.Job.Path[^1]);
                facing = CellPosition(new PorterCell(task.Job.CellIndex % Session.Width, task.Job.CellIndex / Session.Width));
            }
            else if (time < task.Returned)
                position = PathPose(task.Job.Path, time - task.Pickup, true, out facing);
            else if (time < task.Jump)
            {
                facing = new Vector3(0, 0, -.8f);
                position = Vector3.Lerp(CellPosition(task.Job.Path[0]), facing, (float)((time - task.Returned) / (task.Jump - task.Returned)));
            }
            else
            {
                float t = Mathf.Clamp01((float)((time - task.Jump) / .5));
                facing = pit.position;
                position = Vector3.Lerp(new Vector3(0, 0, -.8f), pit.position + Vector3.down * .9f, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .55f);
                avatar.transform.localScale = Vector3.one * Mathf.Lerp(1, .05f, t * t);
            }
            avatar.transform.position = position; Face(avatar.transform, facing);
        }

        private Vector3 PathPose(PorterCell[] path, double elapsed, bool reverse, out Vector3 facing)
        {
            float segment = (float)(elapsed * 3.4 / cellSize);
            int step = Mathf.Min(path.Length - 1, Mathf.FloorToInt(segment));
            int a = reverse ? path.Length - 1 - step : step;
            int b = reverse ? Mathf.Max(0, a - 1) : Mathf.Min(path.Length - 1, a + 1);
            facing = CellPosition(path[b]);
            return Vector3.Lerp(CellPosition(path[a]), facing, segment - step);
        }

        private void OnAssigned(BlockPortersScheduler.Transport task)
        {
            var avatar = porterPool.Count > 0 ? porterPool.Pop() : Instantiate(porterPrefab, actorsRoot);
            avatar.gameObject.SetActive(true); avatar.ResetPose(levelMaterials[task.Job.Color]);
            avatar.transform.position = CellPosition(task.Job.Path[0]);
            actors.Add(new Actor { Avatar = avatar, Transport = task });
            Changed?.Invoke();
        }

        private void OnPickedUp(BlockPortersScheduler.Transport task)
        {
            var actor = actors.Find(item => item.Transport == task);
            actor.Brick = boardBricks[task.Job.CellIndex]; boardBricks[task.Job.CellIndex] = null;
            actor.Brick.SetParent(actor.Avatar.CarryAnchor, false);
            actor.Brick.localPosition = Vector3.zero; actor.Brick.localRotation = Quaternion.identity;
            PlaySound(pickupSound, .15f); Changed?.Invoke();
        }

        private void OnDelivered(BlockPortersScheduler.Transport task)
        {
            var actor = actors.Find(item => item.Transport == task);
            ReturnBrick(actor.Brick);
            actor.Avatar.gameObject.SetActive(false); porterPool.Push(actor.Avatar); actors.Remove(actor);
            pitParticles.Emit(6); PlaySound(dropSound, .3f); Changed?.Invoke();
        }

        /// <summary>派出列头队伍；输入关闭时忽略，不支持派出后排队伍。</summary>
        /// <param name="column">0–3 的队列编号。</param>
        public void Dispatch(int column)
        {
            if (!isReady || IsPaused || isExiting || isRewardPending) return;
            if (scheduler.Dispatch(column)) Changed?.Invoke();
        }

        /// 切换暂停，不修改全局 Time.timeScale。
        public void TogglePause() { if (!isExiting) { IsPaused = !IsPaused; Changed?.Invoke(); } }
        /// 切换当前场景音效开关。
        public void ToggleSound() { IsMuted = !IsMuted; audioSource.mute = IsMuted; Changed?.Invoke(); }
        /// 重置当前关卡与演出，取消旧会话奖励结果。
        public void Restart() { if (!isExiting) LoadLevel(LevelIndex); }
        /// 通关后进入下一关；最后一关回到第一关。
        public void NextLevel() { if (!isExiting && Session.Status == BlockPortersStatus.Won) LoadLevel((LevelIndex + 1) % LevelCount); }
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
            if (levelMaterials != null) foreach (var material in levelMaterials) Destroy(material);
            levelMaterials = new Material[CurrentLevel.Palette.Length];
            for (int color = 0; color < levelMaterials.Length; color++)
            {
                levelMaterials[color] = new Material(colorMaterialTemplate);
                levelMaterials[color].color = CurrentLevel.Palette[color];
            }
            Session = new BlockPortersSession(CurrentLevel.CreateData());
            scheduler = new BlockPortersScheduler(Session);
            scheduler.Assigned += OnAssigned; scheduler.PickedUp += OnPickedUp; scheduler.Delivered += OnDelivered;
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
                brick.GetComponent<Renderer>().sharedMaterial = levelMaterials[color];
                boardBricks[i] = brick;
            }
            if (pitParticles != null) pitParticles.Clear();
            FitCamera(); Changed?.Invoke();
        }

        private Vector3 CellPosition(PorterCell cell) => new(
            (cell.X - (Session.Width - 1) * 0.5f) * cellSize, 0,
            cell.Y * cellSize + (6.4f - Session.Height * cellSize) * 0.5f);
        private void ReturnBrick(Transform brick) { brick.SetParent(boardRoot, false); brick.gameObject.SetActive(false); brickPool.Push(brick); }
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
            if (levelMaterials != null) foreach (var material in levelMaterials) Destroy(material);
            rewardLifetime?.Cancel(); rewardLifetime?.Dispose();
            lifetime?.Cancel(); lifetime?.Dispose(); Changed = null;
        }
    }
}
