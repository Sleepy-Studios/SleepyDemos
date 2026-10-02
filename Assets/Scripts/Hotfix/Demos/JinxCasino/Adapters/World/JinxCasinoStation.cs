using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 保存的机台交互锚点；游戏结果不由碰撞或机台动画决定。
    public sealed class JinxCasinoStation : MonoBehaviour
    {
        [SerializeField] private CasinoGameKind game;
        [SerializeField, Range(0, 3)] private int areaIndex;
        [SerializeField] private Transform interactionAnchor;

        /// 此机台提供的规则类型。
        public CasinoGameKind Game => game;
        /// 所属区域，使用稳定索引0到3。
        public int AreaIndex => areaIndex;
        /// 玩家接近时提示和交互的位置。
        public Vector3 InteractionPosition => interactionAnchor != null ? interactionAnchor.position : transform.position;

        /// <summary>装配固定机台及其可交互锚点。</summary>
        /// <param name="kind">小游戏类型。</param>
        /// <param name="area">所属区域。</param>
        /// <param name="anchor">保存的交互锚点。</param>
        public void Configure(CasinoGameKind kind, int area, Transform anchor)
        {
            game = kind; areaIndex = area; interactionAnchor = anchor;
        }
    }

}
