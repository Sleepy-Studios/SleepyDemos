using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Rules;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.JinxCasino
{
    /// S1样板的Editor装配；使用四个独立源模型保存大厅与机台操作区域。
    public static class JinxCasinoImmersionLayoutBuilder
    {
        /// 交还主Builder的保存引用；宿主、相机与HUD仍由既有正式启动入口配置。
        public sealed class LayoutReferences
        {
            public Transform Root;
            public Transform Spawn;
            public JinxCasinoStation[] Stations;
            public Transform[] StatePlaques;
            public Transform BuddyStand;
        }

        /// <summary>装配独立样板大厅与真实操作目标；不打开/保存Scene，也不改变旧原型。</summary>
        /// <param name="parent">主Builder提供的独立临时场景根。</param>
        /// <param name="blueprintPath">随模型保存的S1Layout.json蓝图路径。</param>
        /// <param name="hallPrefab">独立S1Hall源，含弧壁龛/罩棚/分层装饰，无DCC相机灯或Collider。</param>
        /// <param name="stationPrefabs">S1Slots/S1Blackjack/S1Levers；原Slots等65资源不作为回退。</param>
        public static LayoutReferences Build(Transform parent, string blueprintPath, GameObject hallPrefab,
            Dictionary<string, GameObject> stationPrefabs)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("沉浸样板只允许Editor静态装配。");
            if (parent == null || hallPrefab == null || stationPrefabs == null) throw new ArgumentException("请先提供独立S1大厅及三个正式机台源。");
            var blueprint = JsonUtility.FromJson<Blueprint>(File.ReadAllText(blueprintPath));
            if (blueprint == null || blueprint.version != 1 || blueprint.stations == null || blueprint.stations.Length != 3)
                throw new InvalidOperationException("样板蓝图必须仅有三个正式机台。");
            ValidateSource(hallPrefab);
            foreach (var station in blueprint.stations)
            {
                if (!stationPrefabs.TryGetValue(station.model, out var prefab) || prefab == null)
                    throw new InvalidOperationException("缺少独立新源，不允许用旧模型代替：" + station.model);
                ValidateSource(prefab);
                foreach (var target in station.targets)
                {
                    var visual = RequireNode(prefab.transform, target.node);
                    if (visual.GetComponentsInChildren<Renderer>(true).Length == 0)
                        throw new InvalidOperationException("操作目标必须有真实可见物件：" + target.node);
                    if ((visual.position - prefab.transform.position - Vector(target.position)).sqrMagnitude > .0025f)
                        throw new InvalidOperationException("操作模型挂点与已审核热区相差超过5厘米：" + target.node);
                }
            }
            // 不删除同名根，避免误覆盖调用方已有样板或历史节点；由外层事务决定保存目的地。
            if (parent.Find("ImmersionS1") != null) throw new InvalidOperationException("调用根已有样板，请在独立场景中装配。");
            var root = Child(parent, "ImmersionS1", Vector3.zero);
            var hall = InstantiateSource(hallPrefab, root);
            foreach (var renderer in RequireNode(hall, "Hall.Ceiling").GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            CreateRoomColliders(root, blueprint);
            var result = new LayoutReferences { Root = root, Spawn = Child(root, "PlayerSpawn", Vector(blueprint.spawn)) };
            var stations = new List<JinxCasinoStation>(); var plaques = new List<Transform>();
            foreach (var definition in blueprint.stations)
            {
                var mount = Child(root, definition.id, Vector(definition.position)); mount.localRotation = Quaternion.Euler(0, definition.yaw, 0);
                var visual = InstantiateSource(stationPrefabs[definition.model], mount);
                if ((CasinoGameKind)definition.game == CasinoGameKind.Blackjack)
                {
                    RequireNode(visual, "Blackjack.CardFaceLibrary").gameObject.SetActive(false);
                    RequireNode(visual, "Blackjack.DrawCard").gameObject.SetActive(false);
                    for (int card = 0; card < 12; card++)
                    {
                        RequireNode(visual, "Blackjack.PlayerCard" + card).gameObject.SetActive(false);
                        RequireNode(visual, "Blackjack.DealerCard" + card).gameObject.SetActive(false);
                    }
                }
                if ((CasinoGameKind)definition.game == CasinoGameKind.CooperativeLevers)
                {
                    for (int player = 0; player < 2; player++) RequireNode(visual, "CooperativeLevers.HelpWindow" + player).gameObject.SetActive(false);
                    RequireNode(visual, "CooperativeLevers.HelpWrench").gameObject.SetActive(false);
                }
                if ((CasinoGameKind)definition.game == CasinoGameKind.Slots)
                    for (int chip = 0; chip < 8; chip++) RequireNode(visual, "Slots.PayoutChip" + chip).gameObject.SetActive(false);
                var approach = Child(mount, "Approach", Vector(definition.approach));
                var lookAt = Child(mount, "FocusLookAt", Vector(definition.lookAt));
                var focus = Child(mount, "FocusPose", Vector(definition.focus));
                focus.rotation = Quaternion.LookRotation(lookAt.position - focus.position, Vector3.up);
                var body = Child(mount, "BodyCollision", Vector(definition.bodyCenter)).gameObject.AddComponent<BoxCollider>();
                body.size = Vector(definition.bodySize);
                var station = mount.gameObject.AddComponent<JinxCasinoStation>();
                station.Configure((CasinoGameKind)definition.game, 0, approach);
                var targetsRoot = Child(mount, "OperationTargets", Vector3.zero); var targets = new List<JinxCasinoTableTarget>();
                foreach (var target in definition.targets.OrderBy(value => value.order))
                {
                    var targetRoot = Child(targetsRoot, target.id, Vector(target.position));
                    var collider = targetRoot.gameObject.AddComponent<BoxCollider>(); collider.size = Vector(target.size);
                    var press = RequireNode(visual, target.node); var feedback = press.GetComponentsInChildren<Renderer>(true);
                    var saved = targetRoot.gameObject.AddComponent<JinxCasinoTableTarget>();
                    saved.Configure(definition.id + "." + target.id, (JinxCasinoTableAction)Enum.Parse(typeof(JinxCasinoTableAction), target.action),
                        target.value, target.order, feedback, press);
                    targets.Add(saved);
                }
                var serialized = new SerializedObject(station);
                serialized.FindProperty("stationId").stringValue = definition.id;
                serialized.FindProperty("focusPose").objectReferenceValue = focus;
                serialized.FindProperty("focusFieldOfView").floatValue = definition.fieldOfView;
                var targetArray = serialized.FindProperty("targets"); targetArray.arraySize = targets.Count;
                for (int index = 0; index < targets.Count; index++) targetArray.GetArrayElementAtIndex(index).objectReferenceValue = targets[index];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                // 仅保存短状态铭牌挂点；主Agent绑定真实金额/规则/结果，不构造旧UGUI操作面板。
                plaques.Add(RequireNode(visual, "StatePlaque")); stations.Add(station);
                if ((CasinoGameKind)definition.game == CasinoGameKind.CooperativeLevers)
                    result.BuddyStand = Child(mount, "BuddyStand", new Vector3(-1.62f, 0, -.13f));
            }
            result.Stations = stations.ToArray(); result.StatePlaques = plaques.ToArray(); return result;
        }

        private static void CreateRoomColliders(Transform parent, Blueprint blueprint)
        {
            float width = blueprint.roomWidth, depth = blueprint.roomDepth, height = blueprint.roomHeight;
            var center = Vector(blueprint.roomCenter);
            Box(parent, "FloorCollision", center + Vector3.down * .125f, new Vector3(width, .25f, depth));
            foreach (float side in new[] { -1f, 1f })
                Box(parent, "SideWallCollision" + side, center + new Vector3(side * (width / 2 + .1f), height / 2, 0), new Vector3(.2f, height, depth));
            Box(parent, "BackWallCollision", center + new Vector3(0, height / 2, depth / 2 + .1f), new Vector3(width, height, .2f));
            // 前门开3.2米，源罩棚也按同宽留空；不增加运行区域门或隐形阻挡。
            float segmentWidth = (width - 3.2f) / 2;
            foreach (float side in new[] { -1f, 1f })
                Box(parent, "FrontWallCollision" + side, center + new Vector3(side * (1.6f + segmentWidth / 2), height / 2, -depth / 2 - .1f), new Vector3(segmentWidth, height, .2f));
            if (blueprint.decorColliders != null)
                foreach (var decoration in blueprint.decorColliders)
                    Box(parent, decoration.name, Vector(decoration.position), Vector(decoration.size));
        }
        private static void Box(Transform parent, string name, Vector3 position, Vector3 size)
        { var collider = Child(parent, name, position).gameObject.AddComponent<BoxCollider>(); collider.size = size; }
        private static Transform Child(Transform parent, string name, Vector3 position)
        { var child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = position; return child; }
        private static Transform InstantiateSource(GameObject prefab, Transform parent)
        {
            var root = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)).transform;
            root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one; return root;
        }
        private static Transform RequireNode(Transform root, string name)
        {
            // Blender在同一源文件中为三张通用铭牌附加数值后缀；每个独立模型仍必须唯一。
            var matches = root.GetComponentsInChildren<Transform>(true).Where(value => value.name == name ||
                name == "StatePlaque" && System.Text.RegularExpressions.Regex.IsMatch(value.name, @"^StatePlaque\.\d+$")).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("新样板需要唯一正式视觉节点：" + name);
            return matches[0];
        }
        private static void ValidateSource(GameObject prefab)
        {
            if (!AssetDatabase.Contains(prefab)) throw new InvalidOperationException("样板必须引用已保存的源资源。");
            if (prefab.GetComponentsInChildren<Camera>(true).Length != 0 || prefab.GetComponentsInChildren<AudioListener>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Collider>(true).Length != 0 || prefab.GetComponentsInChildren<Light>(true).Length != 0)
                throw new InvalidOperationException("DCC源夹带运行相机、监听器、碰撞或灯光：" + prefab.name);
            foreach (var node in prefab.GetComponentsInChildren<Transform>(true))
                if (Quaternion.Angle(node.localRotation, Quaternion.identity) > .01f || node.localScale.x <= 0 || node.localScale.y <= 0 || node.localScale.z <= 0)
                    throw new InvalidOperationException("S1源轴向未通过identity/正scale合同：" + node.name);
            if ((prefab.transform.localScale - Vector3.one).sqrMagnitude > .00001f) throw new InvalidOperationException("新源根必须是1米制。");
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterials.Any(material => material == null || material.shader == null || !material.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)))
                    throw new InvalidOperationException("S1正式模型须先完成共享URP材质映射：" + prefab.name);
        }
        private static Vector3 Vector(float[] data)
        { if (data == null || data.Length != 3) throw new InvalidOperationException("S1蓝图坐标必须有三个元素。"); return new Vector3(data[0], data[1], data[2]); }
        [Serializable] private sealed class Blueprint
        { public int version; public float roomWidth, roomDepth, roomHeight; public float[] roomCenter, spawn; public StationDefinition[] stations; public DecorationCollider[] decorColliders; }
        [Serializable] private sealed class DecorationCollider
        { public string name; public float[] position, size; }
        [Serializable] private sealed class StationDefinition
        { public string id, model; public int game; public float yaw, fieldOfView; public float[] position, approach, focus, lookAt, bodyCenter, bodySize; public TargetDefinition[] targets; }
        [Serializable] private sealed class TargetDefinition
        { public string id, action, node; public int value, order; public float[] position, size; }
    }
}
