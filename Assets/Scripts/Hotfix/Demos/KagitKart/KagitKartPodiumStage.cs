using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hotfix.KagitKart
{
    /// 原作 podium.js 与 podiumShot 的领奖台、纸屑和镜头；立体书宿主负责章节与页面折叠。
    public sealed class KagitKartPodiumStage : MonoBehaviour
    {
        [SerializeField] internal TextAsset sourceData;
        [SerializeField] internal MeshFilter blocksFilter;
        [SerializeField] internal Material blockMaterial, confettiMaterial;
        [SerializeField] internal Mesh confettiShape;
        [SerializeField] internal GameObject[] drivers;
        [SerializeField] internal string[] driverIds;

        private const int Capacity = 260;
        private Data data;
        private Mesh blocksMesh;
        private Vector3[] positions, normals;
        private readonly BlockPose[] blocks = new BlockPose[3];
        private readonly List<Hero> heroes = new(4);
        private readonly Particle[] particles = new Particle[Capacity];
        private readonly Matrix4x4[] confettiPoses = new Matrix4x4[Capacity];
        private MaterialPropertyBlock confettiBlock;
        private Camera stageCamera;
        private KagitKartRandom random;
        private double time, confettiTime;
        private int used;
        private bool shown, cameraReducedMotion, erected, burstDone, showerDone;

        /// 当前纸屑与弹簧是否采用减弱动态；开始时的镜头和脚本时点仍按原作固定。
        public bool ReducedMotion { get; set; }
        /// 原作正常六秒、减弱动态三秒后可展示结算，演出仍持续。
        public bool IsComplete { get; private set; }
        public event Action Completed;

        [Serializable]
        private sealed class Data
        {
            public string sourceCommit;
            public Block[] blocks;
            public float[] confettiColors;
        }

        [Serializable]
        private sealed class Block
        {
            public int place, start, count;
            public float x, y, z, top;
            public float[] local, localN;
        }

        private struct BlockPose
        {
            public double u, v, target, delay;
            public bool dirty;
        }

        private sealed class Hero
        {
            public Transform holder;
            public KagitKartCharacterVisual visual;
            public int place;
            public float top;
            public double land, sq, sqv;
            public bool landed, sad;
        }

        private sealed class Particle
        {
            public Vector3 p, v, axis;
            public Quaternion q;
            public double spin, phase;
            public bool rest;
        }

        /// <summary>绑定舞台摄像机；正式 Prefab 已保存八个角色及原作网格材质。</summary>
        /// <param name="camera">由立体书宿主交出的演出摄像机。</param>
        public void Initialize(Camera camera)
        {
            stageCamera = camera;
            if (data != null)
            {
                return;
            }
            data = JsonUtility.FromJson<Data>(sourceData.text);
            if (data.sourceCommit != KagitKartCatalog.SourceCommit)
            {
                throw new InvalidOperationException("领奖台源码版本不匹配。");
            }
            blocksMesh = Instantiate(blocksFilter.sharedMesh);
            blocksMesh.MarkDynamic();
            blocksFilter.sharedMesh = blocksMesh;
            positions = blocksMesh.vertices;
            normals = blocksMesh.normals;
            confettiBlock = new MaterialPropertyBlock();
            var colors = new Vector4[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                colors[i] = new Vector4(data.confettiColors[i * 3], data.confettiColors[i * 3 + 1], data.confettiColors[i * 3 + 2], 1);
                particles[i] = new Particle();
            }
            confettiBlock.SetVectorArray("_InstanceColor", colors);
            Hide();
        }

        /// <summary>按名次展示前三角色，并展示未上榜玩家的失落纸偶；每次重置原作 2026 随机流。</summary>
        /// <param name="top3CharacterIds">按一、二、三名排序的原作角色 ID；空项忽略，未知 ID 用同名次原作角色。</param>
        /// <param name="sadId">未上榜玩家 ID，null 时不展示失落纸偶。</param>
        /// <param name="reducedMotion">开始时的减弱动态设置。</param>
        public void Show(string[] top3CharacterIds, string sadId = null, bool reducedMotion = false)
        {
            if (data == null)
            {
                throw new InvalidOperationException("请先 Initialize 领奖台。");
            }
            ClearHeroes();
            shown = true;
            blocksFilter.gameObject.SetActive(true);
            time = confettiTime = 0;
            used = 0;
            random = new KagitKartRandom(2026);
            ReducedMotion = cameraReducedMotion = reducedMotion;
            IsComplete = erected = burstDone = showerDone = false;
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i] = new BlockPose { dirty = true };
            }
            var ids = new List<string>(3);
            foreach (string id in top3CharacterIds ?? Array.Empty<string>())
            {
                if (!string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }
            }
            for (int i = 0; i < Math.Min(3, ids.Count); i++)
            {
                int place = i + 1;
                var step = Array.Find(data.blocks, b => b.place == place);
                int driver = Array.IndexOf(driverIds, ids[i]);
                if (driver < 0)
                {
                    driver = i;
                }
                AddHero(driver, place, new Vector3(step.x, step.top, 1.3f), place == 2 ? 0.2 : place == 3 ? -0.2 : 0,
                    reducedMotion ? 1.6 : place == 1 ? 3.55 : place == 2 ? 2.75 : 2.1, false);
            }
            int sadDriver = Array.IndexOf(driverIds, sadId);
            if (sadDriver >= 0 && !ids.Contains(sadId))
            {
                AddHero(sadDriver, 0, new Vector3(-5.1f, PageTop(-5.1f) + 0.03f, 4.7f), 0.55, 1.2, true);
            }
            WriteBlocks();
            PoseCamera();
        }

        private void AddHero(int driver, int place, Vector3 position, double yaw, double land, bool sad)
        {
            var holder = new GameObject("Podium" + (sad ? "Sad" : place.ToString()) + KagitKartCatalog.CharacterAssetName(driverIds[driver])).transform;
            holder.SetParent(transform, false);
            holder.localPosition = Source(position);
            holder.localRotation = Quaternion.AngleAxis((float)(-yaw * 180 / Math.PI), Vector3.up);
            var actor = Instantiate(drivers[driver], holder, false);
            var visual = actor.GetComponent<KagitKartCharacterVisual>();
            visual.Bind(null, null);
            visual.SetEnvironment(blockMaterial);
            visual.SetEmotion(sad ? "sad" : "idle");
            holder.gameObject.SetActive(false);
            heroes.Add(new Hero { holder = holder, visual = visual, place = place, top = position.y, land = land, sad = sad });
        }

        /// <summary>逐帧推进领奖台及镜头；无自主 Update，完成后继续保持演出直到 Hide。</summary>
        /// <param name="dt">有限非负帧时间，单帧最多 0.1 秒。</param>
        public void Advance(float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0)
            {
                throw new ArgumentException("领奖台帧时间必须有限且非负。", nameof(dt));
            }
            if (!shown)
            {
                return;
            }
            double frameDt = Math.Min(dt, 0.1f);
            time += frameDt;
            if (!erected && time >= 0.55)
            {
                erected = true;
                for (int i = 0; i < blocks.Length; i++)
                {
                    blocks[i].target = 1;
                    blocks[i].delay = cameraReducedMotion ? 0 : data.blocks[i].place == 1 ? 0.12 : data.blocks[i].place == 2 ? 0 : 0.24;
                    if (cameraReducedMotion)
                    {
                        blocks[i].u = 1;
                        blocks[i].v = 0;
                        blocks[i].dirty = true;
                    }
                }
            }
            if (!burstDone && time >= (cameraReducedMotion ? 1.7 : 3.6))
            {
                burstDone = true;
                if (cameraReducedMotion)
                {
                    Shower(60);
                }
                else
                {
                    Burst(new Vector3(-5.6f, 1.6f, 3), new Vector3(0.45f, 1, -0.15f), 70, 0.7, 13);
                    Burst(new Vector3(5.6f, 1.6f, 3), new Vector3(-0.45f, 1, -0.15f), 70, 0.7, 13);
                }
            }
            if (!showerDone && time >= (cameraReducedMotion ? 99 : 4.3))
            {
                showerDone = true;
                Shower(90);
            }
            UpdateBlocks(frameDt);
            foreach (var hero in heroes)
            {
                if (time < hero.land)
                {
                    hero.holder.gameObject.SetActive(false);
                    continue;
                }
                if (!hero.landed)
                {
                    hero.landed = true;
                    if (!hero.sad)
                    {
                        hero.visual.SetEmotion("cheer");
                    }
                    if (ReducedMotion)
                    {
                        hero.sq = 1;
                    }
                }
                Spring(ref hero.sq, ref hero.sqv, 1, Math.PI * 2 * 2.4, 0.42, frameDt);
                double scale = Math.Max(0.001, hero.sq);
                float width = (float)(0.82 + 0.18 * Math.Min(1, scale));
                hero.holder.localScale = new Vector3(width, (float)scale, width);
                hero.holder.gameObject.SetActive(scale > 0.01);
                // 原作菜单纸偶 update(dt,1) 不采用比赛的减弱动态动画分支。
                hero.visual.Advance((float)frameDt);
            }
            UpdateConfetti(frameDt);
            PoseCamera();
            if (!IsComplete && time >= (cameraReducedMotion ? 3 : 6))
            {
                IsComplete = true;
                Completed?.Invoke();
            }
            if (used > 0)
            {
                Graphics.DrawMeshInstanced(confettiShape, 0, confettiMaterial, confettiPoses, used, confettiBlock,
                    ShadowCastingMode.Off, false, gameObject.layer, stageCamera);
            }
        }

        private static void Spring(ref double position, ref double velocity, double target, double w, double z, double dt)
        {
            if (position == target && velocity == 0)
            {
                return;
            }
            for (double left = dt; left > 1e-6;)
            {
                double h = Math.Min(left, 1.0 / 120);
                velocity += (w * w * (target - position) - 2 * z * w * velocity) * h;
                position += velocity * h;
                left -= h;
            }
            if (Math.Abs(target - position) < 1e-3 && Math.Abs(velocity) < 1e-2)
            {
                position = target;
                velocity = 0;
            }
        }

        private void UpdateBlocks(double dt)
        {
            for (int i = 0; i < blocks.Length; i++)
            {
                ref var b = ref blocks[i];
                if (b.u == b.target && b.v == 0)
                {
                    continue;
                }
                if (b.delay > 0)
                {
                    b.delay -= dt;
                    continue;
                }
                if (ReducedMotion)
                {
                    b.u = b.target;
                    b.v = 0;
                }
                else
                {
                    bool up = b.target > b.u;
                    Spring(ref b.u, ref b.v, b.target, Math.PI * 2 * (up ? 2.2 : 2.4), up ? 0.5 : 1, dt);
                    if (b.target == 0 && b.u < 0)
                    {
                        b.u = b.v = 0;
                    }
                }
                b.dirty = true;
            }
            WriteBlocks();
        }

        private void WriteBlocks()
        {
            bool dirty = false;
            for (int k = 0; k < blocks.Length; k++)
            {
                if (!blocks[k].dirty)
                {
                    continue;
                }
                var b = data.blocks[k];
                double angle = blocks[k].u * Math.PI / 2, s = Math.Sin(angle), c = Math.Cos(angle);
                for (int i = 0; i < b.count; i++)
                {
                    int j = i * 3;
                    positions[b.start + i] = new Vector3(-(b.x + b.local[j]), (float)(b.y + b.local[j + 1] * Math.Max(s, 0.03)),
                        (float)(b.z + b.local[j + 2] - b.local[j + 1] * c));
                    normals[b.start + i] = new Vector3(-b.localN[j], (float)(b.localN[j + 1] * s + b.localN[j + 2] * c),
                        (float)(-b.localN[j + 1] * c + b.localN[j + 2] * s));
                }
                blocks[k].dirty = false;
                dirty = true;
            }
            if (dirty)
            {
                blocksMesh.vertices = positions;
                blocksMesh.normals = normals;
            }
        }

        private Vector3 RandomVector() => new((float)(random.Next() - 0.5), (float)(random.Next() - 0.5), (float)(random.Next() - 0.5));

        private static Quaternion AxisAngle(Vector3 axis, double angle)
        {
            double s = Math.Sin(angle / 2);
            return new Quaternion((float)(axis.x * s), (float)(axis.y * s), (float)(axis.z * s), (float)Math.Cos(angle / 2));
        }

        private void Burst(Vector3 origin, Vector3 direction, int count, double spread, double speed)
        {
            for (int i = 0; i < count && used < Capacity; i++)
            {
                var c = particles[used++];
                c.rest = false;
                c.p = origin + RandomVector() * 0.4f;
                c.v = (direction + RandomVector() * (float)spread).normalized * (float)(speed * (0.7 + random.Next() * 0.5));
                c.axis = RandomVector().normalized;
                c.q = AxisAngle(c.axis, random.Next() * Math.PI * 2);
                c.spin = 5 + random.Next() * 9;
                c.phase = random.Next() * Math.PI * 2;
            }
        }

        private void Shower(int count)
        {
            for (int i = 0; i < count && used < Capacity; i++)
            {
                var c = particles[used++];
                c.rest = false;
                c.p = new Vector3((float)((random.Next() - 0.5) * 12), (float)(8 + random.Next() * 4), (float)(-0.8 + random.Next() * 5));
                c.v = new Vector3((float)((random.Next() - 0.5) * 0.6), (float)(-0.5 - random.Next() * 0.5), (float)((random.Next() - 0.5) * 0.6));
                c.axis = RandomVector().normalized;
                c.q = AxisAngle(c.axis, random.Next() * Math.PI * 2);
                c.spin = 4 + random.Next() * 7;
                c.phase = random.Next() * Math.PI * 2;
            }
        }

        private float FloorAt(float x, float z)
        {
            for (int i = 0; i < data.blocks.Length; i++)
            {
                var b = data.blocks[i];
                if (Math.Abs(x - b.x) < 3.05 / 2 && Math.Abs(z - 1.2) < 3.3 / 2 && blocks[i].u > 0.9)
                {
                    return b.top + 0.015f;
                }
            }
            return Math.Abs(x) < 12 && Math.Abs(z) < 8.25 ? PageTop(x) + 0.03f : 0.015f;
        }

        private void UpdateConfetti(double dt)
        {
            if (used == 0)
            {
                return;
            }
            confettiTime += dt;
            double g = ReducedMotion ? 5 : 9, drag = Math.Exp(-1.9 * dt), term = ReducedMotion ? 0.9 : 1.5;
            for (int i = 0; i < used; i++)
            {
                var c = particles[i];
                if (c.rest)
                {
                    continue;
                }
                c.v.y -= (float)(g * dt);
                c.v.x *= (float)drag;
                c.v.z *= (float)drag;
                if (c.v.y < -term)
                {
                    c.v.y += (float)((-term - c.v.y) * Math.Min(1, dt * 6));
                }
                double flutter = Math.Sin(confettiTime * 5 + c.phase) * 0.9;
                c.p += new Vector3((float)((c.v.x + flutter * 0.4) * dt), (float)(c.v.y * dt),
                    (float)((c.v.z + Math.Cos(confettiTime * 4 + c.phase) * 0.3) * dt));
                c.q = (c.q * AxisAngle(c.axis, c.spin * dt)).normalized;
                float floor = FloorAt(c.p.x, c.p.z);
                if (c.v.y < 0 && c.p.y <= floor)
                {
                    c.p.y = floor;
                    c.rest = true;
                    c.q = AxisAngle(Vector3.right, -Math.PI / 2) * AxisAngle(Vector3.forward, c.phase);
                }
                var q = new Quaternion(c.q.x, -c.q.y, -c.q.z, c.q.w);
                confettiPoses[i] = transform.localToWorldMatrix * Matrix4x4.TRS(Source(c.p), q, Vector3.one);
            }
        }

        private void PoseCamera()
        {
            if (stageCamera == null)
            {
                return;
            }
            double t = Math.Min(1, time / (cameraReducedMotion ? 12 : 9));
            double k = 1 - (1 - t) * (1 - t) * (1 - t);
            double az = -0.2 + 0.24 * k + Math.Sin(time * 0.1) * (cameraReducedMotion ? 0.01 : 0.03);
            double el = 0.36 - 0.17 * k, dist = 31 - 13.5 * k;
            var center = new Vector3(0, 3.1f, 1.2f);
            var eye = center + new Vector3((float)(Math.Sin(az) * Math.Cos(el) * dist), (float)(Math.Sin(el) * dist), (float)(Math.Cos(az) * Math.Cos(el) * dist));
            double aspect = stageCamera.aspect > 0 ? stageCamera.aspect : 1.6;
            double fov = Math.Min(100, Math.Max(30, 2 * Math.Atan(Math.Tan(25 * Math.PI / 180) / Math.Max(0.2, aspect)) * 180 / Math.PI));
            Vector3 forward = (center - eye).normalized;
            Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, forward);
            Vector3 look = center + up * (float)(0.02 * Math.Tan(fov * Math.PI / 360) * Vector3.Distance(center, eye));
            stageCamera.fieldOfView = (float)fov;
            stageCamera.transform.position = transform.TransformPoint(Source(eye));
            stageCamera.transform.LookAt(transform.TransformPoint(Source(look)), transform.up);
        }

        private static Vector3 Source(Vector3 p) => new(-p.x, p.y, p.z);

        private static float PageTop(float x)
        {
            double u = Math.Min(1, Math.Abs(x) / 12.0);
            return (float)(1.34 + 0.32 * (1 - u) * (1 - u) - 0.95 * Math.Exp(-u * u / (0.07 * 0.07)));
        }

        /// 隐藏并释放本轮角色；保留可复用舞台网格与材质。
        public void Hide()
        {
            ClearHeroes();
            shown = false;
            used = 0;
            IsComplete = false;
            blocksFilter.gameObject.SetActive(false);
            if (data != null)
            {
                for (int i = 0; i < blocks.Length; i++)
                {
                    blocks[i] = new BlockPose { dirty = true };
                }
                WriteBlocks();
            }
        }

        private void ClearHeroes()
        {
            foreach (var hero in heroes)
            {
                hero.holder.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(hero.holder.gameObject);
                }
                else
                {
                    DestroyImmediate(hero.holder.gameObject);
                }
            }
            heroes.Clear();
        }

        private void OnDestroy()
        {
            ClearHeroes();
            if (blocksMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(blocksMesh);
                }
                else
                {
                    DestroyImmediate(blocksMesh);
                }
            }
        }
    }
}
