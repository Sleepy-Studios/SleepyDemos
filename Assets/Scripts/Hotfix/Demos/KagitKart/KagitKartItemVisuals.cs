using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hotfix.KagitKart
{
    /// 原作 items/visuals.js；只读道具规则，插值赛车姿态由宿主提供。
    public sealed class KagitKartItemVisuals : MonoBehaviour
    {
        [SerializeField] internal Mesh[] shapes;
        [SerializeField] internal Material[] materials;
        [SerializeField] internal string[] trackIds;
        [SerializeField] internal Color[] themeInk;
        [SerializeField] internal bool[] themeNight;
        [SerializeField] internal Vector3[] sunDirections;

        private const int Box = 0, Gum = 1, Flyer = 2, Pen = 3, Bottle = 4, Drop = 5, Scissors = 6, Splat = 7, Shadow = 8, Dash = 9, Foil = 10;
        private const int TrailCount = 12;
        private const float TrailLife = 0.5f;
        private const float StowScale = 0.62f;
        private static readonly int ItemData = Shader.PropertyToID("_ItemData");
        private static readonly int InstanceColor = Shader.PropertyToID("_InstanceColor");
        private readonly Dictionary<int, InkTrail> inkTrail = new();
        private readonly List<int> staleInkTrails = new();
        private readonly Queue<KagitKartItemEvent> cues = new();
        private KagitKartSession session;
        private Transform[] kartActors;
        private Batch[] batches;
        private KartVisual[] kartVisuals;
        private Tumble[] tumbles;
        private Particle[] drops, bits;
        private Snip[] snips;
        private Trail[] trails;
        private float[] boxAngles;
        private Quaternion[] boxBases;
        private Vector3 sunDirection, cameraPosition;
        private Color dashPlane, dashHoming;
        private double time;
        private float dashNear;

        private sealed class Batch
        {
            public readonly Matrix4x4[] poses;
            public readonly Vector4[] parameters, colors;
            public readonly MaterialPropertyBlock block = new();
            public int count;

            public Batch(int capacity)
            {
                poses = new Matrix4x4[capacity];
                parameters = new Vector4[capacity];
                colors = new Vector4[capacity];
            }
        }

        private sealed class KartVisual
        {
            public bool firing, shell;
            public double fireStart, shellBorn;
            public Matrix4x4 world = Matrix4x4.identity;
        }

        private sealed class Tumble
        {
            public bool on;
            public Vector3 position, velocity, axis;
            public Quaternion rotation;
            public double time;
        }

        private sealed class Particle
        {
            public bool on, big;
            public Vector3 position, velocity;
            public float floor, size, rotation;
            public double time, splat = -1;
        }

        private sealed class Snip
        {
            public bool on;
            public Vector3 position, up;
            public double time;
        }

        private sealed class Trail
        {
            public int id = -1, count, head;
            public bool seen, homing;
            public readonly Vector3[] positions = new Vector3[TrailCount];
            public readonly float[] at = new float[TrailCount];
            public double next;
            public Vector3 tip;
        }

        private sealed class InkTrail
        {
            public int slot;
            public double seen;
        }

        /// <summary>绑定本场规则、插值赛车与纸环境；使用源主题确定夜景、自照和阴影方向。</summary>
        /// <param name="value">已组装道具规则的比赛会话。</param>
        /// <param name="actors">与 Karts 顺序一致的赛车外部 Transform。</param>
        /// <param name="environment">当前赛道的 Paper 材质，复制环境光与雾。</param>
        public void Bind(KagitKartSession value, Transform[] actors, Material environment)
        {
            if (session != null || value == null || actors == null || actors.Length != value.Karts.Count || environment == null)
            {
                throw new ArgumentException("道具表现绑定参数无效或已绑定。");
            }
            session = value;
            kartActors = actors;
            int theme = Array.IndexOf(trackIds, session.Track.Track.definition.id);
            if (theme < 0)
            {
                throw new ArgumentException("道具表现缺少原作赛道主题。");
            }
            sunDirection = sunDirections[theme];
            bool night = themeNight[theme];
            dashPlane = Hex(night ? "#f4e6c4" : "#3a3444");
            dashHoming = Hex("#e0412f");
            int[] capacities = { session.Items.Boxes.Count, 72, 16, 40, 4, 72, 4, 72, 72, 16 * TrailCount, 8 };
            batches = new Batch[capacities.Length];
            for (int i = 0; i < batches.Length; i++)
            {
                batches[i] = new Batch(capacities[i]);
                foreach (string field in new[] { "_AmbientColor", "_FogColor", "_FogRange" })
                {
                    batches[i].block.SetVector(field, environment.GetVector(field));
                }
            }
            batches[Box].block.SetFloat("uSelfLit", night ? 0.42f : 0.16f);
            batches[Gum].block.SetFloat("uSelfLit", night ? 0.3f : 0.06f);
            batches[Shadow].block.SetVector("_BaseColor", new Vector4(themeInk[theme].r, themeInk[theme].g, themeInk[theme].b, 0.3f));
            kartVisuals = Create<KartVisual>(actors.Length);
            tumbles = Create<Tumble>(8);
            drops = Create<Particle>(72);
            bits = Create<Particle>(24);
            snips = Create<Snip>(3);
            trails = Create<Trail>(16);
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i].rotation = i * 2.39996f;
            }
            boxAngles = new float[session.Items.Boxes.Count];
            boxBases = new Quaternion[boxAngles.Length];
            for (int i = 0; i < boxAngles.Length; i++)
            {
                boxAngles[i] = i * 0.9f;
                boxBases[i] = Quaternion.FromToRotation(Vector3.up, session.Items.Boxes[i].Up);
            }
            session.Items.Signaled += QueueCue;
        }

        private static T[] Create<T>(int count) where T : new()
        {
            var values = new T[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new T();
            }
            return values;
        }

        private void QueueCue(KagitKartItemEvent cue)
        {
            if (cue.Kind is "pen" or "ink" or "pop" or "snip")
            {
                if (cues.Count >= 48)
                {
                    cues.Dequeue();
                }
                cues.Enqueue(cue);
            }
        }

        /// <summary>消费道具事件、推进视觉池并绘制；暂停时传零，仍向本帧摄像机提交冻结姿态。</summary>
        /// <param name="dt">有限非负表现帧时间。</param>
        /// <param name="camera">比赛摄像机，虚线路径按其位置避让。</param>
        /// <param name="reducedMotion">减少动态设置。</param>
        public void Advance(float dt, Camera camera, bool reducedMotion = false)
        {
            if (!float.IsFinite(dt) || dt < 0 || camera == null)
            {
                throw new ArgumentException("道具表现帧参数无效。");
            }
            if (session == null)
            {
                return;
            }
            time += dt;
            cameraPosition = Source(camera.transform.position);
            int player = IndexOf(session.Player);
            dashNear = Vector3.Distance(cameraPosition, Source(kartActors[player].position)) + 1.5f;
            foreach (var batch in batches)
            {
                batch.count = 0;
            }
            ConsumeCues();
            float alpha = (float)session.Interpolation;
            UpdateBoxes(dt, reducedMotion);
            UpdateGum(dt, alpha, reducedMotion);
            UpdatePens(dt, reducedMotion);
            UpdateProjectiles(alpha);
            UpdateDrops(dt);
            UpdateSnips(dt);
            UpdateFoil(reducedMotion);
            for (int i = 0; i < batches.Length; i++)
            {
                var batch = batches[i];
                if (batch.count == 0)
                {
                    continue;
                }
                batch.block.SetFloat("uTime", (float)time);
                if (i == Foil)
                {
                    // 源纸壳按赛车分别排序透明面，合成一批会改变相邻赛车纸壳的遮挡。
                    batch.block.SetVector(ItemData, Vector4.zero);
                    batch.block.SetVector(InstanceColor, Vector4.one);
                    for (int instance = 0; instance < batch.count; instance++)
                    {
                        Graphics.DrawMesh(shapes[i], batch.poses[instance], materials[i], gameObject.layer, camera, 0,
                            batch.block, ShadowCastingMode.Off, true);
                    }
                    continue;
                }
                batch.block.SetVectorArray(ItemData, batch.parameters);
                batch.block.SetVectorArray(InstanceColor, batch.colors);
                Graphics.DrawMeshInstanced(shapes[i], 0, materials[i], batch.poses, batch.count, batch.block,
                    ShadowCastingMode.Off, i is Box or Gum or Foil, gameObject.layer, camera);
            }
        }

        private void Put(int kind, Vector3 position, Quaternion rotation, Vector3 scale, float parameter = 0)
        {
            PutMatrix(kind, Matrix4x4.TRS(position, rotation, scale), parameter, Color.white);
        }

        private void PutMatrix(int kind, Matrix4x4 matrix, float parameter, Color color)
        {
            var batch = batches[kind];
            if (batch.count >= batch.poses.Length)
            {
                return;
            }
            // 原作所有计算保留源坐标，仅在提交 Unity 绘制时做 S*M*S 的 X 反射。
            matrix.m01 = -matrix.m01;
            matrix.m02 = -matrix.m02;
            matrix.m03 = -matrix.m03;
            matrix.m10 = -matrix.m10;
            matrix.m20 = -matrix.m20;
            matrix.m30 = -matrix.m30;
            int i = batch.count++;
            batch.poses[i] = matrix;
            batch.parameters[i] = new Vector4(parameter, 0, 0, 0);
            batch.colors[i] = color;
        }

        private Matrix4x4 KartFrame(int index)
        {
            Transform actor = kartActors[index];
            return Matrix4x4.TRS(Source(actor.position), Mirror(actor.rotation), Vector3.one);
        }

        private int IndexOf(KagitKartVehicle vehicle)
        {
            for (int i = 0; i < session.Karts.Count; i++)
            {
                if (session.Karts[i] == vehicle)
                {
                    return i;
                }
            }
            return -1;
        }

        private void ConsumeCues()
        {
            while (cues.Count > 0)
            {
                var cue = cues.Dequeue();
                if (cue.Kind == "pen" && cue.Kart != null)
                {
                    int i = IndexOf(cue.Kart);
                    if (i < 0)
                    {
                        continue;
                    }
                    var visual = kartVisuals[i];
                    if (visual.firing)
                    {
                        SpawnTumble(visual.world, cue.Kart);
                    }
                    visual.firing = true;
                    visual.fireStart = time;
                }
                else if (cue.Kind == "ink")
                {
                    SpawnInkBurst(cue.Position);
                }
                else if (cue.Kind == "pop")
                {
                    SpawnBits(cue.Position);
                }
                else if (cue.Kind == "snip")
                {
                    var snip = Array.Find(snips, item => !item.on) ?? snips[0];
                    snip.on = true;
                    snip.time = 0;
                    snip.position = cue.Position;
                    snip.up = cue.Kart?.TrackInfo.Up ?? Vector3.up;
                }
            }
        }

        private void SpawnTumble(Matrix4x4 world, KagitKartVehicle kart)
        {
            var tumble = Array.Find(tumbles, value => !value.on) ?? tumbles[0];
            tumble.position = world.GetColumn(3);
            tumble.rotation = world.rotation;
            tumble.on = true;
            tumble.time = 0;
            tumble.velocity = kart.Velocity * 0.55f + tumble.rotation * Vector3.back * 2.5f + Vector3.up * 3.2f;
            tumble.axis = tumble.rotation * Vector3.right;
        }

        private void SpawnInkBurst(Vector3 position)
        {
            int n = 0;
            foreach (var particle in drops)
            {
                if (particle.on || n >= 26)
                {
                    continue;
                }
                particle.on = true;
                particle.time = 0;
                particle.splat = -1;
                particle.position = position;
                particle.floor = position.y - 5.5f;
                if (n < 5)
                {
                    particle.big = true;
                    particle.size = 1.3f + n % 2 * 0.55f;
                    particle.position += new Vector3(Mathf.Cos(n * 2.4f) * 0.9f, Mathf.Sin(n * 1.7f) * 0.5f, Mathf.Sin(n * 2.4f) * 0.9f);
                    particle.velocity = Vector3.zero;
                }
                else
                {
                    particle.big = false;
                    float a = n * 2.39996f, e = -0.2f + n % 4 * 0.22f, s = 5 + n % 3 * 2.6f;
                    particle.velocity = new Vector3(Mathf.Cos(a) * Mathf.Cos(e) * s, Mathf.Sin(e) * s + 1.5f, Mathf.Sin(a) * Mathf.Cos(e) * s);
                    particle.size = 0.3f + n % 3 * 0.1f;
                }
                n++;
            }
        }

        private void SpawnBits(Vector3 position)
        {
            int n = 0;
            foreach (var particle in bits)
            {
                if (particle.on || n >= 6)
                {
                    continue;
                }
                particle.on = true;
                particle.time = 0;
                particle.position = position + Vector3.up * 0.25f;
                particle.floor = position.y;
                float a = n * 1.047f + 0.3f;
                particle.velocity = new Vector3(Mathf.Cos(a) * 3.2f, 4 + n % 2 * 1.5f, Mathf.Sin(a) * 3.2f);
                n++;
            }
        }

        private void Decal(Vector3 position, Vector3 up, float height, float sx, float sz, float yaw)
        {
            if (batches[Shadow].count >= 72 || sx <= 0.01f)
            {
                return;
            }
            float k = Mathf.Max(0, height) / Mathf.Max(0.25f, sunDirection.y);
            position += new Vector3(-sunDirection.x * k, 0.045f, -sunDirection.z * k);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up);
            Put(Shadow, position, rotation, new Vector3(sx, 1, sz));
        }

        private void UpdateBoxes(float dt, bool reduced)
        {
            for (int i = 0; i < session.Items.Boxes.Count; i++)
            {
                var box = session.Items.Boxes[i];
                float fold = FoldOf(box, session.RaceTime, reduced);
                float open = Mathf.Clamp01(fold);
                if (box.Active)
                {
                    boxAngles[i] += dt * (i % 2 != 0 ? 0.8f : -0.7f);
                }
                float bob = reduced ? 0 : Sin(time * 2.1f + i * 1.3f) * 0.1f;
                float lift = (0.5f + bob) * (1 - open) + 0.035f * open;
                Quaternion rotation = boxBases[i] * Quaternion.AngleAxis(boxAngles[i] * Mathf.Rad2Deg, Vector3.up);
                Put(Box, box.Position + box.Up * lift, rotation, Vector3.one, fold);
                Decal(box.Position, box.Up, lift + 0.6f, 0.78f * (1 - open), 0.78f * (1 - open), boxAngles[i]);
            }
        }

        private static float FoldOf(KagitKartItemBox box, double now, bool reduced)
        {
            if (box.Active)
            {
                return 0;
            }
            if (reduced)
            {
                return 1;
            }
            float age = (float)(now - box.TakenAt);
            if (age < 0.26f)
            {
                float t = age / 0.26f;
                return 1 - (1 - t) * (1 - t) * (1 - t);
            }
            if (age < 0.8f)
            {
                return 1;
            }
            float closing = Mathf.Min(1, (age - 0.8f) / 0.4f) - 1;
            return -2.9f * closing * closing * closing - 1.9f * closing * closing;
        }

        private void UpdateGum(float dt, float alpha, bool reduced)
        {
            for (int hazardIndex = 0; hazardIndex < session.Items.Hazards.Count; hazardIndex++)
            {
                var hazard = session.Items.Hazards[hazardIndex];
                if (hazard.Dead || batches[Gum].count >= 69)
                {
                    continue;
                }
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hazard.Up);
                if (hazard.Held != null)
                {
                    var kart = hazard.Held;
                    var frame = KartFrame(IndexOf(kart));
                    float sway = reduced ? 0 : Sin(time * 6 + hazard.Id) * 0.18f;
                    float bob = reduced ? 0 : Mathf.Abs(Sin(time * 9 + hazard.Id)) * 0.06f;
                    Vector3 position = frame.MultiplyPoint3x4(new Vector3(sway, 0, -1.9f));
                    position.y = Mathf.LerpUnclamped(hazard.PreviousPosition.y, hazard.Position.y, alpha);
                    rotation *= Quaternion.AngleAxis(kart.Heading * Mathf.Rad2Deg, Vector3.up);
                    Vector3 bubble = position + hazard.Up * (0.42f + 0.04f + bob);
                    Put(Gum, bubble, rotation, Vector3.one * 0.42f, 1);
                    Vector3 knot = bubble + rotation * Vector3.forward * (0.42f * 0.92f);
                    Put(Gum, knot, rotation, new Vector3(0.15f, 0.12f, 0.13f));
                    Vector3 tail = frame.MultiplyPoint3x4(new Vector3(0, 0.42f, -1.02f));
                    float length = Vector3.Distance(tail, knot);
                    Put(Gum, (tail + knot) * 0.5f - Vector3.up * 0.06f, Quaternion.FromToRotation(Vector3.forward, (tail - knot).normalized), new Vector3(0.045f, 0.045f, length * 0.5f), 1);
                }
                else
                {
                    float age = (float)(session.RaceTime - hazard.DroppedAt);
                    float sxz = 0.8f, sy = 0.34f, lift = 0, bubble = 0;
                    if (!reduced)
                    {
                        bool fell = hazard.HeldFor < 0.25;
                        if (fell && age < 0.14f)
                        {
                            float u = age / 0.14f;
                            lift = 0.55f * (1 - u * u);
                            sxz = sy = 0.3f;
                            bubble = 1;
                        }
                        else if (!fell && age < 0.08f)
                        {
                            bubble = 1;
                            sxz = sy = 0.42f * (1 + 0.3f * age / 0.08f);
                        }
                        else
                        {
                            float u = age - (fell ? 0.14f : 0.08f);
                            float w = Mathf.Exp(-7 * u) * Mathf.Cos(17 * u);
                            sxz = 0.8f * (1 + 0.42f * w);
                            sy = 0.34f * (1 - 0.55f * w);
                        }
                    }
                    rotation *= Quaternion.AngleAxis(hazard.Id * 2.4f * Mathf.Rad2Deg, Vector3.up);
                    Vector3 position = hazard.Position + hazard.Up * ((bubble != 0 ? sy + 0.04f : 0.45f * sy) + lift);
                    Put(Gum, position, rotation, new Vector3(sxz, sy, sxz), bubble);
                }
            }
            foreach (var bit in bits)
            {
                if (!bit.on)
                {
                    continue;
                }
                bit.time += dt;
                if (bit.time > 0.5 || batches[Gum].count >= 72)
                {
                    bit.on = false;
                    continue;
                }
                bit.velocity.y -= 16 * dt;
                bit.position += bit.velocity * dt;
                if (bit.position.y < bit.floor + 0.05f)
                {
                    bit.position.y = bit.floor + 0.05f;
                    bit.velocity *= 0.4f;
                }
                float scale = 0.2f * (1 - Smooth(0.3f, 0.5f, bit.time));
                Put(Gum, bit.position, Quaternion.identity, Vector3.one * scale, 1);
            }
        }

        private static Matrix4x4 StowPose(int slot)
        {
            float direction = slot % 2 != 0 ? -1 : 1;
            Vector3 position = new(direction * 1.35f * StowScale * 0.5f, 0.44f + slot * 0.14f, -1.22f - slot * 0.04f);
            return Matrix4x4.TRS(position, Euler(-0.07f * direction, direction * Mathf.PI / 2, 0), Vector3.one * StowScale);
        }

        private Matrix4x4 FirePose(float age, bool reduced)
        {
            float slide = Smooth(0, 1, age / 0.12f);
            float kick = age < 0.05f ? age / 0.05f : Mathf.Exp(-(age - 0.05f) * 9);
            float shake = reduced ? 0 : 0.018f;
            var stow = StowPose(0);
            Vector3 firing = new(Sin(time * 71) * shake, 0.6f + Sin(time * 53) * shake, -1.06f - 0.28f * kick);
            return Matrix4x4.TRS(Vector3.LerpUnclamped(stow.GetColumn(3), firing, slide), Quaternion.SlerpUnclamped(stow.rotation, Quaternion.identity, slide), Vector3.one * (StowScale + (1 - StowScale) * slide));
        }

        private void UpdatePens(float dt, bool reduced)
        {
            for (int i = 0; i < session.Karts.Count; i++)
            {
                var kart = session.Karts[i];
                var visual = kartVisuals[i];
                int stow = kart.Roulette > 0 ? 0 : kart.Item == "rocket" ? 1 : kart.Item == "rocket3" ? Mathf.Min(3, kart.ItemCount) : 0;
                if (stow == 0 && !visual.firing)
                {
                    continue;
                }
                var frame = KartFrame(i);
                for (int slot = 0; slot < stow && batches[Pen].count < 40; slot++)
                {
                    PutMatrix(Pen, frame * StowPose(slot), 0, Color.white);
                }
                if (visual.firing)
                {
                    float age = (float)(time - visual.fireStart);
                    bool burning = kart.BoostTime > 0 && kart.BoostSource == "item" || age < 0.25f;
                    if (!burning || kart.Rescuing)
                    {
                        SpawnTumble(visual.world, kart);
                        visual.firing = false;
                    }
                    else if (batches[Pen].count < 40)
                    {
                        visual.world = frame * FirePose(age, reduced);
                        PutMatrix(Pen, visual.world, 0, Color.white);
                    }
                }
            }
            foreach (var tumble in tumbles)
            {
                if (!tumble.on)
                {
                    continue;
                }
                tumble.time += dt;
                if (tumble.time > 0.9 || batches[Pen].count >= 40)
                {
                    tumble.on = false;
                    continue;
                }
                tumble.velocity.y -= 16 * dt;
                tumble.position += tumble.velocity * dt;
                tumble.rotation = Quaternion.AngleAxis(10 * dt * Mathf.Rad2Deg, tumble.axis) * tumble.rotation;
                float scale = 1 - Smooth(0.6f, 0.9f, tumble.time);
                Put(Pen, tumble.position, tumble.rotation, Vector3.one * scale);
            }
        }

        private void SampleTrail(KagitKartProjectile projectile, Vector3 tail)
        {
            Trail trail = null, free = null;
            foreach (var value in trails)
            {
                if (value.id == projectile.Id)
                {
                    trail = value;
                    break;
                }
                if (free == null && value.id < 0)
                {
                    free = value;
                }
            }
            if (trail == null)
            {
                trail = free;
                if (trail == null)
                {
                    return;
                }
                trail.id = projectile.Id;
                trail.count = trail.head = 0;
                trail.next = time;
            }
            trail.seen = true;
            trail.homing = projectile.Kind == "homing";
            if (time >= trail.next || trail.count == 0)
            {
                trail.positions[trail.head] = tail;
                trail.at[trail.head] = (float)time;
                trail.head = (trail.head + 1) % TrailCount;
                trail.count = Mathf.Min(TrailCount, trail.count + 1);
                trail.next = time + 0.045;
            }
            trail.tip = tail;
        }

        private void PutDash(Vector3 a, Vector3 b, float width, Color color)
        {
            Vector3 middle = (a + b) * 0.5f;
            float distance = Vector3.Distance(cameraPosition, middle);
            if (distance < dashNear)
            {
                return;
            }
            Vector3 difference = b - a;
            float length = difference.magnitude;
            if (length < 0.05f)
            {
                return;
            }
            float k = 1 + Mathf.Clamp((distance - 8) * 0.02f, 0, 0.7f);
            PutMatrix(Dash, Matrix4x4.TRS(middle, Quaternion.FromToRotation(Vector3.forward, difference / length), new Vector3(width * k, width * k, length)), 0, color);
        }

        private void UpdateTrails()
        {
            foreach (var trail in trails)
            {
                if (trail.id < 0)
                {
                    continue;
                }
                int newest = (trail.head - 1 + TrailCount) % TrailCount;
                if (!trail.seen && (trail.count == 0 || time - trail.at[newest] > TrailLife))
                {
                    trail.id = -1;
                    trail.count = 0;
                    continue;
                }
                Color color = trail.homing ? dashHoming : dashPlane;
                Vector3 a = trail.seen ? trail.tip : trail.positions[newest];
                for (int j = trail.seen ? 0 : 1; j < trail.count && batches[Dash].count < 16 * TrailCount; j++)
                {
                    int slot = (trail.head - 1 - j + TrailCount * 2) % TrailCount;
                    double age = time - trail.at[slot];
                    if (age > TrailLife)
                    {
                        break;
                    }
                    Vector3 b = trail.positions[slot];
                    float width = 0.13f * (float)Math.Sqrt(1 - age / TrailLife);
                    PutDash(Vector3.LerpUnclamped(a, b, 0.22f), Vector3.LerpUnclamped(a, b, 0.78f), width, color);
                    a = b;
                }
                trail.seen = false;
            }
        }

        private void UpdateProjectiles(float alpha)
        {
            for (int projectileIndex = 0; projectileIndex < session.Items.Projectiles.Count; projectileIndex++)
            {
                var projectile = session.Items.Projectiles[projectileIndex];
                if (projectile.Dead)
                {
                    continue;
                }
                Vector3 position = Vector3.LerpUnclamped(projectile.PreviousPosition, projectile.Position, alpha);
                float heading = projectile.PreviousHeading + Mathf.Atan2(Mathf.Sin(projectile.Heading - projectile.PreviousHeading), Mathf.Cos(projectile.Heading - projectile.PreviousHeading)) * alpha;
                float age = (float)projectile.Age;
                if ((projectile.Kind is "plane" or "homing") && batches[Flyer].count < 16)
                {
                    float pitch = 0, roll;
                    if (projectile.Kind == "plane")
                    {
                        float flip = age - (float)projectile.FlipAt;
                        roll = (flip < 0.4f ? Mathf.PI * 2 * Smooth(0, 1, flip / 0.4f) : 0) + Mathf.Sin(age * 7 + projectile.Id) * 0.08f;
                    }
                    else
                    {
                        roll = Mathf.Clamp(-projectile.TurnRate * 0.22f, -0.7f, 0.7f) + Mathf.Sin(age * 12 + projectile.Id) * 0.14f;
                        pitch = Mathf.Sin(age * 9 + projectile.Id) * 0.06f;
                    }
                    if (projectile.Falling)
                    {
                        pitch = Mathf.Min(1.2f, (age - (float)projectile.FallAt) * 2.2f);
                    }
                    position.y += Mathf.Sin(age * 8 + projectile.Id) * 0.06f + 0.5f * Smooth(0, 0.18f, age);
                    Quaternion rotation = Euler(pitch, heading, roll);
                    float scale = 1 + Mathf.Clamp((Vector3.Distance(cameraPosition, position) - 8) * 0.02f, 0, 0.7f);
                    Put(Flyer, position, rotation, Vector3.one * scale, projectile.Kind == "homing" ? 1 : 0);
                    if (!projectile.Falling)
                    {
                        Decal(new Vector3(position.x, projectile.GroundY, position.z), projectile.Up, position.y - projectile.GroundY, 0.5f * scale, 0.95f * scale, heading);
                    }
                    SampleTrail(projectile, position + rotation * new Vector3(0, 0, -1.2f * scale));
                }
                else if (projectile.Kind == "ink" && batches[Bottle].count < 4)
                {
                    Vector3 axis = new(Mathf.Cos(heading), 0, -Mathf.Sin(heading));
                    Quaternion rotation = Quaternion.AngleAxis((0.6f + age * 5.5f) * Mathf.Rad2Deg, axis) * Quaternion.AngleAxis(heading * Mathf.Rad2Deg, Vector3.up);
                    Put(Bottle, position, rotation, Vector3.one);
                    Decal(new Vector3(position.x, projectile.To.y, position.z), Vector3.up, position.y - projectile.To.y, 0.6f, 0.6f, 0);
                    DripInk(projectile, position, rotation);
                }
                else if (projectile.Kind == "scissors" && batches[Scissors].count + 2 <= 4)
                {
                    float open, pitch = 0, scale = 1;
                    if (projectile.Phase < 2)
                    {
                        open = 0.14f + 0.5f * (0.5f + 0.5f * Mathf.Sin(age * 13));
                        pitch = Mathf.Sin(age * 3) * 0.08f;
                        if (projectile.Phase == 0)
                        {
                            scale = 0.45f + 0.55f * Smooth(0, 1, (float)projectile.PhaseTime / 0.5f);
                        }
                    }
                    else
                    {
                        float u = (float)projectile.PhaseTime / 0.7f;
                        open = 0.95f * (1 - Smooth(0.72f, 1, u));
                        pitch = 0.75f * Smooth(0, 0.4f, u);
                    }
                    Quaternion rotation = Euler(pitch, heading, 0);
                    Put(Scissors, position, rotation * Quaternion.AngleAxis(-open / 2 * Mathf.Rad2Deg, Vector3.up), Vector3.one * scale);
                    Put(Scissors, position, rotation * Quaternion.AngleAxis(open / 2 * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(180, Vector3.forward), Vector3.one * scale);
                    float high = Mathf.Max(0, position.y - projectile.GroundY);
                    Decal(new Vector3(position.x, projectile.GroundY, position.z), Vector3.up, high, 1.6f + Mathf.Max(0, 9 - high) * 0.06f, 1.1f, heading);
                }
            }
            UpdateTrails();
        }

        private void DripInk(KagitKartProjectile projectile, Vector3 position, Quaternion rotation)
        {
            int slot = (int)Math.Floor(projectile.Age / 0.05);
            if (inkTrail.TryGetValue(projectile.Id, out var previous))
            {
                previous.seen = time;
                if (slot <= previous.slot)
                {
                    return;
                }
            }
            previous ??= new InkTrail();
            previous.slot = slot;
            previous.seen = time;
            inkTrail[projectile.Id] = previous;
            var drop = Array.Find(drops, value => !value.on);
            if (drop == null)
            {
                return;
            }
            Vector3 direction = rotation * Vector3.up;
            drop.on = true;
            drop.big = false;
            drop.time = 0;
            drop.splat = -1;
            drop.position = position + direction * 0.75f;
            drop.velocity = direction * 2.2f;
            drop.floor = projectile.To.y;
            drop.size = 0.32f + slot % 3 * 0.08f;
        }

        private void UpdateDrops(float dt)
        {
            foreach (var drop in drops)
            {
                if (!drop.on)
                {
                    continue;
                }
                drop.time += dt;
                if (drop.big)
                {
                    if (drop.time > 0.45)
                    {
                        drop.on = false;
                        continue;
                    }
                    float scale = drop.size * (drop.time < 0.07 ? (float)(drop.time / 0.07) : 1 - Smooth(0.14f, 0.45f, drop.time));
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.up, (cameraPosition - drop.position).normalized) * Quaternion.AngleAxis(drop.rotation * Mathf.Rad2Deg, Vector3.up);
                    Put(Splat, drop.position, rotation, Vector3.one * scale);
                }
                else if (drop.splat < 0)
                {
                    drop.velocity.y -= 16 * dt;
                    drop.position += drop.velocity * dt;
                    if (drop.position.y <= drop.floor + 0.05f)
                    {
                        drop.position.y = drop.floor + 0.03f;
                        drop.splat = drop.time;
                    }
                    if (drop.time > 2)
                    {
                        drop.on = false;
                        continue;
                    }
                    float speed = drop.velocity.magnitude;
                    Quaternion rotation = speed > 0.001f ? Quaternion.FromToRotation(Vector3.up, -drop.velocity / speed) : Quaternion.identity;
                    Put(Drop, drop.position, rotation, new Vector3(drop.size, drop.size * (1 + Mathf.Min(0.9f, speed * 0.06f)), drop.size));
                }
                else
                {
                    double age = drop.time - drop.splat;
                    if (age > 1.2)
                    {
                        drop.on = false;
                        continue;
                    }
                    float k = (age < 0.06 ? 0.55f + (float)(age / 0.06) * 0.45f : 1) * (1 - Smooth(0.65f, 1.2f, age));
                    Put(Splat, drop.position, Quaternion.AngleAxis(drop.rotation * Mathf.Rad2Deg, Vector3.up), new Vector3(drop.size * 1.9f * k, drop.size * 1.9f, drop.size * 1.9f * k));
                }
            }
            // 原作 Map 仅记录当前仍飞行的墨水瓶；不让历史 projectile id 持续累积。
            staleInkTrails.Clear();
            foreach (var pair in inkTrail)
            {
                if (pair.Value.seen != time)
                {
                    staleInkTrails.Add(pair.Key);
                }
            }
            foreach (int id in staleInkTrails)
            {
                inkTrail.Remove(id);
            }
        }

        private void UpdateSnips(float dt)
        {
            foreach (var snip in snips)
            {
                if (!snip.on)
                {
                    continue;
                }
                snip.time += dt;
                if (snip.time > 0.5)
                {
                    snip.on = false;
                    continue;
                }
                float radius = 6 * (snip.time < 0.12f ? Smooth(0, 1, snip.time / 0.12f) : 1 - Smooth(0.25f, 0.5f, snip.time));
                Decal(snip.position, snip.up, 0, radius, radius, 0);
            }
        }

        private void UpdateFoil(bool reduced)
        {
            for (int i = 0; i < session.Karts.Count; i++)
            {
                var kart = session.Karts[i];
                var visual = kartVisuals[i];
                if (kart.InvincibleTime <= 0)
                {
                    visual.shell = false;
                    continue;
                }
                if (!visual.shell)
                {
                    visual.shell = true;
                    visual.shellBorn = time;
                }
                float age = (float)(time - visual.shellBorn);
                float scale = reduced || age > 0.3f ? 1 : 1 + 0.32f * (1 - Smooth(0, 1, age / 0.3f)) - 0.05f * Mathf.Sin(age / 0.3f * Mathf.PI);
                if (kart.InvincibleTime > 1.2f || (int)Math.Floor(time * 10) % 2 == 0)
                {
                    PutMatrix(Foil, KartFrame(i) * Matrix4x4.Scale(Vector3.one * scale), 0, Color.white);
                }
            }
        }

        private static float Smooth(double a, double b, double value)
        {
            double t = Math.Clamp((value - a) / (b - a), 0, 1);
            return (float)(t * t * (3 - 2 * t));
        }

        private static float Sin(double value) => (float)Math.Sin(value);

        private static Quaternion Euler(float pitch, float yaw, float roll) =>
            Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(pitch * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(roll * Mathf.Rad2Deg, Vector3.forward);

        private static Vector3 Source(Vector3 value) => new(-value.x, value.y, value.z);

        private static Quaternion Mirror(Quaternion value) => new(value.x, -value.y, -value.z, value.w);

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out var color);
            return color.linear;
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.Items.Signaled -= QueueCue;
            }
        }
    }
}
