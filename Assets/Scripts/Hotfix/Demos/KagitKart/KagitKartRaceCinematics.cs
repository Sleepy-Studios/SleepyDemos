using System;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 原作赛前路线与镜头曲线；宿主控制时间，不推进比赛规则。
    public sealed class KagitKartRaceCinematics
    {
        [Serializable] internal sealed class Data { public string sourceCommit; public Track[] tracks; }
        [Serializable] internal sealed class Track { public string id; public Entry[] entries; }
        [Serializable] internal sealed class Entry { public float[] position; public bool reducedMotion; public float duration; public Segment[] segments; }
        [Serializable] internal sealed class Segment { public float start, end; public Key[] keys; }
        [Serializable] internal sealed class Key { public float time, fov; public float[] position, look; }

        private readonly Camera camera;
        private readonly KagitKartSession session;
        private readonly KagitKartCameraTuning tuning;
        private Entry intro;
        private Path[] paths;
        private readonly Entry[] entries;
        private readonly Vector3 introStart;
        private bool reducedMotion;
        private float time;
        private KagitKartQueryInfo ground;
        private int groundHint = -1;
        private bool finishing;
        private float finishTime, finishYaw, startAz, startRadius, startHeight, startFov, finishSide, resultsBlend;
        private Vector3 startLook;

        public bool IntroPlaying { get; private set; } = true;
        public bool Finishing => finishing;

        /// <summary>从原作生成路径选取当前玩家网格；计时赛使用相同的首网格。</summary>
        /// <param name="json">固定版本赛前路径数据。</param>
        /// <param name="camera">本场摄像机。</param>
        /// <param name="session">本场比赛。</param>
        /// <param name="tuning">原作摄像机参数。</param>
        /// <param name="reducedMotion">使用原作三段静态切镜。</param>
        public KagitKartRaceCinematics(string json, Camera camera, KagitKartSession session, KagitKartCameraTuning tuning, bool reducedMotion = false)
        {
            this.camera = camera;
            this.session = session;
            this.tuning = tuning;
            var data = JsonUtility.FromJson<Data>(json);
            if (data?.sourceCommit != KagitKartCatalog.SourceCommit || data.tracks == null)
            {
                throw new ArgumentException("赛前镜头数据缺失或版本不匹配。");
            }
            var track = Array.Find(data.tracks, t => t.id == session.Track.Track.definition.id);
            entries = track.entries;
            introStart = session.Player.Position;
            SetReducedMotion(reducedMotion);
        }

        /// <summary>切换原作低动态镜头；已开始赛前按原进度换路线，不从头重播。</summary>
        /// <param name="value">减少动态使用原作静态切镜和温和冲线构图。</param>
        public void SetReducedMotion(bool value)
        {
            if (intro != null && reducedMotion == value)
            {
                return;
            }
            reducedMotion = value;
            float progress = intro == null ? 0 : time / Mathf.Max(0.001f, intro.duration);
            intro = null;
            float best = float.PositiveInfinity;
            foreach (var entry in entries)
            {
                float distance = (Point(entry.position) - introStart).sqrMagnitude;
                if (entry.reducedMotion == reducedMotion && distance < best)
                {
                    best = distance;
                    intro = entry;
                }
            }
            if (intro == null || best > 0.01f)
            {
                throw new ArgumentException("赛前镜头起跑网格不匹配。");
            }
            paths = new Path[intro.segments.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = new Path(intro.segments[i].keys);
            }
            time = progress * intro.duration;
        }

        /// 跳过演出，下一帧由原作跟随镜头接管并开始倒计时。
        public void SkipIntro() => IntroPlaying = false;

        /// <summary>从当前跟随镜头开始原作冲线环绕，随后为结算表留出主体位置。</summary>
        /// <param name="playerPosition">插值后的 Unity 赛车位置。</param>
        public void BeginFinish(Vector3 playerPosition)
        {
            finishing = true;
            finishTime = resultsBlend = 0;
            finishYaw = session.Player.Heading;
            Vector3 relative = KagitKartVehicle.ToUnity(camera.transform.position - playerPosition);
            startAz = Mathf.Atan2(relative.x, relative.z) - finishYaw;
            startAz = Mathf.Atan2(Mathf.Sin(startAz), Mathf.Cos(startAz));
            startRadius = Mathf.Max(3, new Vector2(relative.x, relative.z).magnitude);
            startHeight = relative.y;
            startFov = camera.fieldOfView;
            finishSide = session.Player.TrackInfo.Lateral > 0 ? 1 : -1;
            if (finishSide > 0 && startAz < 0) startAz += Mathf.PI * 2;
            if (finishSide < 0 && startAz > 0) startAz -= Mathf.PI * 2;
            startLook = KagitKartVehicle.ToUnity(camera.transform.position + camera.transform.forward * 6);
        }

        /// <summary>沿用 shots.js 冲线镜头；暂停传零冻结环绕及结算构图。</summary>
        /// <param name="dt">本场表现时间。</param>
        /// <param name="playerPosition">插值后的 Unity 赛车位置。</param>
        public void AdvanceFinish(float dt, Vector3 playerPosition)
        {
            dt = Mathf.Clamp(dt, 0, 0.1f);
            finishTime += dt;
            finishYaw += Mathf.Atan2(Mathf.Sin(session.Player.Heading - finishYaw), Mathf.Cos(session.Player.Heading - finishYaw)) * Mathf.Min(1, dt / 0.35f);
            resultsBlend += ((session.Race.State == KagitKartRaceState.Done ? 1 : 0) - resultsBlend) * Mathf.Min(1, dt * 1.2f);
            float k = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Min(1, finishTime / 4.2f));
            float hold = Mathf.Max(0, finishTime - 4.2f);
            float az = Mathf.LerpUnclamped(startAz, finishSide * 0.62f, k) + Mathf.Sin(hold * 0.12f) * 0.1f * Mathf.Min(1, hold / 3);
            float radius = Mathf.LerpUnclamped(startRadius, 8, k) + 0.8f * resultsBlend;
            float height = Mathf.LerpUnclamped(startHeight, 1.9f, k) + 0.6f * resultsBlend;
            float fov = Mathf.LerpUnclamped(startFov, KagitKartBookGeometry.FitFov(camera.aspect, 38, 58), Mathf.SmoothStep(0, 1, finishTime / 1.2f));
            if (reducedMotion)
            {
                az = finishSide * 0.62f + Mathf.Sin(finishTime * 0.08f) * 0.05f;
                radius = 8.8f;
                height = 2.4f;
                fov = KagitKartBookGeometry.FitFov(camera.aspect, 38, 58);
            }
            Vector3 p = KagitKartVehicle.ToUnity(playerPosition);
            Vector3 position = p + new Vector3(Mathf.Sin(finishYaw + az) * radius, height, Mathf.Cos(finishYaw + az) * radius);
            session.Track.Query(position, groundHint, ref ground);
            groundHint = ground.Index;
            if (ground.HasGround) position.y = Mathf.Max(position.y, ground.GroundY + 0.9f);
            Vector3 look = KagitKartBookGeometry.FrameAt(position, p + Vector3.up * 0.85f,
                new Vector2(0.72f * resultsBlend, -0.33f * (1 - resultsBlend) - 0.05f * resultsBlend), fov, camera.aspect);
            if (!reducedMotion && finishTime < 1) look = Vector3.LerpUnclamped(startLook, look, Mathf.SmoothStep(0, 1, finishTime));
            camera.transform.position = KagitKartVehicle.ToUnity(position);
            camera.transform.LookAt(KagitKartVehicle.ToUnity(look));
            camera.fieldOfView = fov;
        }

        /// <summary>推进赛前演出，暂停时传零；规则时钟保持冻结。</summary>
        /// <param name="dt">本场表现时间。</param>
        public void AdvanceIntro(float dt)
        {
            if (!IntroPlaying)
            {
                return;
            }
            time += Mathf.Clamp(dt, 0, 0.1f);
            int index = 0;
            for (int i = 1; i < intro.segments.Length; i++)
            {
                if (time >= intro.segments[i].start) index = i;
            }
            float reference = paths[index].Sample(time - intro.segments[index].start, out Vector3 position, out Vector3 look);
            float cine = KagitKartBookGeometry.FitFov(camera.aspect, 42, 64);
            float end = Mathf.Clamp(2 * Mathf.Atan(Mathf.Tan(tuning.hfovRest * Mathf.Deg2Rad / 2) / Mathf.Max(0.2f, camera.aspect)) * Mathf.Rad2Deg, tuning.vfovMin, tuning.vfovMax);
            float referenceEnd = Mathf.Clamp(2 * Mathf.Atan(Mathf.Tan(tuning.hfovRest * Mathf.Deg2Rad / 2) / (16f / 9)) * Mathf.Rad2Deg, tuning.vfovMin, tuning.vfovMax);
            float weight = Mathf.Abs(referenceEnd - 42) < 0.0001f ? 0 : (reference - 42) / (referenceEnd - 42);
            session.Track.Query(position, groundHint, ref ground);
            groundHint = ground.Index;
            if (ground.HasGround) position.y = Mathf.Max(position.y, ground.GroundY + 1.2f);
            camera.transform.position = KagitKartVehicle.ToUnity(position);
            camera.transform.LookAt(KagitKartVehicle.ToUnity(look));
            camera.fieldOfView = Mathf.LerpUnclamped(cine, end, weight);
            if (time >= intro.duration) IntroPlaying = false;
        }

        internal static Vector3 Point(float[] p) => new(p[0], p[1], p[2]);

        /// campath.js 的单调时间曲线与 Three.js 向心 Catmull–Rom，按同一索引空间计算。
        internal sealed class Path
        {
            private readonly Key[] keys;
            private readonly AnimationCurve timing;
            private readonly Vector3[] positions, looks;

            public Path(Key[] keys)
            {
                this.keys = keys;
                if (keys == null || keys.Length < 2) throw new ArgumentException("镜头关键帧缺失。");
                int n = keys.Length;
                var slopes = new float[n - 1];
                var tangents = new float[n];
                positions = new Vector3[n]; looks = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    positions[i] = Point(keys[i].position); looks[i] = Point(keys[i].look);
                    if (i < n - 1)
                    {
                        float span = keys[i + 1].time - keys[i].time;
                        if (!(span > 0)) throw new ArgumentException("镜头时间必须递增。");
                        slopes[i] = 1 / Mathf.Max(0.000001f, span);
                    }
                }
                for (int i = 1; i < n - 1; i++) tangents[i] = (slopes[i - 1] + slopes[i]) / 2;
                for (int i = 0; i < n - 1; i++)
                {
                    float a = tangents[i] / slopes[i], b = tangents[i + 1] / slopes[i], sum = a * a + b * b;
                    if (sum > 9)
                    {
                        float k = 3 / Mathf.Sqrt(sum);
                        tangents[i] = k * a * slopes[i]; tangents[i + 1] = k * b * slopes[i];
                    }
                }
                var frames = new Keyframe[n];
                for (int i = 0; i < n; i++) frames[i] = new Keyframe(keys[i].time, i, tangents[i], tangents[i]);
                timing = new AnimationCurve(frames);
            }

            internal float Sample(float time, out Vector3 position, out Vector3 look)
            {
                float f = Mathf.Clamp(timing.Evaluate(Mathf.Clamp(time, keys[0].time, keys[^1].time)), 0, keys.Length - 1);
                position = Spline(positions, f); look = Spline(looks, f);
                int i = Mathf.Min(keys.Length - 2, Mathf.FloorToInt(f));
                return Mathf.LerpUnclamped(keys[i].fov, keys[i + 1].fov, Mathf.SmoothStep(0, 1, f - i));
            }

            private static Vector3 Spline(Vector3[] points, float f)
            {
                int i = Mathf.Min(points.Length - 2, Mathf.FloorToInt(f));
                float t = f - i;
                Vector3 b = points[i], c = points[i + 1];
                Vector3 a = i > 0 ? points[i - 1] : b * 2 - c;
                Vector3 d = i + 2 < points.Length ? points[i + 2] : c * 2 - b;
                float dt0 = Mathf.Pow((a - b).sqrMagnitude, 0.25f), dt1 = Mathf.Pow((b - c).sqrMagnitude, 0.25f), dt2 = Mathf.Pow((c - d).sqrMagnitude, 0.25f);
                if (dt1 < 0.0001f) dt1 = 1;
                if (dt0 < 0.0001f) dt0 = dt1;
                if (dt2 < 0.0001f) dt2 = dt1;
                Vector3 m1 = ((b - a) / dt0 - (c - a) / (dt0 + dt1) + (c - b) / dt1) * dt1;
                Vector3 m2 = ((c - b) / dt1 - (d - b) / (dt1 + dt2) + (d - c) / dt2) * dt1;
                return b + m1 * t + (-3 * b + 3 * c - 2 * m1 - m2) * t * t + (2 * b - 2 * c + m1 + m2) * t * t * t;
            }
        }
    }
}
