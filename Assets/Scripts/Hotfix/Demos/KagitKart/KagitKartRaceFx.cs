using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hotfix.KagitKart
{
    /// 原作 fx.js / bits / cards / speedlines 的纸屑、火焰与速度线；不写回赛车规则。
    public sealed class KagitKartRaceFx : MonoBehaviour
    {
        [SerializeField] internal Mesh bitsShape, cardsShape, linesShape;
        [SerializeField] internal Material bitsMaterial, cardsMaterial, linesMaterial;
        [SerializeField] internal KagitKartRescueCrane cranes;
        [SerializeField] internal Palette[] palettes;

        [Serializable]
        internal sealed class Palette
        {
            public string id;
            public Color paper, offroad, light, road, ink, curbA, curbB, accent;
            public Color[] confetti;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BitGpu
        {
            public Vector4 P0, V0, Phys, Shape, Look, ColA, ColB;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CardGpu
        {
            public Vector4 A, B, Color, Variant;
        }

        private sealed class Spec
        {
            public float x, y, z, vx, vy, vz, life, delay, drag, grav, floor, shape, size, mode, spin, fold, aspect, flutter, glow, grow;
            public Color colA, colB;
            public float seed = -1;
        }

        private sealed class KartFx
        {
            public double spark, debris, streak, scrape, boostAge = 9, confetti, conAcc;
            public int flameTier = 1, side, n;
        }

        private struct Cue
        {
            public string type, cause;
            public int kart;
            public float value;
        }

        private const int Capacity = 3072;
        private const int Tri = 0, Sliver = 1, Star = 2, Rect = 3, Puff = 4, Stroke = 5, Zap = 6;
        private const int TumbleMode = 0, SpinMode = 1, FlipMode = 2, StreakMode = 3;
        private static readonly int[] Perm = { 0, 2, 1, 3, 2, 0, 3, 1 };
        private static readonly float[] Wobble = { 1, 0.9f, 1.06f, 0.94f, 1.02f, 0.88f, 1.08f, 0.96f };
        private readonly Spec spec = new();
        private readonly Cue[] queue = new Cue[64];
        internal readonly BitGpu[] bitStates = new BitGpu[Capacity];
        internal readonly CardGpu[] cardStates = new CardGpu[96];
        private MaterialPropertyBlock bitsBlock, cardsBlock, linesBlock;
        private ComputeBuffer bitBuffer, cardBuffer;
        private MaterialPropertyBlock[] cameraBlocks;
        private KagitKartSession session;
        private KagitKartKartTuning tuning;
        private KagitKartRandom random;
        private Transform[] actors;
        private KagitKartCharacterVisual[] visuals;
        private Transform[,] anchors;
        private KartFx[] states;
        private Camera camera;
        private Light sun;
        private int qn, current, head, highest, spawned, dirtyLo = Capacity, dirtyHi = -1, cardCount;
        private bool wrapped, dark, reducedMotion;
        private double time, lastDeath = -1, lineIntensity;
        private float qMul = 1;
        private Vector3 position, forward, left, headingForward, headingRight, cameraPosition, a, b, c;
        private Color[] tierA, tierB, offroad, smoke, stars, confetti;
        private Color paper, paperDark, offroadBack, puff, inkish, graphite, sheen, fleck, starBack;
        private Color flameOrange, flameRocket, flameDraft, flameMid, flameInner, flameShadow;

        /// <summary>绑定本场规则、角色插值及原作独立 fx 随机流；资源来自保存的 RaceFx Prefab。</summary>
        /// <param name="value">比赛会话。</param>
        /// <param name="kartActors">与 Karts 顺序一致的角色外部 Transform。</param>
        /// <param name="environment">当前赛道 Paper 环境材质。</param>
        /// <param name="sceneSun">本 Demo 的方向光。</param>
        /// <param name="seed">本场模拟初始种子，单独 Fork fx，不消耗任何规则流。</param>
        /// <param name="kartTuning">原作 kart 配置，救援预召唤读取 outRespawn。</param>
        public void Bind(KagitKartSession value, Transform[] kartActors, Material environment, Light sceneSun, uint seed, KagitKartKartTuning kartTuning)
        {
            if (session != null || value == null || kartActors == null || kartActors.Length != value.Karts.Count || environment == null || sceneSun == null || kartTuning == null)
            {
                throw new ArgumentException("比赛纸艺特效绑定参数无效。");
            }
            session = value;
            bitsBlock = new MaterialPropertyBlock();
            cardsBlock = new MaterialPropertyBlock();
            linesBlock = new MaterialPropertyBlock();
            actors = kartActors;
            tuning = kartTuning;
            sun = sceneSun;
            random = new KagitKartRandom(seed).Fork("fx");
            visuals = new KagitKartCharacterVisual[actors.Length];
            anchors = new Transform[actors.Length, 3];
            states = new KartFx[actors.Length];
            for (int i = 0; i < actors.Length; i++)
            {
                visuals[i] = actors[i].GetComponent<KagitKartCharacterVisual>();
                anchors[i, 0] = visuals[i].GetAnchor("wheelRL");
                anchors[i, 1] = visuals[i].GetAnchor("wheelRR");
                anchors[i, 2] = visuals[i].GetAnchor("exhaust");
                states[i] = new KartFx();
                session.Karts[i].Signaled += OnVehicleSignal;
            }
            int theme = Array.FindIndex(palettes, item => item.id == session.Track.Track.definition.id);
            var palette = palettes[theme];
            paper = palette.paper;
            paperDark = paper * 0.78f;
            offroad = new[] { Color.Lerp(palette.offroad, paper, 0.45f), palette.offroad * 0.7f, Color.Lerp(palette.offroad, paper, 0.2f) };
            offroadBack = palette.offroad * 0.55f;
            puff = Color.Lerp(palette.light, paper, 0.25f);
            dark = 0.2126f * palette.road.r + 0.7152f * palette.road.g + 0.0722f * palette.road.b < 0.2f;
            inkish = dark ? palette.light : Color.Lerp(palette.ink, paper, 0.12f);
            tierA = Colors("#f4ecd6", "#3b8ff0", "#ff8a1f", "#a24ee6");
            tierB = Colors("#ffffff", "#b4d6ff", "#ffd3a0", "#dcb6ff");
            graphite = Hex("#54555d");
            sheen = Hex("#a3a6b1");
            fleck = Hex("#fffaf0");
            smoke = Colors("#8b8680", "#5f5b57");
            stars = new[] { Hex("#ffd23f"), Hex("#fff3d0"), palette.accent };
            starBack = Hex("#fff9e8");
            confetti = palette.confetti;
            flameOrange = Hex("#f45d25");
            flameRocket = Hex("#e3342f");
            flameDraft = Hex("#bfe3f7");
            flameMid = Hex("#ffd23f");
            flameInner = Hex("#fff5da");
            flameShadow = flameMid * 0.62f;
            foreach (var block in new[] { bitsBlock, cardsBlock, linesBlock })
            {
                block.SetVector("uInk", environment.GetVector("uInk"));
                block.SetVector("_FogColor", environment.GetVector("_FogColor"));
                block.SetVector("_FogRange", environment.GetVector("_FogRange"));
            }
            bitsBlock.SetVector("uAmb", environment.GetVector("_AmbientColor"));
            cameraBlocks = new[] { bitsBlock, cardsBlock };
            linesBlock.SetVector("uColor", inkish);
            linesBlock.SetVector("uEdge", dark ? palette.ink : palette.light);
            linesBlock.SetFloat("uOpacity", dark ? 0.55f : 0.72f);
            for (int i = 0; i < bitStates.Length; i++)
            {
                bitStates[i].P0.w = 1e4f;
                bitStates[i].V0.w = 1;
            }
            bitBuffer ??= new ComputeBuffer(Capacity, Marshal.SizeOf<BitGpu>());
            cardBuffer ??= new ComputeBuffer(96, Marshal.SizeOf<CardGpu>());
            bitBuffer.SetData(bitStates);
            bitsBlock.SetBuffer("_Fx", bitBuffer);
            cardsBlock.SetBuffer("_Fx", cardBuffer);
            cranes.Bind(value, actors, visuals, theme);
            session.Race.Signaled += OnRaceSignal;
        }

        private void OnRaceSignal(KagitKartRaceEvent signal)
        {
            if (signal.Kind == "finish" && signal.Kart?.IsPlayer == true)
            {
                Push("finish", signal.Kart);
            }
        }

        private void OnVehicleSignal(KagitKartVehicle kart, KagitKartVehicleSignal signal)
        {
            int index = IndexOf(kart);
            switch (signal)
            {
                case KagitKartVehicleSignal.DriftTier:
                    Push("tier", kart, kart.DriftTier);
                    break;
                case KagitKartVehicleSignal.DriftRelease:
                    if (kart.DriftTier >= 1)
                    {
                        states[index].flameTier = kart.DriftTier;
                        Push("release", kart, kart.DriftTier);
                    }
                    break;
                case KagitKartVehicleSignal.Boost:
                    states[index].boostAge = 0;
                    if (kart.BoostSource == "draft")
                    {
                        Push("draftBoost", kart);
                    }
                    break;
                case KagitKartVehicleSignal.Land:
                    Push("land", kart, kart.AirTime);
                    break;
                case KagitKartVehicleSignal.WallHit:
                    Push("wall", kart, kart.LastImpactSpeed);
                    break;
                case KagitKartVehicleSignal.Hit:
                    Push("hit", kart, cause: kart.SpinCause);
                    break;
                case KagitKartVehicleSignal.RescueLift:
                    Push("respawn", kart);
                    break;
            }
        }

        private void Push(string type, KagitKartVehicle kart, float value = 0, string cause = "")
        {
            if (qn < queue.Length && kart != null)
            {
                queue[qn++] = new Cue { type = type, kart = IndexOf(kart), value = value, cause = cause };
            }
        }

        private int IndexOf(KagitKartVehicle kart)
        {
            for (int i = 0; i < session.Karts.Count; i++)
            {
                if (session.Karts[i] == kart)
                {
                    return i;
                }
            }
            return -1;
        }

        private void Frame(int index)
        {
            current = index;
            var actor = actors[index];
            var kart = session.Karts[index];
            position = Source(actor.position);
            forward = Source(actor.forward);
            left = -Source(actor.right);
            headingForward = KagitKartVehicle.Forward(kart.Heading);
            headingRight = new Vector3(-Mathf.Cos(kart.Heading), 0, Mathf.Sin(kart.Heading));
        }

        private Vector3 Anchor(int kind) => Source(anchors[current, kind].position);

        private float Ground(KagitKartVehicle kart) => kart.Grounded ? kart.Position.y : kart.TrackInfo.HasGround ? kart.TrackInfo.GroundY : -1e4f;

        private float R(float min, float max) => (float)(min + (max - min) * random.Next());

        private Spec Base(Vector3 p, KagitKartVehicle kart, float inherit)
        {
            spec.x = p.x;
            spec.y = p.y;
            spec.z = p.z;
            spec.vx = kart != null ? kart.Velocity.x * inherit : 0;
            spec.vy = 0;
            spec.vz = kart != null ? kart.Velocity.z * inherit : 0;
            spec.delay = spec.flutter = spec.grow = spec.mode = 0;
            spec.aspect = 1;
            spec.fold = 0.6f;
            return spec;
        }

        private void Emit(Spec value)
        {
            int index = head++;
            if (head >= Capacity)
            {
                head = 0;
                wrapped = true;
            }
            highest = Math.Max(highest, index + 1);
            dirtyLo = Math.Min(dirtyLo, index);
            dirtyHi = Math.Max(dirtyHi, index);
            if (value.y < value.floor)
            {
                value.floor = value.y - 0.01f;
            }
            float born = (float)(time + value.delay);
            bitStates[index] = new BitGpu
            {
                P0 = new Vector4(value.x, value.y, value.z, born),
                V0 = new Vector4(value.vx, value.vy, value.vz, value.life),
                Phys = new Vector4(value.drag, value.grav, value.floor, LandingAge(value)),
                Shape = new Vector4(value.shape, value.size, value.seed >= 0 ? value.seed : (float)((spawned * 0.618034 + Math.Abs(value.x) * 0.013) % 1), value.mode),
                Look = new Vector4(value.spin, value.fold, value.aspect, value.flutter),
                ColA = new Vector4(value.colA.r, value.colA.g, value.colA.b, value.glow),
                ColB = new Vector4(value.colB.r, value.colB.g, value.colB.b, value.grow)
            };
            spawned++;
            lastDeath = Math.Max(lastDeath, time + value.delay + value.life);
            value.delay = 0;
            value.seed = -1;
        }

        private static float LandingAge(Spec value)
        {
            if (value.grav <= 0 || value.floor <= -1e3f)
            {
                return 1e4f;
            }
            double previous = 0, drag = Math.Max(value.drag, 0.01);
            for (int i = 1; i <= 10; i++)
            {
                double t = value.life * i / 10.0;
                if (Height(t, value, drag) <= value.floor)
                {
                    double lo = previous, hi = t;
                    for (int j = 0; j < 7; j++)
                    {
                        double middle = (lo + hi) * 0.5;
                        if (Height(middle, value, drag) <= value.floor)
                        {
                            hi = middle;
                        }
                        else
                        {
                            lo = middle;
                        }
                    }
                    return (float)hi;
                }
                previous = t;
            }
            return 1e4f;
        }

        private static double Height(double t, Spec value, double drag)
        {
            double fk = (1 - Math.Exp(-drag * t)) / drag;
            return value.y + value.vy * fk - value.grav * (t - fk) / drag;
        }

        internal void AddCard(Vector3 from, Vector3 to, float width, float shape, Color color, float glow, float variant = 0, float outline = 0, float opacity = 1, float offset = 0)
        {
            if (cardCount >= cardStates.Length)
            {
                return;
            }
            cardStates[cardCount++] = new CardGpu
            {
                A = new Vector4(from.x, from.y, from.z, width), B = new Vector4(to.x, to.y, to.z, shape),
                Color = new Vector4(color.r, color.g, color.b, glow), Variant = new Vector4(variant, outline, opacity, offset)
            };
        }

        private void Spark(KagitKartVehicle kart, Vector3 wheel, int tier, bool big, float grow = 1)
        {
            int dir = kart.DriftDirection != 0 ? kart.DriftDirection : 1;
            var s = Base(wheel + new Vector3(R(-0.05f, 0.05f), R(0, 0.06f), R(-0.05f, 0.05f)), kart, big ? 0.72f : 0.62f);
            float back = big ? R(3, 7) : R(2, 5), outward = big ? R(-1.5f, 4.5f) : R(1.5f, 4.2f), up = big ? R(2.5f, 5.5f) : R(2, 4.4f);
            s.vx += -headingForward.x * back - headingRight.x * dir * outward;
            s.vz += -headingForward.z * back - headingRight.z * dir * outward;
            s.vy = up;
            s.life = big ? R(0.4f, 0.62f) : R(0.24f, 0.4f);
            s.drag = 3;
            s.grav = 14;
            s.floor = Ground(kart) + 0.02f;
            s.shape = Tri;
            s.size = tier > 0 ? R(0.06f, 0.1f) + tier * 0.01f : R(0.045f, 0.07f);
            if (big)
            {
                s.size *= 1.35f * grow;
            }
            s.spin = R(14, 26);
            s.fold = R(0.5f, 1.2f);
            s.aspect = 0.72f;
            s.colA = tierA[tier];
            s.colB = tierB[tier];
            s.glow = tier > 0 ? 0.6f : 0.25f;
            Emit(s);
        }

        private void TierBurst(KagitKartVehicle kart, Vector3 wheel, int tier, int side)
        {
            Vector3 p = wheel + left * side * 0.12f + Vector3.up * 0.3f;
            float size = 0.34f + 0.06f * tier;
            float seed = (float)((time * 7.31 + current * 0.37 + (side > 0 ? 0 : 0.5)) % 1);
            Vector3 camRight = Source(camera.transform.right), camUp = Source(camera.transform.up);
            for (int i = 0; i < 3; i++)
            {
                float sz = i == 2 ? size * 0.5f : size, offset = i == 0 ? 0.1f * sz : 0;
                Color color = i == 0 ? tierA[tier] * 0.35f : i == 1 ? tierA[tier] : tierB[tier];
                var s = Base(p + (camRight - camUp) * offset, kart, 1);
                s.vy = kart.VerticalSpeed;
                s.life = i == 2 ? 0.22f : 0.27f;
                s.drag = s.grav = 0;
                s.floor = -1e4f;
                s.shape = Zap;
                s.mode = SpinMode;
                s.spin = side * (i == 2 ? -3 : 2.5f);
                s.size = sz;
                s.aspect = 1;
                s.fold = 0;
                s.colA = s.colB = color;
                s.glow = i == 0 ? 0.6f : i == 1 ? 0.85f : 1;
                s.seed = seed;
                Emit(s);
            }
            int n = Round(9 * Math.Max(qMul, 0.75));
            for (int i = 0; i < n; i++)
            {
                float angle = (-0.25f + i / (float)(n - 1) * 1.5f) * Mathf.PI, speed = R(6.5f, 9);
                float outward = Mathf.Cos(angle) * speed * side, up = Mathf.Sin(angle) * speed;
                var s = Base(p, kart, 0.92f);
                s.vx += left.x * outward - headingForward.x * R(0.5f, 2);
                s.vz += left.z * outward - headingForward.z * R(0.5f, 2);
                s.vy = up + kart.VerticalSpeed;
                s.life = R(0.22f, 0.3f);
                s.drag = 5;
                s.grav = 5;
                s.floor = Ground(kart) + 0.02f;
                s.shape = Sliver;
                s.mode = StreakMode;
                s.spin = 0;
                s.size = R(0.24f, 0.32f) + tier * 0.03f;
                s.aspect = 0.2f;
                s.fold = 0;
                s.colA = s.colB = tierA[tier];
                s.glow = 0.75f;
                Emit(s);
            }
        }

        private void Debris(KagitKartVehicle kart, Vector3 wheel, int side, bool sandy)
        {
            var s = Base(wheel + new Vector3(R(-0.1f, 0.1f), 0, R(-0.1f, 0.1f)), kart, 0.7f);
            var state = states[current];
            state.n++;
            float back = R(0.8f, 2.8f), lateral = side * R(0.3f, 2.4f);
            s.vx += -headingForward.x * back - headingRight.x * lateral;
            s.vz += -headingForward.z * back - headingRight.z * lateral;
            s.vy = R(3, 5.8f);
            s.life = R(0.6f, 0.95f);
            s.drag = 1.6f;
            s.grav = 15;
            s.floor = Ground(kart) + 0.02f;
            s.mode = TumbleMode;
            s.spin = R(8, 16);
            if (state.n % 7 == 0)
            {
                s.vy = R(2.2f, 3.4f);
                s.life = R(1.3f, 1.7f);
                s.drag = 1.1f;
                s.shape = Rect;
                s.size = R(0.16f, 0.24f);
                s.aspect = R(0.55f, 0.8f);
                s.fold = R(0.6f, 1.1f);
                s.colA = state.n % 2 != 0 ? paper : offroad[0];
                s.colB = state.n % 2 != 0 ? paperDark : offroadBack;
            }
            else if (!sandy && state.n % 5 < 3)
            {
                s.shape = Sliver;
                s.size = R(0.13f, 0.2f);
                s.aspect = R(0.24f, 0.34f);
                s.fold = R(0.3f, 0.8f);
                s.colA = offroad[state.n % 3];
                s.colB = offroadBack;
            }
            else
            {
                s.shape = state.n % 2 != 0 ? Rect : Tri;
                s.size = R(0.07f, 0.11f);
                s.aspect = R(0.5f, 1);
                s.fold = R(0.2f, 0.9f);
                s.colA = paper;
                s.colB = paperDark;
            }
            s.glow = dark ? 0.35f : 0.3f;
            Emit(s);
        }

        private void Streak(KagitKartVehicle kart, bool big)
        {
            var state = states[current];
            state.side = (state.side + 1) % 4;
            int side = state.side % 2 != 0 ? 1 : -1;
            float ahead = R(-0.5f, 3), lateral = side * R(0.95f, 1.7f), height = R(0.25f, 1.5f);
            var s = Base(position + headingForward * ahead + headingRight * lateral + Vector3.up * height, kart, 0.3f);
            s.vy = R(-0.2f, 0.3f);
            s.life = big ? R(0.32f, 0.45f) : R(0.22f, 0.32f);
            s.drag = 0.5f;
            s.grav = 0;
            s.floor = -1e4f;
            s.shape = Stroke;
            s.mode = StreakMode;
            s.size = big ? R(1.2f, 1.6f) : R(0.7f, 1.1f);
            s.aspect = R(0.035f, 0.05f);
            s.spin = s.fold = 0;
            s.colA = s.colB = inkish;
            s.glow = 1;
            Emit(s);
        }

        private void Scrape(KagitKartVehicle kart, bool burst)
        {
            int side = kart.TrackInfo.Lateral == 0 ? 1 : Math.Sign(kart.TrackInfo.Lateral);
            var normal = kart.TrackInfo.Right * side;
            float along = R(-0.6f, 0.8f);
            var s = Base(new Vector3(position.x + normal.x * 0.95f + headingForward.x * along, position.y + R(0.2f, 0.6f), position.z + normal.z * 0.95f + headingForward.z * along), kart, burst ? 0.3f : 0.4f);
            float away = burst ? R(2, 6) : R(0.5f, 2.5f), back = R(0, 2.5f);
            s.vx += -normal.x * away - headingForward.x * back;
            s.vz += -normal.z * away - headingForward.z * back;
            s.vy = burst ? R(2.5f, 6) : R(1.2f, 3.2f);
            s.life = R(0.22f, 0.4f);
            s.drag = 3;
            s.grav = 16;
            s.floor = Ground(kart) + 0.02f;
            s.spin = R(16, 30);
            int n = ++states[current].n;
            if (n % 10 < (burst ? 5 : 7))
            {
                s.shape = Sliver;
                s.mode = StreakMode;
                s.size = burst ? R(0.18f, 0.28f) : R(0.12f, 0.2f);
                s.aspect = 0.11f;
                s.fold = 0;
                s.colA = s.colB = graphite;
                s.glow = 0.35f;
            }
            else
            {
                s.shape = Tri;
                s.mode = TumbleMode;
                s.size = burst ? R(0.07f, 0.1f) : R(0.05f, 0.08f);
                s.fold = 0.9f;
                s.colA = fleck;
                s.colB = sheen;
                s.glow = 0.85f;
            }
            Emit(s);
        }

        private void PuffAt(KagitKartVehicle kart, Vector3 p, float vx, float vz, float vy, float size, float life, Color color)
        {
            var s = Base(p, kart, 0.4f);
            s.vx += vx;
            s.vz += vz;
            s.vy = vy;
            s.life = life;
            s.drag = 4.5f;
            s.grav = -0.5f;
            s.floor = -1e4f;
            s.shape = Puff;
            s.mode = SpinMode;
            s.spin = R(-2, 2);
            s.size = size;
            s.aspect = 1;
            s.fold = 0;
            s.colA = s.colB = color;
            s.glow = 0.55f;
            s.grow = 1;
            Emit(s);
        }

        private void LandPuff(KagitKartVehicle kart, float air)
        {
            if (air < 0.22f)
            {
                return;
            }
            float amount = Mathf.Min(air, 1.5f);
            int n = air < 0.45f ? 2 : Math.Max(4, Math.Min(12, Round((3 + air * 6) * qMul)));
            Color color = kart.Surface is "offroad" or "out" ? offroad[2] : puff;
            float offset = current * 0.37f % 1;
            for (int i = 0; i < n; i++)
            {
                float angle = (i + offset) / n * Mathf.PI * 2, dx = Mathf.Cos(angle), dz = Mathf.Sin(angle);
                float speed = R(1.6f, 2.6f + amount * 2);
                PuffAt(kart, new Vector3(position.x + dx * 0.9f, kart.Position.y + 0.12f, position.z + dz * 0.9f), dx * speed, dz * speed, R(0.3f, 0.9f), R(0.2f, 0.28f) * (1 + amount * 0.35f), 0.45f + 0.15f * amount, color);
            }
        }

        private void HitBurst(KagitKartVehicle kart, string cause)
        {
            if (cause == "burnout")
            {
                Vector3 p = Anchor(2);
                for (int i = 0; i < 5; i++)
                {
                    var s = Base(p + new Vector3(R(-0.15f, 0.15f), R(0, 0.2f), R(-0.15f, 0.15f)), kart, 0);
                    s.vx = -headingForward.x * R(0.4f, 1.2f);
                    s.vz = -headingForward.z * R(0.4f, 1.2f);
                    s.vy = R(0.6f, 1.4f);
                    s.life = R(0.8f, 1.1f);
                    s.drag = 2.5f;
                    s.grav = -2.2f;
                    s.floor = -1e4f;
                    s.shape = Puff;
                    s.mode = SpinMode;
                    s.spin = R(-1.5f, 1.5f);
                    s.size = R(0.25f, 0.4f);
                    s.fold = 0;
                    s.colA = s.colB = smoke[i % 2];
                    s.glow = 0.2f;
                    s.grow = 1;
                    Emit(s);
                }
                return;
            }
            float offset = current * 0.23f % 1;
            for (int i = 0; i < 7; i++)
            {
                float angle = (i + offset) / 7 * Mathf.PI * 2 + R(-0.2f, 0.2f), dx = Mathf.Cos(angle), dz = Mathf.Sin(angle);
                var s = Base(position + new Vector3(dx * 0.3f, 1.3f, dz * 0.3f), kart, 0.5f);
                float speed = R(2.4f, 3.6f);
                s.vx += dx * speed;
                s.vz += dz * speed;
                s.vy = R(2.5f, 4.2f);
                s.life = R(0.8f, 1.05f);
                s.drag = 2.2f;
                s.grav = 7;
                s.floor = Ground(kart) + 0.02f;
                s.shape = Star;
                s.mode = FlipMode;
                s.spin = R(9, 15) * (i % 2 != 0 ? 1 : -1);
                s.size = R(0.26f, 0.34f);
                s.fold = 0.4f;
                s.aspect = 1;
                s.colA = stars[i % stars.Length];
                s.colB = starBack;
                s.glow = 0.55f;
                Emit(s);
            }
            for (int i = 0; i < 5; i++)
            {
                var s = Base(position + Vector3.up * 1.1f, kart, 0.5f);
                s.vx += R(-2, 2);
                s.vz += R(-2, 2);
                s.vy = R(2, 4);
                s.life = R(0.7f, 1);
                s.drag = 2.5f;
                s.grav = 8;
                s.floor = Ground(kart) + 0.02f;
                s.shape = Rect;
                s.size = R(0.05f, 0.08f);
                s.aspect = 0.6f;
                s.fold = R(0.3f, 0.8f);
                s.spin = R(8, 14);
                s.colA = confetti[(i + current) % confetti.Length];
                s.colB = Color.Lerp(s.colA, fleck, 0.5f);
                s.glow = 0.2f;
                Emit(s);
            }
        }

        private void ConfettiPiece(KagitKartVehicle kart, int index, Vector3 p, Vector3 velocity, float life)
        {
            var s = Base(p, null, 0);
            s.vx = velocity.x;
            s.vy = velocity.y;
            s.vz = velocity.z;
            s.life = life;
            s.drag = 1.3f;
            s.grav = 4.5f;
            s.flutter = R(0.2f, 0.45f);
            s.floor = kart.Grounded || !kart.TrackInfo.HasGround ? kart.Position.y + 0.02f : kart.TrackInfo.GroundY + 0.02f;
            int pick = index % 20;
            s.shape = pick < 14 ? Rect : pick < 17 ? Tri : Star;
            s.size = s.shape == Star ? R(0.14f, 0.2f) : R(0.1f, 0.17f);
            s.aspect = s.shape == Rect ? R(0.45f, 0.7f) : 1;
            s.fold = R(0.3f, 0.9f);
            s.spin = R(5, 11);
            s.colA = confetti[index % confetti.Length];
            s.colB = Color.Lerp(s.colA, fleck, 0.45f);
            s.glow = 0.3f;
            Emit(s);
        }

        private void FinishConfetti(KagitKartVehicle kart)
        {
            int count = Round(28 * qMul);
            for (int side = 1; side >= -1; side -= 2)
            {
                Vector3 p = position - headingRight * side * 2.2f + Vector3.up * 0.6f;
                for (int i = 0; i < count; i++)
                {
                    float inward = R(0.5f, 3);
                    ConfettiPiece(kart, i + (side > 0 ? 0 : 7), p, new Vector3(kart.Velocity.x * 0.9f + headingRight.x * side * inward + R(-0.8f, 0.8f), R(6, 10.5f), kart.Velocity.z * 0.9f + headingRight.z * side * inward + R(-0.8f, 0.8f)), R(2.2f, 3.2f));
                }
            }
            states[current].confetti = 2.6;
        }

        private void ConfettiShower(KagitKartVehicle kart, KartFx state, float dt)
        {
            state.confetti -= dt;
            state.conAcc += 90 * qMul * dt;
            int count = Math.Min(8, (int)Math.Floor(state.conAcc));
            state.conAcc -= Math.Floor(state.conAcc);
            double fraction = (kart.TrackInfo.Distance + Math.Max(0, kart.Speed) * 0.35) / session.Track.Length;
            fraction = (fraction % 1 + 1) % 1;
            var point = session.Track.SampleAt((float)fraction);
            Vector3 center = point.Position + point.Right * (float)kart.TrackInfo.Lateral * 0.5f;
            for (; count > 0; count--)
            {
                float radius = (float)Math.Sqrt(random.Next()) * 5, angle = (float)random.Next() * Mathf.PI * 2;
                state.n++;
                ConfettiPiece(kart, state.n, new Vector3(center.x + Mathf.Cos(angle) * radius, point.Position.y + R(2.6f, 5.2f), center.z + Mathf.Sin(angle) * radius), new Vector3(kart.Velocity.x * 0.8f + R(-1, 1), R(0, 1.5f), kart.Velocity.z * 0.8f + R(-1, 1)), R(2.4f, 3.4f));
            }
        }

        private void ProcessQueue()
        {
            for (int i = 0; i < qn; i++)
            {
                var cue = queue[i];
                Frame(cue.kart);
                var kart = session.Karts[current];
                int tier = Mathf.Clamp((int)cue.value, 1, 3);
                switch (cue.type)
                {
                    case "tier":
                        a = Anchor(0);
                        b = Anchor(1);
                        for (int j = 0; j < Round((14 + tier * 2) * Math.Max(qMul, 0.75)); j++)
                        {
                            Spark(kart, j % 2 != 0 ? a : b, tier, true, 1.3f);
                        }
                        TierBurst(kart, a, tier, 1);
                        TierBurst(kart, b, tier, -1);
                        visuals[current].Flash(tierA[tier]);
                        break;
                    case "release":
                        a = Anchor(2);
                        for (int j = 0; j < 8 + tier * 2; j++)
                        {
                            Spark(kart, a, tier, true);
                        }
                        break;
                    case "draftBoost":
                        for (int j = 0; j < 14; j++)
                        {
                            Streak(kart, true);
                        }
                        break;
                    case "land":
                        LandPuff(kart, cue.value);
                        break;
                    case "wall":
                        for (int j = 0; j < Math.Min(28, Round((12 + cue.value * 0.6f) * qMul)); j++)
                        {
                            Scrape(kart, true);
                        }
                        break;
                    case "hit":
                        HitBurst(kart, cue.cause);
                        break;
                    case "respawn":
                        cranes.Summon(kart, false, kart.IsPlayer, camera);
                        break;
                    case "finish":
                        FinishConfetti(kart);
                        break;
                }
            }
            qn = 0;
        }

        private void Flames(KagitKartVehicle kart, KartFx state)
        {
            if (kart.BoostTime <= 0)
            {
                return;
            }
            string source = kart.BoostSource;
            float strength = Math.Max(0.5f, kart.BoostStrength == 0 ? 1 : kart.BoostStrength);
            Color outer = source == "drift" ? tierA[state.flameTier] : source == "item" ? flameRocket : source == "draft" ? flameDraft : flameOrange;
            float kick = 1 + 0.45f * (float)Math.Exp(-state.boostAge * 14);
            float envelope = Smooth(0, 0.05, state.boostAge) * Smooth(0, 0.18, kart.BoostTime);
            if (envelope <= 0.01f)
            {
                return;
            }
            int cycle = (int)Math.Floor(time * (reducedMotion ? 7 : 14) + current * 0.37);
            float wobble = Wobble[(cycle * 5 + current) & 7];
            float length = (0.75f + 0.55f * strength) * kick * envelope * wobble;
            float width = (0.26f + 0.1f * strength) * (0.85f + 0.15f * kick) * envelope;
            a = Anchor(2);
            b = (cameraPosition - a).normalized;
            float behind = Smooth(0.45, 0.95, -Vector3.Dot(forward, b));
            c = (-forward + Vector3.up * (0.2f + 1.25f * behind)).normalized;
            float offset = 2.5f * Math.Max(1, camera.pixelHeight / 720f);
            Color shadow = outer * 0.55f;
            float facing = Smooth(0.3, 0.85, -Vector3.Dot(forward, b));
            if (facing > 0.02f)
            {
                float radius = width * (source == "drift" ? 1.6f : 1.1f) * facing * (0.85f + 0.15f * kick) * wobble;
                a -= forward * 0.12f;
                BurstLayer(0, radius, outer, 1, null, cycle, offset);
                BurstLayer(1, radius * 0.68f, flameMid, 0, shadow, cycle, offset);
                BurstLayer(2, radius * 0.4f, flameInner, 0, flameShadow, cycle, offset);
                a += forward * 0.12f;
            }
            a += c * 0.05f;
            FlameLayer(0, 1, width, outer, 1, null, cycle, length, offset);
            FlameLayer(1, 0.8f, width * 0.72f, flameMid, 0, shadow, cycle, length, offset);
            FlameLayer(2, 0.56f, width * 0.45f, flameInner, 0, flameShadow, cycle, length, offset);
        }

        private void BurstLayer(int layer, float radius, Color color, float outline, Color? shadow, int cycle, float offset)
        {
            int variant = Perm[(cycle + layer * 5 + current + 1) & 7];
            if (shadow.HasValue)
            {
                AddCard(a, a, radius, 2, shadow.Value, 1, variant, 0, 0.8f, offset);
            }
            AddCard(a, a, radius, 2, color, 1, variant, outline);
        }

        private void FlameLayer(int layer, float fraction, float width, Color color, float outline, Color? shadow, int cycle, float length, float offset)
        {
            int variant = Perm[(cycle + layer * 3 + current) & 7];
            b = a + c * length * fraction;
            if (shadow.HasValue)
            {
                AddCard(a, b, width, 0, shadow.Value, 1, variant, 0, 0.8f, offset);
            }
            AddCard(a, b, width, 0, color, 1, variant, outline);
        }

        /// <summary>推进原作纸艺表现；暂停传零仍绘制，数量随原作品质与距离减半。</summary>
        /// <param name="frameDt">有限非负帧时间，源码限制为最多 0.1 秒。</param>
        /// <param name="viewCamera">比赛摄像机。</param>
        /// <param name="reduceMotion">减少动态禁用速度线并降低火焰切换频率。</param>
        /// <param name="qualityScale">原作 low/minimal 为 0.5，medium 为 0.75，其余为 1。</param>
        /// <param name="chaseView">是否处于玩家追尾视角；其它视角立即切掉速度线。</param>
        public void Advance(float frameDt, Camera viewCamera, bool reduceMotion = false, float qualityScale = 1, bool chaseView = true)
        {
            if (!float.IsFinite(frameDt) || frameDt < 0 || viewCamera == null || !float.IsFinite(qualityScale) || qualityScale <= 0)
            {
                throw new ArgumentException("比赛纸艺表现帧参数无效。");
            }
            if (session == null)
            {
                return;
            }
            float dt = Math.Min(frameDt, 0.1f);
            time += dt;
            camera = viewCamera;
            cameraPosition = Source(camera.transform.position);
            qMul = qualityScale;
            reducedMotion = reduceMotion;
            bitsBlock.SetFloat("uTime", (float)time);
            bitsBlock.SetVector("uSunDir", Source(-sun.transform.forward));
            bitsBlock.SetVector("uSunCol", sun.color.linear * sun.intensity);
            foreach (var block in cameraBlocks)
            {
                block.SetVector("_CameraPosition", cameraPosition);
                block.SetVector("_CameraRight", Source(camera.transform.right));
                block.SetVector("_CameraUp", Source(camera.transform.up));
                block.SetVector("_CameraBack", Source(-camera.transform.forward));
            }
            cardsBlock.SetVector("uRes", new Vector4(camera.pixelWidth, camera.pixelHeight, 0, 0));
            cranes.ChaseView = chaseView;
            ProcessQueue();
            cardCount = 0;
            for (int i = 0; i < session.Karts.Count; i++)
            {
                var kart = session.Karts[i];
                var state = states[i];
                if (dt > 0)
                {
                    state.boostAge += dt;
                }
                float distance2 = (cameraPosition - kart.Position).sqrMagnitude;
                bool near = kart.IsPlayer || distance2 < 90 * 90;
                Frame(i);
                if (!kart.Rescuing && !cranes.Has(kart) && (kart.IsPlayer || distance2 < 150 * 150))
                {
                    bool soon = kart.Surface == "out" && kart.Grounded && kart.OutTime > tuning.outRespawn - 0.65f;
                    bool falling = !kart.Grounded && !kart.TrackInfo.HasGround && kart.VerticalSpeed < -1.5f && kart.Position.y < kart.TrackInfo.GroundY - 0.15f;
                    if (soon || falling)
                    {
                        cranes.Summon(kart, true, kart.IsPlayer, camera);
                    }
                }
                if (distance2 < 150 * 150)
                {
                    Flames(kart, state);
                }
                if (state.confetti > 0 && dt > 0)
                {
                    ConfettiShower(kart, state, dt);
                }
                if (dt <= 0 || !near || kart.Rescuing)
                {
                    continue;
                }
                float lod = (kart.IsPlayer || distance2 < 45 * 45 ? 1 : 0.5f) * qMul;
                if (kart.Drifting && kart.Grounded)
                {
                    int tier = kart.DriftTier;
                    int rate = tier == 0 ? 10 : tier == 1 ? 64 : tier == 2 ? 80 : 96;
                    state.spark += rate * (double)dt * lod;
                    int n = Math.Min(8, (int)Math.Floor(state.spark));
                    state.spark -= Math.Floor(state.spark);
                    a = Anchor(0);
                    b = Anchor(1);
                    for (; n > 0; n--)
                    {
                        Spark(kart, n % 2 != 0 ? a : b, tier, false);
                    }
                }
                else
                {
                    state.spark = 0;
                }
                if (kart.Grounded && (kart.Surface is "offroad" or "out" or "sand") && Math.Abs(kart.Speed) > 4)
                {
                    state.debris += 110 * Math.Clamp(Math.Abs(kart.Speed) / (kart.TopSpeed == 0 ? 25 : kart.TopSpeed), 0.6, 1) * dt * lod;
                    int n = Math.Min(6, (int)Math.Floor(state.debris));
                    state.debris -= Math.Floor(state.debris);
                    a = Anchor(0);
                    b = Anchor(1);
                    for (; n > 0; n--)
                    {
                        Debris(kart, n % 2 != 0 ? a : b, n % 2 != 0 ? 1 : -1, kart.Surface == "sand");
                    }
                }
                else
                {
                    state.debris = 0;
                }
                float charge = session.Collisions.GetDraftCharge(i);
                if (charge > 0 && kart.Speed > 8 && (kart.IsPlayer || distance2 < 45 * 45))
                {
                    state.streak += (20 + 70 * charge) * (double)dt * lod;
                    int n = Math.Min(6, (int)Math.Floor(state.streak));
                    state.streak -= Math.Floor(state.streak);
                    for (; n > 0; n--)
                    {
                        Streak(kart, false);
                    }
                }
                else
                {
                    state.streak = 0;
                }
                if (kart.WallContact && kart.Grounded && Math.Abs(kart.Speed) > 4 && distance2 < 60 * 60)
                {
                    state.scrape += 55 * Math.Clamp(Math.Abs(kart.Speed) / 20, 0.3, 1.2) * dt * lod;
                    int n = Math.Min(6, (int)Math.Floor(state.scrape));
                    state.scrape -= Math.Floor(state.scrape);
                    for (; n > 0; n--)
                    {
                        Scrape(kart, false);
                    }
                }
                else
                {
                    state.scrape = 0;
                }
            }
            cranes.Advance(dt, camera, this, inkish, chaseView);
            if (dirtyHi >= 0)
            {
                int from = wrapped ? 0 : dirtyLo, count = wrapped ? Capacity : dirtyHi - dirtyLo + 1;
                bitBuffer.SetData(bitStates, from, from, count);
                dirtyLo = Capacity;
                dirtyHi = -1;
                wrapped = false;
            }
            var bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            if (time < lastDeath)
            {
                Graphics.DrawMeshInstancedProcedural(bitsShape, 0, bitsMaterial, bounds, highest, bitsBlock, ShadowCastingMode.Off, false, gameObject.layer, camera);
            }
            if (cardCount > 0)
            {
                cardBuffer.SetData(cardStates, 0, 0, cardCount);
                Graphics.DrawMeshInstancedProcedural(cardsShape, 0, cardsMaterial, bounds, cardCount, cardsBlock, ShadowCastingMode.Off, false, gameObject.layer, camera);
            }
            var player = session.Player;
            bool cut = reduceMotion || !chaseView || player.Controls.LookBack && !player.Autopilot;
            float target = !cut && player.BoostTime > 0 && session.Race.State != KagitKartRaceState.Done ? Math.Min(1, 0.55f + 0.45f * (player.BoostStrength == 0 ? 1 : player.BoostStrength)) * Smooth(0, 0.15, player.BoostTime) : 0;
            lineIntensity = cut ? 0 : target + (lineIntensity - target) * Math.Pow(2, -dt / (target > lineIntensity ? 0.05 : 0.16));
            if (lineIntensity < 0.015 && target == 0)
            {
                lineIntensity = 0;
            }
            if (lineIntensity > 0)
            {
                linesBlock.SetFloat("uTime", (float)time);
                linesBlock.SetFloat("uIntensity", (float)lineIntensity);
                linesBlock.SetFloat("uAspect", camera.aspect);
                Graphics.DrawMeshInstancedProcedural(linesShape, 0, linesMaterial, bounds, 34, linesBlock, ShadowCastingMode.Off, false, gameObject.layer, camera);
            }
        }

        internal static Vector3 Source(Vector3 p) => new(-p.x, p.y, p.z);

        internal static float Smooth(double min, double max, double value)
        {
            double t = Math.Clamp((value - min) / (max - min), 0, 1);
            return (float)(t * t * (3 - 2 * t));
        }

        private static int Round(double value) => (int)Math.Floor(value + 0.5);

        private static Color[] Colors(params string[] colors) => Array.ConvertAll(colors, Hex);

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out var color);
            return color.linear;
        }

        /// 退订并清空本场效果，GPU 缓冲由共享实例保留。
        public void Unbind()
        {
            if (session != null)
            {
                foreach (var kart in session.Karts)
                {
                    kart.Signaled -= OnVehicleSignal;
                }
                session.Race.Signaled -= OnRaceSignal;
            }
            session = null;
            time = 0;
            lastDeath = -1;
            lineIntensity = 0;
            qn = current = head = highest = spawned = cardCount = 0;
            wrapped = false;
            dirtyLo = Capacity;
            dirtyHi = -1;
        }

        private void OnDestroy()
        {
            Unbind();
            bitBuffer?.Dispose();
            cardBuffer?.Dispose();
        }
    }
}
