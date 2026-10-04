using System;
using System.Collections.Generic;
using System.Linq;
using Core.Editor.MvcBind;
using Core.Runtime;
using Hotfix.HowToFish;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.HowToFish
{
    /// 首岛可玩结构的编辑期装配；地形与建筑处于玩法阶段，后续换入正式环境模型。
    public static class HowToFishSceneBuilder
    {
        private const string Root = HowToFishAssetBuilder.Root;
        private const string HudPath = Root + "/Prefabs/UI/HowToFishHudView.prefab";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HowToFishAssetBuilder.FontPath);

        [MenuItem("Tools/SleepyDemos/HowToFish/装配群岛场景和HUD")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play Mode 后装配。");
            HowToFishAssetBuilder.BuildFirstIslandAssets();
            HowToFishAssetBuilder.BuildForestAssets();
            HowToFishAssetBuilder.BuildDesertAssets();
            HowToFishAssetBuilder.BuildRocksAssets();
            HowToFishAssetBuilder.BuildVolcanoAssets();
            HowToFishAssetBuilder.BuildOptionalAssets();
            HowToFishAssetBuilder.BuildWildlifeAssets();
            HowToFishAssetBuilder.ConfigureVerifiedCreatures();
            HowToFishAssetBuilder.ConfigureVerifiedBaitPools();
            HowToFishAssetBuilder.ConfigureWeaponUpgrades();
            HowToFishAssetBuilder.ConfigureGunAttachments();
            HowToFishAssetBuilder.BuildDynamite();
            HowToFishAssetBuilder.BuildOutfits();
            HowToFishAssetBuilder.EnsureFont();
            HowToFishAssetBuilder.EnsureFolder(Root + "/Prefabs/UI");
            HowToFishAssetBuilder.EnsureFolder(Root + "/Scenes");
            BuildHud();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "/Scenes/Main.unity") == null) BuildScene();
            UpdateEnvironmentModels();
            AssetDatabase.SaveAssets();
            SetupAudio();
            SetupEffects();
            Debug.Log("[HowToFish] 群岛场景及 HUD 已保存；区域内容进度见 Demo 实施计划。");
        }

        /// 只装配原创音频与保存音源，不重建 HUD、地形或其它模型。
        [MenuItem("Tools/SleepyDemos/HowToFish/装配原创音效")]
        public static void SetupAudio()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play Mode 后装配音效。");
            string path = Root + "/Scenes/Main.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("请先保存当前 Demo 场景，再装配音效。");
            var soundIds = (HowToFishSound[])Enum.GetValues(typeof(HowToFishSound));
            var audioClips = new AudioClip[soundIds.Length];
            for (int i = 0; i < soundIds.Length; i++)
            {
                string audioPath = Root + "/Audio/" + soundIds[i] + ".wav";
                audioClips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
                if (audioClips[i] == null) throw new InvalidOperationException("请先导入原创 WAV：" + audioPath);
                var importer = (AudioImporter)AssetImporter.GetAtPath(audioPath);
                importer.forceToMono = true; importer.loadInBackground = false;
                var sample = importer.defaultSampleSettings;
                sample.loadType = AudioClipLoadType.DecompressOnLoad;
                sample.compressionFormat = AudioCompressionFormat.PCM;
                sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = sample;
                importer.SaveAndReimport();
                audioClips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
            }
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var world = roots.SelectMany(value => value.GetComponentsInChildren<HowToFishWorld>(true)).Single();
                var boat = roots.SelectMany(value => value.GetComponentsInChildren<HowToFishBoat>(true)).Single();
                var audioRoot = roots.SingleOrDefault(value => value.name == "HowToFishAudio");
                if (audioRoot == null)
                {
                    audioRoot = new GameObject("HowToFishAudio");
                    SceneManager.MoveGameObjectToScene(audioRoot, scene);
                }
                var director = audioRoot.GetComponent<HowToFishAudioDirector>(); if (director == null) director = audioRoot.AddComponent<HowToFishAudioDirector>();
                var settings = new SerializedObject(director);
                Bind(settings, "owner", world); Bind(settings, "boat", boat);
                var clips = settings.FindProperty("clips"); clips.arraySize = audioClips.Length;
                for (int i = 0; i < audioClips.Length; i++) clips.GetArrayElementAtIndex(i).objectReferenceValue = audioClips[i];
                Bind(settings, "sea", Source("Sea", audioRoot.transform, true, false));
                Bind(settings, "wind", Source("Wind", audioRoot.transform, true, false));
                Bind(settings, "motor", Source("HowToFishMotorAudio", boat.transform, true, true));
                Bind(settings, "reel", Source("Reel", audioRoot.transform, true, false));
                Bind(settings, "struggle", Source("Struggle", audioRoot.transform, true, false));
                Bind(settings, "ui", Source("UI", audioRoot.transform, false, false));
                var effects = settings.FindProperty("effects"); effects.arraySize = 4;
                for (int i = 0; i < effects.arraySize; i++)
                    effects.GetArrayElementAtIndex(i).objectReferenceValue = Source("Effect" + i, audioRoot.transform, false, true);
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("原创音效场景保存失败。");
                Debug.Log("[HowToFish] 原创音效候选和保存音源已装配，仍需暂停/Hub释放与听觉验收。");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }

            AudioSource Source(string name, Transform parent, bool loop, bool spatial)
            {
                var node = parent.Find(name);
                if (node == null) { node = new GameObject(name).transform; node.SetParent(parent, false); }
                var source = node.GetComponent<AudioSource>(); if (source == null) source = node.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = loop; source.volume = .5f; source.dopplerLevel = 0;
                source.spatialBlend = spatial ? 1 : 0; source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 2; source.maxDistance = 60;
                return source;
            }
        }

        /// 保存低多边形爆炸反馈和火山口烟，不改变伤害、音量或其它环境资源。
        [MenuItem("Tools/SleepyDemos/HowToFish/装配爆炸与火山烟")]
        public static void SetupEffects()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play Mode 后装配粒子。");
            string path = Root + "/Scenes/Main.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("请先保存 Demo 场景，再装配粒子。");
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            bool saved = false;
            try
            {
                var roots = scene.GetRootGameObjects();
                var world = roots.SelectMany(value => value.GetComponentsInChildren<HowToFishWorld>(true)).Single();
                var crater = roots.SelectMany(value => value.GetComponentsInChildren<HowToFishVolcanoCrater>(true)).Single();
                var effectRoot = roots.SingleOrDefault(value => value.name == "HowToFishEffects");
                if (effectRoot == null)
                {
                    effectRoot = new GameObject("HowToFishEffects");
                    SceneManager.MoveGameObjectToScene(effectRoot, scene);
                }
                var mesh = EffectMesh();
                var material = EffectMaterial();
                var fire = Effect("ExplosionFire", effectRoot.transform, false, false);
                var dust = Effect("ExplosionSmoke", effectRoot.transform, true, false);
                var smoke = Effect("CraterSmoke", crater.transform, true, true);
                smoke.transform.localPosition = Vector3.up;
                smoke.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                var settings = new SerializedObject(world);
                Bind(settings, "explosionFire", fire); Bind(settings, "explosionSmoke", dust);
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("爆炸与火山烟场景保存失败。");
                saved = true;
                Debug.Log("[HowToFish] 自制爆炸与火山烟候选已保存，仍需暂停、静音与实际画面验收。");

                ParticleSystem Effect(string name, Transform parent, bool isSmoke, bool loop)
                {
                    var node = parent.Find(name);
                    if (node == null) { node = new GameObject(name).transform; node.SetParent(parent, false); }
                    var particles = node.GetComponent<ParticleSystem>();
                    if (particles == null) particles = node.gameObject.AddComponent<ParticleSystem>();
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = particles.main;
                    // 以下粒子数量、寿命、尺寸、速度和颜色全部为自制推定，不复刻来源参数。
                    main.loop = loop; main.playOnAwake = loop; main.useUnscaledTime = false;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                    main.duration = loop ? 8 : 1; main.maxParticles = loop ? 48 : 128;
                    main.startLifetime = loop ? new ParticleSystem.MinMaxCurve(5, 7) :
                        isSmoke ? new ParticleSystem.MinMaxCurve(1.3f, 2.2f) : new ParticleSystem.MinMaxCurve(.25f, .45f);
                    main.startSize = loop ? new ParticleSystem.MinMaxCurve(1.2f, 2.4f) :
                        isSmoke ? new ParticleSystem.MinMaxCurve(.45f, .85f) : new ParticleSystem.MinMaxCurve(.35f, .65f);
                    main.startSpeed = loop ? new ParticleSystem.MinMaxCurve(.3f, .6f) :
                        isSmoke ? new ParticleSystem.MinMaxCurve(.5f, 1.1f) : new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
                    main.startColor = isSmoke ? new ParticleSystem.MinMaxGradient(new Color(.22f, .20f, .19f, .55f), new Color(.4f, .36f, .3f, .45f)) :
                        new ParticleSystem.MinMaxGradient(new Color(1, .25f, .035f), new Color(1, .75f, .16f));
                    main.startRotation3D = true;
                    main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    var emission = particles.emission; emission.enabled = loop; emission.rateOverTime = loop ? 3 : 0;
                    var shape = particles.shape; shape.enabled = true;
                    shape.shapeType = loop ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Sphere;
                    shape.radius = loop ? 5 : isSmoke ? .3f : .12f;
                    var velocity = particles.velocityOverLifetime; velocity.enabled = isSmoke;
                    velocity.space = ParticleSystemSimulationSpace.World;
                    velocity.x = velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
                    // 火口23米、前缘约27米；岸边视角需要烟升至约36米，循环烟上升速度为自制可视性校准。
                    velocity.y = loop ? new ParticleSystem.MinMaxCurve(3, 4) : new ParticleSystem.MinMaxCurve(.5f, 1);
                    var color = particles.colorOverLifetime; color.enabled = true;
                    var fade = new Gradient();
                    fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                        new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.7f, .55f), new GradientAlphaKey(0, 1) });
                    color.color = fade;
                    var size = particles.sizeOverLifetime; size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .45f, 1, isSmoke ? 1.8f : 1.2f));
                    var collision = particles.collision; collision.enabled = false;
                    var renderer = particles.GetComponent<ParticleSystemRenderer>();
                    renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = mesh; renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    return particles;
                }
            }
            finally { if (opened && saved) EditorSceneManager.CloseScene(scene, true); }
        }

        private static Mesh EffectMesh()
        {
            string path = Root + "/Art/Meshes/EffectOctahedron.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            HowToFishAssetBuilder.EnsureFolder(Root + "/Art/Meshes");
            var points = new[] { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            int[] faces = { 0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 4, 2, 1, 3, 4, 1, 5, 3, 1, 2, 5 };
            var vertices = new Vector3[faces.Length]; var triangles = new int[faces.Length];
            for (int i = 0; i < faces.Length; i += 3)
            {
                var a = points[faces[i]] * .5f; var b = points[faces[i + 1]] * .5f; var c = points[faces[i + 2]] * .5f;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0) { var swap = b; b = c; c = swap; }
                vertices[i] = a; vertices[i + 1] = b; vertices[i + 2] = c;
                triangles[i] = i; triangles[i + 1] = i + 1; triangles[i + 2] = i + 2;
            }
            mesh = new Mesh { name = "EffectOctahedron", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Material EffectMaterial()
        {
            string path = Root + "/Art/Materials/FacetedParticles.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) throw new InvalidOperationException("缺少 URP 原生粒子 Unlit Shader。");
            material = new Material(shader) { name = "FacetedParticles" };
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void UpdateEnvironmentModels()
        {
            string path = Root + "/Scenes/Main.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                var boat = scene.GetRootGameObjects().Single(root => root.name == "FishingBoat");
                if (boat.transform.Find("Visual") == null)
                {
                    foreach (var renderer in boat.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    var visual = Model("FishingBoat", scene);
                    visual.name = "Visual";
                    visual.transform.SetParent(boat.transform, false);
                }
                foreach (var renderer in boat.transform.Find("Visual").GetComponentsInChildren<Renderer>())
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                    if (source != null) renderer.sharedMaterials = source.sharedMaterials.Select(HowToFishAssetBuilder.MaterialFor).ToArray();
                }
                boat.transform.Find("WheelBlockout").localPosition = new Vector3(-.4f, 1, -.14f);
                UpdateBoatUpgrades(boat, scene);
                HowToFishAssetBuilder.EnsureSkinView(boat.transform.Find("Visual").gameObject);
                if (!scene.GetRootGameObjects().Any(root => root.name == "Lighthouse"))
                {
                    foreach (var root in scene.GetRootGameObjects().Where(root => root.name == "LighthouseBlockout" || root.name == "LighthouseRoofBlockout"))
                        root.GetComponent<Renderer>().enabled = false;
                    var tower = Model("Lighthouse", scene);
                    tower.transform.position = new Vector3(-5, 2.4f, 2);
                    tower.transform.rotation = Quaternion.Euler(0, 180, 0);
                }
                if (!scene.GetRootGameObjects().Any(root => root.name == "LighthouseKeeper"))
                {
                    var proxy = scene.GetRootGameObjects().Single(root => root.name == "KeeperBlockout");
                    proxy.GetComponent<Renderer>().enabled = false;
                    var keeper = Model("Keeper", scene);
                    keeper.name = "LighthouseKeeper";
                    keeper.transform.position = new Vector3(-4, 2.4f, -3);
                    keeper.transform.rotation = Quaternion.Euler(0, 180, 0);
                }
                var island = scene.GetRootGameObjects().Single(root => root.name == "LighthouseIsland");
                ConfigureIsland(island, 0, "灯塔岛", 36);
                island.GetComponent<Renderer>().sharedMaterials = new[]
                {
                    Material("LighthouseRock", new Color(.38f, .36f, .37f)),
                    Material("LighthouseShore", new Color(.44f, .42f, .41f)),
                    Material("LighthouseWetRock", new Color(.27f, .27f, .29f))
                };
                UpdateFirstIslandShop(scene);
                if (!scene.GetRootGameObjects().Any(root => root.name == "ForestIsland"))
                {
                    var forest = BuildIsland(Material("ForestGrass", new Color(.18f, .27f, .12f)),
                        Material("ForestSand", new Color(.54f, .49f, .35f)), Material("ForestRock", new Color(.28f, .29f, .25f)),
                        "ForestIsland", new Vector3(-180, 0, 180), true);
                    ConfigureIsland(forest, 1, "森林岛", 60);
                    Physics.SyncTransforms();
                    var terrain = forest.GetComponent<MeshCollider>();
                    for (int i = 0; i < 48; i++)
                    {
                        float angle = i * 2.399963f;
                        var local = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (27 + i % 17 * 1.2f);
                        if (local.z < -25 && Mathf.Abs(local.x) < 9) continue;
                        var origin = forest.transform.position + local + Vector3.up * 30;
                        if (!terrain.Raycast(new Ray(origin, Vector3.down), out var hit, 60)) continue;
                        var tree = Model("Pine", scene);
                        tree.name = "Pine" + i;
                        tree.transform.SetParent(forest.transform, true);
                        tree.transform.position = hit.point;
                        tree.transform.rotation = Quaternion.Euler(0, i * 47, 0);
                        tree.transform.localScale = Vector3.one * (1 + i % 4 * .15f);
                        var trunk = tree.AddComponent<CapsuleCollider>(); trunk.radius = .15f; trunk.height = 3; trunk.center = Vector3.up * 1.5f;
                    }
                }
                UpdateForestQuest(scene);
                UpdateDesert(scene);
                UpdateRocks(scene);
                UpdateVolcano(scene);
                UpdateWeaponShops(scene);
                UpdateBaitSupplyShops(scene);
                UpdateCookingStations(scene);
                UpdateSlotMachines(scene);
                UpdateRoulette(scene);
                UpdateEnvironmentDetails(scene);
                UpdateWaterAndSky(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("群岛内容保存失败。");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void UpdateWaterAndSky(Scene scene)
        {
            var ocean = scene.GetRootGameObjects().Single(root => root.name == "Ocean").GetComponent<Renderer>();
            var waterShader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Art/Shaders/HowToFishWater.shader");
            if (waterShader == null || ShaderUtil.ShaderHasError(waterShader))
                throw new InvalidOperationException("群岛水面 Shader 缺失或编译失败。");
            var water = ocean.sharedMaterial;
            water.shader = waterShader;
            water.SetColor("_ShallowColor", new Color(.12f, .43f, .49f, .9f));
            water.SetColor("_DeepColor", new Color(.025f, .16f, .27f, .97f));
            water.SetColor("_FoamColor", new Color(.78f, .87f, .84f));
            water.SetColor("_ReflectionColor", new Color(.55f, .68f, .76f));
            water.SetFloat("_WaveScale", .4f); water.SetFloat("_WaveSpeed", .85f);
            water.SetFloat("_NormalStrength", .12f); water.SetFloat("_FoamWidth", .65f);
            water.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(water);
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader == null) throw new InvalidOperationException("缺少内置程序天空 Shader。");
            string skyPath = Root + "/Art/Materials/IslandSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(skyShader); AssetDatabase.CreateAsset(sky, skyPath); }
            sky.SetColor("_SkyTint", new Color(.45f, .61f, .76f));
            sky.SetColor("_GroundColor", new Color(.39f, .44f, .48f));
            sky.SetFloat("_Exposure", 1); sky.SetFloat("_AtmosphereThickness", .8f);
            sky.SetFloat("_SunSize", .025f);
            EditorUtility.SetDirty(sky);
            var sun = scene.GetRootGameObjects().Single(root => root.name == "Sun").GetComponent<Light>();
            sun.intensity = 1.35f; sun.shadowStrength = .85f;
            RenderSettings.sun = sun; RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.57f, .69f, .77f);
            RenderSettings.ambientEquatorColor = new Color(.43f, .48f, .49f);
            RenderSettings.ambientGroundColor = new Color(.25f, .27f, .25f);
            RenderSettings.fogColor = new Color(.55f, .68f, .76f);
            foreach (var camera in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)))
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            }
        }


        private static void UpdateEnvironmentDetails(Scene scene)
        {
            string[] required = { "ShoreRockCluster", "BasaltRockCluster", "ShorePebbleScatter", "PineBranchedA", "PineBranchedB",
                "ForestShrub", "ForestGrass", "RocksPierModule", "LuckyBaitCasinoShell" };
            foreach (string id in required)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/" + id + ".fbx") == null)
                    throw new InvalidOperationException("环境候选尚未导入：" + id);
            var roots = scene.GetRootGameObjects();
            var lighthouse = roots.Single(root => root.name == "LighthouseIsland");
            var forest = roots.Single(root => root.name == "ForestIsland");
            var volcano = roots.Single(root => root.name == "VolcanoIsland");
            var rocks = roots.Single(root => root.name == "RocksIsland");
            ValidateCasinoPlacement(rocks);
            Physics.SyncTransforms();
            var stations = roots.SelectMany(root => root.GetComponentsInChildren<HowToFishStation>()).ToArray();
            var spawnPoints = roots.SelectMany(root => root.GetComponentsInChildren<Transform>()).Where(value =>
                value.name.StartsWith("ClamSpawn", StringComparison.Ordinal) || value.name.StartsWith("LeechSpawn", StringComparison.Ordinal) ||
                value.name.StartsWith("SnailSpawn", StringComparison.Ordinal) || value.name == "StartPoint").ToArray();
            bool Clear(Vector3 position, float margin) => !stations.Any(station =>
                    Vector3.ProjectOnPlane(station.transform.position - position, Vector3.up).sqrMagnitude < margin * margin) &&
                !spawnPoints.Any(point => Vector3.ProjectOnPlane(point.position - position, Vector3.up).sqrMagnitude < 6.25f);

            foreach (var island in new[] { lighthouse, volcano })
            {
                var detailRoot = ResetEnvironmentRoot(island, "EnvironmentRocks");
                var ground = EnvironmentGround(island);
                bool volcanic = island == volcano;
                var path = volcanic ? island.transform.Find("WoodenAscent") : null;
                int count = volcanic ? 28 : 20;
                for (int i = 0; i < count; i++)
                {
                    float angle = i * 2.399963f;
                    float radius = volcanic ? 52 + i % 7 * 4 : 21 + i % 4 * 1.5f;
                    var local = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
                    if (volcanic ? local.y < -35 : local.y < -5 && Mathf.Abs(local.x) < 18) continue;
                    if (!ground.Raycast(new Ray(island.transform.position + new Vector3(local.x, 90, local.y), Vector3.down), out var hit, 120) || hit.point.y < .12f) continue;
                    if (!Clear(hit.point, 4) || path != null && path.Cast<Transform>().Any(plank =>
                        Vector3.ProjectOnPlane(plank.position - hit.point, Vector3.up).sqrMagnitude < 16)) continue;
                    string model = i % 3 == 0 ? "ShorePebbleScatter" : volcanic ? "BasaltRockCluster" : "ShoreRockCluster";
                    var detail = Model(model, scene); detail.name = model + i;
                    detail.transform.SetParent(detailRoot, true);
                    detail.transform.position = hit.point - hit.normal * .035f;
                    detail.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, i * 53, 0);
                    detail.transform.localScale = Vector3.one * (.75f + i % 4 * .1f);
                }
            }

            // 保留原树根/树干碰撞和位置，只换视觉；不改变已有森林绕行与任务通道。
            var oldTrees = forest.transform.Cast<Transform>().Where(tree => tree.name.StartsWith("Pine", StringComparison.Ordinal) &&
                int.TryParse(tree.name.Substring(4), out _)).ToArray();
            foreach (var tree in oldTrees)
            {
                var previousVisual = tree.Find("BranchedVisual");
                if (previousVisual != null) Object.DestroyImmediate(previousVisual.gameObject);
                foreach (var renderer in tree.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                int index = int.Parse(tree.name.Substring(4));
                var visual = Model(index % 2 == 0 ? "PineBranchedA" : "PineBranchedB", scene);
                visual.name = "BranchedVisual"; visual.transform.SetParent(tree, false);
                visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one * .85f;
            }
            var understory = ResetEnvironmentRoot(forest, "EnvironmentUnderstory");
            var forestGround = EnvironmentGround(forest);
            for (int i = 0; i < 100; i++)
            {
                float angle = i * 2.399963f + .37f;
                var local = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * (26 + i % 13 * 1.55f);
                if (local.y < -21 && Mathf.Abs(local.x) < 20) continue;
                if (!forestGround.Raycast(new Ray(forest.transform.position + new Vector3(local.x, 50, local.y), Vector3.down), out var hit, 80) || hit.point.y < .35f || !Clear(hit.point, 3.5f)) continue;
                string id = i % 5 == 0 ? "ForestShrub" : "ForestGrass";
                var plant = Model(id, scene); plant.name = id + i; plant.transform.SetParent(understory, true);
                plant.transform.position = hit.point - Vector3.up * .015f;
                plant.transform.rotation = Quaternion.Euler(0, i * 67, 0);
                plant.transform.localScale = Vector3.one * (.55f + i % 5 * .1f);
            }

            var architecture = ResetEnvironmentRoot(rocks, "EnvironmentArchitecture");
            var rocksGround = EnvironmentGround(rocks);
            rocksGround.Raycast(new Ray(rocks.transform.position + new Vector3(-10.5f, 90, -27), Vector3.down), out var casinoFloor, 120);
            var casino = Model("LuckyBaitCasinoShell", scene); casino.transform.SetParent(architecture, true);
            casino.transform.position = casinoFloor.point + Vector3.up * .02f; casino.transform.rotation = Quaternion.Euler(0, 180, 0);
            casino.transform.localScale = Vector3.one * .9f;
            AddEnvironmentMeshColliders(casino);
            foreach (var material in casino.GetComponentsInChildren<Renderer>().SelectMany(renderer => renderer.sharedMaterials).Distinct())
                if (material.name == "CasinoSignWarm" && material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", new Color(1, .42f, .08f) * 1.5f);
                    EditorUtility.SetDirty(material);
                }
            // 栈桥在南岸东侧，避开商店、赌博区与主任务线；甲板接岸端只有约0.12米高差。
            if (!rocksGround.Raycast(new Ray(rocks.transform.position + new Vector3(26, 90, -51.86f), Vector3.down), out var shore, 120))
                throw new InvalidOperationException("岩石岛栈桥接岸点不在地形上。");
            var pier = Model("RocksPierModule", scene); pier.transform.SetParent(architecture, true);
            pier.transform.position = rocks.transform.position + new Vector3(26, shore.point.y - rocks.transform.position.y + .12f, -57);
            pier.transform.rotation = Quaternion.Euler(0, 180, 0);
            AddEnvironmentMeshColliders(pier);
        }

        private static void ValidateCasinoPlacement(GameObject island)
        {
            var ground = EnvironmentGround(island);
            Physics.SyncTransforms();
            var roulette = island.transform.Find("RouletteTable");
            var slot = island.transform.Find("SlotMachine");
            if (roulette == null || slot == null) throw new InvalidOperationException("装配赌场外壳前需要已有轮盘与老虎机。");
            foreach (var pair in new[] { (roulette, new Vector3(-13, 0, -30)), (slot, new Vector3(-8, 0, -30)) })
                if (Vector3.ProjectOnPlane(pair.Item1.localPosition - pair.Item2, Vector3.up).sqrMagnitude > .01f)
                    throw new InvalidOperationException("赌博设施位置已改变，停止外壳装配以保护现有交互；不要自动迁移设施。");
            if (!ground.Raycast(new Ray(island.transform.position + new Vector3(-10.5f, 90, -27), Vector3.down), out var center, 120))
                throw new InvalidOperationException("赌场中心不在岩石岛地形上。");
            foreach (var sample in new[] { new Vector2(-10.5f, -31.05f), new Vector2(-10.5f, -32.8275f) })
            {
                if (!ground.Raycast(new Ray(island.transform.position + new Vector3(sample.x, 90, sample.y), Vector3.down), out var entry, 120) ||
                    Mathf.Abs(entry.point.y - center.point.y - .02f) > .25f)
                    throw new InvalidOperationException("赌场入口高差超过0.25米，停止装配；需重新检查地形而不是搬动桌子。");
            }
            var shop = island.transform.Find("RocksShop");
            if (shop == null) throw new InvalidOperationException("岩石岛原商店缺失。");
            float shopLeft = shop.GetComponentsInChildren<Renderer>().Min(renderer => renderer.bounds.min.x);
            float casinoRight = island.transform.position.x - 10.5f + 7.45f * .9f;
            if (shopLeft - casinoRight < .7f)
                throw new InvalidOperationException("赌场与原商店间距不足0.7米，停止外壳装配以保留通路。");
        }

        private static MeshCollider EnvironmentGround(GameObject island)
        {
            var ground = island.GetComponent<MeshCollider>();
            if (ground != null) return ground;
            return island.GetComponentsInChildren<MeshCollider>().Single(collider => collider.name == "Terrain");
        }

        private static Transform ResetEnvironmentRoot(GameObject island, string name)
        {
            var previous = island.transform.Find(name);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var root = new GameObject(name).transform; root.SetParent(island.transform, false); return root;
        }

        private static void AddEnvironmentMeshColliders(GameObject model)
        {
            // 逐个实际静态网格生成非凸碰撞；合并门框/门廊仍保留空洞，绝不退回总包围盒。
            foreach (var part in model.GetComponentsInChildren<MeshFilter>())
            {
                var collider = part.GetComponent<MeshCollider>(); if (collider == null) collider = part.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = part.sharedMesh; collider.convex = false;
            }
        }

        private static void UpdateForestQuest(Scene scene)
        {
            var forest = scene.GetRootGameObjects().Single(root => root.name == "ForestIsland");
            var ground = forest.GetComponent<MeshCollider>();
            Physics.SyncTransforms();
            Vector3 Surface(float x, float z)
            {
                var ray = new Ray(forest.transform.position + new Vector3(x, 30, z), Vector3.down);
                if (!ground.Raycast(ray, out var hit, 60)) throw new InvalidOperationException("森林任务落点不在地形上。");
                return hit.point;
            }
            if (forest.transform.Find("ForestLady") == null)
            {
                var lady = Model("ForestLady", scene);
                lady.transform.SetParent(forest.transform, true);
                lady.transform.position = Surface(0, -25);
                lady.transform.rotation = Quaternion.Euler(0, 180, 0);
                var collider = lady.AddComponent<CapsuleCollider>(); collider.radius = .3f; collider.height = 1.9f; collider.center = Vector3.up * .95f;
                Station(lady, HowToFishStationKind.ForestLady, "湖畔女士");
            }
            if (forest.transform.Find("ForestShop") == null)
            {
                var shop = Model("ForestShop", scene);
                shop.transform.SetParent(forest.transform, true);
                shop.transform.position = Surface(5, -36);
                shop.transform.rotation = Quaternion.Euler(0, 180, 0);
                foreach (var mesh in shop.GetComponentsInChildren<MeshFilter>())
                {
                    if (mesh.name == "Roof") continue;
                    var collider = mesh.gameObject.AddComponent<BoxCollider>();
                    collider.center = mesh.sharedMesh.bounds.center; collider.size = mesh.sharedMesh.bounds.size;
                    if (mesh.name == "Counter") Station(mesh.gameObject, HowToFishStationKind.Sell, "出售鱼获");
                }
                var pistol = Product("Pistol", shop.transform.TransformPoint(new Vector3(1.65f, 1.45f, .72f)), shop.transform.rotation * Quaternion.Euler(-90, 0, 0), 1);
                pistol.transform.SetParent(shop.transform, true);
                var shotgun = Product("Shotgun", shop.transform.TransformPoint(new Vector3(.8f, 1.5f, .72f)), shop.transform.rotation * Quaternion.Euler(-65, 0, 0), 1);
                shotgun.transform.SetParent(shop.transform, true);
                var bait = Product("HotDog", shop.transform.TransformPoint(new Vector3(-1.4f, 1.05f, .75f)), null, 1);
                bait.transform.SetParent(shop.transform, true);
            }
            var savedShop = forest.transform.Find("ForestShop");
            foreach (string id in new[] { "FishingRod", "BeginnerLure", "BeginnerBossLure" })
            {
                if (savedShop.GetComponentsInChildren<HowToFishStation>().Any(station => station.ItemId == id)) continue;
                var position = id == "FishingRod" ? new Vector3(-1.95f, .35f, 1.1f) : new Vector3(id == "BeginnerLure" ? -.6f : .1f, 1.05f, .8f);
                var product = Product(id, savedShop.TransformPoint(position),
                    id == "FishingRod" ? savedShop.rotation * Quaternion.Euler(-90, 0, 0) : savedShop.rotation, 1);
                product.transform.SetParent(savedShop, true);
            }
            var points = forest.transform.Find("LeechSpawns");
            if (points == null)
            {
                points = new GameObject("LeechSpawns").transform;
                points.SetParent(forest.transform, false);
                foreach (var offset in new[] { new Vector2(-6, -27), new Vector2(7, -26), new Vector2(-12, -20) })
                {
                    var point = new GameObject("LeechSpawn").transform;
                    point.SetParent(points, true);
                    point.position = Surface(offset.x, offset.y) + Vector3.up * .15f;
                }
            }
            var world = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HowToFishWorld>()).Single();
            var settings = new SerializedObject(world);
            settings.FindProperty("m_EditorClassIdentifier").stringValue = "Hotfix::" + typeof(HowToFishWorld).FullName;
            var spawns = settings.FindProperty("leechPoints"); spawns.arraySize = points.childCount;
            for (int i = 0; i < points.childCount; i++) spawns.GetArrayElementAtIndex(i).objectReferenceValue = points.GetChild(i);
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateDesert(Scene scene)
        {
            var desert = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "DesertIsland");
            if (desert == null)
                desert = BuildIsland(Material("DesertSand", new Color(.76f, .67f, .43f)),
                    Material("DesertShore", new Color(.82f, .74f, .52f)), Material("DesertRock", new Color(.40f, .35f, .28f)),
                    "DesertIsland", new Vector3(-440, 0, 430));
            ConfigureIsland(desert, 2, "沙漠岛", 72);
            Physics.SyncTransforms();
            var ground = desert.GetComponent<MeshCollider>();
            Vector3 Surface(float x, float z)
            {
                if (!ground.Raycast(new Ray(desert.transform.position + new Vector3(x, 30, z), Vector3.down), out var hit, 60))
                    throw new InvalidOperationException("沙漠落点不在地形上。");
                return hit.point;
            }
            if (desert.transform.Find("Palms") == null)
            {
                var palms = new GameObject("Palms").transform; palms.SetParent(desert.transform, false);
                for (int i = 0; i < 35; i++)
                {
                    float angle = i * 2.399963f;
                    float radius = 16 + i % 13 * 2.7f;
                    float x = Mathf.Sin(angle) * radius, z = Mathf.Cos(angle) * radius;
                    if (z < -27 && Mathf.Abs(x) < 16) continue;
                    var palm = Model("Palm", scene); palm.transform.SetParent(palms, true);
                    palm.transform.position = Surface(x, z); palm.transform.rotation = Quaternion.Euler(0, i * 67, 0);
                    var shape = palm.AddComponent<CapsuleCollider>(); shape.radius = .25f; shape.height = 4.5f; shape.center = Vector3.up * 2.25f;
                }
            }
            if (desert.transform.Find("Tourist") == null)
            {
                var palm = Model("Palm", scene); palm.name = "TouristPalm";
                palm.transform.SetParent(desert.transform, true); palm.transform.position = Surface(-8, -40);
                var trunk = palm.AddComponent<CapsuleCollider>(); trunk.radius = .25f; trunk.height = 4.5f; trunk.center = Vector3.up * 2.25f;
                var tourist = Model("Tourist", scene); tourist.transform.SetParent(desert.transform, true);
                tourist.transform.position = Surface(-8, -40.4f); tourist.transform.rotation = Quaternion.Euler(0, 180, 0);
                var shape = tourist.AddComponent<BoxCollider>(); shape.center = new Vector3(0, .6f, .35f); shape.size = new Vector3(.65f, 1.2f, 1);
                Station(tourist, HowToFishStationKind.Tourist, "棕榈树下的游客");
            }
            if (desert.transform.Find("GrillMaster") == null)
            {
                var chef = Model("GrillMaster", scene); chef.transform.SetParent(desert.transform, true);
                chef.transform.position = Surface(10, -37); chef.transform.rotation = Quaternion.Euler(0, 180, 0);
                var shape = chef.AddComponent<CapsuleCollider>(); shape.radius = .3f; shape.height = 1.9f; shape.center = Vector3.up * .95f;
                Station(chef, HowToFishStationKind.GrillMaster, "烧烤师");
                var grill = Model("Grill", scene); grill.transform.SetParent(desert.transform, true);
                grill.transform.position = Surface(11.4f, -38); grill.transform.rotation = Quaternion.Euler(0, 180, 0);
                var grillShape = grill.AddComponent<BoxCollider>(); grillShape.center = Vector3.up * .48f; grillShape.size = new Vector3(1, .96f, .6f);
                Station(grill, HowToFishStationKind.Grill, "烧烤架");
            }
            if (desert.transform.Find("DesertShop") == null)
            {
                var shop = Model("ForestShop", scene); shop.name = "DesertShop";
                shop.transform.SetParent(desert.transform, true); shop.transform.position = Surface(0, -32);
                shop.transform.rotation = Quaternion.Euler(0, 180, 0);
                foreach (var mesh in shop.GetComponentsInChildren<MeshFilter>())
                {
                    if (mesh.name == "Roof") continue;
                    var shape = mesh.gameObject.AddComponent<BoxCollider>(); shape.center = mesh.sharedMesh.bounds.center; shape.size = mesh.sharedMesh.bounds.size;
                    if (mesh.name == "Counter") Station(mesh.gameObject, HowToFishStationKind.Sell, "出售鱼获");
                }
                foreach (var row in new[] { ("SMG", new Vector3(1.25f, 1.5f, .72f)),
                    ("StandardLure", new Vector3(-1.25f, 1.05f, .8f)), ("StandardBossLure", new Vector3(-.55f, 1.05f, .8f)),
                    ("FishingRod", new Vector3(-1.95f, .35f, 1.1f)) })
                {
                    var product = Product(row.Item1, shop.transform.TransformPoint(row.Item2),
                        shop.transform.rotation * (row.Item1 == "SMG" || row.Item1 == "FishingRod" ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity), 2);
                    product.transform.SetParent(shop.transform, true);
                }
            }
            var savedShop = desert.transform.Find("DesertShop");
            if (!savedShop.GetComponentsInChildren<HowToFishStation>().Any(value => value.ItemId == "Coconut"))
            {
                var coconut = Product("Coconut", savedShop.TransformPoint(new Vector3(.15f, 1.05f, .8f)), savedShop.rotation, 2);
                coconut.transform.SetParent(savedShop, true);
            }
        }

        private static void UpdateRocks(Scene scene)
        {
            var rocks = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "RocksIsland");
            if (rocks == null)
                rocks = BuildIsland(Material("RocksPlateau", new Color(.38f, .38f, .36f)),
                    Material("RocksShore", new Color(.46f, .45f, .42f)), Material("RocksWet", new Color(.27f, .28f, .28f)),
                    "RocksIsland", new Vector3(-720, 0, 650));
            ConfigureIsland(rocks, 3, "岩石岛", 72);
            var savedShop = rocks.transform.Find("RocksShop");
            if (savedShop != null) { UpdateRocksProducts(savedShop); return; }
            Physics.SyncTransforms();
            var ground = rocks.GetComponent<MeshCollider>();
            if (!ground.Raycast(new Ray(rocks.transform.position + new Vector3(0, 30, -25), Vector3.down), out var floor, 60))
                throw new InvalidOperationException("岩石岛商店落点不在地形上。");
            var shop = Model("RocksShop", scene);
            shop.transform.SetParent(rocks.transform, true); shop.transform.position = floor.point;
            shop.transform.rotation = Quaternion.Euler(0, 180, 0);
            foreach (var mesh in shop.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.name.StartsWith("RoofTile", StringComparison.Ordinal))
                    mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                else
                {
                    var shape = mesh.gameObject.AddComponent<BoxCollider>(); shape.center = mesh.sharedMesh.bounds.center; shape.size = mesh.sharedMesh.bounds.size;
                }
                if (mesh.name == "Counter") Station(mesh.gameObject, HowToFishStationKind.Sell, "出售鱼获");
            }
            var islander = Model("Islander", scene); islander.transform.SetParent(shop.transform, false);
            islander.transform.localPosition = new Vector3(.7f, .15f, -1.45f);
            var body = islander.AddComponent<CapsuleCollider>(); body.radius = .25f; body.height = 1.94f; body.center = Vector3.up * .97f;
            Station(islander, HowToFishStationKind.Islander, "岩石岛岛民");
            UpdateRocksProducts(shop.transform);
        }

        private static void UpdateRocksProducts(Transform shop)
        {
            foreach (var row in new[] { ("SniperRifle", new Vector3(-1.95f, 1.48f, -.6f)),
                ("ProfessionalBossLure", new Vector3(.2f, 1.04f, -.7f)), ("ProfessionalLure", new Vector3(1.3f, 1.04f, -.7f)) })
            {
                if (shop.GetComponentsInChildren<HowToFishStation>().Any(station => station.ItemId == row.Item1)) continue;
                var product = Product(row.Item1, shop.TransformPoint(row.Item2),
                    shop.rotation * (row.Item1 == "SniperRifle" ? Quaternion.Euler(0, 90, 0) : Quaternion.identity), 3);
                product.transform.SetParent(shop, true);
            }
        }

        private static void UpdateVolcano(Scene scene)
        {
            var island = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "VolcanoIsland");
            if (island == null)
            {
                island = Model("VolcanoTerrain", scene); island.name = "VolcanoIsland";
                island.transform.position = new Vector3(-720, 0, 1040);
                var terrain = island.GetComponentsInChildren<MeshFilter>().Single(mesh => mesh.name == "Terrain");
                terrain.gameObject.AddComponent<MeshCollider>().sharedMesh = terrain.sharedMesh;
            }
            ConfigureIsland(island, 4, "火山岛", 98);
            var ground = island.GetComponentInChildren<MeshCollider>();
            Physics.SyncTransforms();
            Vector3 Surface(float x, float z)
            {
                if (!ground.Raycast(new Ray(island.transform.position + new Vector3(x, 90, z), Vector3.down), out var hit, 120))
                    throw new InvalidOperationException("火山装配落点不在地形上。");
                return hit.point;
            }
            string[] products = { "AssaultRifle", "ScientificLure", "SniperRifle", "SMG", "Shotgun", "Pistol" };
            if (island.transform.Find("VolcanoCamp") == null)
            {
                var camp = new GameObject("VolcanoCamp").transform; camp.SetParent(island.transform, false);
                var tent = Model("MilitaryTent", scene); tent.transform.SetParent(camp, true);
                tent.transform.position = Surface(0, -63); tent.transform.rotation = Quaternion.Euler(0, 180, 0);
                AddStaticModelColliders(tent);
                var scientist = Model("Scientist", scene); scientist.transform.SetParent(camp, true);
                scientist.transform.position = Surface(4, -71); scientist.transform.rotation = Quaternion.Euler(0, 180, 0);
                var body = scientist.AddComponent<CapsuleCollider>(); body.radius = .26f; body.height = 1.95f; body.center = Vector3.up * .975f;
                Station(scientist, HowToFishStationKind.Scientist, "火山科学家");
                for (int i = 0; i < products.Length; i++)
                {
                    var crate = Model("SupplyCrate", scene); crate.name = "WeaponCrate" + i;
                    crate.transform.SetParent(camp, true); crate.transform.position = Surface(-5 + i * 1.5f, -67.5f);
                    AddStaticModelColliders(crate);
                    var product = Product(products[i], crate.transform.position + Vector3.up * .84f,
                        products[i] == "ScientificLure" ? Quaternion.identity : Quaternion.Euler(0, 90, 0), 4);
                    product.transform.SetParent(camp, true);
                }
                var seller = Model("SupplyCrate", scene); seller.name = "FishCounter"; seller.transform.SetParent(camp, true);
                seller.transform.position = Surface(7, -68); AddStaticModelColliders(seller);
                Station(seller, HowToFishStationKind.Sell, "出售鱼获");
            }
            var savedCamp = island.transform.Find("VolcanoCamp");
            if (!savedCamp.GetComponentsInChildren<HowToFishStation>().Any(value => value.ItemId == "ScientificBossLure"))
            {
                var crate = Model("SupplyCrate", scene); crate.name = "BossLureCrate";
                crate.transform.SetParent(savedCamp, true); crate.transform.position = Surface(4, -67.5f);
                AddStaticModelColliders(crate);
                var product = Product("ScientificBossLure", crate.transform.position + Vector3.up * .9f, Quaternion.identity, 4);
                product.transform.SetParent(savedCamp, true);
            }
            for (int i = 0; i < products.Length; i++)
            {
                var product = savedCamp.GetComponentsInChildren<HowToFishStation>().Single(value => value.ItemId == products[i]);
                var crate = savedCamp.Find("WeaponCrate" + i);
                product.transform.rotation = products[i] == "ScientificLure" ? Quaternion.identity : Quaternion.Euler(0, 90, 90);
                float top = crate.GetComponentsInChildren<Renderer>().Max(value => value.bounds.max.y);
                float bottom = product.GetComponentsInChildren<Renderer>().Min(value => value.bounds.min.y);
                product.transform.position += Vector3.up * (top + .03f - bottom);
            }
            var path = island.transform.Find("WoodenAscent");
            if (path == null) { path = new GameObject("WoodenAscent").transform; path.SetParent(island.transform, false); }
            Vector3 PathPoint(float t)
            {
                float angle = Mathf.PI + .22f + t * Mathf.PI * 1.4f, radius = Mathf.Lerp(70, 20, t);
                return Surface(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius) + Vector3.up * .15f;
            }
            for (int i = 0; i <= 180; i++)
            {
                float t = i / 180f;
                var saved = path.Find("AscentPlank" + i);
                var plank = saved != null ? saved.gameObject : Model("VolcanoPlank", scene);
                if (saved == null)
                {
                    plank.name = "AscentPlank" + i; plank.transform.SetParent(path, true);
                    plank.AddComponent<BoxCollider>().size = new Vector3(2.9f, .1f, 1.05f);
                }
                plank.transform.position = PathPoint(t);
                var direction = PathPoint(Mathf.Min(1, t + .002f)) - PathPoint(Mathf.Max(0, t - .002f));
                plank.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
            if (island.transform.Find("CraterTrigger") == null)
            {
                var trigger = new GameObject("CraterTrigger"); trigger.transform.SetParent(island.transform, false);
                trigger.transform.localPosition = new Vector3(0, 22, 0);
                var shape = trigger.AddComponent<BoxCollider>(); shape.isTrigger = true; shape.size = new Vector3(23, 2, 23);
                trigger.AddComponent<HowToFishVolcanoCrater>();
                var spawn = new GameObject("WhaleEmergence").transform; spawn.SetParent(island.transform, true);
                spawn.position = Surface(24, 0) + Vector3.up * 1.2f;
                var settings = new SerializedObject(trigger.GetComponent<HowToFishVolcanoCrater>());
                Bind(settings, "bossSpawn", spawn); settings.ApplyModifiedPropertiesWithoutUndo();
            }
            if (island.transform.Find("MilitaryBoat") == null)
            {
                var military = Model("MilitaryBoat", scene); military.transform.SetParent(island.transform, false);
                military.transform.localPosition = new Vector3(9, .1f, -94);
                AddStaticModelColliders(military);
                var wheel = military.GetComponentsInChildren<Transform>().Single(node => node.name == "SteeringWheel");
                var shape = wheel.gameObject.AddComponent<BoxCollider>(); shape.size = new Vector3(.6f, .55f, .3f);
                Station(wheel.gameObject, HowToFishStationKind.MilitaryDeparture, "驾驶军用船返回大陆");
                for (int i = 0; i < 8; i++)
                {
                    var plank = Model("VolcanoPlank", scene); plank.name = "MilitaryDock" + i;
                    plank.transform.SetParent(island.transform, false); plank.transform.localPosition = new Vector3(9, .3f, -85 - i);
                    plank.AddComponent<BoxCollider>().size = new Vector3(2.9f, .1f, 1.05f);
                }
            }
            if (island.transform.Find("FootSnailPoints") == null)
            {
                var points = new GameObject("FootSnailPoints").transform; points.SetParent(island.transform, false);
                for (int i = 0; i < 4; i++)
                {
                    var point = new GameObject("FootSnail" + i).transform; point.SetParent(points, true);
                    point.position = Surface(-10 - i * 2, -68 + i) + Vector3.up * .1f;
                }
            }
            var world = scene.GetRootGameObjects().Single(root => root.GetComponent<HowToFishWorld>() != null).GetComponent<HowToFishWorld>();
            var data = new SerializedObject(world);
            Bind(data, "crater", island.GetComponentInChildren<HowToFishVolcanoCrater>());
            var snails = data.FindProperty("snailPoints");
            var snailRoot = island.transform.Find("FootSnailPoints"); snails.arraySize = snailRoot.childCount;
            for (int i = 0; i < snailRoot.childCount; i++) snails.GetArrayElementAtIndex(i).objectReferenceValue = snailRoot.GetChild(i);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateBaitSupplyShops(Scene scene)
        {
            var names = new[] { "LighthouseIsland", "ForestIsland", "DesertIsland", "RocksIsland", "VolcanoIsland" };
            var positions = new[] { new Vector2(-7, -2), new Vector2(9, -36), new Vector2(4, -32), new Vector2(5, -25), new Vector2(-8, -67) };
            var stock = new[] { ("CrabRod", 0), ("Radar", 0), ("HotDog", 0), ("FishingRod", 1),
                ("BeginnerLure", 1), ("BeginnerBossLure", 1), ("StandardLure", 2), ("StandardBossLure", 2),
                ("ProfessionalLure", 3), ("ProfessionalBossLure", 3), ("ScientificLure", 4), ("ScientificBossLure", 4), ("Dynamite", 1) };
            for (int i = 0; i < names.Length; i++)
            {
                var island = scene.GetRootGameObjects().Single(root => root.name == names[i]);
                var ground = island.GetComponent<MeshCollider>();
                if (ground == null) ground = island.GetComponentsInChildren<MeshCollider>().Single(value => value.name == "Terrain");
                int standIndex = 0;
                foreach (var row in stock.Where(row => row.Item2 <= i))
                {
                    var point = positions[i] + new Vector2(3 + standIndex % 3 * 1.65f, -4 - standIndex / 3 * 1.65f);
                    standIndex++;
                    if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HowToFishStation>())
                        .Any(station => station.Kind == HowToFishStationKind.Product && station.ItemId == row.Item1 && station.Island == i)) continue;
                    Physics.SyncTransforms();
                    if (!ground.Raycast(new Ray(island.transform.position + new Vector3(point.x, 90, point.y), Vector3.down), out var floor, 120))
                        throw new InvalidOperationException("补给台落点不在地形上：" + names[i] + "/" + row.Item1);
                    var stand = Model("SupplyCrate", scene); stand.name = row.Item1 + "Supply";
                    stand.transform.SetParent(island.transform, true); stand.transform.position = floor.point; AddStaticModelColliders(stand);
                    float top = stand.GetComponentsInChildren<Renderer>().Max(renderer => renderer.bounds.max.y);
                    var product = Product(row.Item1, new Vector3(floor.point.x, top, floor.point.z),
                        row.Item1.EndsWith("Rod", StringComparison.Ordinal) ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity, i);
                    product.transform.SetParent(stand.transform, true);
                    float bottom = product.GetComponentsInChildren<Renderer>().Min(renderer => renderer.bounds.min.y);
                    product.transform.position += Vector3.up * (top - bottom + .025f);
                }
            }
        }

        private static void UpdateSlotMachines(Scene scene)
        {
            var names = new[] { "LighthouseIsland", "ForestIsland", "DesertIsland", "RocksIsland", "VolcanoIsland" };
            var points = new[] { new Vector2(6, -5), new Vector2(-7, -30), new Vector2(-12, -30), new Vector2(-8, -30), new Vector2(11, -66) };
            Physics.SyncTransforms();
            for (int i = 0; i < names.Length; i++)
            {
                var island = scene.GetRootGameObjects().Single(root => root.name == names[i]);
                if (island.transform.Find("SlotMachine") != null) continue;
                var ground = island.GetComponent<MeshCollider>();
                if (ground == null) ground = island.GetComponentsInChildren<MeshCollider>().Single(value => value.name == "Terrain");
                if (!ground.Raycast(new Ray(island.transform.position + new Vector3(points[i].x, 90, points[i].y), Vector3.down), out var floor, 120))
                    throw new InvalidOperationException("老虎机落点不在地形上：" + names[i]);
                var model = Model("SlotMachine", scene); model.name = "SlotMachine";
                model.transform.SetParent(island.transform, true); model.transform.position = floor.point;
                var body = model.AddComponent<BoxCollider>(); body.center = new Vector3(0, .9f, -.1f); body.size = new Vector3(1.05f, 1.8f, .55f);
                var intake = model.GetComponentsInChildren<Transform>().Single(node => node.name == "Intake");
                var area = intake.gameObject.AddComponent<BoxCollider>(); area.isTrigger = true; area.size = new Vector3(.8f, .7f, .7f);
                var machine = intake.gameObject.AddComponent<HowToFishSlotMachine>();
                var settings = new SerializedObject(machine); settings.FindProperty("island").intValue = i; settings.ApplyModifiedPropertiesWithoutUndo();
                var label = new GameObject("Instructions", typeof(RectTransform), typeof(Canvas)); label.transform.SetParent(model.transform, false);
                label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var rect = label.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(450, 100);
                rect.localScale = Vector3.one * .003f; rect.localPosition = new Vector3(0, 2.05f, .38f); rect.localRotation = Quaternion.Euler(0, 180, 0);
                Text("Label", rect, "皮肤老虎机\n拿起死 Drip，再放入投料口", new Vector2(450, 100), Vector2.zero, 25);
            }
        }

        private static void UpdateRoulette(Scene scene)
        {
            var island = scene.GetRootGameObjects().Single(root => root.name == "RocksIsland");
            if (island.transform.Find("RouletteTable") != null) return;
            Physics.SyncTransforms();
            var terrain = island.GetComponent<MeshCollider>();
            if (!terrain.Raycast(new Ray(island.transform.position + new Vector3(-13, 90, -30), Vector3.down), out var floor, 120))
                throw new InvalidOperationException("轮盘桌落点不在岩石岛地形上。");
            var table = Model("RouletteTable", scene); table.name = "RouletteTable";
            table.transform.SetParent(island.transform, true); table.transform.position = floor.point;
            var body = table.AddComponent<BoxCollider>(); body.center = new Vector3(0, .5f, 0); body.size = new Vector3(2.8f, 1, 1.8f);
            var roulette = table.AddComponent<HowToFishRoulette>();
            var settings = new SerializedObject(roulette);
            foreach (string color in new[] { "Red", "Black", "Green" })
            {
                var mount = table.GetComponentsInChildren<Transform>().Single(node => node.name == color + "Bet");
                var area = mount.gameObject.AddComponent<BoxCollider>(); area.isTrigger = true;
                area.center = Vector3.up * .3f; area.size = new Vector3(.58f, .65f, .55f);
                Bind(settings, char.ToLowerInvariant(color[0]) + color.Substring(1) + "Zone", area);
            }
            foreach (string part in new[] { "Wheel", "Ball" })
                Bind(settings, part.ToLowerInvariant(), table.GetComponentsInChildren<Transform>().Single(node => node.name == part));
            settings.ApplyModifiedPropertiesWithoutUndo();
            var label = new GameObject("Instructions", typeof(RectTransform), typeof(Canvas)); label.transform.SetParent(table.transform, false);
            label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = label.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(700, 110);
            rect.localScale = Vector3.one * .003f; rect.localPosition = new Vector3(0, 1.8f, -.8f); rect.localRotation = Quaternion.Euler(0, 180, 0);
            Text("Label", rect, "实物轮盘\n放下鱼获后启动 · 红/黑 ×2 · 绿 ×35", new Vector2(700, 110), Vector2.zero, 25);
        }

        private static void UpdateCookingStations(Scene scene)
        {
            var rocks = scene.GetRootGameObjects().Single(root => root.name == "RocksIsland");
            if (rocks.transform.Find("GrillMaster") == null)
            {
                var ground = rocks.GetComponent<MeshCollider>();
                foreach (var row in new[] { ("GrillMaster", new Vector2(12, -31)), ("Grill", new Vector2(10.4f, -31)) })
                {
                    if (!ground.Raycast(new Ray(rocks.transform.position + new Vector3(row.Item2.x, 60, row.Item2.y), Vector3.down), out var floor, 90))
                        throw new InvalidOperationException("岩石岛烤架落点不在地形上。");
                    var model = Model(row.Item1, scene); model.transform.SetParent(rocks.transform, true);
                    model.transform.position = floor.point; model.transform.rotation = Quaternion.Euler(0, 180, 0);
                    if (row.Item1 == "GrillMaster")
                    {
                        var shape = model.AddComponent<CapsuleCollider>(); shape.center = Vector3.up * .9f; shape.height = 1.8f; shape.radius = .3f;
                        Station(model, HowToFishStationKind.GrillMaster, "烧烤师");
                    }
                    else
                    {
                        var shape = model.AddComponent<BoxCollider>(); shape.center = Vector3.up * .48f; shape.size = new Vector3(1, .96f, .6f);
                        Station(model, HowToFishStationKind.Grill, "烧烤架");
                    }
                }
            }
            foreach (var station in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HowToFishStation>())
                .Where(value => value.Kind == HowToFishStationKind.Grill || value.Kind == HowToFishStationKind.GrillMaster))
            {
                var stationSettings = new SerializedObject(station);
                stationSettings.FindProperty("island").intValue = station.GetComponentInParent<HowToFishIsland>().Index;
                stationSettings.ApplyModifiedPropertiesWithoutUndo();
                if (station.Kind != HowToFishStationKind.Grill) continue;
                if (station.transform.Find("HeatArea") != null) continue;
                var area = new GameObject("HeatArea", typeof(BoxCollider), typeof(HowToFishGrill));
                area.transform.SetParent(station.transform, false);
                var shape = area.GetComponent<BoxCollider>(); shape.isTrigger = true;
                shape.center = Vector3.up * 1.12f; shape.size = new Vector3(1.08f, .4f, .68f);
                var lightObject = new GameObject("Embers", typeof(Light)); lightObject.transform.SetParent(area.transform, false);
                lightObject.transform.localPosition = Vector3.up * .88f;
                var glow = lightObject.GetComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(1, .32f, .06f);
                glow.intensity = 1.2f; glow.range = 1.8f; glow.enabled = false;
                var settings = new SerializedObject(area.GetComponent<HowToFishGrill>()); Bind(settings, "embers", glow); settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void UpdateBoatUpgrades(GameObject boat, Scene scene)
        {
            var settings = new SerializedObject(boat.GetComponent<HowToFishBoat>());
            var motors = settings.FindProperty("motors"); motors.arraySize = 3;
            var propellers = new List<Transform>();
            var names = new[] { "SmallMotor", "MediumMotor", "BigMotor" };
            for (int i = 0; i < names.Length; i++)
            {
                var mount = boat.transform.Find(names[i]);
                if (mount == null)
                {
                    mount = Model(names[i], scene).transform;
                    mount.SetParent(boat.transform, false);
                    mount.localPosition = new Vector3(0, .4f, -2.35f);
                }
                mount.gameObject.SetActive(i == 0);
                motors.GetArrayElementAtIndex(i).objectReferenceValue = mount.gameObject;
                propellers.AddRange(mount.GetComponentsInChildren<Transform>(true).Where(child => child.name.StartsWith("Propeller", StringComparison.Ordinal)));
            }
            var propellerRefs = settings.FindProperty("propellers"); propellerRefs.arraySize = propellers.Count;
            for (int i = 0; i < propellers.Count; i++) propellerRefs.GetArrayElementAtIndex(i).objectReferenceValue = propellers[i];
            var radar = boat.transform.Find("BoatRadar");
            if (radar == null)
            {
                radar = Model("BoatRadar", scene).transform;
                radar.SetParent(boat.transform, false);
                radar.localPosition = new Vector3(.38f, .83f, -.05f);
                radar.localRotation = Quaternion.Euler(0, 180, 0);
                var screen = new GameObject("Screen", typeof(RectTransform), typeof(Canvas));
                screen.transform.SetParent(radar, false);
                screen.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var rect = screen.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(200, 160); rect.localScale = Vector3.one * .0018f;
                rect.localPosition = new Vector3(0, .21f, .084f); rect.localRotation = Quaternion.Euler(0, 180, 0);
                Text("You", rect, "▲", new Vector2(20, 20), Vector2.zero, 16).color = new Color(.5f, 1, .65f);
                for (int i = 0; i < 5; i++)
                    Text("Island" + i, rect, "", new Vector2(24, 24), Vector2.zero, 18).color = new Color(.5f, 1, .65f);
            }
            Bind(settings, "radar", radar.gameObject);
            var dots = settings.FindProperty("radarIslands"); dots.arraySize = 5;
            for (int i = 0; i < 5; i++) dots.GetArrayElementAtIndex(i).objectReferenceValue = radar.Find("Screen/Island" + i).GetComponent<TextMeshProUGUI>();
            radar.gameObject.SetActive(false);
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateWeaponShops(Scene scene)
        {
            var names = new[] { "LighthouseIsland", "ForestIsland", "DesertIsland", "RocksIsland", "VolcanoIsland" };
            var positions = new[] { new Vector2(-7, -2), new Vector2(9, -36), new Vector2(4, -32), new Vector2(5, -25), new Vector2(-8, -67) };
            var oldAnvil = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Anvil");
            if (oldAnvil != null) Object.DestroyImmediate(oldAnvil);
            Physics.SyncTransforms();
            for (int i = 0; i < names.Length; i++)
            {
                var island = scene.GetRootGameObjects().Single(root => root.name == names[i]);
                var ground = island.GetComponent<MeshCollider>();
                if (ground == null) ground = island.GetComponentsInChildren<MeshCollider>().Single(collider => collider.name == "Terrain");
                foreach (var upgrade in new[] { ("MediumMotor", 1, "中型马达", 1), ("BigMotor", 3, "大型双机马达", 2), ("BoatRadar", 2, "船载雷达", 0) })
                {
                    if (i < upgrade.Item2) continue;
                    var point = positions[i] + new Vector2(-3 - upgrade.Item4 * 1.4f, -6.5f);
                    if (!ground.Raycast(new Ray(island.transform.position + new Vector3(point.x, 90, point.y), Vector3.down), out var floor, 120))
                        throw new InvalidOperationException("船只升级台落点不在地形上。");
                    var existing = island.transform.Find(upgrade.Item1 + "Shop");
                    if (existing != null) { existing.position = floor.point; continue; }
                    var stand = Model("SupplyCrate", scene); stand.name = upgrade.Item1 + "Shop";
                    stand.transform.SetParent(island.transform, true); stand.transform.position = floor.point; AddStaticModelColliders(stand);
                    float baseHeight = stand.GetComponentsInChildren<Renderer>().Max(renderer => renderer.bounds.max.y) - stand.transform.position.y;
                    var display = Model(upgrade.Item1, scene); display.transform.SetParent(stand.transform, false);
                    float bottom = display.GetComponentsInChildren<Renderer>().Min(renderer => renderer.bounds.min.y) - display.transform.position.y;
                    display.transform.localPosition = Vector3.up * (baseHeight - bottom); AddStaticModelColliders(display);
                    Station(stand, upgrade.Item4 == 0 ? HowToFishStationKind.BoatRadar : HowToFishStationKind.MotorUpgrade, upgrade.Item3);
                    var settings = new SerializedObject(stand.GetComponent<HowToFishStation>());
                    settings.FindProperty("island").intValue = i;
                    settings.FindProperty("motorTier").intValue = upgrade.Item4; settings.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (string modelId in i == 0 ? new[] { "UpgradeAnvil" } : new[] { "UpgradeAnvil", "AmmoUpgrade" })
                {
                    if (island.transform.Find(modelId) != null) continue;
                    var point = positions[i] + (modelId == "AmmoUpgrade" ? Vector2.right * 1.4f : Vector2.zero);
                    if (!ground.Raycast(new Ray(island.transform.position + new Vector3(point.x, 90, point.y), Vector3.down), out var hit, 120))
                        throw new InvalidOperationException("升级台落点不在地形上：" + names[i]);
                    var bench = Model(modelId, scene); bench.transform.SetParent(island.transform, true);
                    bench.transform.position = hit.point;
                    bench.transform.rotation = Quaternion.Euler(0, 180, 0);
                    AddStaticModelColliders(bench);
                    Station(bench, modelId == "UpgradeAnvil" ? HowToFishStationKind.Anvil : HowToFishStationKind.AmmoUpgrade,
                        modelId == "UpgradeAnvil" ? "磨锐当前近战武器" : "升级当前枪械弹药");
                    var settings = new SerializedObject(bench.GetComponent<HowToFishStation>());
                    settings.FindProperty("island").intValue = i; settings.ApplyModifiedPropertiesWithoutUndo();
                }
                var anvil = island.transform.Find("UpgradeAnvil");
                foreach (var row in new[] { ("BrassKnuckles", -.12f), ("Knife", .10f) })
                {
                    if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HowToFishStation>())
                        .Any(station => station.Kind == HowToFishStationKind.Product && station.ItemId == row.Item1 && station.Island == i)) continue;
                    var product = Product(row.Item1, anvil.TransformPoint(new Vector3(row.Item2, .89f, 0)), Quaternion.Euler(0, 90, 90), i);
                    product.transform.SetParent(anvil, true);
                }
                if (i > 0 && island.transform.Find("InventoryUpgrade") == null)
                {
                    var point = positions[i] + Vector2.left * 1.5f;
                    if (!ground.Raycast(new Ray(island.transform.position + new Vector3(point.x, 90, point.y), Vector3.down), out var floor, 120))
                        throw new InvalidOperationException("背包扩容台落点不在地形上。");
                    var station = Model("SupplyCrate", scene); station.name = "InventoryUpgrade";
                    station.transform.SetParent(island.transform, true); station.transform.position = floor.point; AddStaticModelColliders(station);
                    float height = station.GetComponentsInChildren<Renderer>().Max(renderer => renderer.bounds.max.y) - station.transform.position.y;
                    var bag = Model("Backpack", scene); bag.transform.SetParent(station.transform, false);
                    bag.transform.localPosition = Vector3.up * height; bag.transform.localRotation = Quaternion.Euler(0, 180, 0); AddStaticModelColliders(bag);
                    Station(station, HowToFishStationKind.InventoryUpgrade, "扩充装备栏");
                    var settings = new SerializedObject(station.GetComponent<HowToFishStation>());
                    settings.FindProperty("island").intValue = i; settings.ApplyModifiedPropertiesWithoutUndo();
                }
                int displayIndex = 0;
                foreach (var row in new[] { (HowToFishAttachment.RedDotSight, 2, "红点瞄具"), (HowToFishAttachment.SniperScope, 3, "瞄准镜"),
                    (HowToFishAttachment.Compensator, 2, "补偿器"), (HowToFishAttachment.Suppressor, 3, "消音器"),
                    (HowToFishAttachment.LaserSight, 1, "激光瞄具"), (HowToFishAttachment.ExtendedMag, 2, "扩容弹匣") })
                {
                    if (i < row.Item2) continue;
                    var point = positions[i] + new Vector2(displayIndex % 3 * 1.4f, -2.2f - displayIndex / 3 * 1.6f);
                    displayIndex++;
                    string name = "AttachmentShop_" + row.Item1;
                    if (island.transform.Find(name) != null) continue;
                    if (!ground.Raycast(new Ray(island.transform.position + new Vector3(point.x, 90, point.y), Vector3.down), out var hit, 120))
                        throw new InvalidOperationException("配件箱落点不在地形上：" + names[i]);
                    var shop = new GameObject(name); SceneManager.MoveGameObjectToScene(shop, scene);
                    shop.transform.SetParent(island.transform, true); shop.transform.position = hit.point; shop.transform.rotation = Quaternion.Euler(0, 180, 0);
                    var support = Model("SupplyCrate", scene); support.transform.SetParent(shop.transform, false); AddStaticModelColliders(support);
                    float height = support.GetComponentsInChildren<Renderer>().Max(renderer => renderer.bounds.max.y) - shop.transform.position.y;
                    var crate = Model("AttachmentCrate", scene); crate.transform.SetParent(shop.transform, false); crate.transform.localPosition = Vector3.up * height;
                    AddStaticModelColliders(crate);
                    var display = crate.GetComponentsInChildren<Transform>().Single(node => node.name == "Display");
                    var part = Model(row.Item1.ToString(), scene); part.transform.SetParent(display, false);
                    part.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    float bottom = part.GetComponentsInChildren<Renderer>().Min(renderer => renderer.bounds.min.y);
                    part.transform.position += Vector3.up * (display.position.y - bottom + .005f);
                    Station(shop, HowToFishStationKind.Attachment, row.Item3);
                    var settings = new SerializedObject(shop.GetComponent<HowToFishStation>());
                    settings.FindProperty("island").intValue = i; settings.FindProperty("attachment").enumValueIndex = (int)row.Item1;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void AddStaticModelColliders(GameObject model)
        {
            foreach (var mesh in model.GetComponentsInChildren<MeshFilter>())
                mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
        }

        private static void ConfigureIsland(GameObject root, int index, string name, float radius)
        {
            var marker = root.GetComponent<HowToFishIsland>() ?? root.AddComponent<HowToFishIsland>();
            var settings = new SerializedObject(marker);
            settings.FindProperty("index").intValue = index;
            settings.FindProperty("displayName").stringValue = name;
            settings.FindProperty("radius").floatValue = radius;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateFirstIslandShop(Scene scene)
        {
            string[] products = { "CrabRod", "Knife", "HotDog", "Beer", "Radar" };
            foreach (var station in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HowToFishStation>()).ToArray())
                if (station.Kind == HowToFishStationKind.Product && station.Island == 0 && products.Contains(station.ItemId))
                    Object.DestroyImmediate(station.gameObject);
            foreach (var root in scene.GetRootGameObjects().Where(root => root.name == "StoreBoard" || root.name == "SellingCounter" || root.name == "FreeRodSpawn"))
                Object.DestroyImmediate(root);
            Product("CrabRod", new Vector3(-5.40f, 2.65f, .12f), Quaternion.Euler(-90, 0, 0));
            Product("Knife", new Vector3(-5.12f, 3.25f, .12f), Quaternion.Euler(-90, 0, 0));
            Product("HotDog", new Vector3(-4.66f, 3.95f, .12f), Quaternion.Euler(-90, 0, 0));
            Product("Beer", new Vector3(-4.66f, 3.02f, .12f));
            Product("Radar", new Vector3(-3.45f, 3.05f, -3.15f));
        }

        private static GameObject Model(string name, Scene scene)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/" + name + ".fbx");
            if (source == null) throw new InvalidOperationException("缺少模型：" + name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            model.name = name;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(HowToFishAssetBuilder.MaterialFor).ToArray();
            return model;
        }

        private static void BuildHud()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HudPath) == null)
                throw new InvalidOperationException("渔力全开 UI 已按页面独立维护，请从版本库恢复 Prefabs/UI 资源。");
            var existing = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var previousFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset");
                foreach (var text in existing.GetComponentsInChildren<TextMeshProUGUI>(true))
                    if (text.font == previousFont) text.font = Font;
                GenerateHudBinding(existing);
            }
            finally { PrefabUtility.UnloadPrefabContents(existing); }
        }

        private static void GenerateHudBinding(GameObject root)
        {
            var presenter = new SerializedObject(root.GetComponent<HowToFishHudPresenter>());
            presenter.FindProperty("m_EditorClassIdentifier").stringValue = "Hotfix::" + typeof(HowToFishHudPresenter).FullName;
            presenter.ApplyModifiedPropertiesWithoutUndo();
            var nodes = MvcPrefabScanner.Scan(root);
            foreach (var node in nodes)
            {
                var component = node.gameObject.GetComponent<HowToFishHudPresenter>() as Component
                    ?? node.gameObject.GetComponent<Button>() as Component
                    ?? node.gameObject.GetComponent<TextMeshProUGUI>() as Component
                    ?? node.gameObject.GetComponent<Image>();
                if (component != null) Select(node, component.GetType());
            }
            var settings = new MvcBindSettings
            {
                prefabPath = HudPath, moduleName = "HowToFish", viewName = "HowToFishHudView", namespaceName = "Hotfix",
                address = MvcBindPathUtility.ToRuntimeAddress(HudPath), viewType = ViewType.View,
                layer = UILayer.Decorate, viewMode = UIViewMode.Widget, mask = MaskType.None,
                isHotfix = true, enableOnInit = true, destroyOnHide = true,
                useCustomModuleOutputDirectory = true,
                customModuleOutputDirectory = "Assets/Scripts/Hotfix/Demos/HowToFish/UI",
                uiTransitionType = typeof(EmptyUITransition).FullName
            };
            settings.outputFolder = MvcBindPathUtility.ToOutputFolder(settings);
            if (!MvcBindComponentWindowBridge.GenerateAndBind(root, settings, nodes, false, out _, out var message)) throw new InvalidOperationException(message);
            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        }

        private static void BuildScene()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var sand = Material("Sand", new Color(.78f, .69f, .48f));
                var grass = Material("Grass", new Color(.26f, .38f, .14f));
                var rock = Material("CoastalRock", new Color(.34f, .31f, .26f));
                BuildIsland(grass, sand, rock);
                Primitive("Ocean", PrimitiveType.Plane, Vector3.down * .03f, new Vector3(300, 1, 300), Material("Ocean", new Color(.1f, .39f, .58f)), false);
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.6f; light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(45, -35, 0);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.52f, .60f, .67f);
                RenderSettings.fog = true; RenderSettings.fogColor = new Color(.44f, .66f, .79f); RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 180; RenderSettings.fogEndDistance = 650;
                var wood = Material("WeatheredWood", new Color(.34f, .21f, .13f));
                Primitive("LighthouseBlockout", PrimitiveType.Cylinder, new Vector3(-5, 7, 2), new Vector3(3.4f, 4.5f, 3.4f), Material("LighthouseStone", new Color(.77f, .76f, .68f)));
                Primitive("LighthouseRoofBlockout", PrimitiveType.Cylinder, new Vector3(-5, 12, 2), new Vector3(4, .6f, 4), Material("LighthouseRoof", new Color(.44f, .12f, .08f)));
                var keeper = Primitive("KeeperBlockout", PrimitiveType.Capsule, new Vector3(-4, 3.4f, -3), new Vector3(.65f, .95f, .65f), Material("KeeperClothes", new Color(.22f, .24f, .25f)));
                Station(keeper, HowToFishStationKind.Keeper, "与灯塔看守人交谈");
                var anvil = Primitive("Anvil", PrimitiveType.Cube, new Vector3(-7, 2.9f, -2), new Vector3(.8f, .6f, .5f), Material("AnvilIron", new Color(.15f, .16f, .18f)));
                Station(anvil, HowToFishStationKind.Anvil, "升级当前武器");
                var player = BuildPlayer();
                var boat = BuildBoat(wood);
                var world = new GameObject("HowToFishWorld").AddComponent<HowToFishWorld>();
                var state = new SerializedObject(world);
                Bind(state, "catalog", AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset"));
                Bind(state, "inputTemplate", AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/Data/PlayerInput.asset"));
                Bind(state, "player", player); Bind(state, "boat", boat);
                var start = new GameObject("StartPoint").transform; start.position = new Vector3(0, 3, -15);
                Bind(state, "startPoint", start);
                player.transform.position = start.position;
                var points = state.FindProperty("clamPoints"); points.arraySize = 12;
                for (int i = 0; i < 12; i++)
                {
                    var point = new GameObject("ClamSpawn" + i).transform;
                    float angle = (-.7f + i * .13f);
                    point.position = new Vector3(Mathf.Sin(angle) * 23, 1.6f, -Mathf.Cos(angle) * 23);
                    points.GetArrayElementAtIndex(i).objectReferenceValue = point;
                }
                state.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene, Root + "/Scenes/Main.unity")) throw new InvalidOperationException("场景保存失败。");
            }
            finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }

        private static HowToFishPlayer BuildPlayer()
        {
            var root = new GameObject("Fisher");
            var motor = root.AddComponent<CharacterController>(); motor.height = 1.8f; motor.radius = .28f; motor.center = Vector3.up * .9f; motor.stepOffset = .35f;
            var eye = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            eye.transform.SetParent(root.transform, false); eye.transform.localPosition = Vector3.up * 1.65f;
            eye.tag = "MainCamera"; eye.nearClipPlane = .03f; eye.farClipPlane = 1200; eye.fieldOfView = 75;
            eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = new Color(.44f, .66f, .79f);
            eye.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var equipment = new GameObject("Equipment").transform; equipment.SetParent(eye.transform, false);
            var line = new GameObject("FishingLine").AddComponent<LineRenderer>(); line.transform.SetParent(root.transform, false);
            line.sharedMaterial = Material("FishingLine", new Color(.9f, .92f, .87f)); line.startWidth = .007f; line.endWidth = .003f;
            line.positionCount = 18; line.enabled = false;
            const string bobberPath = Root + "/Prefabs/Items/Bobber.prefab";
            var bobber = AssetDatabase.LoadAssetAtPath<Rigidbody>(bobberPath);
            if (bobber == null)
            {
                var floating = Primitive("Bobber", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .09f, Material("BobberRed", new Color(.92f, .18f, .08f)));
                var body = floating.AddComponent<Rigidbody>(); body.mass = .04f; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                bobber = PrefabUtility.SaveAsPrefabAsset(floating, bobberPath).GetComponent<Rigidbody>(); Object.DestroyImmediate(floating);
            }
            var fishing = root.AddComponent<HowToFishFishingRig>(); var rig = new SerializedObject(fishing); Bind(rig, "bobberPrefab", bobber); Bind(rig, "line", line); rig.ApplyModifiedPropertiesWithoutUndo();
            var player = root.AddComponent<HowToFishPlayer>(); var settings = new SerializedObject(player);
            Bind(settings, "eye", eye); Bind(settings, "equipmentRoot", equipment); Bind(settings, "fishing", fishing); settings.ApplyModifiedPropertiesWithoutUndo();
            return player;
        }

        private static HowToFishBoat BuildBoat(Material wood)
        {
            var root = new GameObject("FishingBoat"); root.transform.position = new Vector3(8, .2f, -30);
            var deck = Primitive("DeckBlockout", PrimitiveType.Cube, Vector3.zero, new Vector3(2.1f, .18f, 4.5f), wood); deck.transform.SetParent(root.transform, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = Primitive("Gunwale" + side, PrimitiveType.Cube, Vector3.zero, new Vector3(.15f, .6f, 4.5f), wood);
                wall.transform.SetParent(root.transform, false); wall.transform.localPosition = new Vector3(side * 1.1f, .3f, 0);
            }
            var body = root.AddComponent<Rigidbody>(); body.mass = 180; body.linearDamping = .6f; body.angularDamping = 3;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var seat = new GameObject("DriverSeat").transform; seat.SetParent(root.transform, false); seat.localPosition = new Vector3(0, .2f, -.8f);
            var exit = new GameObject("ExitPoint").transform; exit.SetParent(root.transform, false); exit.localPosition = new Vector3(0, .3f, -1.5f);
            var wheel = Primitive("WheelBlockout", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.5f, .08f, .5f), wood);
            wheel.transform.SetParent(root.transform, false); wheel.transform.localPosition = new Vector3(0, 1, 0);
            wheel.transform.localRotation = Quaternion.Euler(65, 0, 0); Station(wheel, HowToFishStationKind.BoatWheel, "驾驶船只");
            var boat = root.AddComponent<HowToFishBoat>(); var settings = new SerializedObject(boat); Bind(settings, "seat", seat); Bind(settings, "exitPoint", exit); settings.ApplyModifiedPropertiesWithoutUndo();
            return boat;
        }

        private static GameObject BuildIsland(Material grass, Material sand, Material rock,
            string name = "LighthouseIsland", Vector3 position = default, bool lake = false)
        {
            bool desert = name == "DesertIsland";
            bool rocks = name == "RocksIsland";
            var meshPath = Root + (rocks ? "/Data/RocksTerrain.asset" : desert ? "/Data/DesertTerrain.asset" : lake ? "/Data/ForestTerrain.asset" : "/Data/IslandTerrain.asset");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                var vertices = new List<Vector3>(); var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
                float[] radius = desert || rocks ? new[] { 0f, 14, 35, 53, 64, 72 } : lake ? new[] { 0f, 15, 21, 35, 48, 60 } : new[] { 0f, 7, 20, 25, 30, 36 };
                float[] height = rocks ? new[] { 4.8f, 4.8f, 4.8f, 1.2f, .2f, -3f } : desert ? new[] { 3.5f, 3.1f, 2.6f, 1f, .2f, -3f } : lake ? new[] { -4f, -3, .3f, 2.4f, .6f, -3 } : new[] { 2.4f, 2.4f, 2.1f, .6f, -.4f, -3f };
                Vector3 Point(int ring, int index)
                {
                    float angle = index * Mathf.PI * 2 / 48;
                    float r = radius[ring] * (1 + (ring == 0 ? 0 : Mathf.Sin(angle * 5) * .035f));
                    return new Vector3(Mathf.Sin(angle) * r, height[ring], Mathf.Cos(angle) * r);
                }
                void Triangle(Vector3 a, Vector3 b, Vector3 c, int submesh)
                { int first = vertices.Count; vertices.Add(a); vertices.Add(c); vertices.Add(b); triangles[submesh].AddRange(new[] { first, first + 1, first + 2 }); }
                for (int ring = 0; ring < 5; ring++) for (int i = 0; i < 48; i++)
                {
                    int sub = lake ? (ring < 2 ? 2 : ring < 4 ? 0 : 1) : (ring < 2 ? 0 : ring < 4 ? 1 : 2);
                    Triangle(Point(ring, i), Point(ring + 1, i + 1), Point(ring + 1, i), sub);
                    if (ring > 0) Triangle(Point(ring, i), Point(ring, i + 1), Point(ring + 1, i + 1), sub);
                }
                mesh = new Mesh { name = "IslandTerrain", subMeshCount = 3 }; mesh.SetVertices(vertices);
                for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, meshPath);
            }
            var island = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            island.transform.position = position;
            island.GetComponent<MeshFilter>().sharedMesh = mesh; island.GetComponent<MeshCollider>().sharedMesh = mesh;
            island.GetComponent<MeshRenderer>().sharedMaterials = new[] { grass, sand, rock };
            return island;
        }

        private static GameObject Product(string id, Vector3 position, Quaternion? rotation = null, int island = 0)
        {
            var definition = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset").FindItem(id);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/" +
                (id == "StandardBossLure" ? "StandardLure" : id == "ProfessionalBossLure" ? "ProfessionalLure" : id) + ".fbx");
            GameObject item;
            if (source != null)
            {
                item = (GameObject)PrefabUtility.InstantiatePrefab(source); item.transform.position = position;
                foreach (var renderer in item.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Materials/" + m.name + ".mat")).ToArray();
                var bounds = new Bounds(item.transform.position, Vector3.zero);
                foreach (var renderer in item.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                var collider = item.AddComponent<BoxCollider>();
                collider.center = bounds.center - item.transform.position;
                collider.size = Vector3.Max(bounds.size, Vector3.one * .14f);
            }
            else item = Primitive(id + "DisplayBlockout", PrimitiveType.Cylinder, position, new Vector3(.16f, .22f, .16f), Material("ShopDisplay", new Color(.67f, .35f, .11f)));
            item.transform.rotation = rotation ?? Quaternion.identity;
            Station(item, HowToFishStationKind.Product, "购买 " + definition.DisplayName, id);
            var settings = new SerializedObject(item.GetComponent<HowToFishStation>());
            settings.FindProperty("island").intValue = island; settings.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static void Station(GameObject target, HowToFishStationKind kind, string label, string id = "")
        {
            var settings = new SerializedObject(target.AddComponent<HowToFishStation>());
            settings.FindProperty("kind").enumValueIndex = (int)kind; settings.FindProperty("label").stringValue = label; settings.FindProperty("itemId").stringValue = id; settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collider = true)
        {
            var result = GameObject.CreatePrimitive(type); result.name = name; result.transform.position = position; result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(result.GetComponent<Collider>());
            return result;
        }

        private static Material Material(string name, Color color)
        {
            var path = Root + "/Art/Materials/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .05f);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position, Vector2? anchor = null)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.gameObject.layer = LayerMask.NameToLayer("UI");
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(.5f, .5f); rect.pivot = anchor ?? new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string value, Vector2 size, Vector2 position, int fontSize, Vector2? anchor = null)
        {
            var text = Rect(name, parent, size, position, anchor).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font; text.fontSize = fontSize; text.text = value; text.color = new Color(.96f, .94f, .86f); text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false; return text;
        }

        private static Image Panel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        { var image = Rect(name, parent, size, position).gameObject.AddComponent<Image>(); image.color = color; return image; }

        private static void Bind(SerializedObject owner, string field, Object value) => owner.FindProperty(field).objectReferenceValue = value;
        private static void Select(MvcBindNode node, Type type)
        { node.selectedComponentType = type; node.selectedComponentTypeName = type.FullName; node.selectedComponentTypes.Clear(); node.selectedMethodNames.Clear(); node.selectedMethodNamesByComponentTypeName.Clear(); }
    }
}
