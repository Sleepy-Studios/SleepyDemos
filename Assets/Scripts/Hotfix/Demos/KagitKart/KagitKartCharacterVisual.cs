using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hotfix.KagitKart
{
    /// 原作 animate.js 的八角色共用纸偶表现；宿主提供帧时间，比赛与菜单共用。
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class KagitKartCharacterVisual : MonoBehaviour
    {
        [SerializeField] private string rigDefinition;
        [SerializeField] private float rosterSeed;

        private const float Tau = Mathf.PI * 2;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly Vector3 Center = new(0, 0.6f, 0);
        private static readonly Color FoilA = new Color(1, 241 / 255f, 184 / 255f).linear;
        private static readonly Color FoilB = new Color(191 / 255f, 227 / 255f, 1).linear;
        private SkinnedMeshRenderer skin;
        private Transform[] bones;
        private Vector3[] rest;
        private Definition definition;
        private MaterialPropertyBlock block;
        private KagitKartVehicle kart;
        private KagitKartKartTuning tuning;
        private Material ghostMaterial;
        private float seed;
        private bool ghost;
        private bool initialized;
        private float t, angF, angR, propAng;
        private float steer, drift, tier, boost, lookBack, ink, hang, speedN, throttle;
        private float sq, sqV, air, flat, flatV, wobble;
        private bool wasGrounded = true;
        private bool spinning;
        private string spinCause, forced;
        private int spins;
        private float spinTotal = 1, spinP, spinDir = 1;
        private float trickT = -1, trickDir = 1;
        private float cheer, sad, dizzy, idle;
        private float flashT, flashDur;
        private Color flashColor;
        private float ghostSpeed, ghostSteer, prevYaw;
        private Vector3 prevPosition;
        private bool havePrevious;

        [Serializable]
        private sealed class Definition
        {
            public Chain[] chains;
            public int prop = -1;
            public int peri = -1;
        }

        [Serializable]
        private sealed class Chain
        {
            public int[] bones;
            public string kind;
            public float side;
            public float phase;
        }

        private struct Kind
        {
            public float stream, sway, freq, flutter, fFreq, droop, splay, perk, drift, wag, wave, hang;
            public bool roll;
        }

        private static Kind KindFor(string name)
        {
            return name switch
            {
                "roundEar" => new Kind { stream = -0.18f, sway = 0.03f, roll = true, freq = 1.5f, flutter = 0.05f, fFreq = 12, droop = -0.35f, splay = 0.3f, perk = 0.1f, drift = 0.1f },
                "tuft" => new Kind { stream = -0.7f, sway = 0.05f, roll = true, freq = 1.8f, flutter = 0.14f, fFreq = 13, droop = -0.6f, splay = 0.4f, perk = 0.2f, drift = 0.2f },
                "longEar" => new Kind { stream = -1.35f, sway = 0.08f, roll = true, freq = 1.6f, flutter = 0.18f, fFreq = 11, droop = -1.25f, splay = 0.45f, perk = 0.12f, drift = 0.3f },
                "tail" => new Kind { stream = -0.3f, sway = 0.1f, freq = 2.6f, flutter = 0.14f, fFreq = 9, droop = -0.65f, perk = 0.2f, wag = 0.55f, drift = 0.45f },
                "catTail" => new Kind { stream = -0.3f, sway = 0.16f, roll = true, freq = 1.3f, flutter = 0.06f, fFreq = 7, droop = -0.55f, perk = 0.1f, wag = 0.4f, drift = 0.3f },
                "scarf" => new Kind { hang = -0.95f, sway = 0.2f, freq = 3, flutter = 0.3f, fFreq = 17, droop = -0.2f, drift = 0.5f },
                "tentacle" => new Kind { stream = 0.05f, sway = 0.3f, freq = 2.2f, flutter = 0.1f, fFreq = 6, droop = -0.2f, perk = 0.2f, wag = 0.3f, drift = 0.35f, wave = 0.16f },
                _ => new Kind { stream = -0.55f, sway = 0.05f, roll = true, freq = 2.1f, flutter = 0.1f, fFreq = 15, droop = -0.75f, splay = 0.35f, perk = 0.18f, drift = 0.22f }
            };
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (bones != null)
            {
                return;
            }
            skin = GetComponent<SkinnedMeshRenderer>();
            bones = skin.bones;
            definition = JsonUtility.FromJson<Definition>(rigDefinition);
            rest = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                rest[i] = bones[i].localPosition;
            }
            block = new MaterialPropertyBlock();
            seed = rosterSeed;
            t = seed * 7.31f % 10;
        }

        /// <summary>绑定本场赛车及原作打转配置；null 赛车用于菜单或幽灵，仅调用一次。</summary>
        /// <param name="vehicle">规则赛车；菜单和幽灵为 null。</param>
        /// <param name="kartTuning">原作 kart 配置，菜单可为 null。</param>
        /// <param name="seedOffset">比赛为赛车索引乘 0.37；菜单为零。</param>
        /// <param name="isGhost">是否采用原作描图纸幽灵材质和运动推导。</param>
        public void Bind(KagitKartVehicle vehicle, KagitKartKartTuning kartTuning, float seedOffset = 0, bool isGhost = false)
        {
            Initialize();
            if (kart != null)
            {
                kart.Signaled -= OnSignal;
            }
            kart = vehicle;
            tuning = kartTuning;
            ghost = isGhost;
            seed = rosterSeed + seedOffset + (ghost ? 0.5f : 0);
            t = seed * 7.31f % 10;
            if (kart != null)
            {
                kart.Signaled += OnSignal;
            }
            if (ghost)
            {
                ghostMaterial = new Material(skin.sharedMaterial);
                ghostMaterial.SetVector("_BaseColor", new Color(213 / 255f, 228 / 255f, 1, 0.42f).linear);
                ghostMaterial.SetFloat("_UseHalftone", 0);
                ghostMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                ghostMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                ghostMaterial.SetFloat("_ZWrite", 1);
                ghostMaterial.renderQueue = (int)RenderQueue.Transparent;
                skin.sharedMaterial = ghostMaterial;
                skin.shadowCastingMode = ShadowCastingMode.Off;
            }
            Advance(0);
        }

        /// <summary>返回原作挂点，随所在骨骼运动；不存在时返回 null。</summary>
        /// <param name="name">wheelRL、wheelRR、exhaust 或 head。</param>
        /// <returns>挂点 Transform。</returns>
        public Transform GetAnchor(string name)
        {
            Initialize();
            foreach (var bone in bones)
            {
                var anchor = bone.Find("anchor." + name);
                if (anchor != null)
                {
                    return anchor;
                }
            }
            return null;
        }

        /// <summary>覆盖自动情绪，非法值或 null 恢复菜单待机/比赛结果自动情绪。</summary>
        /// <param name="name">cheer、sad、dizzy、idle 或 null。</param>
        public void SetEmotion(string name)
        {
            forced = name is "cheer" or "sad" or "dizzy" or "idle" ? name : null;
        }

        /// <summary>施加原作碰撞晃动；碰撞调度方传入相对冲量。</summary>
        /// <param name="impulse">非负碰撞冲量。</param>
        public void Bump(float impulse)
        {
            wobble = Mathf.Min(1, wobble + 0.25f + impulse * 0.06f);
        }

        /// <summary>施加短暂发光，使用实例属性块，不污染八角色共享材质。</summary>
        /// <param name="color">线性发光颜色。</param>
        /// <param name="duration">持续秒数，最小 0.01。</param>
        public void Flash(Color color, float duration = 0.08f)
        {
            flashColor = color;
            flashDur = Mathf.Max(0.01f, duration);
            flashT = flashDur;
        }

        /// <summary>采用当前舞台/赛道的纸材质环境光与雾；只覆盖此实例属性。</summary>
        /// <param name="paperMaterial">当前环境的 KagitKart/Paper 材质。</param>
        public void SetEnvironment(Material paperMaterial)
        {
            Initialize();
            skin.GetPropertyBlock(block);
            foreach (string field in new[] { "_AmbientColor", "_FogColor", "_FogRange" })
            {
                block.SetVector(field, paperMaterial.GetVector(field));
            }
            skin.SetPropertyBlock(block);
        }

        private void OnSignal(KagitKartVehicle vehicle, KagitKartVehicleSignal signal)
        {
            if (signal == KagitKartVehicleSignal.Trick)
            {
                trickT = 0;
                trickDir = steer >= 0 ? -1 : 1;
            }
            else if (signal == KagitKartVehicleSignal.WallHit)
            {
                wobble = Mathf.Min(1, wobble + 0.6f);
            }
        }

        /// <summary>逐帧应用原作姿态；暂停传零，单帧按源码限制至 0.1 秒。</summary>
        /// <param name="dt">有限非负帧时间；初始化后零时间不修改姿态。</param>
        /// <param name="reducedMotion">原作减少动态将振幅乘 0.4。</param>
        /// <param name="topSpeed">菜单幽灵的速度归一化上限，赛车直接使用 BaseTop。</param>
        public void Advance(float dt, bool reducedMotion = false, float topSpeed = 25)
        {
            if (!float.IsFinite(dt) || dt < 0 || !float.IsFinite(topSpeed) || topSpeed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dt));
            }
            Initialize();
            if (dt <= 0 && initialized)
            {
                return;
            }
            initialized = true;
            dt = Mathf.Min(dt, 0.1f);
            t += dt;
            float rm = reducedMotion ? 0.4f : 1;
            float speed = kart?.Speed ?? 0;
            float steerIn = kart?.Controls.Steer ?? 0;
            bool grounded = kart?.Grounded ?? true;
            bool driftActive = kart?.Drifting ?? false;
            float driftDir = kart?.DriftDirection ?? 0;
            float spin = kart?.SpinTime ?? 0;
            if (kart == null && ghost)
            {
                Vector3 position = KagitKartVehicle.ToUnity(transform.position);
                Vector3 forward = KagitKartVehicle.ToUnity(transform.forward);
                float yawNow = Mathf.Atan2(forward.x, forward.z);
                if (havePrevious && dt > 0)
                {
                    float vs = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(prevPosition.x, prevPosition.z)) / dt;
                    float dy = yawNow - prevYaw;
                    dy = Mathf.Atan2(Mathf.Sin(dy), Mathf.Cos(dy));
                    ghostSpeed = Damp(ghostSpeed, vs < 80 ? vs : ghostSpeed, 0.1f, dt);
                    ghostSteer = Damp(ghostSteer, Mathf.Clamp(-dy / dt / 1.6f, -1, 1), 0.12f, dt);
                }
                prevPosition = position;
                prevYaw = yawNow;
                havePrevious = true;
                speed = ghostSpeed;
                steerIn = ghostSteer;
            }
            bool driving = kart != null || ghost;
            float sN = Mathf.Clamp(Mathf.Abs(speed) / (kart?.BaseTop ?? topSpeed), 0, 1.3f);
            speedN = Damp(speedN, sN, 0.2f, dt);
            float sS = speedN;
            steer = Damp(steer, steerIn, 0.07f, dt);
            drift = Damp(drift, driftActive ? driftDir : 0, 0.09f, dt);
            tier = Damp(tier, kart?.DriftTier ?? 0, 0.1f, dt);
            bool boosting = (kart?.BoostTime ?? 0) > 0;
            boost = Damp(boost, boosting ? 1 : 0, boosting ? 0.05f : 0.2f, dt);
            lookBack = Damp(lookBack, kart != null && kart.Controls.LookBack ? 1 : 0, 0.07f, dt);
            ink = Damp(ink, (kart?.InkTime ?? 0) > 0 ? 1 : 0, 0.15f, dt);
            hang = Damp(hang, kart != null && kart.Rescuing ? 1 : 0, 0.15f, dt);
            throttle = Damp(throttle, kart?.Controls.Throttle ?? 0, 0.1f, dt);
            wobble = Damp(wobble, 0, 0.3f, dt);
            string target = forced ?? (kart != null ? (kart.Finished ? (kart.Place <= 3 ? "cheer" : "sad") : null) : (ghost ? null : "idle"));
            cheer = Damp(cheer, target == "cheer" ? 1 : 0, 0.18f, dt);
            sad = Damp(sad, target == "sad" ? 1 : 0, 0.18f, dt);
            dizzy = Damp(dizzy, target == "dizzy" ? 1 : 0, 0.18f, dt);
            idle = Damp(idle, target == "idle" ? 1 : 0, 0.18f, dt);

            angF += speed * dt / 0.27f;
            angR += speed * dt / 0.31f;
            if (Mathf.Abs(angF) > 1e4f)
            {
                angF %= Tau;
                angR %= Tau;
            }
            float frontYaw = driftActive ? driftDir * 0.32f - steer * 0.12f : -steer * 0.45f;
            Rotate(2, 0, frontYaw, 0);
            Rotate(3, 0, frontYaw, 0);
            Rotate(4, angF, 0, 0);
            Rotate(5, angF, 0, 0);
            Rotate(6, angR, 0, 0);
            Rotate(7, angR, 0, 0);

            if (!grounded)
            {
                air += dt;
            }
            if (!grounded && wasGrounded)
            {
                sqV += 2.2f * rm + 0.6f;
            }
            if (grounded && !wasGrounded)
            {
                sqV -= Mathf.Min(7.5f, 2.8f + air * 7) * (0.6f + 0.4f * rm);
                air = 0;
            }
            wasGrounded = grounded;
            sqV += (-380 * sq - 15 * sqV) * dt;
            sq = Mathf.Clamp(sq + sqV * dt, -0.45f, 0.35f);
            if (spin > 0)
            {
                if (!spinning)
                {
                    spinning = true;
                    spinCause = kart.SpinCause ?? "default";
                    spinTotal = Mathf.Max(spin, 0.5f);
                    if (tuning != null && (tuning.spins.TryGetValue(spinCause, out var value) || tuning.spins.TryGetValue("default", out value)) && value.time > 0)
                    {
                        spinTotal = Mathf.Max(value.time, spin);
                    }
                    spins++;
                    spinDir = steer > 0.2f ? -1 : steer < -0.2f ? 1 : (((spins + (int)seed) & 1) != 0 ? 1 : -1);
                }
                spinP = Mathf.Clamp01(1 - spin / spinTotal);
            }
            else if (spinning)
            {
                spinning = false;
                if (spinCause is "plane" or "homing" or "rocket")
                {
                    sqV -= 5;
                }
                spinP = 1;
            }
            string sc = spinning ? spinCause : null;
            float p = spinP;
            float yaw = 0, pitch = 0, roll = 0, lift = 0, jx = 0, jz = 0, flail = 0, dazed = 0;
            if (sc == "gum")
            {
                yaw = spinDir * Tau * 2 * EaseOut(p);
                roll = Mathf.Sin(p * Mathf.PI * 5) * 0.14f * (1 - p);
                flail = dazed = 1;
            }
            else if (sc is "plane" or "homing" or "rocket")
            {
                float e = Smooth(p);
                pitch = -Tau * e;
                yaw = spinDir * Mathf.PI * e;
                lift = Mathf.Sin(Mathf.PI * p) * 1.25f;
                flail = 1;
                dazed = p;
            }
            else if (sc == "foil")
            {
                yaw = spinDir * Tau * EaseOut(p);
                lift = Mathf.Sin(Mathf.PI * p) * 0.35f;
                roll = Mathf.Sin(Mathf.PI * p) * 0.28f * spinDir;
                flail = 0.6f;
                dazed = 1;
            }
            else if (sc == "burnout")
            {
                jx = Mathf.Sin(t * 71) * Mathf.Sin(t * 23) * 0.015f;
                jz = Mathf.Sin(t * 59 + 1) * Mathf.Sin(t * 29) * 0.015f;
                pitch = Mathf.Sin(t * 31) * 0.035f;
                lift = Mathf.Abs(Mathf.Sin(t * 13)) * 0.03f;
                dazed = 0.7f;
            }
            else if (sc != null && sc != "scissors")
            {
                yaw = spinDir * Tau * EaseOut(p);
                flail = 0.6f;
                dazed = 1;
            }
            bool flattening = sc == "scissors" && p < 0.86f;
            flatV += ((flattening ? 1100 : 240) * ((flattening ? 1 : 0) - flat) - (flattening ? 60 : 9) * flatV) * dt;
            flat += flatV * dt;
            if (flat > 1 || flat < -0.6f)
            {
                flat = Mathf.Clamp(flat, -0.6f, 1);
                flatV = 0;
            }
            if (flat > 0.05f)
            {
                pitch += Mathf.Sin(t * 7.5f) * 0.07f * flat * rm;
                roll += Mathf.Sin(t * 5.3f + 1) * 0.05f * flat * rm;
                lift += 0.03f * flat;
            }
            if (trickT >= 0)
            {
                trickT += dt;
                float tp = trickT / 0.6f;
                if (tp >= 1)
                {
                    trickT = -1;
                }
                else
                {
                    roll += trickDir * Tau * Smooth(tp);
                }
            }
            if (hang > 0.01f)
            {
                roll += Mathf.Sin(t * 2.6f) * 0.12f * hang * rm;
                pitch += Mathf.Sin(t * 1.9f) * 0.07f * hang * rm;
            }
            Rotate(0, pitch, yaw, roll, "YXZ");
            Vector3 center = KagitKartVehicle.ToUnity(Center);
            bones[0].localPosition = center - bones[0].localRotation * center + new Vector3(-jx, lift, jz);
            float sy = (1 + sq) * (1 - 0.93f * flat);
            float sxz = (1 - 0.5f * sq) * (1 + 0.28f * flat);
            bones[0].localScale = new Vector3(sxz, Mathf.Max(0.05f, sy), sxz);
            ApplyDriver(dt, driving, rm, speed, sS, flail, dazed);
            ApplyTint(dt, spin);
        }

        private void ApplyDriver(float dt, bool driving, float rm, float speed, float sS, float flail, float dazed)
        {
            float cy = 0, cRoll = 0, cPitch = 0;
            if (driving)
            {
                cRoll = -drift * 0.1f - steer * sS * 0.05f;
                cPitch = -0.07f * boost + 0.04f * sq;
                cy += Mathf.Sin(t * 55) * 0.005f * tier * rm;
                cy += Mathf.Sin(t * 62) * 0.006f * throttle * (1 - Mathf.Min(1, sS * 3)) * rm;
                if (kart != null && (kart.Surface is "offroad" or "sand" or "out"))
                {
                    cy += Mathf.Sin(t * 23) * Mathf.Sin(t * 7.3f) * 0.028f * Mathf.Min(1, sS * 2) * rm;
                    cRoll += Mathf.Sin(t * 17) * 0.03f * sS * rm;
                }
                cRoll += Mathf.Sin(t * 30) * 0.08f * wobble;
            }
            else
            {
                cy = Mathf.Sin(t * 40) * 0.0025f * rm;
            }
            bones[1].localPosition = rest[1] + Vector3.up * cy;
            Rotate(1, cPitch, 0, cRoll);
            Quaternion steering = Quaternion.AngleAxis((driving ? -steer : 0) * 1.3f * Mathf.Rad2Deg, new Vector3(0, 0.55f, -0.83f).normalized);
            bones[8].localRotation = Mirror(steering);
            float tx = 0, ty = 0, tz = 0, tyPos = 0, tScale = 1, hx = 0, hy = 0, hz = 0;
            if (driving)
            {
                tx = 0.07f * sS - 0.2f * boost;
                tz = drift * 0.2f + steer * sS * 0.1f;
                ty = -drift * 0.12f + lookBack * 0.5f;
                hx = -0.08f * boost;
                hy = -drift * 0.3f - steer * 0.22f + lookBack * 2.4f + Mathf.Sin(t * 12) * 0.28f * ink;
                hz = drift * 0.1f;
                tyPos = Mathf.Sin(t * 48) * 0.004f * tier * rm;
            }
            tScale += Mathf.Sin(t * 2.4f) * 0.018f * idle * rm;
            hy += (Mathf.Sin(t * 0.7f) * 0.35f + Mathf.Sin(t * 1.9f) * 0.08f) * idle * rm;
            hx += Mathf.Sin(t * 1.1f) * 0.05f * idle * rm;
            tyPos += Mathf.Abs(Mathf.Sin(t * 6.2f)) * 0.07f * cheer * rm;
            hx += -0.25f * cheer + Mathf.Sin(t * 6.2f) * 0.06f * cheer;
            tx += 0.22f * sad;
            hx += 0.5f * sad;
            hy += Mathf.Sin(t * 0.8f) * 0.15f * sad;
            float dizziness = Mathf.Max(dizzy, dazed);
            hz += Mathf.Sin(t * 4.3f) * 0.3f * dizziness * rm;
            hx += Mathf.Cos(t * 4.3f) * 0.18f * dizziness * rm;
            tz += Mathf.Sin(t * 4.3f + 1) * 0.08f * dizziness * rm;
            bones[9].localPosition = rest[9] + Vector3.up * tyPos;
            Rotate(9, tx, ty, tz, "YXZ");
            bones[9].localScale = new Vector3(1, tScale, 1);
            Rotate(10, hx, hy, hz, "YXZ");
            float grip = driving ? 1 - Mathf.Max(cheer, Mathf.Max(hang, flail)) : 0;
            float up = Mathf.Max(cheer, Mathf.Max(hang * 0.9f, flail * 0.8f));
            float steerLift = steer * 0.28f * grip;
            float wave = Mathf.Sin(t * 9) * 0.35f * cheer + Mathf.Sin(t * 19) * 0.5f * flail;
            float hangX = 0.35f * (1 - grip) * (1 - up) * (driving ? 0 : 1) + 0.25f * sad;
            Rotate(11, -steerLift - up * 1.8f + wave + hangX - ink * 1.6f * grip, 0, -up * 0.42f, "ZYX");
            Rotate(12, steerLift - up * 1.8f - wave + hangX, 0, up * 0.42f, "ZYX");
            ApplyChains(rm, sS);
            if (definition.prop >= 0)
            {
                propAng += (driving ? 7 + Mathf.Abs(speed) * 2.2f : 4 + cheer * 14) * dt;
                if (propAng > 1e4f)
                {
                    propAng %= Tau;
                }
                Rotate(definition.prop, 0, 0, propAng);
            }
            if (definition.peri >= 0)
            {
                Rotate(definition.peri, 0, Mathf.Sin(t * 0.5f) * 0.7f * rm + lookBack * Mathf.PI, 0);
            }
        }

        private void ApplyChains(float rm, float sS)
        {
            for (int ci = 0; ci < definition.chains.Length; ci++)
            {
                var chain = definition.chains[ci];
                var k = KindFor(chain.kind);
                int n = chain.bones.Length;
                float side = chain.side == 0 ? 1 : chain.side;
                float ph = chain.phase + ci * 0.7f;
                if (chain.kind == "flag")
                {
                    Rotate(chain.bones[0], 0, 0, -drift * 0.25f + Mathf.Sin(t * 2.3f) * 0.05f * rm);
                    float f = (0.12f + 0.3f * sS) * rm;
                    Rotate(chain.bones[1], Mathf.Sin(t * 5.1f) * 0.05f, Mathf.Sin(t * (5 + 11 * sS)) * f + drift * 0.4f, 0);
                    continue;
                }
                if (chain.kind == "bell")
                {
                    Rotate(chain.bones[0], Mathf.Sin(t * 8.5f) * (0.05f + 0.3f * wobble + 0.08f * sS) * rm - 0.2f * boost, 0, Mathf.Sin(t * 6.1f) * 0.06f * rm - drift * 0.25f);
                    continue;
                }
                if (chain.kind == "wobble")
                {
                    float wv = wobble + Mathf.Abs(sq) * 1.5f;
                    Rotate(chain.bones[0], -0.08f * boost + Mathf.Sin(t * 13) * 0.12f * wv * rm, 0, -drift * 0.12f + Mathf.Sin(t * 14 + ci) * 0.18f * wv * rm + Mathf.Sin(t * 2.1f) * 0.02f * rm);
                    continue;
                }
                for (int j = 0; j < n; j++)
                {
                    float w = chain.kind == "longEar" ? (j == 0 ? 0.62f : 0.38f) : 1f / n;
                    float flutter = Mathf.Sin(t * k.fFreq + ph - j * 1.1f) * k.flutter * (0.15f + sS) * rm;
                    float x = k.stream * Mathf.Min(1, sS) * (1 + boost * 0.35f) * w + k.hang * (1 - Mathf.Min(1, sS * 1.5f)) * w + k.droop * sad * w + k.perk * cheer * w + flutter;
                    float s = k.sway * Mathf.Sin(t * k.freq + ph - j * 0.9f) * (0.35f + 0.65f * (1 - Mathf.Min(1, sS))) * rm;
                    s += Mathf.Sin(t * 14 - j) * k.wag * cheer * rm;
                    x += Mathf.Sin(t * 1.7f + ph - j * 1.2f) * k.wave * rm;
                    if (k.roll)
                    {
                        s += -side * k.splay * sad * w - drift * k.drift * w;
                        if (idle > 0.5f && k.splay != 0)
                        {
                            float tw = Mathf.Sin(t * 0.83f + side * 2 + ci);
                            if (tw > 0.985f)
                            {
                                x += (tw - 0.985f) * 40 * 0.3f;
                            }
                        }
                        Rotate(chain.bones[j], x, 0, s);
                    }
                    else
                    {
                        s += -drift * k.drift * w + lookBack * 0.2f * side * w;
                        Rotate(chain.bones[j], x, s * (chain.kind == "tentacle" ? side : 1), 0, "YXZ");
                    }
                }
            }
        }

        private void ApplyTint(float dt, float spin)
        {
            skin.enabled = ghost || (kart?.GraceTime ?? 0) <= 0 || spin > 0 || (int)(t * 16) % 2 == 0;
            Color emission = ghost ? new Color(93 / 255f, 127 / 255f, 184 / 255f).linear * 0.55f : Color.black;
            if ((kart?.InvincibleTime ?? 0) > 0)
            {
                float pulse = Mathf.Abs(Mathf.Sin(t * 14));
                emission = Color.Lerp(FoilA, FoilB, 0.5f + 0.5f * Mathf.Sin(t * 9)) * (0.22f + 0.4f * pulse * pulse);
            }
            if (flashT > 0)
            {
                flashT = Mathf.Max(0, flashT - dt);
                emission = Color.Lerp(emission, flashColor, Mathf.Clamp01(flashT / flashDur * 1.2f));
            }
            skin.GetPropertyBlock(block);
            block.SetVector(EmissionColor, emission);
            skin.SetPropertyBlock(block);
        }

        private void Rotate(int index, float x, float y, float z, string order = "XYZ")
        {
            // Three 的 Euler 顺序不能由 Unity Euler 替代；X 镜像使 quaternion 的 Y/Z 分量反号。
            Quaternion qx = Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right);
            Quaternion qy = Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up);
            Quaternion qz = Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward);
            bones[index].localRotation = Mirror(order == "YXZ" ? qy * qx * qz : order == "ZYX" ? qz * qy * qx : qx * qy * qz);
        }

        private static Quaternion Mirror(Quaternion q) => new(q.x, -q.y, -q.z, q.w);

        private static float Damp(float a, float b, float halfLife, float dt) => b + (a - b) * Mathf.Pow(2, -dt / halfLife);

        private static float Smooth(float p) => p * p * (3 - 2 * p);

        private static float EaseOut(float p) => 1 - (1 - p) * (1 - p) * (1 - p);

        private void OnDestroy()
        {
            if (kart != null)
            {
                kart.Signaled -= OnSignal;
            }
            if (ghostMaterial != null)
            {
                Destroy(ghostMaterial);
            }
        }
    }
}
