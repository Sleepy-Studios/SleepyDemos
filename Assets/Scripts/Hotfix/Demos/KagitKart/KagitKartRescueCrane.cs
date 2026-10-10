using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hotfix.KagitKart
{
    /// 原作 crane.js 的四只折纸救援鹤；由比赛特效宿主驱动并共用纸卡绳索。
    public sealed class KagitKartRescueCrane : MonoBehaviour
    {
        [SerializeField] internal Mesh bodyShape, wingShape;
        [SerializeField] internal Material[] paperMaterials;

        private const float FootY = -0.48f;
        private const float StringLength = 2.8635643f;
        private static readonly Matrix4x4 WingLeft = Matrix4x4.Translate(new Vector3(0.036f, 0.36f, 0.06f));
        private static readonly Matrix4x4 WingRight = Matrix4x4.TRS(new Vector3(-0.036f, 0.36f, 0.06f), Quaternion.identity, new Vector3(-1, 1, 1));
        private readonly Slot[] slots = { new(0), new(1), new(2), new(3) };
        internal readonly Matrix4x4[] bodyPoses = new Matrix4x4[4];
        internal readonly Matrix4x4[] wingPoses = new Matrix4x4[8];
        private MaterialPropertyBlock block;
        private KagitKartSession session;
        private Transform[] actors, heads;
        private int theme, prewarm = 3;
        internal bool ChaseView { get; set; } = true;
        internal int BodyCount { get; private set; }
        internal int WingCount { get; private set; }

        private enum Phase
        {
            Free,
            In,
            Hold,
            Out
        }

        private sealed class Slot
        {
            public readonly int index;
            public KagitKartVehicle kart;
            public Phase phase;
            public double time, age, waited, flap;
            public bool early, attached, released, dropping;
            public Vector3 position, from, control, velocity, previous, relative;
            public float yaw, pitch, bank, stringLength, scale = 1;

            public Slot(int index)
            {
                this.index = index;
                flap = index * 1.3;
            }
        }

        internal void Bind(KagitKartSession value, Transform[] kartActors, KagitKartCharacterVisual[] visuals, int themeIndex)
        {
            block = new MaterialPropertyBlock();
            session = value;
            actors = kartActors;
            theme = themeIndex;
            heads = new Transform[visuals.Length];
            for (int i = 0; i < heads.Length; i++)
            {
                heads[i] = visuals[i].GetAnchor("head");
            }
        }

        internal bool Has(KagitKartVehicle kart)
        {
            foreach (var slot in slots)
            {
                if (slot.kart == kart && slot.phase is not (Phase.Free or Phase.Out))
                {
                    return true;
                }
            }
            return false;
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

        private Vector3 Hook(KagitKartVehicle kart) => KagitKartRaceFx.Source(heads[IndexOf(kart)].position) + Vector3.up * 0.3f;

        private Vector3 HoldTarget(KagitKartVehicle kart, Vector3 hook, Camera camera)
        {
            Vector3 target = hook + KagitKartVehicle.Forward(kart.Heading) * 2.6f + Vector3.up * (1.2f - FootY);
            bool framed = ChaseView && kart == session.Player && !(kart.Controls.LookBack && !kart.Autopilot);
            if (!framed)
            {
                return target;
            }
            Vector3 forward = KagitKartRaceFx.Source(camera.transform.forward), up = KagitKartRaceFx.Source(camera.transform.up);
            Vector3 difference = target - KagitKartRaceFx.Source(camera.transform.position);
            float height = Vector3.Dot(difference, up) + 1.9f * up.y;
            if (height > 0)
            {
                float need = height / (Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2) * 0.78f) - Vector3.Dot(difference, forward);
                if (need > 0)
                {
                    target += forward * Math.Min(need, 8);
                }
            }
            return target;
        }

        internal void Summon(KagitKartVehicle kart, bool early, bool force, Camera camera)
        {
            if (Has(kart))
            {
                return;
            }
            Slot chosen = null;
            foreach (var slot in slots)
            {
                if (slot.phase == Phase.Free)
                {
                    chosen = slot;
                    break;
                }
            }
            if (chosen == null)
            {
                foreach (var slot in slots)
                {
                    if (slot.phase == Phase.Out && (chosen == null || slot.time > chosen.time))
                    {
                        chosen = slot;
                    }
                }
            }
            if (chosen == null && force)
            {
                foreach (var slot in slots)
                {
                    if (slot.kart != null && !slot.kart.IsPlayer)
                    {
                        chosen = slot;
                        break;
                    }
                }
            }
            if (chosen == null)
            {
                return;
            }
            chosen.kart = kart;
            chosen.phase = Phase.In;
            chosen.time = chosen.age = chosen.waited = 0;
            chosen.early = early;
            chosen.attached = chosen.released = chosen.dropping = false;
            chosen.scale = 1;
            chosen.stringLength = 0;
            Vector3 target = HoldTarget(kart, Hook(kart), camera);
            Vector3 direction = target - KagitKartRaceFx.Source(camera.transform.position);
            direction.y = 0;
            if (direction.sqrMagnitude < 1e-4f)
            {
                direction = Vector3.forward;
            }
            direction.Normalize();
            int side = chosen.index % 2 == 0 ? 1 : -1;
            chosen.from = target + direction * (early ? 7 : 2) + new Vector3(-direction.z * (early ? 5 : 2.5f) * side, early ? 11 : 5.5f, direction.x * (early ? 5 : 2.5f) * side);
            chosen.control = target + direction * (early ? 2 : 0.6f) + Vector3.up * (early ? 4 : 1.6f);
            chosen.position = chosen.previous = chosen.from;
            chosen.yaw = Mathf.Atan2(target.x - chosen.from.x, target.z - chosen.from.z);
            chosen.pitch = chosen.bank = 0;
        }

        private static void Release(Slot slot)
        {
            slot.phase = Phase.Out;
            slot.time = 0;
            slot.released = true;
            slot.velocity = KagitKartVehicle.Forward(slot.kart.Heading) * 8 + Vector3.up * 1.5f;
        }

        internal void Advance(float dt, Camera camera, KagitKartRaceFx host, Color stringColor, bool chaseView)
        {
            ChaseView = chaseView;
            int bodies = 0, wings = 0;
            foreach (var slot in slots)
            {
                if (slot.phase == Phase.Free)
                {
                    continue;
                }
                var kart = slot.kart;
                slot.time += dt;
                slot.age += dt;
                slot.previous = slot.position;
                float flapHz = 3, flapAmount = 0.7f;
                float? faceYaw = null;
                if (slot.phase is Phase.In or Phase.Hold)
                {
                    Vector3 hook = Hook(kart);
                    if (kart.RescueStage == 3)
                    {
                        slot.dropping = true;
                    }
                    Vector3 target = HoldTarget(kart, hook, camera);
                    if (slot.phase == Phase.In)
                    {
                        double duration = slot.early ? 0.5 : 0.25;
                        float u = EaseOut(slot.time / duration);
                        float a = (1 - u) * (1 - u), b = 2 * (1 - u) * u;
                        slot.position = slot.from * a + slot.control * b + target * (u * u);
                        flapHz = 2.4f;
                        flapAmount = 0.85f;
                        if (slot.time >= duration)
                        {
                            slot.phase = Phase.Hold;
                            slot.time = 0;
                            slot.relative = slot.position - hook;
                        }
                    }
                    else
                    {
                        float fraction = 1 - (float)Math.Pow(2, -dt / 0.06);
                        slot.relative += (target - hook - slot.relative) * fraction;
                        slot.position = hook + slot.relative + Vector3.up * ((float)Math.Sin(slot.age * 6.5) * 0.08f);
                        flapHz = slot.dropping ? 2.2f : 3.6f;
                        flapAmount = slot.dropping ? 0.55f : 0.8f;
                    }
                    Vector3 foot = slot.position + Vector3.up * FootY;
                    if (kart.Rescuing && !slot.attached && (slot.phase == Phase.Hold || Vector3.Distance(foot, hook) < StringLength * 1.3f))
                    {
                        slot.attached = true;
                    }
                    if (kart.Rescuing)
                    {
                        Vector3 difference = kart.RescueTo - kart.RescueFrom;
                        faceYaw = (difference.x * difference.x + difference.z * difference.z > 1 ? Mathf.Atan2(difference.x, difference.z) : kart.RescueHeading) + (IndexOf(kart) % 2 != 0 ? 0.7f : -0.7f);
                    }
                    else if (slot.attached)
                    {
                        Release(slot);
                    }
                    else if (slot.phase == Phase.Hold)
                    {
                        slot.waited += dt;
                        if (slot.waited > 1.4 || kart.Grounded && kart.Surface != "out" && kart.TrackInfo.HasGround && slot.waited > 0.3)
                        {
                            Release(slot);
                        }
                    }
                    if (slot.phase != Phase.Out)
                    {
                        if (slot.attached)
                        {
                            host.AddCard(foot, hook, 0.022f, 1, stringColor, 1);
                        }
                        else
                        {
                            slot.stringLength = Math.Min(1.8f, slot.stringLength + dt * 7);
                            host.AddCard(foot, foot - Vector3.up * slot.stringLength, 0.022f, 1, stringColor, 1);
                        }
                    }
                }
                if (slot.phase == Phase.Out)
                {
                    Vector3 velocity = KagitKartVehicle.Forward(kart.Heading) * 11 + Vector3.up * 3.5f;
                    slot.velocity = Vector3.LerpUnclamped(slot.velocity, velocity, 1 - (float)Math.Pow(2, -dt / 0.25));
                    slot.position += slot.velocity * dt;
                    flapHz = 3.2f;
                    flapAmount = 0.8f;
                    slot.scale = 1 - (float)Math.Max(0, (slot.time - 1) / 0.35);
                    slot.stringLength = slot.released ? (float)Math.Max(0, (slot.attached ? 1.8f : slot.stringLength) * (1 - slot.time / 0.18)) : 0;
                    if (slot.stringLength > 0.02f)
                    {
                        Vector3 foot = slot.position + Vector3.up * FootY;
                        host.AddCard(foot, foot - Vector3.up * slot.stringLength, 0.022f, 1, stringColor, 1);
                    }
                    if (slot.time >= 1.35)
                    {
                        slot.phase = Phase.Free;
                        slot.kart = null;
                        continue;
                    }
                }
                Vector3 movement = slot.position - slot.previous;
                float horizontal2 = movement.x * movement.x + movement.z * movement.z;
                if (!faceYaw.HasValue && horizontal2 > 0.09f * dt * dt && dt > 0)
                {
                    faceYaw = Mathf.Atan2(movement.x, movement.z);
                }
                float previousYaw = slot.yaw;
                if (faceYaw.HasValue && dt > 0)
                {
                    float difference = faceYaw.Value - slot.yaw;
                    difference = Mathf.Atan2(Mathf.Sin(difference), Mathf.Cos(difference));
                    slot.yaw = faceYaw.Value - difference * (float)Math.Pow(2, -dt / 0.12);
                }
                if (dt > 0)
                {
                    float vertical = movement.y / dt;
                    float yawRate = Mathf.Atan2(Mathf.Sin(slot.yaw - previousYaw), Mathf.Cos(slot.yaw - previousYaw)) / dt;
                    slot.pitch += (Math.Clamp(-vertical * 0.05f, -0.5f, 0.5f) - slot.pitch) * Math.Min(1, dt * 8);
                    slot.bank += (Math.Clamp(-yawRate * 0.25f, -0.6f, 0.6f) - slot.bank) * Math.Min(1, dt * 6);
                    slot.flap += dt * flapHz * Math.PI * 2;
                }
                Quaternion rotation = Quaternion.AngleAxis(slot.yaw * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(slot.pitch * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(slot.bank * Mathf.Rad2Deg, Vector3.forward);
                Matrix4x4 matrix = Matrix4x4.TRS(slot.position, rotation, Vector3.one * Math.Max(0, slot.scale));
                bodyPoses[bodies++] = Mirror(matrix);
                float flap = 0.15f + flapAmount * (float)Math.Sin(slot.flap);
                var wingRotation = Matrix4x4.Rotate(Quaternion.AngleAxis(flap * Mathf.Rad2Deg, Vector3.forward));
                wingPoses[wings++] = Mirror(matrix * WingLeft * wingRotation);
                wingPoses[wings++] = Mirror(matrix * WingRight * wingRotation);
            }
            if (prewarm > 0)
            {
                prewarm--;
                if (bodies == 0)
                {
                    bodyPoses[0] = wingPoses[0] = Matrix4x4.Scale(Vector3.zero);
                    bodies = wings = 1;
                }
            }
            BodyCount = bodies;
            WingCount = wings;
            if (bodies > 0)
            {
                Graphics.DrawMeshInstanced(bodyShape, 0, paperMaterials[theme], bodyPoses, bodies, block, ShadowCastingMode.On, false, gameObject.layer, camera);
            }
            if (wings > 0)
            {
                Graphics.DrawMeshInstanced(wingShape, 0, paperMaterials[theme], wingPoses, wings, block, ShadowCastingMode.On, false, gameObject.layer, camera);
            }
        }

        private static Matrix4x4 Mirror(Matrix4x4 value)
        {
            value.m01 = -value.m01;
            value.m02 = -value.m02;
            value.m03 = -value.m03;
            value.m10 = -value.m10;
            value.m20 = -value.m20;
            value.m30 = -value.m30;
            return value;
        }

        private static float EaseOut(double value)
        {
            double t = 1 - Math.Clamp(value, 0, 1);
            return (float)(1 - t * t * t);
        }
    }
}
