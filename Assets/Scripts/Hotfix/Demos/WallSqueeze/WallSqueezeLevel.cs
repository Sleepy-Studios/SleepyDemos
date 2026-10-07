using System;
using UnityEngine;

namespace Hotfix.WallSqueeze
{
    /// 单轴滑墙的保存布局，Axis 0 为纵墙，1 为横墙。
    [Serializable]
    public struct WallSqueezeWallLayout
    {
        /// 允许移动轴：0 为 X，1 为 Y。
        public int Axis;
        /// 初始中心。
        public Vector2 Center;
        /// 保存的墙碰撞矩形尺寸。
        public Vector2 Size;
        /// 允许轴的最小和最大中心坐标。
        public Vector2 Track;
        /// 固定墙不接受输入，位置和尺寸仍参与相同碰撞约束。
        public bool IsFixed;
    }

    /// 怪物行为类型；护甲仅可沿 Y 压碎，薄怪可穿较窄的水平通道。
    public enum WallSqueezeMonsterType
    {
        Normal,
        Armored,
        Slipper
    }

    /// 保存的特殊怪类型与初始位置。
    [Serializable]
    public struct WallSqueezeMonsterLayout
    {
        /// 怪物类型。
        public WallSqueezeMonsterType Type;
        /// 初始世界中心。
        public Vector2 Center;
    }

    /// 一个可复现关卡的初始对象与随机种子。
    [CreateAssetMenu(menuName = "SleepyDemos/夹爆它/关卡")]
    public sealed class WallSqueezeLevel : ScriptableObject
    {
        [SerializeField] private string title;
        [SerializeField] private int seed = 17;
        [SerializeField] private WallSqueezeWallLayout[] walls;
        [SerializeField] private Vector2[] monsters;
        [SerializeField] private Vector2[] residents;
        [SerializeField] private WallSqueezeMonsterLayout[] specialMonsters = Array.Empty<WallSqueezeMonsterLayout>();
        [SerializeField] private WallSqueezeWallLayout[] fixedWalls = Array.Empty<WallSqueezeWallLayout>();
        [SerializeField] private float timeLimit;

        /// 当前关卡的 HUD 标题。
        public string Title { get => title; internal set => title = value; }
        /// 重试时恢复的游走随机种子。
        public int Seed { get => seed; internal set => seed = value; }
        /// 保存的初始滑墙。
        public WallSqueezeWallLayout[] Walls { get => walls; internal set => walls = value; }
        /// 红怪初始中心。
        public Vector2[] Monsters { get => monsters; internal set => monsters = value; }
        /// 蓝色住户初始中心。
        public Vector2[] Residents { get => residents; internal set => residents = value; }
        /// 额外特殊怪，不改变普通怪数组的含义。
        public WallSqueezeMonsterLayout[] SpecialMonsters { get => specialMonsters; internal set => specialMonsters = value; }
        /// 固定障碍，构造规则时追加到滑墙之后。
        public WallSqueezeWallLayout[] FixedWalls { get => fixedWalls; internal set => fixedWalls = value; }
        /// 规则限时秒数，0 表示不限时。
        public float TimeLimit { get => timeLimit; internal set => timeLimit = value; }
    }
}
