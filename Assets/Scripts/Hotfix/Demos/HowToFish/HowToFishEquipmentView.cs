using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 装备的配件外观和挂点；只有第一人称实例驱动局部动作，落地物理由物品根负责。
    public sealed class HowToFishEquipmentView : MonoBehaviour
    {
        [SerializeField] private Transform tip;
        [SerializeField] private Transform reloadPart;
        [SerializeField] private bool hingedReload;
        [SerializeField] private Light muzzleFlash;
        [SerializeField] private GameObject[] attachments = new GameObject[6];
        [SerializeField] private Transform[] sightPoints = new Transform[3];
        [SerializeField] private GameObject[] ironSightVisuals = new GameObject[0];
        [SerializeField] private Transform laserEmitter;
        [SerializeField] private LineRenderer laserBeam;
        [SerializeField] private float ironSightFov = 60;
        [SerializeField] private float redDotFov = 50;
        [SerializeField] private float scopeFov = 20;
        [SerializeField] private float compensatorRecoil = .65f;
        [SerializeField] private float suppressorRecoil = .3f;
        private Vector3 reloadRestPosition;
        private float flashUntil;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float kick;
        private float spinRemaining;
        private float aimBlend;
        private Transform activeSight;
        private HowToFishAttachment sight;
        private HowToFishAttachment barrel;
        private bool hasLaser;
        private float appliedCooking = -1;
        private string appliedSkin;
        private static MaterialPropertyBlock cookingBlock;
        private static readonly List<MeshRenderer> cookingRenderers = new List<MeshRenderer>();
        private static readonly List<Material> cookingMaterials = new List<Material>();

        /// <summary>使用服装模型的手臂材质更新现有持握模型，不修改共享材质或武器外观。</summary>
        /// <param name="outfit">已装配的服装模型；裸臂与短袖不显示袖口。</param>
        public void SetOutfit(HowToFishOutfitVisual outfit)
        {
            var hand = transform.Find("HandVisual");
            if (hand == null) return;
            if (outfit?.Prefab == null) throw new ArgumentException("服装模型未配置。", nameof(outfit));
            var source = outfit.Prefab.GetComponentsInChildren<MeshRenderer>(true);
            var sleeve = source.FirstOrDefault(value => value.name == "ForearmSleeveRight");
            bool longSleeve = sleeve != null;
            if (sleeve == null) sleeve = source.FirstOrDefault(value => value.name == "ForearmSkinRight");
            var palm = source.FirstOrDefault(value => value.name == "PalmRight");
            if (sleeve == null || palm == null) throw new InvalidOperationException("服装缺少右前臂或手掌材质。");
            foreach (var renderer in hand.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool cloth = renderer.name == "Sleeve" || renderer.name == "Cuff";
                renderer.sharedMaterial = cloth ? sleeve.sharedMaterial : palm.sharedMaterial;
                if (renderer.name == "Cuff") renderer.gameObject.SetActive(longSleeve);
            }
        }

        /// <summary>切换本件装备的外观，并在新材质上重新应用受热程度。</summary>
        /// <param name="skinId">已验证的类型与皮肤标识；空值使用默认材质。</param>
        public void SetSkin(string skinId)
        {
            skinId = skinId ?? "";
            if (appliedSkin == skinId) return;
            appliedSkin = skinId;
            GetComponent<HowToFishSkinView>()?.SetSkin(skinId);
            appliedCooking = -1;
        }

        /// 鱼竿尖或枪口的真实模型挂点。
        public Transform Tip => tip;
        /// 花式动作尚在进行时不能立即追加第二次。
        public bool IsSpinning => spinRemaining > 0;
        public float AimFieldOfView => sight == HowToFishAttachment.SniperScope ? scopeFov : sight == HowToFishAttachment.RedDotSight ? redDotFov : ironSightFov;
        public float RecoilMultiplier => barrel == HowToFishAttachment.Suppressor ? suppressorRecoil : barrel == HowToFishAttachment.Compensator ? compensatorRecoil : 1;
        public Vector3 CookingCenter => tip == null ? transform.position : Vector3.Lerp(transform.position, tip.position, .5f);

        /// <summary>应用保存的工具受热外观，不创建或改写共享材质。</summary>
        /// <param name="cooking">0至1的受热程度。</param>
        public void SetCooking(float cooking)
        {
            if (appliedCooking == cooking) return;
            appliedCooking = cooking;
            ApplyCookingTint(transform, cooking);
        }

        internal static void ApplyCookingTint(Transform root, float cooking, bool drip = false)
        {
            // 同步主线程调用复用缓冲，持续受热时不逐帧创建属性块或材质/渲染器数组。
            cookingBlock ??= new MaterialPropertyBlock();
            cookingBlock.Clear();
            root.GetComponentsInChildren(true, cookingRenderers);
            var hand = root.Find("HandVisual");
            foreach (var renderer in cookingRenderers)
            {
                if (hand != null && renderer.transform.IsChildOf(hand)) continue;
                renderer.GetSharedMaterials(cookingMaterials);
                for (int i = 0; i < cookingMaterials.Count; i++)
                {
                    var material = cookingMaterials[i];
                    if (material == null || !material.HasProperty("_BaseColor")) continue;
                    var color = material.GetColor("_BaseColor");
                    // Cod/Eel 对照支持浅白淡粉方向；统一 60% 混色是项目推定，不代表逐物种原作材质。
                    // 只调整基础色，保留纹理和部位色差，再叠加原有熟成与焦化曲线。
                    if (drip) color = Color.Lerp(color, new Color(1, .88f, .94f, color.a), .6f);
                    var cooked = Color.Lerp(color, new Color(.43f, .22f, .075f, color.a), Mathf.Clamp01(cooking * 2) * .65f);
                    renderer.GetPropertyBlock(cookingBlock, i);
                    cookingBlock.SetColor("_BaseColor", Color.Lerp(cooked, new Color(.025f, .018f, .012f, color.a), Mathf.Clamp01((cooking - .5f) * 2)));
                    renderer.SetPropertyBlock(cookingBlock, i);
                    cookingBlock.Clear();
                }
            }
            cookingRenderers.Clear();
            cookingMaterials.Clear();
        }

        private void Awake()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            if (reloadPart != null) reloadRestPosition = reloadPart.localPosition;
        }

        /// 触发挥动或后坐力。
        public void Strike() { kick = RecoilMultiplier; flashUntil = Time.time + .06f; }

        /// <summary>应用当前武器的配件外观和瞄准挂点。</summary>
        /// <param name="owned">该武器的独立持久状态。</param>
        public void SetAttachments(HowToFishOwnedItem owned)
        {
            sight = owned.sight; barrel = owned.barrel; hasLaser = owned.hasLaser;
            for (int i = 0; i < attachments.Length; i++)
                if (attachments[i] != null) attachments[i].SetActive(owned.HasAttachment((HowToFishAttachment)(i + 1)));
            foreach (var visual in ironSightVisuals) if (visual != null) visual.SetActive(sight == HowToFishAttachment.None);
            activeSight = (int)sight < sightPoints.Length ? sightPoints[(int)sight] : null;
            if (laserBeam != null) laserBeam.enabled = hasLaser;
        }

        /// <summary>以实际射击中心线投射激光，终点停在最近遮挡表面。</summary>
        /// <param name="eye">射击相机。</param>
        /// <param name="range">当前武器射程。</param>
        public void UpdateLaser(Camera eye, float range)
        {
            if (!hasLaser || laserEmitter == null || laserBeam == null) return;
            var direction = eye.transform.forward;
            var end = Physics.Raycast(eye.transform.position, direction, out var hit, range, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point : eye.transform.position + direction * range;
            laserBeam.SetPosition(0, laserEmitter.position);
            laserBeam.SetPosition(1, end);
        }

        /// 转动手持装备，提供可见花式动作。
        public void Spin() { if (spinRemaining <= 0) spinRemaining = 0.7f; }

        /// <summary>根据移动速度推进局部装备姿态。</summary>
        /// <param name="movement">归一化移动强度。</param>
        /// <param name="reloadProgress">换弹归一化进度；零表示没有换弹。</param>
        /// <param name="aiming">是否将瞄准挂点对齐相机中心。</param>
        public void Animate(float movement, float reloadProgress = 0, bool aiming = false)
        {
            kick = Mathf.MoveTowards(kick, 0, Time.deltaTime * 4);
            spinRemaining = Mathf.Max(0, spinRemaining - Time.deltaTime);
            var bob = new Vector3(Mathf.Sin(Time.time * 7) * 0.012f, Mathf.Cos(Time.time * 14) * 0.008f, 0) * movement;
            float reload = Mathf.Sin(reloadProgress * Mathf.PI);
            if (reloadPart != null)
            {
                if (hingedReload) reloadPart.localRotation = Quaternion.Euler(reload * 35, 0, 0);
                else reloadPart.localPosition = reloadRestPosition + Vector3.down * reload * .12f;
            }
            if (muzzleFlash != null) muzzleFlash.enabled = barrel != HowToFishAttachment.Suppressor && Time.time < flashUntil;
            aimBlend = Mathf.MoveTowards(aimBlend, aiming && activeSight != null ? 1 : 0, Time.deltaTime * 8);
            var aimPosition = restPosition;
            if (activeSight != null)
            {
                var local = transform.InverseTransformPoint(activeSight.position);
                aimPosition = new Vector3(-local.x, -local.y, restPosition.z - local.z);
            }
            transform.localPosition = Vector3.Lerp(restPosition, aimPosition, aimBlend) + bob * (1 - aimBlend * .85f) +
                new Vector3(0, -kick * 0.08f - reload * .15f, -kick * 0.08f);
            transform.localRotation = Quaternion.Slerp(restRotation, Quaternion.identity, aimBlend) * Quaternion.Euler(kick * 28 + reload * 35, -kick * 20,
                spinRemaining > 0 ? (1 - spinRemaining / 0.7f) * 360 : 0);
        }
    }
}
