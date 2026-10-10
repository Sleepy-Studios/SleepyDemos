using System;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Rendering;

namespace Hotfix.KagitKart
{
    /// 只负责原作纸景表现，使用宿主提供的比赛时间，不读取全局时间或修改共享材质。
    public sealed class KagitKartSceneryWorld : MonoBehaviour
    {
        [SerializeField] internal KagitKartSceneryAsset asset;
        [SerializeField] internal MeshRenderer[] staticRenderers;
        [SerializeField] internal Transform[] rampTransforms;

        private Matrix4x4[][] poses;
        private Matrix4x4[][] worldPoses;
        private MaterialPropertyBlock[] blocks;
        private PopupState[][] states;
        private PopupState[] rampStates;
        private Quaternion[] rampRotations;
        private MaterialPropertyBlock staticBlock;
        private Dynamics dynamics;
        private double dynamicsTime;

        private struct PopupState
        {
            public float angle;
            public float speed;
            public float flutter;
            public bool target;
        }

        public KagitKartSceneryAsset Asset => asset;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (poses != null || asset == null)
            {
                return;
            }
            poses = new Matrix4x4[asset.batches.Length][];
            worldPoses = new Matrix4x4[poses.Length][];
            blocks = new MaterialPropertyBlock[poses.Length];
            for (int b = 0; b < poses.Length; b++)
            {
                poses[b] = (Matrix4x4[])asset.batches[b].poses.Clone();
                worldPoses[b] = new Matrix4x4[poses[b].Length];
                blocks[b] = new MaterialPropertyBlock();
                blocks[b].SetVectorArray("_InstanceColor", asset.batches[b].colors);
            }
            states = new PopupState[asset.popups.Length][];
            for (int g = 0; g < states.Length; g++)
            {
                var group = asset.popups[g];
                states[g] = new PopupState[group.items.Length];
                for (int i = 0; i < group.items.Length; i++)
                {
                    states[g][i].target = group.items[i].alwaysUp;
                    states[g][i].angle = group.items[i].alwaysUp ? 1 : 0;
                    WritePose(group, i, states[g][i].angle, 0);
                }
            }
            rampStates = new PopupState[asset.ramps.Length];
            rampRotations = new Quaternion[rampStates.Length];
            for (int i = 0; i < rampStates.Length; i++)
            {
                rampRotations[i] = rampTransforms[i].localRotation;
            }
            staticBlock = new MaterialPropertyBlock();
            dynamics = JsonConvert.DeserializeObject<Dynamics>(asset.dynamicsData.text);
            if (dynamics.sourceCommit != KagitKartCatalog.SourceCommit)
            {
                throw new InvalidOperationException("纸景动态源码版本不匹配。");
            }
        }

        /// <summary>推进纸景并提交给指定摄像机；暂停时 dt 为零，演出和动画时间由宿主冻结。</summary>
        /// <param name="camera">本 Demo 的摄像机，不向其它摄像机提交实例。</param>
        /// <param name="cameraDistance">摄像机对应的赛道距离，单位米。</param>
        /// <param name="dt">本帧比赛表现时间，必须有限且非负。</param>
        /// <param name="time">累计表现时间，用于纸纹动画，必须有限。</param>
        /// <param name="lookBack">回头时保持身后纸景立起。</param>
        /// <param name="reducedMotion">减少动态时直接应用目标姿态。</param>
        public void Render(Camera camera, float cameraDistance, float dt, float time,
            bool lookBack = false, bool reducedMotion = false)
        {
            if (camera == null || !float.IsFinite(dt) || dt < 0 || !float.IsFinite(time)
                || !float.IsFinite(cameraDistance))
            {
                throw new ArgumentException("纸景绘制参数无效。");
            }
            Initialize();
            if (poses == null)
            {
                return;
            }
            for (int g = 0; g < asset.popups.Length; g++)
            {
                var group = asset.popups[g];
                for (int i = 0; i < group.items.Length; i++)
                {
                    var item = group.items[i];
                    ref var state = ref states[g][i];
                    if (!item.alwaysUp)
                    {
                        state.target = Target(state.target, Wrap(item.distance - cameraDistance), item.trigger, lookBack);
                    }
                    float before = state.angle;
                    AdvanceSpring(ref state, dt, reducedMotion);
                    if (!reducedMotion && group.flutter && state.target && before < 1 && state.angle >= 1
                        && state.flutter <= 0)
                    {
                        state.flutter = 0.42f;
                    }
                    float twist = 0;
                    if (state.flutter > 0)
                    {
                        state.flutter = Mathf.Max(0, state.flutter - dt);
                        float k = state.flutter / 0.42f;
                        twist = 0.07f * k * k * Mathf.Sin((0.42f - state.flutter) * 11 * Mathf.PI * 2);
                    }
                    WritePose(group, i, state.angle, twist);
                }
            }
            for (int i = 0; i < rampStates.Length; i++)
            {
                ref var state = ref rampStates[i];
                state.target = Target(state.target, Wrap(asset.ramps[i].distance - cameraDistance), 125, lookBack);
                AdvanceSpring(ref state, dt, reducedMotion);
                rampTransforms[i].localRotation = rampRotations[i] * Quaternion.AngleAxis(
                    asset.ramps[i].theta * (1 - Mathf.Clamp(state.angle, 0, 1.25f)) * Mathf.Rad2Deg, Vector3.right);
            }
            AdvanceDynamics(dt, camera);
            staticBlock.SetFloat("uTime", time);
            foreach (var renderer in staticRenderers)
            {
                renderer.SetPropertyBlock(staticBlock);
            }
            Matrix4x4 root = transform.localToWorldMatrix;
            for (int b = 0; b < poses.Length; b++)
            {
                var batch = asset.batches[b];
                Matrix4x4 parent = root * batch.parent;
                for (int i = 0; i < poses[b].Length; i++)
                {
                    worldPoses[b][i] = parent * poses[b][i];
                }
                blocks[b].SetFloat("uTime", time);
                for (int submesh = 0; submesh < batch.mesh.subMeshCount; submesh++)
                {
                    // 原作按沿程切分批次，每批少于 Unity 的 1023 实例上限。
                    Graphics.DrawMeshInstanced(batch.mesh, submesh,
                        batch.materials[Mathf.Min(submesh, batch.materials.Length - 1)], worldPoses[b],
                        poses[b].Length, blocks[b], batch.castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off,
                        batch.receiveShadow, gameObject.layer, camera, LightProbeUsage.Off);
                }
            }
        }

        [Serializable]
        private sealed class Dynamics
        {
            public string sourceCommit, id;
            public Bindings bindings;
            public int mill = -1;
            public Cloud[] clouds;
            public Windmill[] windmills;
            public Ferry[] ferries;
            public Puff[] puffs;
            public Point[] cable;
            public Point chimney;
            public Mug mug;
            public double west, east, pageY;
        }

        [Serializable]
        private sealed class Bindings
        {
            public int cloud = -1, strings = -1, sails = -1, ferry = -1, ferryLights = -1, smoke = -1, gondola = -1, steam = -1;
        }

        [Serializable]
        private sealed class Cloud
        {
            public double x, y, z, yaw, sc;
        }

        [Serializable]
        private sealed class Windmill
        {
            public int i;
            public double spin, phase;
        }

        [Serializable]
        private sealed class Ferry
        {
            public double z, t, speed, x;
            public int dir;
        }

        [Serializable]
        private sealed class Puff
        {
            public int ferry;
            public double age;
        }

        [Serializable]
        private class Point
        {
            public double x, y, z;
        }

        [Serializable]
        private sealed class Mug : Point
        {
            public double top;
        }

        // 四章实际 onUpdate 的逐句迁移；仅本地实例矩阵变化，共享资产保持不变。
        private void AdvanceDynamics(double dt, Camera camera)
        {
            dynamicsTime += dt;
            double time = dynamicsTime;
            var bind = dynamics.bindings;
            Vector3 cameraLocal = transform.InverseTransformPoint(camera.transform.position);
            double cameraX = -cameraLocal.x, cameraZ = cameraLocal.z;
            for (int i = 0; i < dynamics.windmills.Length; i++)
            {
                var w = dynamics.windmills[i];
                poses[bind.sails][i] = poses[dynamics.mill][w.i]
                    * Matrix4x4.Translate(new Vector3(0, 9.3f, 2.75f))
                    * Matrix4x4.Rotate(Quaternion.AngleAxis((float)(-(w.phase + time * w.spin) * 180 / Math.PI), Vector3.forward));
            }
            for (int i = 0; i < dynamics.clouds.Length; i++)
            {
                var c = dynamics.clouds[i];
                double yaw = Math.Atan2(cameraX - c.x, cameraZ - c.z);
                var matrix = Pose(c.x, c.y, c.z, yaw, c.sc, c.sc, c.sc);
                poses[bind.cloud][i] = matrix;
                poses[bind.strings][i] = matrix;
            }
            for (int i = 0; i < dynamics.ferries.Length; i++)
            {
                var f = dynamics.ferries[i];
                f.t += dt;
                double width = dynamics.east - dynamics.west - 36, cycle = width / f.speed + 8;
                double phase = (f.t % (2 * cycle)) / cycle, go = phase < 1 ? phase : phase - 1;
                double u = Math.Min(1, Math.Max(0, (go * cycle - 4) / (cycle - 8))), ease = u * u * (3 - 2 * u);
                bool toEast = (phase < 1) == (f.dir > 0);
                f.x = toEast ? dynamics.west + 18 + ease * width : dynamics.east - 18 - ease * width;
                double bob = Math.Sin(time * 1.3 + i) * 0.12;
                var matrix = Pose(f.x, dynamics.pageY + 0.2 + bob, f.z, toEast ? Math.PI / 2 : -Math.PI / 2);
                poses[bind.ferry][i] = matrix;
                poses[bind.ferryLights][i] = matrix;
            }
            for (int i = 0; i < dynamics.puffs.Length; i++)
            {
                var p = dynamics.puffs[i];
                p.age += dt;
                if (p.age > 4)
                {
                    p.age -= 4;
                }
                var ferry = dynamics.ferries[p.ferry];
                double g = p.age / 4, scale = 0.6 + g * 2.4;
                poses[bind.smoke][i] = Pose(ferry.x + Math.Sin(i * 1.7 + time) * g * 2 - g * 3,
                    dynamics.pageY + 8.6 + g * 11, ferry.z + g * 1.5, 0, scale, scale * 0.85, scale);
            }
            if (dynamics.chimney != null)
            {
                var c = dynamics.chimney;
                for (int i = 0; i < 14; i++)
                {
                    double g = (time * 0.28 + i / 14.0) % 1, scale = 0.5 + g * 2.6;
                    poses[bind.smoke][i] = Pose(c.x + Math.Sin(i * 2.1 + time * 0.5) * g * 2 + g * 5,
                        c.y + g * 16, c.z + g * 2, 0, scale, scale * 0.8, scale);
                }
            }
            if (dynamics.cable.Length > 1)
            {
                int segments = dynamics.cable.Length - 1;
                for (int k = 0; k < 4; k++)
                {
                    double f = (time * 0.018 + k / 4.0) % 1, u = f < 0.5 ? f * 2 : 2 - f * 2;
                    double position = u * segments;
                    int i = Math.Min(segments - 1, (int)Math.Floor(position));
                    double w = position - i;
                    var a = dynamics.cable[i];
                    var b = dynamics.cable[i + 1];
                    double yaw = Math.Atan2(b.x - a.x, b.z - a.z) + Math.Sin(time * 1.3 + k) * 0.03;
                    poses[bind.gondola][k] = Pose(a.x + (b.x - a.x) * w, a.y + (b.y - a.y) * w, a.z + (b.z - a.z) * w, yaw);
                }
            }
            if (dynamics.mug != null)
            {
                var mug = dynamics.mug;
                for (int i = 0; i < 8; i++)
                {
                    double g = (time * 0.12 + i / 8.0) % 1, scale = 1.2 + g * 2.2 * (1 - g * 0.5);
                    double yaw = Math.Atan2(cameraX - mug.x, cameraZ - mug.z) + Math.Sin(i * 1.7) * 0.4;
                    poses[bind.steam][i] = Pose(mug.x + Math.Sin(time * 0.6 + i) * 3 * g, mug.top + g * 30,
                        mug.z + Math.Cos(time * 0.5 + i * 2) * 3 * g, yaw, scale * (1 - g * 0.3), scale, scale);
                }
            }
        }

        private static Matrix4x4 Pose(double x, double y, double z, double yaw, double sx = 1, double sy = 1, double sz = 1)
        {
            return Matrix4x4.TRS(new Vector3((float)-x, (float)y, (float)z),
                Quaternion.AngleAxis((float)(-yaw * 180 / Math.PI), Vector3.up), new Vector3((float)sx, (float)sy, (float)sz));
        }

        private float Wrap(float distance)
        {
            distance %= asset.length;
            if (distance > asset.length / 2)
            {
                distance -= asset.length;
            }
            else if (distance < -asset.length / 2)
            {
                distance += asset.length;
            }
            return distance;
        }

        private static bool Target(bool current, float ahead, float trigger, bool lookBack)
        {
            if (!current)
            {
                return ahead >= -60 && ahead <= trigger;
            }
            return !((ahead < -60 && !lookBack) || ahead > trigger + 50);
        }

        private static void AdvanceSpring(ref PopupState state, float dt, bool reduced)
        {
            float target = state.target ? 1 : 0;
            if (reduced)
            {
                state.angle = target;
                state.speed = state.flutter = 0;
                return;
            }
            float w = 2 * Mathf.PI * (state.target ? 2.2f : 1.5f);
            float z = state.target ? 0.5f : 1;
            while (dt > 1e-6f)
            {
                float step = Mathf.Min(1f / 60, dt);
                state.speed += (w * w * (target - state.angle) - 2 * z * w * state.speed) * step;
                state.angle += state.speed * step;
                if (!state.target && state.angle < 0)
                {
                    state.angle = state.speed = 0;
                }
                dt -= step;
            }
            if (Mathf.Abs(state.angle - target) < 0.001f && Mathf.Abs(state.speed) < 0.001f
                && state.flutter <= 0)
            {
                state.angle = target;
                state.speed = 0;
            }
        }

        private void WritePose(KagitKartPopupGroup group, int index, float angle, float twist)
        {
            var item = group.items[index];
            Vector3 scale = item.scale;
            if (group.volume)
            {
                scale.z *= 0.05f + 0.95f * Mathf.Clamp01(angle);
            }
            Matrix4x4 pose = Matrix4x4.TRS(item.position,
                Quaternion.AngleAxis(-(item.yaw + twist) * Mathf.Rad2Deg, Vector3.up)
                * Quaternion.AngleAxis(-(1 - angle) * 90, Vector3.right), scale);
            foreach (int batch in group.batches)
            {
                poses[batch][index] = pose;
            }
        }
    }
}
