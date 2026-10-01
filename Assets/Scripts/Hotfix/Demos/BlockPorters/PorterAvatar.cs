using UnityEngine;

namespace Hotfix.BlockPorters
{
    /// 仅保存角色骨架引用；所有角色由场景控制器统一更新。
    public sealed class PorterAvatar : MonoBehaviour
    {
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform carryAnchor;
        [SerializeField] private Renderer[] coloredParts;
        [SerializeField] private Renderer contactShadow;
        public Transform CarryAnchor => carryAnchor;

        /// <summary>编辑器装配角色，运行时不搜索子节点。</summary>
        /// <param name="arms">左右手臂枢轴。</param>
        /// <param name="legs">左右腿枢轴。</param>
        /// <param name="anchor">头顶的方块挂点。</param>
        /// <param name="parts">需要随队伍变色的共享材质渲染器。</param>
        public void Configure(Transform[] arms, Transform[] legs, Transform anchor, Renderer[] parts)
        {
            leftArm = arms[0]; rightArm = arms[1]; leftLeg = legs[0]; rightLeg = legs[1];
            carryAnchor = anchor; coloredParts = parts;
        }

        /// <summary>重置复用角色的颜色和动画姿态。</summary>
        /// <param name="material">队伍颜色的共享材质，不创建实例材质。</param>
        public void ResetPose(Material material)
        {
            foreach (var part in coloredParts) part.sharedMaterial = material;
            transform.localScale = Vector3.one;
            SetGrounded(true);
            Animate(0, false, false);
        }

        /// <summary>起跳时隐藏接触投影，避免投影跟随角色悬浮。</summary>
        /// <param name="grounded">角色是否仍在地面搬运。</param>
        public void SetGrounded(bool grounded) { if (contactShadow != null) contactShadow.enabled = grounded; }

        /// <summary>集中驱动行走摆臂和举砖姿态。</summary>
        /// <param name="phase">步伐相位。</param>
        /// <param name="walking">是否摆动腿部。</param>
        /// <param name="carrying">举砖时双臂抬向头顶。</param>
        public void Animate(float phase, bool walking, bool carrying)
        {
            float swing = walking ? Mathf.Sin(phase) * 30 : 0;
            leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            leftArm.localRotation = Quaternion.Euler(carrying ? -160 : -swing, 0, carrying ? -12 : 0);
            rightArm.localRotation = Quaternion.Euler(carrying ? -160 : swing, 0, carrying ? 12 : 0);
        }
    }
}
