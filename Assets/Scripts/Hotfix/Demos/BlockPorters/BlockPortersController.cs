using System;
using System.Collections.Generic;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using UnityEngine;

namespace Hotfix.BlockPorters
{
    /// 场景会话、对象池与集中搬运演出的唯一所有者。
    public sealed class BlockPortersController : MonoBehaviour
    {
        private static readonly Color RippleColor = new Color(.55f, .8f, .7f, 1);
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
        [SerializeField] private Renderer backgroundRenderer;
        [SerializeField] private BlockPortersUiStyle uiStyle;
        [SerializeField] private BlockPortersThemeCatalog themeCatalog;
        [SerializeField] private Transform pitOpening;
        private BlockPortersThemeLoader themeLoader;
        private readonly System.Random themeRandom = new();
        private static string lastAppliedTheme;
        public string CurrentThemeId => themeLoader?.AppliedId;
        [SerializeField] private Renderer[] pitInteriors;
        [SerializeField] private float pitApertureRadius = .43f;
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Transform actorsRoot;
        [SerializeField] private Transform brickPrefab;
        [SerializeField] private PorterAvatar porterPrefab;
        [SerializeField] private Material colorMaterialTemplate;
        [SerializeField] private Transform pit;
        [SerializeField] private ParticleSystem pitParticles;
        [SerializeField] private Transform pitRipple;
        [SerializeField] private Renderer rippleRenderer;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private AudioClip dropSound;
        private readonly Stack<Transform> brickPool = new();
        private readonly Stack<PorterAvatar> porterPool = new();
        private readonly List<Actor> actors = new();
        private readonly List<Light> suspendedLights = new();
        private Transform[] boardBricks;
        private BlockPortersHudView hud;
        private BlockPortersUIController ui;
        internal BlockPortersUIController UI => ui;
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
        private Rect lastSafeArea;
        private MaterialPropertyBlock backgroundProperties;
        private MaterialPropertyBlock pitInteriorProperties;
        private float animationClock;
        private float rippleAge = 1;
        private MaterialPropertyBlock rippleProperties;

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
                if (navigator == null) throw new InvalidOperationException("请从 AppEntrance 的 Hub 进入小小搬豆工 Demo。");
                await navigator.WaitUntilStableAsync(GameSceneId.BlockPorters, lifetime.Token);
                // Hub 的相机虽已停用，灯光仍在 Additive 场景中；仅在本 Demo 存活期间隔离。
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.enabled && light.gameObject.scene != gameObject.scene)
                    { suspendedLights.Add(light); light.enabled = false; }
                LoadLevel(0);
                ui = new BlockPortersUIController(this, lifetime.Token);
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
            if (!IsPaused && !isApplicationPaused) UpdateRipple(Time.deltaTime);
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
            avatar.SetGrounded(time < task.Jump);
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
            rippleAge = 0;
        }

        private void UpdateRipple(float delta)
        {
            if (pitRipple == null) return;
            rippleAge += delta;
            bool visible = rippleAge < .32f;
            pitRipple.gameObject.SetActive(visible);
            if (!visible) return;
            float t = rippleAge / .32f;
            pitRipple.localScale = Vector3.one * Mathf.Lerp(.88f, 1.16f, t);
            rippleProperties ??= new MaterialPropertyBlock();
            rippleProperties.SetColor("_BaseColor", ColorUtil.WithAlpha(RippleColor, (1 - t) * .45f));
            rippleRenderer.SetPropertyBlock(rippleProperties);
        }

        /// <summary>派出列头队伍；输入关闭时忽略，不支持派出后排队伍。</summary>
        /// <param name="column">当前关卡的队列编号，最多五列。</param>
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
        public void Restart() { if (!isExiting) LoadLevel(LevelIndex, false); }
        /// 通关后进入下一关；最后一关回到第一关。
        public void NextLevel() { if (!isExiting && Session.Status == BlockPortersStatus.Won) LoadLevel((LevelIndex + 1) % LevelCount); }
        /// <summary>请求单侧模拟广告，当前会话每侧只解锁一次。</summary>
        /// <param name="side">0 为左侧，1 为右侧。</param>
        public void RequestUnlockSlot(int side) => UnlockSlotAsync(side).Forget();

        /// <summary>替换当前会话奖励服务，不更改核心玩法规则。</summary>
        /// <param name="provider">不可为 null；正式平台奖励服务必须返回真实完成结果。</param>
        public void SetRewardProvider(IBlockPortersReward provider) => reward = provider ?? throw new ArgumentNullException(nameof(provider));

        private async UniTaskVoid UnlockSlotAsync(int side)
        {
            if (!isReady || isExiting || isRewardPending || side < 0 || side > 1 ||
                Session.Status == BlockPortersStatus.Won || Session.IsSlotAvailable(side + 5)) return;
            int version = sessionVersion;
            isRewardPending = true; Changed?.Invoke();
            try
            {
                var result = await reward.RequestExtraSlotAsync(side, rewardLifetime.Token);
                if (version == sessionVersion && !isExiting && result == PorterRewardResult.Completed) Session.TryUnlockExtraSlot(side);
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
            isExiting = true; sessionVersion++; themeLoader?.Invalidate(); rewardLifetime?.Cancel(); Changed?.Invoke();
            try
            {
                await ui.CloseAsync();
                if (hud != null)
                {
                    var closed = await UIManager.Instance.CloseAsync(hud, animated: false);
                    if (closed.Status == UIOperationStatus.Failed) throw closed.Exception;
                }
                var result = await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
                if (result.Status != GameSceneSwitchStatus.Succeeded)
                {
                    isExiting = false; isRewardPending = false; ui.Restore();
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
                isExiting = false; isRewardPending = false; ui.Restore(); ResetRewardLifetime();
                Debug.LogException(exception, this);
                var restored = await UIManager.Instance.ShowAsync<BlockPortersHudView, BlockPortersController>(this, new UIShowOptions(animated: false), lifetime.Token);
                if (restored.Status == UIOperationStatus.Failed) Debug.LogException(restored.Exception, this);
                hud = UIManager.Instance.Get<BlockPortersHudView>();
                Changed?.Invoke();
            }
        }

        internal void LoadLevel(int index, bool chooseTheme = true)
        {
            sessionVersion++; isRewardPending = false; IsPaused = false; LevelIndex = index;
            themeLoader?.Invalidate();
            if (chooseTheme && themeCatalog != null) ApplyThemeAsync(themeCatalog.Choose(lastAppliedTheme, themeRandom)).Forget();
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
            rippleAge = 1;
            if (pitRipple != null) pitRipple.gameObject.SetActive(false);
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
            if (screenWidth == Screen.width && screenHeight == Screen.height && lastSafeArea == Screen.safeArea) return;
            screenWidth = Screen.width; screenHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
            var layout = BlockPortersScreenLayout.Calculate(screenWidth, screenHeight, lastSafeArea);
            float pixelsPerUnit = uiStyle.WorldPixelsPerUnit * layout.Scale;
            worldCamera.rect = new Rect(0, 0, 1, 1);
            worldCamera.orthographicSize = screenHeight / (2 * pixelsPerUnit);
            var desired = layout.ToScreen(uiStyle.BoardCenter);
            var offset = (desired - new Vector2(screenWidth, screenHeight) * .5f) / pixelsPerUnit;
            var cameraTransform = worldCamera.transform;
            cameraTransform.position = new Vector3(0, 0, 3.2f) - cameraTransform.forward * 20
                - cameraTransform.right * offset.x - cameraTransform.up * offset.y;
            if (backgroundRenderer == null) return;
            var background = backgroundRenderer.transform;
            background.position = cameraTransform.position + cameraTransform.forward * 40;
            background.rotation = cameraTransform.rotation;
            background.localScale = new Vector3(screenWidth / pixelsPerUnit, screenHeight / pixelsPerUnit, 1);
            backgroundProperties ??= new MaterialPropertyBlock();
            backgroundProperties.SetVector("_BaseMap_ST", new Vector4(screenWidth / layout.ContentPixels.width,
                screenHeight / layout.ContentPixels.height, -layout.ContentPixels.x / layout.ContentPixels.width,
                -layout.ContentPixels.y / layout.ContentPixels.height));
            backgroundRenderer.SetPropertyBlock(backgroundProperties);
            pitInteriorProperties ??= new MaterialPropertyBlock();
            var opening = pitOpening != null ? pitOpening.position : pit.position;
            float radius = Mathf.Max(0, pitApertureRadius - .5f / uiStyle.WorldPixelsPerUnit);
            pitInteriorProperties.SetVector("_PitCenter", new Vector4(opening.x, opening.y, opening.z, radius));
            pitInteriorProperties.SetVector("_ViewRay", cameraTransform.forward);
            if (pitInteriors != null) foreach (var interior in pitInteriors) interior.SetPropertyBlock(pitInteriorProperties);
        }
        /// <summary>仅演出切换主题；由当前加载器隔离旧请求并跟踪资源。</summary>
        internal async UniTask<bool> ApplyThemeAsync(BlockPortersThemeCatalog.Theme theme)
        {
            themeLoader ??= new BlockPortersThemeLoader(ResourceServices.CreateLoader);
            try
            {
                bool applied = await themeLoader.ApplyAsync(theme, texture =>
                {
                    backgroundProperties ??= new MaterialPropertyBlock();
                    backgroundProperties.SetTexture("_BaseMap", texture);
                    backgroundRenderer.SetPropertyBlock(backgroundProperties);
                });
                if (applied) lastAppliedTheme = theme.Id;
                return applied;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"背景加载未成功，保留当前背景：{exception.Message}", this);
                return false;
            }
        }
        private void OnApplicationPause(bool paused) => isApplicationPaused = paused;
        private void ResetRewardLifetime()
        {
            rewardLifetime?.Cancel(); rewardLifetime?.Dispose();
            rewardLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        }
        private void RestoreLighting()
        {
            foreach (var light in suspendedLights) if (light != null) light.enabled = true;
            suspendedLights.Clear();
        }
        private void OnDisable() { isReady = false; themeLoader?.Invalidate(); RestoreLighting(); }
        private void OnDestroy()
        {
            ui?.Dispose();
            RestoreLighting();
            themeLoader?.Dispose();
            if (levelMaterials != null) foreach (var material in levelMaterials) Destroy(material);
            rewardLifetime?.Cancel(); rewardLifetime?.Dispose();
            lifetime?.Cancel(); lifetime?.Dispose(); Changed = null;
        }
    }
}
