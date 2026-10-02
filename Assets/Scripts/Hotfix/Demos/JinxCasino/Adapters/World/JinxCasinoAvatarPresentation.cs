using System;
using System.Collections.Generic;
using Hotfix.JinxCasino.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hotfix.JinxCasino.Adapters
{
    [Serializable]
    public sealed class CasinoAvatarColorBinding { public string Id; public Material Material; }
    [Serializable]
    public sealed class CasinoAvatarModelBinding { public string Id; public GameObject Model; }

    /// 只驱动保存的模型关节和装扮；角色根、碰撞体与第一人称相机仍归宿主管理。
    public sealed class JinxCasinoAvatarPresentation : MonoBehaviour
    {
        [SerializeField] private JinxCasinoController owner;
        [SerializeField] private Transform actor;
        [SerializeField] private Transform visual;
        [SerializeField] private JinxCasinoSceneEffects sceneEffects;
        [SerializeField] private bool localAvatar;
        [SerializeField] private CasinoAvatarColorBinding[] colors = Array.Empty<CasinoAvatarColorBinding>();
        [SerializeField] private CasinoAvatarModelBinding[] hats = Array.Empty<CasinoAvatarModelBinding>();
        [SerializeField] private CasinoAvatarModelBinding[] faces = Array.Empty<CasinoAvatarModelBinding>();
        private readonly List<ClothingSlots> clothing = new List<ClothingSlots>();
        private readonly HashSet<string> unlockedEmotes = new HashSet<string>(StringComparer.Ordinal);
        private Joint body, head, leftArm, rightArm;
        private Transform capturedVisual;
        private string requestedColor = "color_blue", requestedHat = "hat_none", requestedEmote = "emote_wave";
        private string activeEmote;
        private float emoteStarted;
        private bool appearancePending;
        private bool bound;
        public string CurrentColor { get; private set; }
        public string CurrentHat { get; private set; }
        public string CurrentEmote => requestedEmote;
        public Transform ActorRoot => actor;
        public bool IsPlayingEmote => activeEmote != null;

        /// <summary>绑定编辑器保存的关节、材质与装扮实例；运行时不生成模型。</summary>
        /// <param name="controller">只读取已成功持久化的成长配置。</param>
        /// <param name="actorRoot">旧玩家或助手根，不能在本组件内改变位置。</param>
        /// <param name="visualRoot">位于效果视觉根内的正式Avatar模型。</param>
        /// <param name="effects">临时泼墨/换装的材质所有者。</param>
        /// <param name="isLocal">本地角色头部、面具与帽子仅投影。</param>
        /// <param name="colorBindings">五种共享URP配色材质。</param>
        /// <param name="hatBindings">五件实体帽实例；hat_none不创建占位模型。</param>
        /// <param name="faceBindings">八张保存的表情纸面具实例。</param>
        public void Setup(JinxCasinoController controller, Transform actorRoot, Transform visualRoot,
            JinxCasinoSceneEffects effects, bool isLocal, CasinoAvatarColorBinding[] colorBindings,
            CasinoAvatarModelBinding[] hatBindings, CasinoAvatarModelBinding[] faceBindings)
        {
            Unbind(); ResetJoints(); owner = controller; actor = actorRoot; visual = visualRoot; sceneEffects = effects; localAvatar = isLocal;
            colors = colorBindings ?? Array.Empty<CasinoAvatarColorBinding>();
            hats = hatBindings ?? Array.Empty<CasinoAvatarModelBinding>(); faces = faceBindings ?? Array.Empty<CasinoAvatarModelBinding>();
            if (Application.isPlaying && isActiveAndEnabled) Bind();
        }

        /// <summary>播放一种已解锁的纸片角色动作；动画不会写入小游戏或钱包。</summary>
        /// <param name="id">emote_wave/clap/shrug/dance/salute/bow/crown/fireworks稳定ID。</param>
        /// <returns>已绑定且表情已解锁时为true。</returns>
        public bool PlayEmote(string id)
        {
            if (!bound || string.IsNullOrEmpty(id) || !unlockedEmotes.Contains(id)) return false;
            activeEmote = id; emoteStarted = Time.unscaledTime;
            SelectModel(faces, id); return true;
        }

        private void OnEnable() { if (Application.isPlaying) Bind(); }
        private void OnDisable() { Unbind(); ResetJoints(); activeEmote = null; }
        private void Bind()
        {
            if (bound || owner == null || visual == null) return;
            if (capturedVisual != visual)
            {
                body = Capture("Avatar.Body"); head = Capture("Avatar.Head");
                leftArm = Capture("Avatar.ArmLeftPivot"); rightArm = Capture("Avatar.ArmRightPivot");
                clothing.Clear();
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials; var slots = new List<int>();
                    for (int index = 0; index < materials.Length; index++)
                        if (materials[index] != null && materials[index].name.StartsWith("color_", StringComparison.Ordinal)) slots.Add(index);
                    if (slots.Count > 0) clothing.Add(new ClothingSlots { Renderer = renderer, Indices = slots.ToArray() });
                }
                capturedVisual = visual;
            }
            if (localAvatar && head.Transform != null)
                foreach (var renderer in head.Transform.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            bound = true; owner.Changed += RefreshProfile; RefreshProfile();
        }

        private void Unbind() { if (bound && owner != null) owner.Changed -= RefreshProfile; bound = false; }
        private void RefreshProfile()
        {
            // ProfileData是深副本，只在绑定或Changed时读取；每帧运动使用缓存。
            var data = owner.Game.ProfileData;
            if (data == null) return;
            unlockedEmotes.Clear();
            foreach (var id in data.UnlockedIds) if (id.StartsWith("emote_", StringComparison.Ordinal)) unlockedEmotes.Add(id);
            bool changed = requestedColor != data.EquippedColor || requestedHat != data.EquippedHat || requestedEmote != data.EquippedEmote;
            requestedColor = data.EquippedColor; requestedHat = data.EquippedHat; requestedEmote = data.EquippedEmote;
            appearancePending |= changed || CurrentColor == null;
        }

        private void LateUpdate()
        {
            if (!bound) return;
            // 等效果所有者完整Finish后再换色，避免旧Original材质随后覆盖刚提交的新配色。
            if (appearancePending && (sceneEffects == null || !sceneEffects.IsCostumeOverridden)) ApplyAppearance();
            ResetJoints();
            float now = Time.unscaledTime;
            body.Rotate(new Vector3(0, 0, Mathf.Sin(now * 1.7f) * 1.5f));
            head.Rotate(new Vector3(Mathf.Sin(now * 1.3f) * 1.3f, 0, 0));
            if (activeEmote == null) return;
            float elapsed = now - emoteStarted;
            if (elapsed >= 2.4f) { activeEmote = null; SelectModel(faces, requestedEmote); return; }
            float envelope = Mathf.Min(Mathf.Clamp01(elapsed / 0.25f), Mathf.Clamp01((2.4f - elapsed) / 0.35f));
            float beat = Mathf.Sin(elapsed * 10);
            switch (activeEmote)
            {
                case "emote_wave": rightArm.Rotate(new Vector3(0, beat * 16, 115 + beat * 12) * envelope); break;
                case "emote_clap": leftArm.Rotate(new Vector3(-65, 0, 35 + beat * 15) * envelope); rightArm.Rotate(new Vector3(-65, 0, -35 - beat * 15) * envelope); break;
                case "emote_shrug": leftArm.Rotate(new Vector3(0, -40, -75) * envelope); rightArm.Rotate(new Vector3(0, 40, 75) * envelope); head.Rotate(new Vector3(0, 0, 15) * envelope); break;
                case "emote_dance": body.Rotate(new Vector3(0, beat * 12, beat * 10) * envelope); leftArm.Rotate(new Vector3(-35, 0, -70 + beat * 22) * envelope); rightArm.Rotate(new Vector3(-35, 0, 70 + beat * 22) * envelope); break;
                case "emote_salute": rightArm.Rotate(new Vector3(-35, -35, 155) * envelope); head.Rotate(new Vector3(-8, 0, 0) * envelope); break;
                case "emote_bow": body.Rotate(new Vector3(38, 0, 0) * envelope); head.Rotate(new Vector3(12, 0, 0) * envelope); break;
                case "emote_crown": leftArm.Rotate(new Vector3(0, 0, -145) * envelope); rightArm.Rotate(new Vector3(0, 0, 145) * envelope); head.Rotate(new Vector3(-12, beat * 8, 0) * envelope); break;
                case "emote_fireworks": leftArm.Rotate(new Vector3(beat * 20, 0, -150 + beat * 20) * envelope); rightArm.Rotate(new Vector3(-beat * 20, 0, 150 - beat * 20) * envelope); body.Rotate(new Vector3(0, beat * 8, 0) * envelope); break;
            }
        }

        private void ApplyAppearance()
        {
            Material material = null;
            foreach (var binding in colors) if (binding != null && binding.Id == requestedColor) { material = binding.Material; break; }
            if (material == null) return;
            foreach (var entry in clothing)
            {
                if (entry.Renderer == null) continue;
                var materials = entry.Renderer.sharedMaterials;
                foreach (int index in entry.Indices) if (index < materials.Length) materials[index] = material;
                entry.Renderer.sharedMaterials = materials;
            }
            SelectModel(hats, requestedHat); SelectModel(faces, activeEmote ?? requestedEmote);
            CurrentColor = requestedColor; CurrentHat = requestedHat; appearancePending = false;
        }
        private static void SelectModel(CasinoAvatarModelBinding[] bindings, string id)
        { foreach (var entry in bindings) if (entry != null && entry.Model != null) entry.Model.SetActive(entry.Id == id); }
        private Joint Capture(string name)
        {
            foreach (var child in visual.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return new Joint { Transform = child, Rotation = child.localRotation };
            return new Joint();
        }
        private void ResetJoints() { body.Reset(); head.Reset(); leftArm.Reset(); rightArm.Reset(); }
        private struct Joint
        {
            public Transform Transform; public Quaternion Rotation;
            public void Reset() { if (Transform != null) Transform.localRotation = Rotation; }
            public void Rotate(Vector3 euler) { if (Transform != null) Transform.localRotation = Rotation * Quaternion.Euler(euler); }
        }
        private sealed class ClothingSlots { public Renderer Renderer; public int[] Indices; }
    }
}
