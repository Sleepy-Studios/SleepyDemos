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
        [SerializeField] private string stationId;
        [SerializeField] private Transform focusPose;
        [SerializeField, Range(30, 75)] private float focusFieldOfView = 48;
        [SerializeField] private JinxCasinoTableTarget[] targets = System.Array.Empty<JinxCasinoTableTarget>();

        /// 保存后不随机台类型或轮换内容改变的场景实例标识。
        public string StationId => stationId;
        /// 桌面聚焦时使用的相机位置及朝向。
        public Transform FocusPose => focusPose;
        /// 桌面视角的垂直视野。
        public float FocusFieldOfView => focusFieldOfView;
        /// 此机台明确装配的操作目标，不包含相邻机台。
        public System.Collections.Generic.IReadOnlyList<JinxCasinoTableTarget> Targets => targets;
        /// 原型未配置桌面时保持旧入口，样板须通过装配校验后启用。
        public bool HasTableInteraction => !string.IsNullOrWhiteSpace(stationId) && focusPose != null && targets.Length > 0;

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

        /// <summary>装配沉浸机台；实例ID由资源保存，不在运行时随机生成。</summary>
        /// <param name="id">场景唯一且存档稳定的机台ID。</param>
        /// <param name="pose">以位置及朝向定义桌面视角的子挂点。</param>
        /// <param name="fieldOfView">垂直视野角，限制30至75度。</param>
        /// <param name="operationTargets">此机台所有操作目标的保存顺序。</param>
        public void ConfigureTable(string id, Transform pose, float fieldOfView, JinxCasinoTableTarget[] operationTargets)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new System.ArgumentException("机台ID不能为空。", nameof(id));
            if (pose == null || !pose.IsChildOf(transform)) throw new System.ArgumentException("桌面挂点必须属于此机台。", nameof(pose));
            if (float.IsNaN(fieldOfView) || float.IsInfinity(fieldOfView)) throw new System.ArgumentOutOfRangeException(nameof(fieldOfView));
            if (operationTargets == null || operationTargets.Length == 0) throw new System.ArgumentException("机台必须有操作目标。", nameof(operationTargets));
            var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (var target in operationTargets)
                if (target == null || !target.transform.IsChildOf(transform) || string.IsNullOrWhiteSpace(target.TargetId) || !ids.Add(target.TargetId))
                    throw new System.ArgumentException("目标必须属于此机台且拥有唯一ID。", nameof(operationTargets));
            stationId = id; focusPose = pose; focusFieldOfView = Mathf.Clamp(fieldOfView, 30, 75);
            targets = (JinxCasinoTableTarget[])operationTargets.Clone();
        }
    }

}
