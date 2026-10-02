using System;
using System.Collections.Generic;
using System.Text;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 接收已提交的领域效果；只修改场景表现，不修改筹码、库存或小游戏结果。
    public sealed class JinxCasinoSceneEffects : MonoBehaviour
    {
        [SerializeField] private Transform localPlayer;
        [SerializeField] private Transform[] companions;
        [SerializeField] private Transform[] safePoints;
        [SerializeField] private CasinoEffectPrefabBinding[] prefabs;
        [SerializeField] private JinxCasinoAreaFacilities[] facilities = Array.Empty<JinxCasinoAreaFacilities>();
        [SerializeField] private CasinoActorEffectBinding[] actors = Array.Empty<CasinoActorEffectBinding>();
        [SerializeField] private Material disguiseMaterial;
        [SerializeField] private Material inkMaterial;
        [SerializeField] private GameObject carriedGoldPrefab;
        [SerializeField] private Transform carryAnchor;
        [SerializeField] private LayerMask collisionLayers = -1;
        private readonly HashSet<string> applied = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ActiveEffect> active = new List<ActiveEffect>();
        private readonly Dictionary<Transform, float> protectedTargets = new Dictionary<Transform, float>();
        private readonly List<RestoredProtection> restoredProtection = new List<RestoredProtection>();
        private CasinoAdventureState currentState;
        private string runId;
        private int stageIndex = -1;
        private long radarExpiresMilliseconds;
        private GameObject carriedGold;
        private Transform[] missionMarkers = Array.Empty<Transform>();
        private float announcementUntil;
        private string announcement;
        private JinxCasinoPresentationClock presentationClock;
        private float PresentationTime => presentationClock?.TimeSeconds ?? Time.unscaledTime;
        private float PresentationDelta => presentationClock?.GetDeltaSeconds(Time.frameCount) ?? Time.unscaledDeltaTime;

        /// <summary>绑定本Demo演出时钟，迁移正在演出的剩余时间；null保留旧原型真实时间。</summary>
        /// <param name="clock">宿主共享时钟，不关闭组件，不清除已提交效果。</param>
        public void BindPresentationClock(JinxCasinoPresentationClock clock)
        {
            if (ReferenceEquals(presentationClock, clock)) return;
            float before = PresentationTime;
            presentationClock = clock;
            float offset = PresentationTime - before;
            foreach (var effect in active) { effect.StartedAt += offset; effect.EndsAt += offset; }
            foreach (var target in new List<Transform>(protectedTargets.Keys)) protectedTargets[target] += offset;
            foreach (var saved in restoredProtection) saved.Until += offset;
            announcementUntil += offset;
            foreach (var area in facilities) if (area != null) area.BindPresentationClock(clock);
        }

        /// 短整蛊只影响移动，面板输入与已提交结果不受影响。
        public float LocalMovementMultiplier
        {
            get
            {
                float multiplier = 1;
                foreach (var effect in active)
                    if (effect.Target == localPlayer && PresentationTime < effect.EndsAt)
                    {
                        if (effect.Kind == "Bubble") multiplier = 0;
                        else if (effect.Kind == "Banana" || effect.Kind == "Slow" || effect.Kind == "SlipperyFloor") multiplier = Mathf.Min(multiplier, 0.4f);
                    }
                return multiplier;
            }
        }

        /// HUD使用已有边缘墨迹控件展示；中心确认区不得遮挡。
        public float LocalInkIntensity
        {
            get
            {
                foreach (var effect in active) if (effect.Target == localPlayer && effect.Kind == "Ink" && PresentationTime < effect.EndsAt) return 0.7f;
                return 0;
            }
        }
        public string Announcement => PresentationTime < announcementUntil ? announcement ?? string.Empty : string.Empty;
        public bool IsRadarActive => currentState != null && currentState.Phase == CasinoAdventurePhase.Playing && currentState.ElapsedMilliseconds < radarExpiresMilliseconds;
        public bool IsCarryingGold => IsOpenMission("gold_delivery") && currentState.ActiveMission.Carrying;
        public int ActiveVisualCount => active.Count + (carriedGold != null ? 1 : 0);
        /// 服装覆盖结束前，外观系统须等待，避免换装/墨迹结束后还原过时装备。
        public bool IsCostumeOverridden => active.Exists(effect => (effect.Kind == "Disguise" || effect.Kind == "Ink") && effect.Materials.Count > 0);
        public string RadarHint => IsRadarActive ? BuildRadarHint() : string.Empty;

        /// <summary>保持原P1装配接口，依赖保存的效果Prefab，不运行时创建模型或UI。</summary>
        /// <param name="player">本地CharacterController根。</param>
        /// <param name="buddies">不含Camera/Listener的本地助手。</param>
        /// <param name="safeSpawns">按区域0..3顺序保存的旧安全点；单元素仅适用一区。</param>
        /// <param name="bindings">效果种类到保存Prefab的映射，音效可在Prefab内保存AudioSource。</param>
        public void Configure(Transform player, Transform[] buddies, Transform[] safeSpawns, CasinoEffectPrefabBinding[] bindings)
        { localPlayer = player; companions = buddies; safePoints = safeSpawns; prefabs = bindings; }

        /// <summary>绑定P3真实设施和资源；缺少安全设施时拒绝危险移动，不跨区寻找最近点。</summary>
        /// <param name="areas">四区设施引用，可为仅一区的数组。</param>
        /// <param name="costume">临时换装材质，只作用实例Renderer并在结束时恢复。</param>
        /// <param name="goldPrefab">已保存的持箱模型，不能含相机或阻挡玩家的Collider。</param>
        /// <param name="anchor">可选手部持箱锚点，空值使用本地玩家根的前方位置。</param>
        /// <param name="collisionMask">碰撞与地面校验层掩码，默认全部层；不是默认零LayerMask。</param>
        public void ConfigureFacilities(JinxCasinoAreaFacilities[] areas, Material costume, GameObject goldPrefab, Transform anchor = null, int collisionMask = -1)
        {
            ClearEffects(); facilities = areas ?? Array.Empty<JinxCasinoAreaFacilities>(); disguiseMaterial = costume;
            carriedGoldPrefab = goldPrefab; carryAnchor = anchor; collisionLayers = collisionMask;
            foreach (var area in facilities) if (area != null) area.BindPresentationClock(presentationClock);
        }

        /// <summary>为角色绑定独立视觉根及可换装Renderer，避免扭动玩家相机和碰撞体。</summary>
        /// <param name="bindings">Target对应Configure传入的根，VisualRoot仅包含模型；空数组仍可显示效果Prefab。</param>
        public void ConfigureActors(CasinoActorEffectBinding[] bindings) => actors = bindings ?? Array.Empty<CasinoActorEffectBinding>();

        /// <summary>绑定保存的换装和泼墨材质，结束精确恢复Renderer原来的每个材质槽。</summary>
        /// <param name="costume">临时换装材质，可与ConfigureFacilities传入值相同。</param>
        /// <param name="inkPaint">助手模型的墨迹材质；本地视口边缘墨迹由LocalInkIntensity控制已有UI。</param>
        public void ConfigureMaterials(Material costume, Material inkPaint) { disguiseMaterial = costume; inkMaterial = inkPaint; }

        /// <summary>同步领域状态后再接收本次Effects；恢复只重建仍有效设施，不重播历史整蛊与传送。</summary>
        /// <param name="state">宿主CaptureState的独立副本；空值清理本场景变化。</param>
        /// <param name="restore">加载存档为true，清理旧演出并标记历史效果已消费；正常刷新为false。</param>
        public void SynchronizeState(CasinoAdventureState state, bool restore = false)
        {
            if (state == null) { ClearEffects(); return; }
            bool newRun = runId != state.RunId;
            if (newRun || restore) ClearEffects();
            else if (stageIndex != state.StageIndex) ClearTransientEffects();
            if (state.Phase == CasinoAdventurePhase.Ended) ClearTransientEffects();
            runId = state.RunId; stageIndex = state.StageIndex; currentState = state;
            if (restore || newRun && state.Revision > 0)
            {
                foreach (var effect in state.Effects) if (effect != null && !string.IsNullOrEmpty(effect.Id)) applied.Add(effect.Id);
                foreach (var protection in state.TargetProtection)
                {
                    if (protection == null) continue;
                    long remaining = protection.UntilMilliseconds - state.ElapsedMilliseconds;
                    if (remaining > 0) restoredProtection.Add(new RestoredProtection { TargetId = protection.TargetId, Until = PresentationTime + Mathf.Min(5, remaining / 1000f) });
                }
            }
            ReconcileRestoredProtection();
            radarExpiresMilliseconds = 0;
            bool shortcut = false;
            if (state.Phase == CasinoAdventurePhase.Playing)
                foreach (var effect in state.Effects)
                {
                    if (effect.Value != state.StageIndex) continue;
                    if (effect.EffectKind == "OpenShortcut") shortcut = true;
                    if (effect.EffectKind == "MapRadar") radarExpiresMilliseconds = Math.Max(radarExpiresMilliseconds, effect.CreatedAtMilliseconds + effect.DurationMilliseconds);
                }
            var area = CurrentFacility;
            foreach (var facility in facilities)
            {
                if (facility == null) continue;
                bool current = facility == area;
                facility.SetShortcutOpen(current && shortcut);
                facility.SetPowerFaulted(current && IsOpenMission("power_repair"));
                facility.SetOccupiedGame(current && !string.IsNullOrEmpty(state.ActiveRoundJson) ? state.ActiveGame : (CasinoGameKind?)null);
            }
            SynchronizeCarriedGold();
        }

        /// <summary>绑定当前任务的可见目标，供雷达显示方向；任务清理时传空数组。</summary>
        /// <param name="markers">只包含当前任务实际目标实例，不保留旧任务Transform。</param>
        public void SetMissionMarkers(Transform[] markers) => missionMarkers = markers ?? Array.Empty<Transform>();

        /// <summary>领域消费物品之前检查真实目标，至少一个目标且所有实体均不在五秒保护期。</summary>
        /// <param name="targetId">local为本机、buddy为最近当前区助手、team为本机与当前区全部助手；未知ID拒绝。</param>
        /// <returns>只有全部目标可应用时才为true，查询不播放效果、不消费物品。</returns>
        public bool CanApplyPrank(string targetId)
        {
            ReconcileRestoredProtection();
            bool hasTarget = false;
            foreach (var target in ResolveTargets(targetId))
            {
                hasTarget = true;
                if (protectedTargets.TryGetValue(target, out float until) && until > PresentationTime) return false;
            }
            return hasTarget;
        }

        /// <summary>执行新提交记录，每个ID最多一次，目标实体另有五秒保护以避免team/local别名绕过。</summary>
        /// <param name="effects">本次领域结果中的Effects，空值无操作；必须先SynchronizeState。</param>
        public void ApplyEffects(CasinoSceneEffect[] effects)
        {
            if (effects == null) return;
            foreach (var effect in effects)
            {
                if (effect == null || string.IsNullOrEmpty(effect.Id) || !applied.Add(effect.Id)) continue;
                float duration = Mathf.Clamp(effect.DurationMilliseconds / 1000f, 0.1f, effect.ProtectionMilliseconds > 0 ? 3 : 45);
                if (effect.EffectKind == "MovingTables") CurrentFacility?.MoveIdleTables(Mathf.Min(3, duration), ActiveGame, collisionLayers);
                else if (effect.EffectKind == "PowerOutage") CurrentFacility?.SetPowerFaulted(IsOpenMission("power_repair"));
                else if (effect.EffectKind == "PowerRestored") CurrentFacility?.SetPowerFaulted(false);
                else if (effect.EffectKind == "MapRadar" || effect.EffectKind == "OpenShortcut" || effect.EffectKind == "CarryGold")
                    SynchronizeCarriedGold();
                else if (IsActorEffect(effect.EffectKind))
                    foreach (var target in ResolveTargets(effect.TargetId)) ApplyToTarget(effect, target, duration);
                announcement = effect.EffectKind == "FakeJackpot" ? "整蛊广播：假大奖！不会增加筹码。" : effect.Description;
                announcementUntil = PresentationTime + Mathf.Min(3, duration);
            }
        }

        /// 清理自己创建的实例，精确恢复灯光、门、材质与模型姿态；不改领域局。
        public void ClearEffects()
        {
            ClearTransientEffects();
            foreach (var facility in facilities) if (facility != null) facility.ClearRuntimeState();
            if (carriedGold != null) { carriedGold.SetActive(false); Destroy(carriedGold); carriedGold = null; }
            applied.Clear(); protectedTargets.Clear(); restoredProtection.Clear(); missionMarkers = Array.Empty<Transform>();
            currentState = null; runId = null; stageIndex = -1; radarExpiresMilliseconds = 0;
        }

        private JinxCasinoAreaFacilities CurrentFacility => Array.Find(facilities, area => area != null && area.AreaIndex == stageIndex % 4 && area.gameObject.activeInHierarchy);
        private CasinoGameKind? ActiveGame => currentState != null && !string.IsNullOrEmpty(currentState.ActiveRoundJson) ? currentState.ActiveGame : (CasinoGameKind?)null;
        private bool IsOpenMission(string id) => currentState?.ActiveMission != null && currentState.Phase == CasinoAdventurePhase.Playing && currentState.ActiveMission.EventId == id &&
            !currentState.ActiveMission.Completed && !currentState.ActiveMission.Failed && currentState.ElapsedMilliseconds < currentState.ActiveMission.DeadlineMilliseconds;

        private void SynchronizeCarriedGold()
        {
            if (!IsCarryingGold)
            {
                if (carriedGold != null) { carriedGold.SetActive(false); Destroy(carriedGold); carriedGold = null; }
                return;
            }
            if (carriedGold != null || carriedGoldPrefab == null || localPlayer == null) return;
            carriedGold = Instantiate(carriedGoldPrefab, carryAnchor != null ? carryAnchor : localPlayer);
            carriedGold.transform.localPosition = carryAnchor != null ? Vector3.zero : new Vector3(0, 0.9f, 0.6f);
            carriedGold.transform.localRotation = Quaternion.identity;
            DisableEffectColliders(carriedGold); carriedGold.SetActive(true);
        }

        private IEnumerable<Transform> ResolveTargets(string target)
        {
            if (currentState == null) yield break;
            if ((target == "local" || target == "team") && localPlayer != null && localPlayer.gameObject.activeInHierarchy) yield return localPlayer;
            if (target != "buddy" && target != "team") yield break;
            if (companions == null) yield break;
            Transform nearest = null;
            float nearestDistance = float.MaxValue;
            var yielded = new HashSet<Transform>();
            if (localPlayer != null) yielded.Add(localPlayer);
            foreach (var companion in companions)
            {
                if (companion == null || !companion.gameObject.activeInHierarchy || !IsCurrentAreaCompanion(companion) || !yielded.Add(companion)) continue;
                if (target == "team") { yield return companion; continue; }
                var origin = localPlayer != null ? localPlayer.position : GetCurrentSafePoint()?.position ?? transform.position;
                float distance = (companion.position - origin).sqrMagnitude;
                if (distance < nearestDistance) { nearestDistance = distance; nearest = companion; }
            }
            if (target == "buddy" && nearest != null) yield return nearest;
        }

        private bool IsCurrentAreaCompanion(Transform companion)
        {
            int currentArea = stageIndex % 4;
            var area = companion.GetComponentInParent<JinxCasinoWorldArea>();
            if (area != null) return area.Index == currentArea;
            // 旧场景或测试没有WorldArea父级时，按保存的区域安全点归属；不能按距玩家远近跨区选NPC。
            int nearestArea = -1;
            float nearestDistance = float.MaxValue;
            foreach (var facility in facilities)
            {
                if (facility == null || facility.SafeSpawn == null) continue;
                float distance = (facility.SafeSpawn.position - companion.position).sqrMagnitude;
                if (distance < nearestDistance) { nearestDistance = distance; nearestArea = facility.AreaIndex; }
            }
            if (nearestArea < 0 && safePoints != null)
                for (int index = 0; index < safePoints.Length; index++)
                {
                    if (safePoints[index] == null) continue;
                    float distance = (safePoints[index].position - companion.position).sqrMagnitude;
                    if (distance < nearestDistance) { nearestDistance = distance; nearestArea = index; }
                }
            return nearestArea == currentArea;
        }

        private void ReconcileRestoredProtection()
        {
            for (int index = restoredProtection.Count - 1; index >= 0; index--)
            {
                var saved = restoredProtection[index];
                if (saved.Until <= PresentationTime) { restoredProtection.RemoveAt(index); continue; }
                // Host会在同步后再开放区域及传送玩家，因此保留逻辑记录，下一查询再映射新激活实体。
                foreach (var target in ResolveTargets(saved.TargetId))
                {
                    protectedTargets.TryGetValue(target, out float previous);
                    protectedTargets[target] = Mathf.Max(previous, saved.Until);
                }
            }
        }

        private void ApplyToTarget(CasinoSceneEffect effect, Transform target, float duration)
        {
            if (effect.ProtectionMilliseconds > 0)
            {
                if (protectedTargets.TryGetValue(target, out float until) && until > PresentationTime) return;
                protectedTargets[target] = PresentationTime + 5;
            }
            var binding = Array.Find(actors, actor => actor != null && actor.Target == target);
            var visual = new ActiveEffect { Target = target, VisualRoot = binding?.VisualRoot,
                StartedAt = PresentationTime, EndsAt = PresentationTime + duration, Kind = effect.EffectKind, Duration = duration };
            if (visual.VisualRoot != null)
            {
                visual.OriginalLocalPosition = visual.VisualRoot.localPosition;
                visual.OriginalLocalRotation = visual.VisualRoot.localRotation;
            }
            var prefab = prefabs == null ? null : Array.Find(prefabs, candidate => candidate != null && candidate.Kind == effect.EffectKind)?.Prefab;
            if (prefab != null)
            {
                visual.Instance = Instantiate(prefab, target.position + Vector3.up, Quaternion.identity, transform);
                DisableEffectColliders(visual.Instance); visual.Instance.SetActive(true);
                visual.Audio = visual.Instance.GetComponentsInChildren<AudioSource>(true);
                visual.Particles = visual.Instance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var audio in visual.Audio)
                {
                    audio.volume = Mathf.Min(audio.volume, 0.5f);
                    var director = GetComponent<JinxCasinoAudioDirector>();
                    if (director != null) director.RegisterEffectSource(audio, audio.volume);
                    audio.Play();
                }
            }
            Material temporaryMaterial = effect.EffectKind == "Disguise" ? disguiseMaterial : effect.EffectKind == "Ink" ? inkMaterial : null;
            if (temporaryMaterial != null)
            {
                var renderers = binding?.CostumeRenderers ?? target.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    if (renderer == null || renderer.GetComponent<TMPro.TMP_Text>() != null) continue;
                    var original = renderer.sharedMaterials;
                    visual.Materials.Add(new MaterialState { Renderer = renderer, Original = original });
                    var replacement = new Material[original.Length];
                    for (int index = 0; index < replacement.Length; index++) replacement[index] = temporaryMaterial;
                    renderer.sharedMaterials = replacement;
                }
            }
            if (effect.EffectKind == "Teleport" || effect.EffectKind == "PortalFault") TeleportToCurrentArea(target);
            if (effect.EffectKind == "SpringPunch")
            {
                Vector3 forward = target == localPlayer ? target.forward : localPlayer != null ? target.position - localPlayer.position : target.forward;
                forward.y = 0; visual.MotionDirection = forward.sqrMagnitude > 0.01f ? forward.normalized : target.forward;
            }
            if (effect.EffectKind == "Magnet")
            {
                Transform safe = GetCurrentSafePoint();
                if (safe != null) { visual.MotionDirection = safe.position - target.position; visual.MotionDirection.y = 0; visual.MotionDirection = Vector3.ClampMagnitude(visual.MotionDirection, 1); }
            }
            active.Add(visual);
            SynchronizeOwnedPlayback(visual, presentationClock?.IsPaused ?? false);
        }

        private Transform GetCurrentSafePoint()
        {
            if (currentState == null || stageIndex < 0) return null;
            if (CurrentFacility?.SafeSpawn != null) return CurrentFacility.SafeSpawn;
            int area = stageIndex % 4;
            return safePoints != null && area < safePoints.Length ? safePoints[area] : null;
        }
        private void TeleportToCurrentArea(Transform target)
        {
            var safe = GetCurrentSafePoint();
            if (safe == null) return;
            // 同一集合点多人到达时尝试固定短偏移，每一落点都通过地面和胶囊占位校验。
            var offsets = new[] { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            foreach (var offset in offsets) if (JinxCasinoWorldMotion.TeleportActor(target, safe.position + offset, collisionLayers)) return;
        }

        private string BuildRadarHint()
        {
            var text = new StringBuilder("地图雷达");
            var area = CurrentFacility;
            if (area != null)
                foreach (var marker in area.RadarStations)
                {
                    if (marker == null || !marker.gameObject.activeInHierarchy) continue;
                    var station = marker.GetComponentInParent<JinxCasinoStation>();
                    var game = station == null ? null : Array.Find(CasinoContentCatalog.Games, candidate => candidate.Kind == station.Game);
                    AppendDirection(text, marker.position, game?.Name ?? "机台");
                }
            foreach (var marker in missionMarkers) if (marker != null && marker.gameObject.activeInHierarchy) AppendDirection(text, marker.position, "任务目标");
            return text.ToString();
        }
        private void AppendDirection(StringBuilder text, Vector3 point, string label)
        {
            if (localPlayer == null) return;
            Vector3 relative = localPlayer.InverseTransformDirection(point - localPlayer.position);
            string direction = Mathf.Abs(relative.x) > Mathf.Abs(relative.z) ? relative.x > 0 ? "右" : "左" : relative.z > 0 ? "前" : "后";
            text.Append("\n").Append(label).Append(" · ").Append(direction).Append("方 ").Append(Vector3.Distance(localPlayer.position, point).ToString("0.0")).Append("米");
        }

        private void Update()
        {
            for (int index = active.Count - 1; index >= 0; index--)
            {
                var effect = active[index];
                if (effect.Target == null) { Finish(effect); active.RemoveAt(index); continue; }
                bool paused = presentationClock?.IsPaused ?? false;
                SynchronizeOwnedPlayback(effect, paused);
                if (paused) continue;
                if (PresentationTime >= effect.EndsAt) { Finish(effect); active.RemoveAt(index); continue; }
                if (effect.Instance != null) effect.Instance.transform.position = effect.Target.position + Vector3.up;
                float elapsed = PresentationTime - effect.StartedAt;
                if (effect.Kind == "SpringPunch") JinxCasinoWorldMotion.MoveActor(effect.Target, effect.MotionDirection * (0.65f * PresentationDelta / effect.Duration), collisionLayers);
                else if (effect.Kind == "Magnet") JinxCasinoWorldMotion.MoveActor(effect.Target, effect.MotionDirection * (PresentationDelta / effect.Duration), collisionLayers);
                if (effect.VisualRoot == null) continue;
                if (effect.Kind == "Bubble") effect.VisualRoot.localPosition = effect.OriginalLocalPosition + Vector3.up * (0.12f + Mathf.Sin(elapsed * 9) * 0.05f);
                else if (effect.Kind == "Banana" || effect.Kind == "SlipperyFloor") effect.VisualRoot.localRotation = effect.OriginalLocalRotation * Quaternion.Euler(Mathf.Sin(elapsed * 10) * 12, 0, Mathf.Sin(elapsed * 7) * 8);
            }
        }

        private static void SynchronizeOwnedPlayback(ActiveEffect effect, bool paused)
        {
            if (effect.PlaybackPaused == paused) return;
            effect.PlaybackPaused = paused;
            if (paused)
            {
                foreach (var audio in effect.Audio)
                    if (audio != null && audio.isPlaying) { effect.PausedAudio.Add(audio); audio.Pause(); }
                // 单独暂停每个系统，避免父系统重复操纵已暂停的子系统。
                foreach (var particles in effect.Particles)
                    if (particles != null && particles.isPlaying) { effect.PausedParticles.Add(particles); particles.Pause(false); }
            }
            else
            {
                foreach (var audio in effect.PausedAudio) if (audio != null && audio.gameObject.activeInHierarchy) audio.UnPause();
                foreach (var particles in effect.PausedParticles) if (particles != null && particles.gameObject.activeInHierarchy) particles.Play(false);
                effect.PausedAudio.Clear(); effect.PausedParticles.Clear();
            }
        }

        private void ClearTransientEffects()
        {
            for (int index = active.Count - 1; index >= 0; index--) Finish(active[index]);
            active.Clear(); announcementUntil = 0;
        }
        private void Finish(ActiveEffect effect)
        {
            if (effect.Instance != null) { effect.Instance.SetActive(false); Destroy(effect.Instance); }
            if (effect.VisualRoot != null)
            {
                // 只恢复本效果实际拥有的姿态变化，提示/传送不能恢复别的效果中途拍下的姿态。
                if (effect.Kind == "Bubble") effect.VisualRoot.localPosition = effect.OriginalLocalPosition;
                else if (effect.Kind == "Banana" || effect.Kind == "SlipperyFloor") effect.VisualRoot.localRotation = effect.OriginalLocalRotation;
            }
            foreach (var saved in effect.Materials) if (saved.Renderer != null) saved.Renderer.sharedMaterials = saved.Original;
        }
        private static bool IsActorEffect(string kind) => kind == "Bubble" || kind == "BubbleStorm" || kind == "Banana" || kind == "SpringPunch" || kind == "FakeJackpot" ||
            kind == "Ink" || kind == "Magnet" || kind == "Horn" || kind == "Disguise" || kind == "Teleport" || kind == "PortalFault" || kind == "SlipperyFloor" || kind == "CooperationHint";
        private static void DisableEffectColliders(GameObject instance)
        { foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false; }
        private void OnDisable() => ClearEffects();

        private sealed class ActiveEffect
        {
            public Transform Target;
            public Transform VisualRoot;
            public Vector3 OriginalLocalPosition;
            public Quaternion OriginalLocalRotation;
            public Vector3 MotionDirection;
            public GameObject Instance;
            public float StartedAt;
            public float EndsAt;
            public float Duration;
            public string Kind;
            public bool PlaybackPaused;
            public AudioSource[] Audio = Array.Empty<AudioSource>();
            public ParticleSystem[] Particles = Array.Empty<ParticleSystem>();
            public readonly List<AudioSource> PausedAudio = new List<AudioSource>();
            public readonly List<ParticleSystem> PausedParticles = new List<ParticleSystem>();
            public readonly List<MaterialState> Materials = new List<MaterialState>();
        }
        private sealed class MaterialState { public Renderer Renderer; public Material[] Original; }
        private sealed class RestoredProtection { public string TargetId; public float Until; }
    }

    [Serializable]
    public sealed class CasinoEffectPrefabBinding { public string Kind; public GameObject Prefab; }
    [Serializable]
    public sealed class CasinoActorEffectBinding
    {
        public Transform Target;
        public Transform VisualRoot;
        public Renderer[] CostumeRenderers;
    }
}
