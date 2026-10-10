using System;
using System.IO;
using System.Collections.Generic;
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
        private IResourceLoader raceLoader;
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
                LoadSettings();
                var token = this.GetCancellationTokenOnDestroy();
                if (!await DemoIslandEditorBootstrap.EnsureReadyAsync(token))
                {
                    return;
                }
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
                bookStage.PageTurnStarted += audio.PageTurn;
                ready = true;
                await OpenMenuAsync(KagitKartCommand.Title);
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

        private async UniTask StartRoundAsync()
        {
            busy = true;
            Input.SetDriving(false);
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                while (uiBusy)
                {
                    await UniTask.Yield();
                }
                await CloseViewsAsync();
                bookStage.gameObject.SetActive(false);
                StopRound();
                var token = this.GetCancellationTokenOnDestroy();
                var owned = ResourceServices.CreateLoader();
                raceLoader = owned;
                string name = KagitKartCatalog.TrackAssetName(TrackId);
                var trackAsset = await owned.LoadAssetAsync<TextAsset>(Root + "Data/Tracks/" + name);
                var planAsset = await owned.LoadAssetAsync<TextAsset>(Root + "Data/AiPlans/" + name);
                token.ThrowIfCancellationRequested();
                var data = KagitKartTrackData.Parse(trackAsset.text);
                var plan = KagitKartAiPlan.Parse(planAsset.text, data);
                Feedback = null;
                ghost = null;
                if (Mode == KagitKartRaceMode.TimeTrial)
                {
                    try
                    {
                        ghost = records.Load(TrackId, SpeedClass, CharacterId).ghost;
                    }
                    catch (IOException exception)
                    {
                        Feedback = "无法读取最佳记录，本场仍可正常比赛。";
                        Debug.LogWarning(exception.Message, this);
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        Feedback = "无法读取最佳记录，本场仍可正常比赛。";
                        Debug.LogWarning(exception.Message, this);
                    }
                }
                uint seed = unchecked((uint)DateTime.UtcNow.Ticks);
                Session = new KagitKartSession(source, data, plan, Mode, CharacterId, SpeedClass,
                    seed, GrandPrix, ghost);
                foreach (var entry in Session.Race.Entries)
                {
                    if (entry.Vehicle.IsPlayer)
                    {
                        PlayerEntry = entry;
                    }
                }
                var world = await owned.InstantiateAsync(Root + "Prefabs/Tracks/" + name, transform);
                token.ThrowIfCancellationRequested();
                scenery = world.GetComponent<KagitKartSceneryWorld>();
                postProcess.SetInk(scenery.Asset.batches[0].materials[0].GetVector("uInk"));
                worldCamera.clearFlags = CameraClearFlags.Skybox;
                worldCamera.GetComponent<Skybox>().material = scenery.Asset.SkyMaterial;
                sun.color = scenery.Asset.SunColor;
                sun.intensity = scenery.Asset.SunIntensity;
                sun.transform.rotation = Quaternion.LookRotation(-scenery.Asset.SunDirection);
                actors = new Transform[Session.Karts.Count];
                actorVisuals = new KagitKartCharacterVisual[actors.Length];
                for (int i = 0; i < actors.Length; i++)
                {
                    var actor = await owned.InstantiateAsync(CharacterAddress(Session.Karts[i].Character.id), transform);
                    token.ThrowIfCancellationRequested();
                    if (actor == null)
                    {
                        throw new InvalidOperationException("赛车角色资源加载失败。");
                    }
                    actors[i] = actor.transform;
                    actors[i].GetComponent<SkinnedMeshRenderer>().updateWhenOffscreen = true;
                    actorVisuals[i] = actor.GetComponent<KagitKartCharacterVisual>();
                    actorVisuals[i].Bind(Session.Karts[i], source.config.kart, i * 0.37f);
                    actorVisuals[i].SetEnvironment(scenery.Asset.batches[0].materials[0]);
                }
                if (ghost != null)
                {
                    ghostActor = (await owned.InstantiateAsync(CharacterAddress(Session.Player.Character.id), transform)).transform;
                    token.ThrowIfCancellationRequested();
                    ghostVisual = ghostActor.GetComponent<KagitKartCharacterVisual>();
                    ghostVisual.Bind(null, source.config.kart, isGhost: true);
                    ghostVisual.SetEnvironment(scenery.Asset.batches[0].materials[0]);
                }
                cameraRig = new KagitKartCameraRig(worldCamera, source.config.camera, Session.Player, Session.Track.PageY);
                ApplySettings();
                var items = await owned.InstantiateAsync(Root + "Prefabs/ItemVisuals", transform);
                token.ThrowIfCancellationRequested();
                itemVisuals = items.GetComponent<KagitKartItemVisuals>();
                itemVisuals.Bind(Session, actors, scenery.Asset.batches[0].materials[0]);
                var fx = await owned.InstantiateAsync(Root + "Prefabs/RaceFx", transform);
                token.ThrowIfCancellationRequested();
                raceFx = fx.GetComponent<KagitKartRaceFx>();
                raceFx.Bind(Session, actors, scenery.Asset.batches[0].materials[0], sun, seed, source.config.kart);
                cinematics = new KagitKartRaceCinematics(introPaths.text, worldCamera, Session, source.config.camera, ReducedMotion);
                audio.Bind(Session);
                audio.PlayIntro(TrackId);
                Session.Pause.Changed += OnPauseChanged;
                Session.Race.Signaled += OnRaceSignal;
                Session.Collisions.Bumped += OnBumped;
                presentationTime = refreshTime = 0;
                cameraTrackHint = -1;
                var result = await UIManager.Instance.ShowAsync<KagitKartHudView>(view => view.SetData(this),
                    new UIShowOptions(animated: false), token);
                if (result.Status == UIOperationStatus.Failed)
                {
                    throw result.Exception;
                }
                token.ThrowIfCancellationRequested();
                hud = result.View as KagitKartHudView;
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

        private static string CharacterAddress(string id) => Root + "Prefabs/Characters/" + KagitKartCatalog.CharacterAssetName(id);

        private void Update()
        {
            SampleFrameRate();
            if (!ready || Busy)
            {
                return;
            }
            if (Input.Pressed("Mute"))
            {
                GlobalData.Dispatch(new KagitKartAction(this, KagitKartCommand.ChangeSetting, Settings.Muted ? 0 : 1, "Muted"));
            }
            if (Session == null || OptionsOpen) return;
            if (IntroPlaying && !Session.Pause.IsPaused && Input.Pressed("Skip"))
            {
                Dispatch(KagitKartCommand.SkipIntro);
                return;
            }
            if (Input.Pressed("Pause") && Session.Race.State != KagitKartRaceState.Done)
            {
                Dispatch(Session.Pause.IsPaused ? KagitKartCommand.Resume : KagitKartCommand.Pause);
            }
            if (Input.Pressed("Restart") && Mode == KagitKartRaceMode.TimeTrial)
            {
                Dispatch(KagitKartCommand.Restart);
                return;
            }
            hud?.ReadTouch();
            gameplayMenuScope.Update();
            if (IntroPlaying)
            {
                return;
            }
            Input.CaptureFrame();
            Session.AdvanceInputFrame(Time.unscaledDeltaTime, readControls);
            refreshTime += Time.unscaledDeltaTime;
            if (refreshTime >= 0.1f)
            {
                refreshTime = 0;
                Dispatch(KagitKartCommand.Refresh);
            }
        }

        private void LateUpdate()
        {
            if (Session == null && bookStage != null && bookStage.gameObject.activeInHierarchy)
            {
                bookStage.Advance(Time.unscaledDeltaTime);
                if (PodiumPlaying) podium.Advance(Time.unscaledDeltaTime);
                return;
            }
            if (Busy || Session == null || actors == null || scenery == null)
            {
                return;
            }
            float alpha = (float)Session.Interpolation;
            float dt = Session.Pause.IsPaused ? 0 : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            for (int i = 0; i < actors.Length; i++)
            {
                var kart = Session.Karts[i];
                actors[i].position = KagitKartVehicle.ToUnity(Vector3.LerpUnclamped(kart.PreviousPosition, kart.Position, alpha));
                actors[i].rotation = Quaternion.SlerpUnclamped(kart.PreviousRotation, kart.Rotation, alpha);
                actorVisuals[i].Advance(dt, ReducedMotion);
            }
            if (ghostActor != null && KagitKartGhostPlayback.TrySample(ghost, Session.RaceTime, out Vector3 p, out float heading))
            {
                ghostActor.position = KagitKartVehicle.ToUnity(p);
                ghostActor.rotation = Quaternion.Euler(0, -heading * Mathf.Rad2Deg, 0);
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
            if (!ready || Busy)
            {
                return;
            }
            var command = action.Command;
            if (command != KagitKartCommand.Refresh && command != KagitKartCommand.PreviewCharacter && command != KagitKartCommand.PreviewTrack)
            {
                audio.Click();
            }
            if (command == KagitKartCommand.Hub)
            {
                ReturnHubAsync().Forget();
                return;
            }
            switch (command)
            {
                case KagitKartCommand.Settings:
                    OpenOptionsAsync(false).Forget();
                    return;
                case KagitKartCommand.Controls:
                    OpenOptionsAsync(true).Forget();
                    return;
                case KagitKartCommand.ChangeSetting:
                    SaveSetting(action);
                    return;
                case KagitKartCommand.GraphicsSettings:
                    OpenGraphicsSettingsAsync().Forget();
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
                    return;
                case KagitKartCommand.PreviewTrack:
                    KagitKartCatalog.TrackName(action.Id);
                    TrackId = action.Id;
                    bookStage.ShowChapter(action.Id);
                    return;
                case KagitKartCommand.ChooseMode:
                    if (!Enum.IsDefined(typeof(KagitKartRaceMode), action.Number))
                    {
                        return;
                    }
                    Mode = (KagitKartRaceMode)action.Number;
                    Feedback = null;
                    OpenMenuAsync(KagitKartCommand.Classes).Forget();
                    return;
                case KagitKartCommand.ChooseSpeedClass:
                    if (action.Number != 80 && action.Number != 120 && action.Number != 200)
                    {
                        return;
                    }
                    SpeedClass = action.Number;
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
                    Session.Pause.TryResume();
                    break;
                case KagitKartCommand.Restart:
                    GrandPrix?.RestartRound();
                    ChangeRoundAsync().Forget();
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
            MenuEntranceDelay = menu is KagitKartTitleView && page == KagitKartCommand.Modes ? 0.8f : 0;
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
                if (page == KagitKartCommand.Modes)
                {
                    GrandPrix = null;
                }
                var token = this.GetCancellationTokenOnDestroy();
                var options = new UIShowOptions(animated: false);
                UIOperationResult shown;
                switch (page)
                {
                    case KagitKartCommand.Title:
                        shown = await UIManager.Instance.ShowAsync<KagitKartTitleView>(view => view.SetData(this), options, token);
                        break;
                    case KagitKartCommand.Modes:
                        shown = await UIManager.Instance.ShowAsync<KagitKartModeView>(view => view.SetData(this), options, token);
                        break;
                    case KagitKartCommand.Classes:
                        shown = await UIManager.Instance.ShowAsync<KagitKartClassView>(view => view.SetData(this), options, token);
                        break;
                    case KagitKartCommand.Characters:
                        shown = await UIManager.Instance.ShowAsync<KagitKartCharacterView>(view => view.SetData(this), options, token);
                        break;
                    default:
                        shown = await UIManager.Instance.ShowAsync<KagitKartTrackView>(view => view.SetData(this), options, token);
                        break;
                }
                if (shown.Status == UIOperationStatus.Failed)
                {
                    throw shown.Exception;
                }
                token.ThrowIfCancellationRequested();
                menu = shown.View;
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
                var token = this.GetCancellationTokenOnDestroy();
                if (podium == null)
                {
                    var instance = await loader.InstantiateAsync(Root + "Prefabs/PodiumStage", transform);
                    token.ThrowIfCancellationRequested();
                    podium = instance.GetComponent<KagitKartPodiumStage>();
                    podium.Initialize(worldCamera);
                    podium.Completed += OnPodiumCompleted;
                }
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
                var shown = await UIManager.Instance.ShowAsync<KagitKartPodiumView>(view => view.SetData(this),
                    new UIShowOptions(animated: false), this.GetCancellationTokenOnDestroy());
                if (shown.Status == UIOperationStatus.Failed) throw shown.Exception;
                menu = shown.View;
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

        /// <summary>章节预览读取当前速度与角色的最佳成绩；读取失败返回 null 供中文页面提示。</summary>
        /// <param name="trackId">原作章节标识。</param>
        public KagitKartTimeTrialRecord LoadRecord(string trackId)
        {
            try
            {
                return records.Load(trackId, SpeedClass, CharacterId);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
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
                if (Mode == KagitKartRaceMode.TimeTrial)
                {
                    try
                    {
                        var snapshot = Session.Race.GhostRecorder.Snapshot(PlayerEntry.FinishTime, PlayerEntry.Progress.LapTimes);
                        records.SaveBest(TrackId, SpeedClass, CharacterId, snapshot);
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
                        var token = this.GetCancellationTokenOnDestroy();
                        var shown = results
                            ? await UIManager.Instance.ShowAsync<KagitKartResultsView>(view => view.SetData(this), new UIShowOptions(animated: false), token)
                            : await UIManager.Instance.ShowAsync<KagitKartPauseView>(view => view.SetData(this), new UIShowOptions(animated: false), token);
                        if (shown.Status == UIOperationStatus.Failed)
                        {
                            throw shown.Exception;
                        }
                        modal = shown.View;
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
                var result = await UIManager.Instance.CloseAsync(menu);
                if (result.Status == UIOperationStatus.Failed)
                {
                    throw result.Exception;
                }
                menu = null;
            }
            if (modal != null)
            {
                var result = await UIManager.Instance.CloseAsync(modal);
                if (result.Status == UIOperationStatus.Failed)
                {
                    throw result.Exception;
                }
                modal = null;
            }
            if (hud != null)
            {
                var result = await UIManager.Instance.CloseAsync(hud);
                if (result.Status == UIOperationStatus.Failed)
                {
                    throw result.Exception;
                }
                hud = null;
            }
        }

        private async UniTaskVoid ReturnHubAsync()
        {
            exiting = true;
            Input.SetDriving(false);
            try
            {
                while (uiBusy)
                {
                    await UniTask.Yield();
                }
                await CloseViewsAsync();
                var result = await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub);
                if (result.Status == GameSceneSwitchStatus.Failed)
                {
                    throw new InvalidOperationException(result.Error);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                exiting = false;
                Session?.Pause.RequestPause(LocalPauseReason.User);
                Debug.LogException(exception, this);
                if (Session == null)
                {
                    await OpenMenuAsync(KagitKartCommand.Modes);
                    return;
                }
                var shown = await UIManager.Instance.ShowAsync<KagitKartHudView>(view => view.SetData(this), new UIShowOptions(animated: false));
                hud = shown.View as KagitKartHudView;
                SynchronizeModalAsync().Forget();
            }
        }

        /// <summary>从当前页面或输入派发本场命令。</summary>
        /// <param name="command">要执行的领域流程操作。</param>
        public void Dispatch(KagitKartCommand command) => GlobalData.Dispatch(new KagitKartAction(this, command));

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
            PodiumPlaying = false;
            podium?.Hide();
            audio?.Unbind();
            cinematics = null;
            if (Session != null)
            {
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
            raceLoader?.Dispose();
            raceLoader = null;
        }

        private void OnDestroy()
        {
            ready = false;
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
            loader?.Dispose();
            loader = null;
            if (GlobalData.Get<KagitKartData>()?.World == this)
            {
                GlobalData.Remove<KagitKartData>();
            }
        }
    }
}
