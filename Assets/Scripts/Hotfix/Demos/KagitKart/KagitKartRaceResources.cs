using System;
using System.Collections.Generic;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// Demo 内共享比赛实例，保留当前准备章、大奖赛下一章和正在使用章。
    public sealed class KagitKartRaceResources : IDisposable
    {
        private const string Root = "LoadResources/Demos/kagit_kart/";
        private static readonly ProfilerMarker ParseTrack = new("KagitKart.Resources.ParseTrack");
        private static readonly ProfilerMarker ParseAiPlan = new("KagitKart.Resources.ParseAiPlan");
        private IResourceLoader shared = ResourceServices.CreateLoader();
        private readonly Dictionary<string, GameObject> characters = new();
        private readonly Dictionary<string, AsyncLazy<Round>> rounds = new();
        private readonly List<Round> loaded = new();
        private readonly SemaphoreSlim ghostPreparation = new(1, 1);
        private readonly Transform parent;
        private readonly Light sun;
        private readonly KagitKartSourceCatalog source;
        private readonly CancellationToken token;
        private AsyncLazy commonTask;
        private bool commonStarted;
        private bool effectsPrepared;
        private bool disposed;
        private string desiredTrack;
        private string nextTrack;
        private Round activeRound;
        private GameObject ghost;
        private string ghostId;

        /// 一章解析数据及已初始化的纸景实例，由本缓存持有加载器。
        public sealed class Round
        {
            internal IResourceLoader Loader;
            public string Id;
            public KagitKartTrackData Data;
            public KagitKartAiPlan Plan;
            public KagitKartSceneryWorld Scenery;
        }

        public KagitKartItemVisuals Items { get; private set; }
        public KagitKartRaceFx Effects { get; private set; }

        /// <summary>绑定场景生命周期；所有共享实例退出 Demo 时释放。</summary>
        /// <param name="parent">本 Demo 的场景根。</param>
        /// <param name="source">已校验的配置。</param>
        /// <param name="sun">赛道方向光。</param>
        /// <param name="token">场景退出令牌。</param>
        public KagitKartRaceResources(Transform parent, KagitKartSourceCatalog source, Light sun, CancellationToken token)
        {
            this.parent = parent;
            this.sun = sun;
            this.source = source;
            this.token = token;
        }

        /// <summary>准备当前选择；同章复用任务与实例，取消选择后的旧章不激活。</summary>
        /// <param name="id">有效赛道标识。</param>
        /// <param name="next">大奖赛下一章；单场与计时赛不准备后续赛道。</param>
        /// <returns>已完成数据解析与实例初始化的一章。</returns>
        public async UniTask<Round> PrepareAsync(string id, string next = null)
        {
            token.ThrowIfCancellationRequested();
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(KagitKartRaceResources));
            }
            desiredTrack = id;
            nextTrack = next;
            if (!commonStarted)
            {
                commonStarted = true;
                commonTask = new AsyncLazy(PrepareCommonAsync);
            }
            await commonTask;
            var round = await GetRoundAsync(id);
            if (next != null) await GetRoundAsync(next);
            token.ThrowIfCancellationRequested();
            if (!effectsPrepared)
            {
                using var session = new KagitKartSession(source, round.Data, round.Plan, KagitKartRaceMode.SingleRace, source.roster[0].id, 120, 1);
                var actors = new Transform[session.Karts.Count];
                for (int i = 0; i < actors.Length; i++) actors[i] = characters[session.Karts[i].Id].transform;
                var environment = round.Scenery.Asset.batches[0].materials[0];
                Items.Bind(session, actors, environment);
                Effects.Bind(session, actors, environment, sun, 1, source.config.kart);
                Items.Unbind();
                Effects.Unbind();
                effectsPrepared = true;
            }
            Prune();
            return round;
        }

        private UniTask<Round> GetRoundAsync(string id)
        {
            if (!rounds.TryGetValue(id, out var pending))
            {
                pending = new AsyncLazy<Round>(() => LoadRoundAsync(id));
                rounds.Add(id, pending);
            }
            return pending.Task;
        }

        private async UniTask PrepareCommonAsync()
        {
            try
            {
                foreach (var character in source.roster)
                {
                    var actor = await shared.InstantiateAsync(Root + "Prefabs/Characters/" + KagitKartCatalog.CharacterAssetName(character.id), parent);
                    token.ThrowIfCancellationRequested();
                    actor.GetComponent<KagitKartCharacterVisual>().Bind(null, source.config.kart);
                    actor.GetComponent<SkinnedMeshRenderer>().updateWhenOffscreen = true;
                    actor.SetActive(false);
                    characters.Add(character.id, actor);
                    await UniTask.Yield(token);
                }
                var items = await shared.InstantiateAsync(Root + "Prefabs/ItemVisuals", parent);
                token.ThrowIfCancellationRequested();
                Items = items.GetComponent<KagitKartItemVisuals>();
                items.SetActive(false);
                var effects = await shared.InstantiateAsync(Root + "Prefabs/RaceFx", parent);
                token.ThrowIfCancellationRequested();
                Effects = effects.GetComponent<KagitKartRaceFx>();
                effects.SetActive(false);
            }
            catch
            {
                shared.Dispose();
                characters.Clear();
                Items = null;
                Effects = null;
                commonStarted = false;
                if (!disposed && !token.IsCancellationRequested) shared = ResourceServices.CreateLoader();
                throw;
            }
        }

        private async UniTask<Round> LoadRoundAsync(string id)
        {
            var owned = ResourceServices.CreateLoader();
            var round = new Round { Id = id, Loader = owned };
            loaded.Add(round);
            try
            {
                string name = KagitKartCatalog.TrackAssetName(id);
                var data = await owned.LoadAssetAsync<TextAsset>(Root + "Data/Tracks/" + name);
                token.ThrowIfCancellationRequested();
                // Unity资产在主线程取文本，工作线程仅解析独立字符串与托管数据。
                string trackJson = data.text;
                var parsedTrack = await UniTask.RunOnThreadPool(() =>
                {
                    using (ParseTrack.Auto())
                    {
                        return KagitKartTrackData.Parse(trackJson);
                    }
                }, configureAwait: true, cancellationToken: token);
                token.ThrowIfCancellationRequested();
                round.Data = parsedTrack;
                var plan = await owned.LoadAssetAsync<TextAsset>(Root + "Data/AiPlans/" + name);
                token.ThrowIfCancellationRequested();
                string planJson = plan.text;
                var parsedPlan = await UniTask.RunOnThreadPool(() =>
                {
                    using (ParseAiPlan.Auto())
                    {
                        return KagitKartAiPlan.Parse(planJson, parsedTrack);
                    }
                }, configureAwait: true, cancellationToken: token);
                token.ThrowIfCancellationRequested();
                round.Plan = parsedPlan;
                var instance = await owned.InstantiateAsync(Root + "Prefabs/Tracks/" + name, parent);
                token.ThrowIfCancellationRequested();
                round.Scenery = instance.GetComponent<KagitKartSceneryWorld>();
                instance.SetActive(false);
                return round;
            }
            catch
            {
                // RunOnThreadPool的前置取消可能在worker抛出，资源释放始终回主线程。
                await UniTask.SwitchToMainThread();
                owned.Dispose();
                loaded.Remove(round);
                rounds.Remove(id);
                throw;
            }
        }

        /// <summary>激活已准备比赛，不重新实例化赛车或赛道。</summary>
        /// <param name="round">已准备的章节。</param>
        /// <param name="session">本场新规则。</param>
        /// <param name="sun">当前方向光。</param>
        /// <param name="seed">表现随机种子。</param>
        /// <returns>与规则顺序一致的赛车 Transform。</returns>
        public Transform[] Activate(Round round, KagitKartSession session, Light sun, uint seed)
        {
            Hide();
            activeRound = round;
            round.Scenery.ResetPresentation();
            round.Scenery.gameObject.SetActive(true);
            var actors = new Transform[session.Karts.Count];
            var environment = round.Scenery.Asset.batches[0].materials[0];
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = characters[session.Karts[i].Id];
                actors[i] = actor.transform;
                var visual = actor.GetComponent<KagitKartCharacterVisual>();
                visual.Bind(session.Karts[i], source.config.kart, i * .37f);
                visual.SetEnvironment(environment);
                actor.SetActive(true);
            }
            Items.Bind(session, actors, environment);
            Effects.Bind(session, actors, environment, sun, seed, source.config.kart);
            Items.gameObject.SetActive(true);
            Effects.gameObject.SetActive(true);
            Prune();
            return actors;
        }

        /// <summary>单独准备幽灵实例，避免修改正常赛车材质。</summary>
        /// <param name="id">本机所选车手。</param>
        /// <returns>同一时间仅保留一份的幽灵实例。</returns>
        public async UniTask<Transform> PrepareGhostAsync(string id)
        {
            await ghostPreparation.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                if (disposed) throw new ObjectDisposedException(nameof(KagitKartRaceResources));
                if (ghost != null && ghostId != id)
                {
                    shared.ReleaseInstance(ghost);
                    ghost = null;
                }
                if (ghost == null)
                {
                    ghost = await shared.InstantiateAsync(Root + "Prefabs/Characters/" + KagitKartCatalog.CharacterAssetName(id), parent);
                    token.ThrowIfCancellationRequested();
                    ghostId = id;
                    ghost.GetComponent<KagitKartCharacterVisual>().Bind(null, source.config.kart, isGhost: true);
                    ghost.SetActive(false);
                }
                return ghost.transform;
            }
            finally
            {
                ghostPreparation.Release();
            }
        }

        /// 退订本场并隐藏实例，保留下一次开赛所需资源。
        public void Hide()
        {
            Items?.Unbind();
            Effects?.Unbind();
            if (Items != null) Items.gameObject.SetActive(false);
            if (Effects != null) Effects.gameObject.SetActive(false);
            foreach (var actor in characters.Values)
            {
                actor.GetComponent<KagitKartCharacterVisual>().Unbind();
                actor.SetActive(false);
            }
            if (activeRound?.Scenery != null) activeRound.Scenery.gameObject.SetActive(false);
            if (ghost != null) ghost.SetActive(false);
            activeRound = null;
        }

        private void Prune()
        {
            for (int i = loaded.Count - 1; i >= 0; i--)
            {
                var round = loaded[i];
                if (round.Scenery != null && round != activeRound && round.Id != desiredTrack && round.Id != nextTrack)
                {
                    rounds.Remove(round.Id);
                    round.Loader.Dispose();
                    loaded.RemoveAt(i);
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Hide();
            foreach (var round in loaded) round.Loader.Dispose();
            loaded.Clear();
            rounds.Clear();
            shared.Dispose();
        }
    }
}
