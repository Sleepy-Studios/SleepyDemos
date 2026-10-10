using System;
using UnityEngine;
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
                bool faceCamera = batch.sourceName == "scenery:clouds" || batch.sourceName == "scenery:strings";
                Vector3 reader = faceCamera ? parent.inverse.MultiplyPoint3x4(camera.transform.position) : Vector3.zero;
                for (int i = 0; i < poses[b].Length; i++)
                {
                    if (faceCamera)
                    {
                        Vector3 p = poses[b][i].GetColumn(3);
                        float yaw = Mathf.Atan2(reader.x - p.x, reader.z - p.z) * Mathf.Rad2Deg;
                        poses[b][i] = Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), poses[b][i].lossyScale);
                    }
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
