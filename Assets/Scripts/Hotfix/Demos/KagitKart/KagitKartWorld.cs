using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Inputs;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Hotfix.KagitKart
{
    /// 场景组合根：资源、规则会话、赛车表现与页面按本场生命周期收口。
    public sealed partial class KagitKartWorld : MonoBehaviour
    {
        private const string Root = "LoadResources/Demos/kagit_kart/";
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Light sun;
        [SerializeField] private InputActionAsset inputTemplate;
        private IResourceLoader loader;
        private KagitKartRaceResources raceResources;
        private KagitKartTimeTrialRecord currentRecord;
        private int prepareVersion;
        private readonly HashSet<View> preparedViews = new();
        private readonly CancellationTokenSource lifetimeCancellation = new CancellationTokenSource();
        private KagitKartSourceCatalog source;
        private KagitKartRecordStore records;
        private KagitKartSceneryWorld scenery;
        private KagitKartItemVisuals itemVisuals;
        private KagitKartRaceFx raceFx;
        private Transform[] actors;
        private KagitKartCharacterVisual[] actorVisuals;
        private Transform ghostActor;
        private KagitKartCharacterVisual ghostVisual;
        private KagitKartGhostData ghost;
        private KagitKartCameraRig cameraRig;
        private Func<KagitKartControls> readControls;
        private MenuInputScope gameplayMenuScope;
        private KagitKartHudView hud;
        private View modal;
        private View menu;
        private KagitKartBookStage bookStage;
        private KagitKartPodiumStage podium;
        private AsyncLazy<KagitKartPodiumStage> preparedPodium;
        private KagitKartAudio audio;
        private KagitKartPostProcess postProcess;
        private Color bookInk;
        private KagitKartRaceCinematics cinematics;
        private TextAsset introPaths;
        private bool ready;
        private bool busy;
        private bool exiting;
        private bool uiBusy;
        private bool uiDirty;
        private float presentationTime;
        private float refreshTime;
        private KagitKartAction refreshAction;
        private KagitKartQueryInfo cameraTrackInfo;
        private int cameraTrackHint = -1;

        public KagitKartSession Session { get; private set; }
        public KagitKartInput Input { get; private set; }
        public KagitKartRaceEntry PlayerEntry { get; private set; }
        public KagitKartGrandPrix GrandPrix { get; private set; }
        public KagitKartRaceMode Mode { get; private set; } = KagitKartRaceMode.GrandPrix;
        public string TrackId { get; private set; } = "meadow";
        public string CharacterId { get; private set; } = "tilki";
        public int SpeedClass { get; private set; } = 120;
        public bool Busy => busy || exiting;
        public string Feedback { get; private set; }
        public bool IntroPlaying => cinematics?.IntroPlaying == true;
        public bool PodiumPlaying { get; private set; }
        /// 首次开书时纸卡等待封面落下。
        public float MenuEntranceDelay { get; private set; }
        /// 菜单预览直接读取原作属性，不复制比赛配置。
        public IReadOnlyList<KagitKartCharacter> Roster => source?.roster;

        private void Start() => InitializeAsync().Forget();

        private async UniTaskVoid InitializeAsync()
        {
            try
            {
                records = new KagitKartRecordStore();
                var token = lifetimeCancellation.Token;
                if (!await DemoIslandEditorBootstrap.EnsureReadyAsync(token))
                {
                    return;
                }
                LoadSettings();
                await GameSceneNavigator.Instance.WaitUntilStableAsync(GameSceneId.KagitKart, token);
                loader = ResourceServices.CreateLoader();
                var catalog = await loader.LoadAssetAsync<TextAsset>(Root + "Data/SourceCatalog");
                token.ThrowIfCancellationRequested();
                source = KagitKartSourceCatalog.Parse(catalog.text);
                introPaths = await loader.LoadAssetAsync<TextAsset>(Root + "Data/IntroPaths");
                var bank = await loader.LoadAssetAsync<KagitKartAudioBank>(Root + "Data/AudioBank");
                token.ThrowIfCancellationRequested();
                audio = gameObject.AddComponent<KagitKartAudio>();
                audio.Initialize(bank, worldCamera);
                var postTemplate = await loader.LoadAssetAsync<Material>(Root + "Art/Materials/Post");
                token.ThrowIfCancellationRequested();
                bookInk = postTemplate.GetVector("uInk");
                postProcess = gameObject.AddComponent<KagitKartPostProcess>();
                postProcess.Initialize(worldCamera, postTemplate);
                Input = new KagitKartInput(inputTemplate);
                ApplySettings();
                gameplayMenuScope = new MenuInputScope(EventSystem.current);
                gameplayMenuScope.SetContext(GameplayInputContext.Interaction);
                Input.InputCleared += ClearInput;
                Input.DeviceChanged += OnDeviceChanged;
                readControls = () => Input.ReadControls(Session.Race.State == KagitKartRaceState.Countdown);
                GlobalData.Add<KagitKartData>().World = this;
                var book = await loader.InstantiateAsync(Root + "Prefabs/BookStage", transform);
                token.ThrowIfCancellationRequested();
                bookStage = book.GetComponent<KagitKartBookStage>();
                bookStage.Initialize(worldCamera, sun);
                ApplySettings();
                await InitializeHintsAsync(token);
                bookStage.PageTurnStarted += audio.PageTurn;
                raceResources = new KagitKartRaceResources(transform, source, sun, token);
                ready = true;
                await OpenMenuAsync(KagitKartCommand.Title);
                PrepareSelectedRaceAsync().Forget();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Feedback = "比赛加载失败，请返回大厅重试。";
                Debug.LogException(exception, this);
                Dispatch(KagitKartCommand.Refresh);
            }
        }

        /// <summary>菜单确认后创建完整比赛；模式、角色和赛道在加载前统一校验。</summary>
        /// <param name="mode">大奖赛、单场或计时赛。</param>
        /// <param name="trackId">原作赛道标识，大奖赛始终从第一章开始。</param>
        /// <param name="characterId">原作角色标识。</param>
        /// <param name="speedClass">80、120 或 200。</param>
        public async UniTask StartRaceAsync(KagitKartRaceMode mode, string trackId, string characterId, int speedClass)
        {
            if (!ready || Busy)
            {
                return;
            }
            KagitKartCatalog.TrackName(trackId);
            KagitKartCatalog.CharacterName(characterId);
            if (!Enum.IsDefined(typeof(KagitKartRaceMode), mode) || speedClass != 80 && speedClass != 120 && speedClass != 200)
            {
                throw new ArgumentException("比赛模式或速度组别无效。");
            }
            Mode = mode;
            CharacterId = characterId;
            SpeedClass = speedClass;
            GrandPrix = mode == KagitKartRaceMode.GrandPrix ? new KagitKartGrandPrix(source.roster) : null;
            TrackId = GrandPrix?.TrackId ?? trackId;
            await StartRoundAsync();
        }

        private async UniTask StartRoundAsync(CancellationToken preparationToken = default)
        {
            ++prepareVersion;
            busy = true;
            Input.SetDriving(false);
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                var token = preparationToken.CanBeCanceled ? preparationToken : lifetimeCancellation.Token;
                Feedback = "正在准备比赛资源……";
                var prepared = await raceResources.PrepareAsync(TrackId, NextPreparedTrack(TrackId)).AttachExternalCancellation(token);
                await PrepareRaceViewsAsync(token);
                await audio.PrepareRaceAudioAsync(TrackId, token);
                while (uiBusy)
                {
                    await UniTask.Yield(token);
                }
                await CloseViewsAsync();
                token.ThrowIfCancellationRequested();
                bookStage.gameObject.SetActive(false);
                StopRound();
                CapturePreviousStandings();
                var data = prepared.Data;
                var plan = prepared.Plan;
                Feedback = null;
                ghost = null;
                currentRecord = null;
                if (Mode == KagitKartRaceMode.TimeTrial)
                {
                    currentRecord = records.Load(TrackId, SpeedClass, CharacterId);
                    ghost = currentRecord.ghost;
                }
                uint seed = Network?.Connected == true ? Network.Room.Seed : unchecked((uint)DateTime.UtcNow.Ticks);
                Session = new KagitKartSession(source, data, plan, Mode, CharacterId, SpeedClass,
                    seed, GrandPrix, ghost, Network?.Connected == true ? Network.Room.Grid : null,
                    Network?.Connected == true ? Network.Room.Players.Where(p => p.Connected).Select(p => p.CharacterId).ToArray() : null);
                foreach (var entry in Session.Race.Entries)
                {
                    if (entry.Vehicle.IsPlayer)
                    {
                        PlayerEntry = entry;
                    }
                }
                scenery = prepared.Scenery;
                actors = raceResources.Activate(prepared, Session, sun, seed);
                scenery.Popup += audio.Popup;
                postProcess.SetInk(scenery.Asset.batches[0].materials[0].GetVector("uInk"));
                worldCamera.clearFlags = CameraClearFlags.Skybox;
                worldCamera.GetComponent<Skybox>().material = scenery.Asset.SkyMaterial;
                sun.color = scenery.Asset.SunColor;
                sun.intensity = scenery.Asset.SunIntensity;
                sun.transform.rotation = Quaternion.LookRotation(-scenery.Asset.SunDirection);
                actorVisuals = new KagitKartCharacterVisual[actors.Length];
                for (int i = 0; i < actors.Length; i++) actorVisuals[i] = actors[i].GetComponent<KagitKartCharacterVisual>();
                if (ghost != null)
                {
                    ghostActor = await raceResources.PrepareGhostAsync(Session.Player.Character.id);
                    token.ThrowIfCancellationRequested();
                    ghostVisual = ghostActor.GetComponent<KagitKartCharacterVisual>();
                    ghostVisual.Bind(null, source.config.kart, isGhost: true);
                    ghostVisual.SetEnvironment(scenery.Asset.batches[0].materials[0]);
                    ghostActor.gameObject.SetActive(false);
                }
                cameraRig = new KagitKartCameraRig(worldCamera, source.config.camera, Session.Player, Session.Track.PageY);
                ApplySettings();
                itemVisuals = raceResources.Items;
                raceFx = raceResources.Effects;
                cinematics = new KagitKartRaceCinematics(introPaths.text, worldCamera, Session, source.config.camera, ReducedMotion);
                audio.Bind(Session);
                audio.PlayIntro(TrackId);
                Hints.Bind(Session.Track.Track.data);
                Session.Player.Signaled += OnPlayerHintSignal;
                Session.Items.Signaled += OnItemHintSignal;
                Session.Pause.Changed += OnPauseChanged;
                Session.Race.Signaled += OnRaceSignal;
                Session.Collisions.Bumped += OnBumped;
                presentationTime = refreshTime = 0;
                cameraTrackHint = -1;
                var result = await UIManager.Instance.OpenAsync<KagitKartHudView>(view => view.SetData(this),
                    new UIOpenOptions(animated: false), token);
                token.ThrowIfCancellationRequested();
                hud = result;
                busy = false;
                Input.SetDriving(false);
                Dispatch(KagitKartCommand.Refresh);
            }
            catch
            {
                busy = false;
                StopRound();
                throw;
            }
        }

        private async UniTask PrepareSelectedRaceAsync()
        {
            if (Session != null && GrandPrix?.Completed == true) return;
            int version = ++prepareVersion;
            string track = Mode == KagitKartRaceMode.GrandPrix && GrandPrix?.RoundComplete == true
                ? GrandPrix.Completed ? "meadow" : new[] { "meadow", "bosphorus", "glacier", "desk" }[GrandPrix.Index + 1]
                : TrackId;
            try
            {
                var token = lifetimeCancellation.Token;
                await raceResources.PrepareAsync(track, NextPreparedTrack(track));
                if (version != prepareVersion || Session != null && Session.Race.State != KagitKartRaceState.Done) return;
                await audio.PrepareRaceAudioAsync(track, token);
                if (version != prepareVersion) return;
                if (Mode == KagitKartRaceMode.TimeTrial) await raceResources.PrepareGhostAsync(CharacterId);
                await PrepareRaceViewsAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (!exiting && version == prepareVersion)
                {
                    Feedback = "赛前准备失败，开始比赛时将重试。";
                    Debug.LogWarning(exception.Message, this);
                    Dispatch(KagitKartCommand.Refresh);
                }
            }
        }

        private async UniTask PrepareRaceViewsAsync(CancellationToken token)
        {
            preparedViews.Add(await UIManager.Instance.PreloadAsync<KagitKartHudView>(cancellationToken: token));
            preparedViews.Add(await UIManager.Instance.PreloadAsync<KagitKartResultsView>(cancellationToken: token));
            preparedViews.Add(await UIManager.Instance.PreloadAsync<KagitKartStandingsView>(cancellationToken: token));
            preparedViews.Add(await UIManager.Instance.PreloadAsync<KagitKartPodiumView>(cancellationToken: token));
            if (Mode == KagitKartRaceMode.GrandPrix) await PreparePodiumAsync();
        }

        private string NextPreparedTrack(string track)
        {
            if (Mode != KagitKartRaceMode.GrandPrix) return null;
            for (int i = 0; i < KagitKartCatalog.Tracks.Count - 1; i++)
            {
                if (KagitKartCatalog.Tracks[i] == track) return KagitKartCatalog.Tracks[i + 1];
            }
            return null;
        }

        private UniTask<KagitKartPodiumStage> PreparePodiumAsync()
        {
            preparedPodium ??= new AsyncLazy<KagitKartPodiumStage>(CreatePodiumAsync);
            return preparedPodium.Task;
        }

        private async UniTask<KagitKartPodiumStage> CreatePodiumAsync()
        {
            try
            {
                var instance = await loader.InstantiateAsync(Root + "Prefabs/PodiumStage", transform);
                lifetimeCancellation.Token.ThrowIfCancellationRequested();
                podium = instance.GetComponent<KagitKartPodiumStage>();
                podium.Initialize(worldCamera);
                podium.Completed += OnPodiumCompleted;
                podium.Hide();
                return podium;
            }
            catch
            {
                preparedPodium = null;
                throw;
            }
        }

        private void Update()
        {
            SampleFrameRate();
            if (!ready || Busy)
            {
                return;
            }
            if (Input.Pressed("Mute"))
            {
                GlobalData.Dispatch(new KagitKartAction(this, KagitKartCommand.ChangeSetting, Settings.Muted ? 0 : 1, "Muted", playClick: false));
            }
            if (TryHandleRaceShortcuts()) return;
            if (Session == null || OptionsOpen) return;
            if (IntroPlaying && !Session.Pause.IsPaused && Input.Pressed("Skip"))
            {
                Dispatch(KagitKartCommand.SkipIntro);
                return;
            }
            gameplayMenuScope.Update();
            if (IntroPlaying)
            {
                return;
            }
            Input.CaptureFrame();
            if (Core.Runtime.TimeControls.GameTimeService.Scale == 0)
            {
                Input.ResetInput();
                return;
            }
            if (!Session.Networked)
            {
                Session.AdvanceInputFrame(Core.Runtime.TimeControls.GameTimeService.DeltaTime, readControls);
            }
            refreshTime += Time.unscaledDeltaTime;
            if (refreshTime >= 0.1f)
            {
                refreshTime = 0;
                Dispatch(KagitKartCommand.Refresh);
            }
        }

        private void LateUpdate()
        {
            hintsView?.RefreshFrameRate(FramesPerSecond);
            hud?.AdvanceVisual();
            if (modal is KagitKartResultsView resultView) resultView.AdvanceVisual();
            else if (modal is KagitKartStandingsView standingsView) standingsView.AdvanceVisual();
            UpdateHints();
            if (Session == null && bookStage != null && bookStage.gameObject.activeInHierarchy)
            {
                bookStage.Advance(Core.Runtime.TimeControls.GameTimeService.DeltaTime);
                if (PodiumPlaying) podium.Advance(Core.Runtime.TimeControls.GameTimeService.DeltaTime);
                return;
            }
            if (Busy || Session == null || actors == null || scenery == null)
            {
                return;
            }
            float alpha = Network?.IsHost == true ? 1 : (float)Session.Interpolation;
            float dt = Session.Pause.IsPaused && Network?.Connected != true ? 0 : Mathf.Min(Core.Runtime.TimeControls.GameTimeService.DeltaTime, 0.1f);
            for (int i = 0; i < actors.Length; i++)
            {
                var kart = Session.Karts[i];
                float kartAlpha = Network?.Connected == true && kart.IsPlayer && !kart.Autopilot ? 1 : alpha;
                actors[i].position = KagitKartVehicle.ToUnity(Vector3.LerpUnclamped(kart.PreviousPosition, kart.Position, kartAlpha));
                actors[i].rotation = Quaternion.SlerpUnclamped(kart.PreviousRotation, kart.Rotation, kartAlpha);
                actorVisuals[i].Advance(dt, ReducedMotion);
            }
            if (ghostActor != null)
            {
                if (IntroPlaying || Session.Race.State != KagitKartRaceState.Countdown)
                {
                    bool visible = KagitKartGhostPlayback.TrySample(ghost, Session.RaceTime, out Vector3 p, out float heading);
                    ghostActor.gameObject.SetActive(visible);
                    if (visible)
                    {
                        ghostActor.position = KagitKartVehicle.ToUnity(p);
                        ghostActor.rotation = Quaternion.Euler(0, -heading * Mathf.Rad2Deg, 0);
                    }
                }
                ghostVisual.Advance(dt, ReducedMotion, topSpeed: Session.Player.BaseTop);
            }
            presentationTime += dt;
            itemVisuals.Advance(dt, worldCamera, ReducedMotion);
            if (IntroPlaying)
            {
                cinematics.AdvanceIntro(dt);
                if (!IntroPlaying) FinishIntro();
            }
            else if (cinematics.Finishing)
            {
                cinematics.AdvanceFinish(dt, actors[PlayerIndex()].position);
            }
            else
            {
                cameraRig.Advance(dt, actors[PlayerIndex()].position);
            }
            raceFx.Advance(dt, worldCamera, reduceMotion: ReducedMotion, chaseView: !IntroPlaying && !cinematics.Finishing);
            Vector3 cameraPosition = worldCamera.transform.position;
            Session.Track.Query(new Vector3(-cameraPosition.x, cameraPosition.y, cameraPosition.z), cameraTrackHint, ref cameraTrackInfo);
            cameraTrackHint = cameraTrackInfo.Index;
            scenery.Render(worldCamera, (float)cameraTrackInfo.Distance, dt, presentationTime, cameraRig.LookBack, ReducedMotion);
        }

        private int PlayerIndex()
        {
            for (int i = 0; i < Session.Karts.Count; i++)
            {
                if (Session.Karts[i].IsPlayer)
                {
                    return i;
                }
            }
            return 0;
        }

        internal void ApplyCommand(KagitKartAction action)
        {
            if (!ready || Busy || GameSceneNavigator.Instance?.IsTransitioning == true)
            {
                return;
            }
            var command = action.Command;
            if (action.PlayClick && command != KagitKartCommand.Refresh && command != KagitKartCommand.PreviewCharacter && command != KagitKartCommand.PreviewTrack && command != KagitKartCommand.MarkTutorialSeen)
            {
                audio.Click();
            }
            if (command == KagitKartCommand.Hub)
            {
                ReturnHubAsync().Forget();
                return;
            }
            if (ApplyOnlineCommand(action)) return;
            switch (command)
            {
                case KagitKartCommand.MarkTutorialSeen:
                    SaveSetting(new KagitKartAction(this, KagitKartCommand.ChangeSetting, 1, "SeenTutorial"));
                    return;
                case KagitKartCommand.Settings:
                    OpenOptionsAsync(false).Forget();
                    return;
                case KagitKartCommand.Controls:
                    OpenOptionsAsync(true).Forget();
                    return;
                case KagitKartCommand.ChangeSetting:
                    SaveSetting(action);
                    return;
                case KagitKartCommand.CloseOptions:
                    CloseOptionsCommandAsync().Forget();
                    return;
                case KagitKartCommand.Modes:
                case KagitKartCommand.Classes:
                case KagitKartCommand.Characters:
                case KagitKartCommand.Tracks:
                case KagitKartCommand.Title:
                    OpenMenuAsync(command).Forget();
                    return;
                case KagitKartCommand.PreviewCharacter:
                    KagitKartCatalog.CharacterName(action.Id);
                    CharacterId = action.Id;
                    bookStage.PreviewCharacter(action.Id);
                    PrepareSelectedRaceAsync().Forget();
                    return;
                case KagitKartCommand.PreviewTrack:
                    KagitKartCatalog.TrackName(action.Id);
                    TrackId = action.Id;
                    bookStage.ShowChapter(action.Id);
                    PrepareSelectedRaceAsync().Forget();
                    return;
                case KagitKartCommand.ChooseMode:
                    if (!Enum.IsDefined(typeof(KagitKartRaceMode), action.Number))
                    {
                        return;
                    }
                    Mode = (KagitKartRaceMode)action.Number;
                    PrepareSelectedRaceAsync().Forget();
                    Feedback = null;
                    OpenMenuAsync(KagitKartCommand.Classes).Forget();
                    return;
                case KagitKartCommand.ChooseSpeedClass:
                    if (action.Number != 80 && action.Number != 120 && action.Number != 200)
                    {
                        return;
                    }
                    SpeedClass = action.Number;
                    PrepareSelectedRaceAsync().Forget();
                    OpenMenuAsync(KagitKartCommand.Characters).Forget();
                    return;
                case KagitKartCommand.ChooseCharacter:
                    KagitKartCatalog.CharacterName(action.Id);
                    CharacterId = action.Id;
                    if (Mode == KagitKartRaceMode.GrandPrix)
                    {
                        BeginSelectedRaceAsync().Forget();
                    }
                    else
                    {
                        OpenMenuAsync(KagitKartCommand.Tracks).Forget();
                    }
                    return;
                case KagitKartCommand.ChooseTrack:
                    KagitKartCatalog.TrackName(action.Id);
                    TrackId = action.Id;
                    BeginSelectedRaceAsync().Forget();
                    return;
            }
            if (Session == null)
            {
                return;
            }
            switch (command)
            {
                case KagitKartCommand.SkipIntro:
                    if (IntroPlaying && !Session.Pause.IsPaused)
                    {
                        cinematics.SkipIntro();
                        FinishIntro();
                    }
                    break;
                case KagitKartCommand.Pause:
                    Session.Pause.RequestPause(LocalPauseReason.User);
                    break;
                case KagitKartCommand.Resume:
                    if (optionsView != null) ResumeFromOptionsAsync().Forget();
                    else Session.Pause.TryResume();
                    break;
                case KagitKartCommand.Restart:
                    GrandPrix?.RestartRound();
                    ChangeRoundAsync().Forget();
                    break;
                case KagitKartCommand.ShowStandings:
                    ShowStandingsAsync().Forget();
                    break;
                case KagitKartCommand.NextRound:
                    if (GrandPrix?.Completed == true)
                    {
                        ShowPodiumAsync().Forget();
                    }
                    else if (GrandPrix?.NextRound() == true)
                    {
                        TrackId = GrandPrix.TrackId;
                        ChangeRoundAsync().Forget();
                    }
                    break;
            }
        }

        private async UniTask OpenMenuAsync(KagitKartCommand page)
        {
            busy = true;
            MenuEntranceDelay = menu is KagitKartTitleView && page == KagitKartCommand.PlayModes ? 0.8f : 0;
            Input.SetDriving(false);
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                while (uiBusy)
                {
                    await UniTask.Yield();
                }
                await CloseViewsAsync();
                StopRound();
                audio.PlayMenu();
                postProcess.SetInk(bookInk);
                bookStage.SetPage(page, CharacterId, TrackId);
                if (page == KagitKartCommand.Modes || page == KagitKartCommand.Title)
                {
                    GrandPrix = null;
                }
                PrepareSelectedRaceAsync().Forget();
                var token = lifetimeCancellation.Token;
                var options = new UIOpenOptions(animated: false);
                View shown;
                switch (page)
                {
                    case KagitKartCommand.PlayModes:
                        shown = await UIManager.Instance.OpenAsync<KagitKartPlayModeView>(view => view.SetData(this), options, token);
                        break;
                    case KagitKartCommand.Online:
                        shown = await UIManager.Instance.OpenAsync<KagitKartOnlineView>(view => view.SetData(this), options, token);
                        break;
                    case KagitKartCommand.CreateRoom:
                        shown = await UIManager.Instance.OpenAsync<KagitKartCreateRoomView>(view => view.SetData(this), options, token);
                        break;
                    case KagitKartCommand.JoinRoom:
                        shown = await UIManager.Instance.OpenAsync<KagitKartJoinRoomView>(view => view.SetData(this), options, token);
                        break;
                    case KagitKartCommand.Room:
                        shown = await UIManager.Instance.OpenAsync<KagitKartRoomView>(view => view.SetData(this), options, token);
                        break;
                    case KagitKartCommand.Title:
                        shown = await UIManager.Instance.OpenAsync<KagitKartTitleView>(view => view.SetData(this), options, token);
                        break;
                    case KagitKartCommand.Modes:
                        shown = await UIManager.Instance.OpenAsync<KagitKartModeView>(view => view.SetData(this), options, token);
                        break;
                    case KagitKartCommand.Classes:
                        shown = await UIManager.Instance.OpenAsync<KagitKartClassView>(view => view.SetData(this), options, token);
                        break;
                    case KagitKartCommand.Characters:
                        shown = await UIManager.Instance.OpenAsync<KagitKartCharacterView>(view => view.SetData(this), options, token);
                        break;
                    default:
                        shown = await UIManager.Instance.OpenAsync<KagitKartTrackView>(view => view.SetData(this), options, token);
                        break;
                }
                token.ThrowIfCancellationRequested();
                menu = shown;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Feedback = "页面加载失败，请返回大厅重试。";
                Debug.LogException(exception, this);
            }
            finally
            {
                busy = false;
                Dispatch(KagitKartCommand.Refresh);
            }
        }

        private async UniTaskVoid ShowPodiumAsync()
        {
            busy = true;
            Input.SetDriving(false);
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                while (uiBusy) await UniTask.Yield();
                await CloseViewsAsync();
                StopRound();
                await PreparePodiumAsync();
                postProcess.SetInk(bookInk);
                var standings = GrandPrix.SortedStandings();
                bookStage.SetPodium(ReducedMotion);
                podium.Show(new[] { standings[0].CharacterId, standings[1].CharacterId, standings[2].CharacterId }, CharacterId, ReducedMotion);
                PodiumPlaying = true;
                audio.PlayMenu(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Feedback = "颁奖典礼加载失败，请返回菜单重试。";
                Debug.LogException(exception, this);
                await OpenMenuAsync(KagitKartCommand.Title);
            }
            finally
            {
                busy = false;
                Dispatch(KagitKartCommand.Refresh);
            }
        }

        private void OnPodiumCompleted() => ShowPodiumPageAsync().Forget();

        private async UniTaskVoid ShowPodiumPageAsync()
        {
            if (!PodiumPlaying || menu != null || Busy) return;
            busy = true;
            try
            {
                var shown = await UIManager.Instance.OpenAsync<KagitKartPodiumView>(view => view.SetData(this),
                    new UIOpenOptions(animated: false), lifetimeCancellation.Token);

                menu = shown;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                await OpenMenuAsync(KagitKartCommand.Title);
            }
            finally
            {
                busy = false;
                Dispatch(KagitKartCommand.Refresh);
            }
        }

        private async UniTaskVoid BeginSelectedRaceAsync()
        {
            try
            {
                await StartRaceAsync(Mode, TrackId, CharacterId, SpeedClass);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Feedback = "比赛加载失败，请重试或返回大厅。";
                Debug.LogException(exception, this);
                await OpenMenuAsync(Mode == KagitKartRaceMode.GrandPrix ? KagitKartCommand.Characters : KagitKartCommand.Tracks);
            }
        }

        /// <summary>章节预览读取当前速度与角色的最佳成绩；没有有效数据时返回空记录。</summary>
        /// <param name="trackId">原作章节标识。</param>
        public KagitKartTimeTrialRecord LoadRecord(string trackId)
        {
            return Session != null && trackId == TrackId && currentRecord != null ? currentRecord : records.Load(trackId, SpeedClass, CharacterId);
        }

        private async UniTaskVoid ChangeRoundAsync()
        {
            try
            {
                await StartRoundAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Feedback = "本场加载失败，请返回大厅重试。";
                Debug.LogException(exception, this);
                await OpenMenuAsync(Mode == KagitKartRaceMode.GrandPrix ? KagitKartCommand.Characters : KagitKartCommand.Tracks);
            }
        }

        private void OnRaceSignal(KagitKartRaceEvent signal)
        {
            if (signal.Kind == "finish" && signal.Kart == Session.Player)
            {
                cinematics.BeginFinish(actors[PlayerIndex()].position);
            }
            if (signal.Kind == "raceEnd")
            {
                Input.SetDriving(false);
                RememberCupResultOrder();
                if (GrandPrix != null && !GrandPrix.Completed)
                {
                    PrepareSelectedRaceAsync().Forget();
                }
                if (Mode == KagitKartRaceMode.TimeTrial)
                {
                    try
                    {
                        var snapshot = Session.Race.GhostRecorder.Snapshot(PlayerEntry.FinishTime, PlayerEntry.Progress.LapTimes);
                        currentRecord = records.SaveBest(TrackId, SpeedClass, CharacterId, snapshot, currentRecord);
                        Feedback = "计时成绩与最佳幽灵已保存。";
                    }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
                    {
                        Feedback = "本场已完成，但成绩保存失败。";
                        Debug.LogWarning(exception.Message, this);
                    }
                }
                SynchronizeModalAsync().Forget();
            }
            Dispatch(KagitKartCommand.Refresh);
        }

        private void OnBumped(KagitKartVehicle a, KagitKartVehicle b)
        {
            for (int i = 0; i < Session.Karts.Count; i++)
            {
                if (Session.Karts[i] == a || Session.Karts[i] == b)
                {
                    actorVisuals[i].Bump(source.config.kart.bumpImpulse);
                }
            }
        }

        private void FinishIntro()
        {
            audio.BeginCountdown();
            Hints.OnCountdown(3, Settings.SeenTutorial);
            cameraRig.Advance(0, actors[PlayerIndex()].position);
            Input.SetDriving(true);
            Dispatch(KagitKartCommand.Refresh);
        }

        private void OnPauseChanged()
        {
            Input.SetDriving(!IntroPlaying && !Session.Pause.IsPaused && Session.Race.State != KagitKartRaceState.Done);
            SynchronizeModalAsync().Forget();
            Dispatch(KagitKartCommand.Refresh);
        }

        private async UniTaskVoid SynchronizeModalAsync()
        {
            uiDirty = true;
            if (uiBusy || Busy || Session == null)
            {
                return;
            }
            uiBusy = true;
            try
            {
                while (uiDirty && !Busy && Session != null)
                {
                    uiDirty = false;
                    bool results = Session.Race.State == KagitKartRaceState.Done;
                    bool desired = results || Session.Pause.IsPaused;
                    if (desired && modal == null)
                    {
                        var token = lifetimeCancellation.Token;
                        View shown = results
                            ? await UIManager.Instance.OpenAsync<KagitKartResultsView>(view => view.SetData(this), new UIOpenOptions(animated: false), token)
                            : await UIManager.Instance.OpenAsync<KagitKartPauseView>(view => view.SetData(this), new UIOpenOptions(animated: false), token);
                        modal = shown;
                    }
                    else if (!desired && modal != null)
                    {
                        await UIManager.Instance.CloseAsync(modal);
                        modal = null;
                    }
                    uiDirty |= desired != (Session.Race.State == KagitKartRaceState.Done || Session.Pause.IsPaused);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                uiBusy = false;
            }
        }

        private async UniTask CloseViewsAsync()
        {
            await CloseOptionsAsync();
            if (menu != null)
            {
                await UIManager.Instance.CloseAsync(menu);
                menu = null;
            }
            if (modal != null)
            {
                await UIManager.Instance.CloseAsync(modal);
                modal = null;
            }
            if (hud != null)
            {
                await UIManager.Instance.CloseAsync(hud);
                hud = null;
            }
        }

        private async UniTaskVoid ReturnHubAsync()
        {
            if (GameSceneNavigator.Instance.IsTransitioning) return;
            exiting = true;
            Input.SetDriving(false);
            try
            {
                if (Network != null) await Network.LeaveAsync();
                Network = null;
                while (uiBusy)
                {
                    await UniTask.Yield();
                }
                if (this == null || !ready) return;
                // 来源界面保留到场景成功卸载；公共 Loading 覆盖它，失败仍可使用原实例。
                var result = await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
                if (result.Status == GameSceneSwitchStatus.Failed)
                {
                    throw new InvalidOperationException(result.Error);
                }
                if (result.Status != GameSceneSwitchStatus.Succeeded && this != null && ready)
                {
                    exiting = false;
                    if (Session?.Networked == true) await OpenMenuAsync(KagitKartCommand.Online);
                    else if (Session != null) OnPauseChanged();
                    else Dispatch(KagitKartCommand.Refresh);
                }
            }
            catch (Exception exception)
            {
                if (this == null || !ready) return;
                exiting = false;
                Debug.LogException(exception, this);
                if (Session?.Networked == true) await OpenMenuAsync(KagitKartCommand.Online);
                else if (Session != null) OnPauseChanged();
                else Dispatch(KagitKartCommand.Refresh);
            }
        }

        /// <summary>从当前页面或输入派发本场命令。</summary>
        /// <param name="command">要执行的领域流程操作。</param>
        /// <param name="playClick">UI 确认默认为 true，源全局硬件动作可显式省略 click。</param>
        public void Dispatch(KagitKartCommand command, bool playClick = true)
        {
            // Refresh 同步派发且没有回填数据；只在本 World 生命周期内复用。
            GlobalData.Dispatch(command == KagitKartCommand.Refresh
                ? refreshAction ??= new KagitKartAction(this, command)
                : new KagitKartAction(this, command, playClick: playClick));
        }

        private void ClearInput()
        {
            Session?.ClearInput();
            hud?.ClearTouch();
        }

        private void OnDeviceChanged()
        {
            bool available = InputDeviceState.ActiveKind != InputDeviceKind.Gamepad || InputDeviceState.ActiveDevice?.added == true;
            Session?.Pause.SetGamepadAvailable(available);
            Dispatch(KagitKartCommand.Refresh);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                Input?.ResetInput();
            }
            Session?.Pause.SetApplicationFocus(focused);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Input?.ResetInput();
            }
            Session?.Pause.SetApplicationPaused(paused);
        }

        private void StopRound()
        {
            Hints?.Unbind();
            PodiumPlaying = false;
            podium?.Hide();
            if (scenery != null && audio != null)
            {
                scenery.Popup -= audio.Popup;
            }
            audio?.Unbind();
            cinematics = null;
            if (Session != null)
            {
                Session.Player.Signaled -= OnPlayerHintSignal;
                Session.Items.Signaled -= OnItemHintSignal;
                Session.Pause.Changed -= OnPauseChanged;
                Session.Race.Signaled -= OnRaceSignal;
                Session.Collisions.Bumped -= OnBumped;
            }
            cameraRig?.Dispose();
            cameraRig = null;
            Session?.Dispose();
            Session = null;
            PlayerEntry = null;
            actors = null;
            actorVisuals = null;
            scenery = null;
            itemVisuals = null;
            raceFx = null;
            ghostActor = null;
            ghostVisual = null;
            raceResources?.Hide();
        }

        private void OnDestroy()
        {
            exiting = true;
            lifetimeCancellation.Cancel();
            Network?.Dispose();
            Network = null;
            foreach (var view in preparedViews)
            {
                if (view != null) UIManager.Instance.CloseAsync(view, false).Forget();
            }
            preparedViews.Clear();
            // 公共 Navigator 重载可能绕过 ReturnHub；捕获旧实例，不能按类型误关新会话。
            foreach (var view in new View[] { optionsView, menu, modal, hud, hintsView })
                if (view != null) UIManager.Instance.CloseAsync(view, false).Forget();
            optionsView = menu = modal = null;
            hud = null;
            hintsView = null;
            Core.Runtime.Settings.GameSettingsService.Changed -= OnPublicSettingsChanged;
            ready = false;
            DisposeHints();
            gameplayMenuScope?.Dispose();
            gameplayMenuScope = null;
            if (Input != null)
            {
                Input.InputCleared -= ClearInput;
                Input.DeviceChanged -= OnDeviceChanged;
                Input.Dispose();
                Input = null;
            }
            StopRound();
            if (podium != null) podium.Completed -= OnPodiumCompleted;
            if (bookStage != null && audio != null)
            {
                bookStage.PageTurnStarted -= audio.PageTurn;
            }
            if (postProcess != null) postProcess.enabled = false;
            raceResources?.Dispose();
            raceResources = null;
            loader?.Dispose();
            loader = null;
            lifetimeCancellation.Dispose();
            if (GlobalData.Get<KagitKartData>()?.World == this)
            {
                GlobalData.Remove<KagitKartData>();
            }
        }
    }
}
