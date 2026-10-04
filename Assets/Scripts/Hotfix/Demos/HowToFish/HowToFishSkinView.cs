using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 自制程序外观；保留原材质供默认恢复，不覆盖手部、瞄具和购买配件。
    public sealed class HowToFishSkinView : MonoBehaviour
    {
        [SerializeField] private Shader skinShader;
        private readonly Dictionary<MeshRenderer, Material[]> originals = new Dictionary<MeshRenderer, Material[]>();
        private readonly List<Material> instances = new List<Material>();
        private string appliedSkin;

        /// <summary>切换实例外观；拥有者随后重新应用当前烹饪颜色。</summary>
        /// <param name="skinId">目录中的完整外观 ID；空字符串恢复默认。</param>
        public void SetSkin(string skinId)
        {
            var definition = string.IsNullOrEmpty(skinId) ? null : HowToFishSkinCatalog.Find(skinId);
            if (!string.IsNullOrEmpty(skinId) && definition == null) throw new ArgumentException("未知外观：" + skinId, nameof(skinId));
            if (definition?.Name == "Default") definition = null;
            skinId = definition == null ? null : skinId;
            if (appliedSkin == skinId) return;
            if (definition != null && skinShader == null) throw new InvalidOperationException("外观组件缺少序列化 Shader 引用。");
            if (originals.Count == 0)
                foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
                    if (!IsDecoration(renderer.transform)) originals.Add(renderer, renderer.sharedMaterials);
            Restore();
            appliedSkin = skinId;
            if (definition == null) return;
            var style = Style(definition.Name);
            foreach (var pair in originals)
            {
                if (pair.Key == null) continue;
                var materials = (Material[])pair.Value.Clone();
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null || original.renderQueue >= 2500 || !original.HasProperty("_BaseColor")) continue;
                    var material = new Material(skinShader) { name = original.name + " [" + skinId + "]" };
                    material.SetColor("_BaseColor", Color.white);
                    material.SetColor("_ColorA", Tone(style.a));
                    material.SetColor("_ColorB", Tone(style.b));
                    material.SetFloat("_Pattern", style.pattern);
                    material.SetFloat("_PatternScale", definition.ItemId == "Boat" ? 3 : 16);
                    material.SetFloat("_Metallic", style.metallic);
                    material.SetFloat("_Smoothness", style.smoothness);
                    material.SetFloat("_Rainbow", definition.Effect == HowToFishSkinEffect.Rainbow ? 1 : 0);
                    instances.Add(material);
                    materials[i] = material;
                }
                pair.Key.sharedMaterials = materials;
                ResetTint(pair.Key, materials);
            }
        }

        private bool IsDecoration(Transform node)
        {
            for (; node != null; node = node.parent)
            {
                if (node.name == "HandVisual" || node.name == "Attachments" || node.name.StartsWith("Attachment_", StringComparison.Ordinal) ||
                    node.name == "FrontSight" || node.name.StartsWith("RearSight", StringComparison.Ordinal)) return true;
                if (node == transform) break;
            }
            return false;
        }

        private static void ResetTint(MeshRenderer renderer, Material[] materials)
        {
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || materials[i].renderQueue >= 2500 || !materials[i].HasProperty("_BaseColor")) continue;
                renderer.GetPropertyBlock(block, i);
                block.SetColor("_BaseColor", materials[i].GetColor("_BaseColor"));
                renderer.SetPropertyBlock(block, i);
                block.Clear();
            }
        }

        private void Restore()
        {
            foreach (var pair in originals)
                if (pair.Key != null) { pair.Key.sharedMaterials = pair.Value; ResetTint(pair.Key, pair.Value); }
            foreach (var material in instances)
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            instances.Clear();
        }

        private void OnDestroy() { Restore(); }

        private static Color Tone(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        // 图案与配色为自制候选，动画只由目录 Effect 决定，不能由名称猜测。
        private static (int a, int b, int pattern, float metallic, float smoothness) Style(string name) => name switch
        {
            "Wood" => (0x8C5228, 0x30190B, 1, 0f, .28f),
            "Redwood" => (0xAA4532, 0x381610, 1, 0f, .3f),
            "Chess" or "Black and White" => (0xEEEDE6, 0x151821, 2, .05f, .4f),
            "Missing" => (0xE93EEF, 0x151219, 2, 0f, .3f),
            "Hazard" => (0xEDBD16, 0x202020, 3, .1f, .4f),
            "GoldStriped" => (0xDCA62A, 0x27221A, 3, .8f, .7f),
            "Tiger" => (0xE39134, 0x242020, 4, .05f, .4f),
            "Cow" => (0xF1ECE2, 0x191B21, 5, 0f, .35f),
            "Polka" => (0x153344, 0xF5D888, 6, .05f, .4f),
            "Camouflage" => (0x9A9B62, 0x293E2C, 7, 0f, .28f),
            "Galaxy" => (0x192650, 0xA05EE0, 8, .25f, .75f),
            "Emerald" => (0x1C9D67, 0x064331, 9, .5f, .85f),
            "Ruby" => (0xD62759, 0x500D28, 9, .5f, .85f),
            "Diamond" => (0xDCFAFF, 0x568BA9, 9, .65f, .9f),
            "Inverted Diamond" => (0x172B49, 0xA4D9F3, 9, .65f, .9f),
            "Blue Fade" => (0x12407B, 0xB9F0FF, 10, .1f, .55f),
            "Sponge" => (0xEDD249, 0x9D7133, 11, 0f, .2f),
            "Blaze" or "Fire" => (0xF3A22C, 0x8D2118, 12, .1f, .55f),
            "Bluze" => (0x63CEEF, 0x153EAD, 12, .1f, .55f),
            "Asiimov" => (0xE9ECE8, 0xF07821, 13, .2f, .55f),
            "Winter" => (0xD6E8ED, 0x7B99A6, 7, .05f, .4f),
            "Rainbow" => (0xFFFFFF, 0xD2EEFF, 10, .3f, .7f),
            "Gold" => (0xEABC48, 0xEABC48, 0, .9f, .8f),
            "Bronze" => (0xAD713C, 0xAD713C, 0, .85f, .6f),
            "Pink" => (0xE988BA, 0xE988BA, 0, .1f, .5f),
            "Red" => (0xCC353E, 0xCC353E, 0, .1f, .5f),
            "Orange" => (0xE17D27, 0xE17D27, 0, .1f, .5f),
            "Blue" => (0x3873C8, 0x3873C8, 0, .1f, .5f),
            "Purple" => (0x8D5CCC, 0x8D5CCC, 0, .1f, .5f),
            "Brown" => (0x825738, 0x825738, 0, .05f, .35f),
            "Greyscale" => (0xDBDBDB, 0x555555, 13, .1f, .45f),
            "Light Gray" => (0xB8C0C4, 0xB8C0C4, 0, .1f, .45f),
            "White" => (0xF0EFE7, 0xF0EFE7, 0, .1f, .45f),
            _ => throw new InvalidOperationException("缺少自制外观配色：" + name)
        };
    }
}
