using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 原作桌面立体书的运行时表现，资源实例与动态网格按本场景释放。
    public sealed class KagitKartBookStage : MonoBehaviour
    {
        [SerializeField] internal TextAsset sourceData;
        [SerializeField] internal Transform[] nodes;
        [SerializeField] internal Mesh[] shapeAssets;
        [SerializeField] internal GameObject[] drivers;
        [SerializeField] internal string[] driverIds;
        [SerializeField] internal Texture2D[] leftPages;
        [SerializeField] internal Texture2D[] rightPages;
        [SerializeField] internal Material sky;

        private BookData data;
        private MeshPose[] meshPoses;
        private PiecePose[][] piecePoses;
        private Camera stageCamera;
        private Light stageSun;
        private Transform spin;
        private Transform turntable;
        private Transform sheet;
        private MeshRenderer[] leftPrints;
        private MeshRenderer rightPrint;
        private MeshRenderer sheetFront;
        private MeshRenderer sheetBack;
        private readonly List<Hero> heroes = new();
        private Hero deskHero;
        private Hero currentHero;
        private MaterialPropertyBlock block;
        private KagitKartCommand page;
        private bool initialized;
        private int chapter;
        private int nextChapter;
        private int pendingChapter = -1;
        private int turnPhase;
        private float turnTime;
        private bool forwardTurn;
        private string turnHero;
        private float closed;
        private float bookFrom;
        private float bookTo;
        private float bookTime;
        private float bookDuration;
        private float bookWait;
        private float cameraOpen;
        private float cameraFrom;
        private float cameraTarget;
        private float cameraTime;
        private float cameraDuration;
        private float shotTime;
        private float leafTime;
        private float leafAmount;
        private float spinYaw;
        private float spinSpeed;
        private float spinTarget;
        private float blendTime;
        private float blendDuration;
        private Vector3 blendPosition;
        private Quaternion blendRotation;
        private float blendFov;

        /// 与原作相同：0 打开，1 闭合。
        public float ClosedFraction => closed;
        /// 正在展示或翻向的章节。
        public string ChapterId => KagitKartCatalog.Tracks[turnPhase == 0 ? chapter : nextChapter];
        public bool Turning => turnPhase != 0;
        public string CurrentCharacterId => currentHero?.id;
        public event Action PageTurnStarted;

        private sealed class MeshPose
        {
            public Mesh mesh;
            public Vector3[] positions;
            public Vector3[] normals;
            public Vector2[] uv;
            public float amount = -1;
        }

        private struct PiecePose
        {
            public float u, v, target, delay;
            public bool dirty;
        }

        private sealed class Hero
        {
            public string id;
            public Transform holder;
            public KagitKartCharacterVisual visual;
            public float cheerTime;
            public float s, v, target = 1, delay;
        }

        private sealed class BookData
        {
            public string sourceCommit;
            public NodeData[] nodes;
            public Dynamics dynamics;
        }

        private sealed class NodeData
        {
            public string name;
            public int geometry = -1;
        }

        private sealed class Dynamics
        {
            public ShapeBase[] cover;
            public ShapeBase[] leaves;
            public int spine, sheet;
            public float[] sheetBase;
            public PopupBatch[] popups;
        }

        private sealed class ShapeBase
        {
            public int geometry, k;
            public float[] @base;
        }

        private sealed class PopupBatch
        {
            public int node;
            public Piece[] pieces;
        }

        private sealed class Piece
        {
            public string kind;
            public float x, y, z, yaw;
            public int fdir, start, count;
            public float[] local, localN;
        }

        /// <summary>借用本 Demo 摄像机和日光，所有动态网格均克隆本实例资源。</summary>
        /// <param name="camera">本场景比赛与菜单共用的摄像机。</param>
        /// <param name="sun">本场景日光，不修改项目全局画面设置。</param>
        public void Initialize(Camera camera, Light sun)
        {
            stageCamera = camera;
            stageSun = sun;
            block = new MaterialPropertyBlock();
            data = JsonConvert.DeserializeObject<BookData>(sourceData.text, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                MaxDepth = 64
            });
            if (data?.sourceCommit != KagitKartCatalog.SourceCommit || data.dynamics == null || data.nodes?.Length != nodes.Length)
            {
                throw new ArgumentException("立体书数据缺失或源码版本不匹配。");
            }
            meshPoses = new MeshPose[shapeAssets.Length];
            foreach (var part in data.dynamics.cover)
            {
                Mutable(part.geometry);
            }
            foreach (var leaf in data.dynamics.leaves)
            {
                Mutable(leaf.geometry);
            }
            Mutable(data.dynamics.spine);
            Mutable(data.dynamics.sheet);
            piecePoses = new PiecePose[data.dynamics.popups.Length][];
            for (int b = 0; b < piecePoses.Length; b++)
            {
                var batch = data.dynamics.popups[b];
                Mutable(data.nodes[batch.node].geometry);
                piecePoses[b] = new PiecePose[batch.pieces.Length];
            }
            turntable = Named("cinematics:turntable");
            spin = turntable.GetChild(0);
            sheet = Named("cinematics:sheet");
            rightPrint = Named("book:leaf0").GetComponent<MeshRenderer>();
            leftPrints = new[] { Named("book:pageLeft").GetComponent<MeshRenderer>() };
            sheetFront = Named("sheet:front").GetComponent<MeshRenderer>();
            sheetBack = Named("sheet:back").GetComponent<MeshRenderer>();
            ApplyChapter(0);
        }

        private Transform Named(string name)
        {
            for (int i = 0; i < data.nodes.Length; i++)
            {
                if (data.nodes[i].name == name)
                {
                    return nodes[i];
                }
            }
            throw new ArgumentException("立体书节点缺失：" + name);
        }

        private MeshPose Mutable(int geometry)
        {
            if (meshPoses[geometry] != null)
            {
                return meshPoses[geometry];
            }
            var mesh = Instantiate(shapeAssets[geometry]);
            mesh.MarkDynamic();
            var pose = new MeshPose { mesh = mesh, positions = mesh.vertices, normals = mesh.normals, uv = mesh.uv };
            meshPoses[geometry] = pose;
            for (int n = 0; n < data.nodes.Length; n++)
            {
                if (data.nodes[n].geometry == geometry)
                {
                    nodes[n].GetComponent<MeshFilter>().sharedMesh = mesh;
                }
            }
            return pose;
        }

        /// <summary>切换原作标题/浏览/车手镜头，选赛页面不另建静态背景。</summary>
        /// <param name="next">封面、模式、克重、车手或章节页面。</param>
        /// <param name="characterId">当前选赛车手。</param>
        /// <param name="trackId">当前章节。</param>
        public void SetPage(KagitKartCommand next, string characterId, string trackId)
        {
            gameObject.SetActive(true);
            stageCamera.clearFlags = CameraClearFlags.Skybox;
            stageCamera.GetComponent<Skybox>().material = sky;
            ColorUtility.TryParseHtmlString("#ffc47e", out var color);
            stageSun.color = color;
            stageSun.intensity = 2.5f;
            stageSun.transform.rotation = Quaternion.LookRotation(-new Vector3(0.72f, 0.53f, 0.45f));
            bool wasTitle = initialized && page == KagitKartCommand.Title;
            bool selecting = next == KagitKartCommand.Characters;
            bool changedShot = initialized && selecting != (page == KagitKartCommand.Characters);
            page = next;
            shotTime = changedShot ? 0 : shotTime;
            if (changedShot)
            {
                blendPosition = stageCamera.transform.position;
                blendRotation = stageCamera.transform.rotation;
                blendFov = stageCamera.fieldOfView;
                blendTime = 0;
                blendDuration = selecting ? 1.15f : 1.3f;
            }
            spinTarget = selecting ? 0.22f : 0;
            if (next == KagitKartCommand.Title)
            {
                Fold(false, !initialized);
                HideHeroes();
                SetBook(1, !initialized);
                SetDeskHero(characterId, !initialized, initialized ? 1.3f : 0);
                MoveCamera(0, 1.9f);
            }
            else
            {
                if (deskHero != null)
                {
                    deskHero.target = 0;
                    deskHero.delay = 0;
                }
                SetBook(0, !initialized);
                if (wasTitle || !initialized)
                {
                    Fold(true, !initialized);
                }
                SetHero(characterId, initialized, selecting ? 1.05f : 1.25f, wasTitle ? 1 : 0.1f);
                if (selecting)
                {
                    CheerHero();
                }
                MoveCamera(1, 1.7f);
                if (next == KagitKartCommand.Tracks)
                {
                    ShowChapter(trackId);
                }
            }
            if (!initialized)
            {
                cameraOpen = cameraFrom = cameraTarget = next == KagitKartCommand.Title ? 0 : 1;
            }
            initialized = true;
        }

        private void SetBook(float target, bool snap)
        {
            if (target == 1 && turnPhase != 0)
            {
                FinishTurn();
                pendingChapter = -1;
            }
            if (snap)
            {
                closed = bookFrom = bookTo = target;
                bookDuration = bookWait = 0;
                ApplyCover();
                return;
            }
            if (bookTo == target && bookDuration > 0 || closed == target)
            {
                return;
            }
            bookFrom = closed;
            bookTo = target;
            bookTime = 0;
            bookWait = target == 1 && closed == 0 ? 0.42f : 0;
            bookDuration = target == 1 ? 0.95f * Mathf.Max(0.3f, 1 - closed) : 1.05f * Mathf.Max(0.3f, closed);
        }

        private void MoveCamera(float target, float duration)
        {
            if (cameraTarget == target)
            {
                return;
            }
            cameraFrom = cameraOpen;
            cameraTarget = target;
            cameraTime = 0;
            cameraDuration = duration * Mathf.Abs(target - cameraOpen);
        }

        private void Fold(bool up, bool snap = false)
        {
            float remaining = bookTo == 0 ? Mathf.Max(0, bookDuration - bookTime) : 0;
            for (int b = 0; b < piecePoses.Length; b++)
            {
                for (int p = 0; p < piecePoses[b].Length; p++)
                {
                    var piece = data.dynamics.popups[b].pieces[p];
                    ref var pose = ref piecePoses[b][p];
                    pose.target = up ? 1 : 0;
                    pose.delay = snap ? 0 : up
                        ? 0.05f + Mathf.Abs(piece.x) / 12 * 0.28f + (piece.z + 8.25f) / 16.5f * 0.12f
                            + (piece.x < 0 ? remaining : Mathf.Max(0, remaining - bookDuration * 0.5f))
                        : (1 - Mathf.Abs(piece.x) / 12) * 0.12f;
                    if (snap)
                    {
                        pose.u = pose.target;
                        pose.v = 0;
                    }
                    pose.dirty = true;
                }
            }
        }

        /// <summary>焦点变动时弹出相应车手，不触发比赛。</summary>
        /// <param name="id">原作稳定车手标识。</param>
        public void PreviewCharacter(string id)
        {
            SetHero(id, true, 1.05f, 0.1f);
            CheerHero();
        }

        private void CheerHero()
        {
            currentHero.visual.SetEmotion("cheer");
            currentHero.cheerTime = 1.8f;
        }

        private Hero CreateHero(string id, Transform parent)
        {
            int index = Array.IndexOf(driverIds, id);
            if (index < 0)
            {
                throw new ArgumentException("立体书车手资源缺失。", nameof(id));
            }
            var holder = new GameObject("纸艺车手：" + KagitKartCatalog.CharacterName(id)).transform;
            holder.SetParent(parent, false);
            var visual = Instantiate(drivers[index], holder).GetComponent<KagitKartCharacterVisual>();
            visual.Bind(null, null);
            visual.SetEmotion("idle");
            visual.SetEnvironment(rightPrint.sharedMaterial);
            return new Hero { id = id, holder = holder, visual = visual };
        }

        private void SetHero(string id, bool animate, float face, float delay)
        {
            if (currentHero?.id == id && currentHero.target > 0)
            {
                return;
            }
            for (int i = heroes.Count - 1; i >= 0; i--)
            {
                if (heroes[i] != currentHero)
                {
                    Destroy(heroes[i].holder.gameObject);
                    heroes.RemoveAt(i);
                }
            }
            if (currentHero != null)
            {
                currentHero.target = 0;
            }
            currentHero = CreateHero(id, spin);
            currentHero.s = animate ? 0 : 1;
            currentHero.delay = animate ? delay : 0;
            currentHero.holder.localPosition = new Vector3(0, 0.105f, 0);
            currentHero.holder.localRotation = Quaternion.Euler(0, -(face - spinYaw) * Mathf.Rad2Deg, 0);
            heroes.Add(currentHero);
            ApplyHero(currentHero, 1);
        }

        private void HideHeroes()
        {
            foreach (var hero in heroes)
            {
                hero.target = 0;
            }
            currentHero = null;
        }

        private void SetDeskHero(string id, bool snap, float delay)
        {
            if (deskHero?.id != id)
            {
                if (deskHero != null)
                {
                    Destroy(deskHero.holder.gameObject);
                }
                deskHero = CreateHero(id, transform);
                deskHero.holder.localPosition = new Vector3(4.6f, 0.005f, 5);
                deskHero.holder.localRotation = Quaternion.Euler(0, -1.15f * Mathf.Rad2Deg, 0);
            }
            deskHero.target = 1;
            deskHero.delay = delay;
            if (snap)
            {
                deskHero.s = 1;
            }
            ApplyHero(deskHero, 1.55f);
        }

        private static void ApplyHero(Hero hero, float size)
        {
            float s = Mathf.Max(0.001f, hero.s);
            float width = 0.82f + 0.18f * Mathf.Min(1, s);
            hero.holder.localScale = new Vector3(width, s, width) * size;
            hero.holder.gameObject.SetActive(s > 0.01f);
        }

        private static void Spring(ref float value, ref float velocity, float target, float omega, float damping, float dt)
        {
            while (dt > 0.000001f)
            {
                float step = Mathf.Min(dt, 1f / 120);
                velocity += (omega * omega * (target - value) - 2 * damping * omega * velocity) * step;
                value += velocity * step;
                dt -= step;
            }
            if (Mathf.Abs(value - target) < 0.001f && Mathf.Abs(velocity) < 0.01f)
            {
                value = target;
                velocity = 0;
            }
        }

        /// <summary>宿主每帧交付表现时间，暂停或演出停止时传入 0。</summary>
        /// <param name="dt">未缩放帧时间，单帧最多推进 0.1 秒。</param>
        public void Advance(float dt)
        {
            if (!initialized || !gameObject.activeInHierarchy)
            {
                return;
            }
            dt = Mathf.Clamp(dt, 0, 0.1f);
            shotTime += dt;
            if (bookDuration > 0)
            {
                if (bookWait > 0)
                {
                    bookWait -= dt;
                }
                else
                {
                    bookTime += dt;
                    float t = Mathf.Min(1, bookTime / bookDuration);
                    closed = Mathf.LerpUnclamped(bookFrom, bookTo, Ease(t));
                    ApplyCover();
                    if (t >= 1)
                    {
                        bookDuration = 0;
                    }
                }
            }
            UpdateTurn(dt);
            UpdatePopups(dt);
            spinSpeed += (spinTarget - spinSpeed) * Mathf.Min(1, dt * 2.2f);
            if (Mathf.Abs(spinTarget) > 0.0001f || Mathf.Abs(spinSpeed) > 0.001f)
            {
                spinYaw += spinSpeed * dt;
            }
            else if (currentHero != null)
            {
                float worldYaw = spinYaw - currentHero.holder.localEulerAngles.y * Mathf.Deg2Rad;
                float difference = Mathf.Atan2(Mathf.Sin(1.25f - worldYaw), Mathf.Cos(1.25f - worldYaw));
                spinYaw += difference * Mathf.Min(1, dt * 1.6f);
            }
            spin.localRotation = Quaternion.Euler(0, -spinYaw * Mathf.Rad2Deg, 0);
            UpdateHeroes(dt);
            UpdateLeaves(dt);
            cameraTime += dt;
            float blend = cameraDuration > 0 ? Mathf.Min(1, cameraTime / cameraDuration) : 1;
            cameraOpen = Mathf.LerpUnclamped(cameraFrom, cameraTarget, 0.5f - 0.5f * Mathf.Cos(Mathf.PI * blend));
            ApplyCamera(dt);
        }

        private static float Ease(float t) => t < 0.5f ? 2 * t * t : 1 - 2 * (1 - t) * (1 - t);

        private void UpdateHeroes(float dt)
        {
            for (int i = heroes.Count - 1; i >= 0; i--)
            {
                var h = heroes[i];
                UpdateHero(h, dt, 1);
                if (h.target == 0 && h.s < 0.03f)
                {
                    Destroy(h.holder.gameObject);
                    heroes.RemoveAt(i);
                }
            }
            if (deskHero != null)
            {
                UpdateHero(deskHero, dt, 1.55f, true);
            }
        }

        private static void UpdateHero(Hero h, float dt, float size, bool desk = false)
        {
            if (h.delay > 0)
            {
                h.delay -= dt;
            }
            else
            {
                bool up = desk ? h.target > h.s : h.target > 0;
                Spring(ref h.s, ref h.v, h.target, Mathf.PI * 2 * (up ? 2.4f : 3.2f), up ? 0.42f : 1, dt);
            }
            ApplyHero(h, size);
            if (h.cheerTime > 0)
            {
                h.cheerTime -= dt;
                if (h.cheerTime <= 0 && h.target > 0)
                {
                    h.visual.SetEmotion("idle");
                }
            }
            if (h.holder.gameObject.activeSelf)
            {
                h.visual.Advance(dt);
            }
        }

        private static Vector3 Point(float[] values, int offset) => new(values[offset], values[offset + 1], values[offset + 2]);

        private static void Upload(MeshPose pose, bool recalculate)
        {
            pose.mesh.vertices = pose.positions;
            if (recalculate)
            {
                pose.mesh.RecalculateNormals();
            }
            else
            {
                pose.mesh.normals = pose.normals;
            }
            pose.mesh.RecalculateBounds();
        }

        private void ApplyCover()
        {
            foreach (var part in data.dynamics.cover)
            {
                var pose = Mutable(part.geometry);
                for (int i = 0; i < pose.positions.Length; i++)
                {
                    pose.positions[i] = KagitKartVehicle.ToUnity(KagitKartBookGeometry.CoverPoint(Point(part.@base, i * 3), closed));
                }
                Upload(pose, true);
            }
            var spineNode = Named("book:spine");
            spineNode.gameObject.SetActive(closed > 0.002f);
            if (spineNode.gameObject.activeSelf)
            {
                var pose = Mutable(data.dynamics.spine);
                var end = KagitKartBookGeometry.CoverPoint(Vector3.zero, closed);
                float length = Mathf.Max(new Vector2(end.x, end.y).magnitude, 0.000001f);
                float nx = -end.y / length, ny = end.x / length;
                for (int row = 0; row < 2; row++)
                {
                    for (int column = 0; column <= 6; column++)
                    {
                        float t = column / 6f;
                        float curve = Mathf.Sin(Mathf.PI * t) * 0.34f * closed * closed;
                        int i = row * 7 + column;
                        pose.positions[i] = KagitKartVehicle.ToUnity(new Vector3(end.x * t + nx * curve, end.y * t + ny * curve, row == 0 ? -8.7f : 8.7f));
                        pose.uv[i] = new Vector2(t * length / 1.6f, (row == 0 ? -8.7f : 8.7f) / 1.6f);
                    }
                }
                pose.mesh.uv = pose.uv;
                Upload(pose, true);
            }
            Named("cinematics:popupsLeft").gameObject.SetActive(closed < 0.999f);
            Named("cinematics:popups").gameObject.SetActive(closed < 0.45f);
            turntable.gameObject.SetActive(closed < 0.999f && turnPhase == 0);
            for (int b = 0; b < piecePoses.Length; b++)
            {
                if (data.nodes[data.dynamics.popups[b].node].name == "cinematics:popupsLeft")
                {
                    for (int p = 0; p < piecePoses[b].Length; p++)
                    {
                        piecePoses[b][p].dirty = true;
                    }
                }
            }
        }

        private void UpdatePopups(float dt)
        {
            for (int b = 0; b < piecePoses.Length; b++)
            {
                var batch = data.dynamics.popups[b];
                var mesh = Mutable(data.nodes[batch.node].geometry);
                bool cover = data.nodes[batch.node].name == "cinematics:popupsLeft";
                bool dirty = false;
                for (int p = 0; p < batch.pieces.Length; p++)
                {
                    var piece = batch.pieces[p];
                    ref var pose = ref piecePoses[b][p];
                    if (piece.kind != "static" && (pose.u != pose.target || pose.v != 0))
                    {
                        if (pose.delay > 0)
                        {
                            pose.delay -= dt;
                        }
                        else
                        {
                            bool up = pose.target > pose.u;
                            Spring(ref pose.u, ref pose.v, pose.target, Mathf.PI * 2 * (up ? 2.2f : 2.4f), up ? 0.5f : 1, dt);
                            if (pose.target == 0 && pose.u < 0)
                            {
                                pose.u = pose.v = 0;
                            }
                            pose.dirty = true;
                        }
                    }
                    if (!pose.dirty)
                    {
                        continue;
                    }
                    dirty = true;
                    pose.dirty = false;
                    float angle = piece.fdir < 0 ? Mathf.PI - pose.u * Mathf.PI / 2 : pose.u * Mathf.PI / 2;
                    float s = Mathf.Sin(angle), c = Mathf.Cos(angle), cy = Mathf.Cos(piece.yaw), sy = Mathf.Sin(piece.yaw);
                    for (int i = 0; i < piece.count; i++)
                    {
                        var local = Point(piece.local, i * 3);
                        var normal = Point(piece.localN, i * 3);
                        float yy = local.y, zz = local.z;
                        if (piece.kind == "hinge")
                        {
                            yy = local.y * s + local.z * c;
                            zz = -local.y * c + local.z * s;
                        }
                        else if (piece.kind == "box")
                        {
                            yy = local.y * Mathf.Max(s, 0.03f);
                            zz = local.z - local.y * c;
                        }
                        var position = new Vector3(piece.x + local.x * cy + zz * sy, piece.y + yy, piece.z - local.x * sy + zz * cy);
                        float my = normal.y, mz = normal.z;
                        if (piece.kind != "static")
                        {
                            my = normal.y * s + normal.z * c;
                            mz = -normal.y * c + normal.z * s;
                        }
                        normal = new Vector3(normal.x * cy + mz * sy, my, -normal.x * sy + mz * cy);
                        if (cover && closed > 0)
                        {
                            position = KagitKartBookGeometry.CoverPoint(position, closed);
                            float co = Mathf.Cos(-Mathf.PI * closed), si = Mathf.Sin(-Mathf.PI * closed);
                            normal = new Vector3(normal.x * co - normal.y * si, normal.x * si + normal.y * co, normal.z);
                        }
                        mesh.positions[piece.start + i] = KagitKartVehicle.ToUnity(position);
                        mesh.normals[piece.start + i] = KagitKartVehicle.ToUnity(normal);
                    }
                }
                if (dirty)
                {
                    Upload(mesh, false);
                }
            }
        }

        private void UpdateLeaves(float dt)
        {
            bool want = closed == 0 && bookDuration == 0 && turnPhase == 0;
            leafAmount += ((want ? 1 : 0) - leafAmount) * Mathf.Min(1, dt * (want ? 1.5f : 6));
            leafTime += dt;
            foreach (var leaf in data.dynamics.leaves)
            {
                float wave = Mathf.Max(0, Mathf.Sin((leafTime - leaf.k * 0.45f) / 6.5f * Mathf.PI * 2 - 0.6f));
                float amount = leafAmount * (0.06f + 0.62f * wave * wave) * (leaf.k == 0 ? 1 : 0.55f);
                var pose = Mutable(leaf.geometry);
                if (Mathf.Abs(amount - pose.amount) <= 0.0001f)
                {
                    continue;
                }
                pose.amount = amount;
                for (int row = 0; row <= 10; row++)
                {
                    float z = Mathf.SmoothStep(0, 1, Mathf.Clamp01((row / 10f - 0.55f) / 0.45f));
                    for (int column = 0; column <= 26; column++)
                    {
                        int i = row * 27 + column;
                        float edge = Mathf.SmoothStep(0, 1, Mathf.Clamp01((column / 26f - 0.64f) / 0.36f));
                        float lift = amount * Mathf.Pow(edge, 1.5f) * z;
                        var point = Point(leaf.@base, i * 3);
                        point.x -= lift * lift * 0.08f;
                        point.y += lift;
                        pose.positions[i] = KagitKartVehicle.ToUnity(point);
                    }
                }
                Upload(pose, true);
            }
        }

        /// <summary>焦点改变时按原作折下纸景并卷页，连续请求保留最后一个待翻章节。</summary>
        /// <param name="id">原作章节标识。</param>
        public void ShowChapter(string id)
        {
            int index = -1;
            for (int i = 0; i < KagitKartCatalog.Tracks.Count; i++)
            {
                if (KagitKartCatalog.Tracks[i] == id)
                {
                    index = i;
                }
            }
            if (index < 0)
            {
                throw new ArgumentException("未知立体书章节。", nameof(id));
            }
            if (turnPhase != 0)
            {
                pendingChapter = index;
                return;
            }
            if (index == chapter)
            {
                return;
            }
            if (closed > 0 || bookDuration > 0)
            {
                ApplyChapter(index);
                return;
            }
            nextChapter = index;
            pendingChapter = -1;
            forwardTurn = index >= chapter;
            turnPhase = 1;
            turnTime = 0;
            turnHero = currentHero?.id ?? turnHero;
            Fold(false);
            HideHeroes();
        }

        private void UpdateTurn(float dt)
        {
            if (turnPhase == 0)
            {
                return;
            }
            turnTime += dt;
            if (turnPhase == 1)
            {
                if (turnTime < 0.34f)
                {
                    return;
                }
                turnPhase = 2;
                turnTime = 0;
                PageTurnStarted?.Invoke();
                ApplyTexture(sheetFront, rightPages[forwardTurn ? chapter : nextChapter]);
                ApplyTexture(sheetBack, leftPages[forwardTurn ? nextChapter : chapter], true);
                if (forwardTurn)
                {
                    ApplyTexture(rightPrint, rightPages[nextChapter]);
                }
                else
                {
                    foreach (var renderer in leftPrints)
                    {
                        ApplyTexture(renderer, leftPages[nextChapter]);
                    }
                }
                sheet.gameObject.SetActive(true);
                turntable.gameObject.SetActive(false);
            }
            float t = Mathf.Min(1, turnTime / 0.9f);
            float progress = forwardTurn ? Ease(t) : 1 - Ease(t);
            var pose = Mutable(data.dynamics.sheet);
            for (int i = 0; i < pose.positions.Length; i++)
            {
                pose.positions[i] = KagitKartVehicle.ToUnity(KagitKartBookGeometry.SheetPoint(Point(data.dynamics.sheetBase, i * 3), progress, forwardTurn));
            }
            Upload(pose, true);
            if (t < 1)
            {
                return;
            }
            FinishTurn();
            int pending = pendingChapter;
            pendingChapter = -1;
            if (pending >= 0 && pending != chapter)
            {
                ShowChapter(KagitKartCatalog.Tracks[pending]);
                return;
            }
            Fold(true);
            if (turnHero != null)
            {
                SetHero(turnHero, true, 1.25f, 0.1f);
            }
            turnHero = null;
        }

        private void FinishTurn()
        {
            ApplyChapter(nextChapter);
            turnPhase = 0;
            sheet.gameObject.SetActive(false);
            turntable.gameObject.SetActive(closed < 0.999f);
        }

        private void ApplyChapter(int index)
        {
            chapter = index;
            ApplyTexture(rightPrint, rightPages[index]);
            foreach (var renderer in leftPrints)
            {
                ApplyTexture(renderer, leftPages[index]);
            }
        }

        private void ApplyTexture(MeshRenderer renderer, Texture texture, bool mirror = false)
        {
            renderer.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", texture);
            block.SetVector("_BaseMap_ST", mirror ? new Vector4(-1, 1, 1, 0) : new Vector4(1, 1, 0, 0));
            renderer.SetPropertyBlock(block);
            block.Clear();
        }

        private void ApplyCamera(float dt)
        {
            float aspect = stageCamera.aspect;
            Vector3 focus;
            float azimuth, elevation, distance, fov;
            Vector2 frame;
            if (page == KagitKartCommand.Characters)
            {
                var subject = turntable.TransformPoint(new Vector3(0, 0.905f, 0));
                focus = KagitKartVehicle.ToUnity(subject);
                azimuth = 0.3f + Mathf.Sin(shotTime * 0.21f) * 0.05f;
                elevation = 0.13f;
                distance = 9.6f;
                fov = KagitKartBookGeometry.FitFov(aspect, 32, 44);
                frame = new Vector2(aspect > 1.2f ? 0.2f : 0, 0.06f);
            }
            else
            {
                float narrow = Mathf.Clamp01((1.25f - aspect) / 0.8f);
                float closedAz = 0.2f - 0.12f * narrow + Mathf.Sin(shotTime / 40 * Mathf.PI * 2) * 0.035f;
                float openAz = 0.3f + Mathf.Sin(shotTime / 64 * Mathf.PI * 2) * 0.42f;
                float openEl = 0.19f + Mathf.Sin(shotTime / 64 * Mathf.PI * 2 + 1.1f) * 0.02f;
                float smoothed = cameraOpen * cameraOpen * (3 - 2 * cameraOpen);
                focus = Vector3.LerpUnclamped(new Vector3(6.25f, 1.5f, 0.5f), new Vector3(3, 2.7f, -1.4f), smoothed);
                azimuth = Mathf.LerpUnclamped(closedAz, openAz, cameraOpen);
                elevation = Mathf.LerpUnclamped(0.8f + 0.22f * narrow, openEl, cameraOpen);
                distance = Mathf.LerpUnclamped(30.5f - 6.5f * narrow, 28, cameraOpen);
                fov = Mathf.LerpUnclamped(KagitKartBookGeometry.FitFov(aspect, 32, 44), KagitKartBookGeometry.FitFov(aspect, 31, 56), cameraOpen);
                frame = new Vector2(0, Mathf.LerpUnclamped(aspect < 1 ? 0.12f : 0.08f, 0.24f, cameraOpen));
            }
            Vector3 eye = focus + new Vector3(Mathf.Sin(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Cos(azimuth) * Mathf.Cos(elevation)) * distance;
            Vector3 look = KagitKartBookGeometry.FrameAt(eye, focus, frame, fov, aspect);
            var position = KagitKartVehicle.ToUnity(eye);
            var rotation = Quaternion.LookRotation(KagitKartVehicle.ToUnity(look - eye));
            if (blendDuration > 0)
            {
                blendTime += dt;
                float t = Mathf.Min(1, blendTime / blendDuration);
                float k = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t);
                position = Vector3.LerpUnclamped(blendPosition, position, k);
                rotation = Quaternion.SlerpUnclamped(blendRotation, rotation, k);
                fov = Mathf.LerpUnclamped(blendFov, fov, k);
                if (t >= 1)
                {
                    blendDuration = 0;
                }
            }
            stageCamera.transform.SetPositionAndRotation(position, rotation);
            stageCamera.fieldOfView = fov;
        }

        private void OnDestroy()
        {
            if (meshPoses == null)
            {
                return;
            }
            foreach (var pose in meshPoses)
            {
                if (pose != null)
                {
                    Destroy(pose.mesh);
                }
            }
        }
    }
}
