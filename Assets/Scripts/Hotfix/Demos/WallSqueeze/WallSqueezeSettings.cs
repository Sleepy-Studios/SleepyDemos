using UnityEngine;

namespace Hotfix.WallSqueeze
{
    /// 玩法参数真源；场景引用保存的资产。
    [CreateAssetMenu(menuName = "SleepyDemos/夹爆它/参数")]
    public sealed class WallSqueezeSettings : ScriptableObject
    {
        [SerializeField] private Vector2 room = new Vector2(16, 9);
        [SerializeField] private float bodySize = .8f;
        [SerializeField] private float wallSpeed = 4;
        [SerializeField] private float monsterSpeed = .7f;
        [SerializeField] private float turnSeconds = 1;
        [SerializeField] private float minimumSize = .24f;
        [SerializeField] private float holdSeconds = .12f;
        [SerializeField] private float recoverySpeed = 4;
        [SerializeField] private float slipperSpeedMultiplier = 1.8f;

        /// 从左下角零点开始的房间有效尺寸。
        public Vector2 Room { get => room; internal set => room = value; }
        /// 方块未受压边长。
        public float BodySize { get => bodySize; internal set => bodySize = value; }
        /// 滑墙每秒最大位移。
        public float WallSpeed { get => wallSpeed; internal set => wallSpeed = value; }
        /// 普通红怪每秒游走速度。
        public float MonsterSpeed { get => monsterSpeed; internal set => monsterSpeed = value; }
        /// 主动换向间隔秒数。
        public float TurnSeconds { get => turnSeconds; internal set => turnSeconds = value; }
        /// 受压轴不再缩短的尺寸。
        public float MinimumSize { get => minimumSize; internal set => minimumSize = value; }
        /// 最小尺寸下持续夹持的死亡秒数。
        public float HoldSeconds { get => holdSeconds; internal set => holdSeconds = value; }
        /// 撤墙后每秒恢复尺寸速度。
        public float RecoverySpeed { get => recoverySpeed; internal set => recoverySpeed = value; }
        /// 钻缝怪相对普通怪的游走速度倍率。
        public float SlipperSpeedMultiplier { get => slipperSpeedMultiplier; internal set => slipperSpeedMultiplier = value; }
    }
}
