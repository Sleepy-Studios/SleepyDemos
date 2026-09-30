using System;
using UnityEngine;

namespace Hotfix.BlockPorters
{
    [Serializable]
    public sealed class PorterQueueDefinition
    {
        [SerializeField] private PorterTeamDefinition[] teams = Array.Empty<PorterTeamDefinition>();
        public PorterTeamDefinition[] Teams => teams;
        public PorterQueueDefinition(PorterTeamDefinition[] definitions) { teams = definitions; }
    }

    /// 可编辑的关卡资产；运行时会话不改写资产中的队列和网格。
    [CreateAssetMenu(fileName = "Level", menuName = "SleepyDemos/BlockPorters/关卡")]
    public sealed class BlockPortersLevel : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField, Range(1, 32)] private int width;
        [SerializeField, Range(1, 32)] private int height;
        [SerializeField] private int[] cells;
        [SerializeField] private Color[] palette;
        [SerializeField] private PorterQueueDefinition[] columns;
        [SerializeField, Range(1, 5)] private int capacity = 5;
        [SerializeField] private int[] solution;

        public string DisplayName => displayName;
        public Color[] Palette => palette;
        public int[] Solution => solution;

        /// 返回独立且经过数量、颜色和尺寸检查的规则数据。
        public BlockPortersLevelData CreateData()
        {
            if (palette == null || columns == null || columns.Length != 4)
                throw new InvalidOperationException("关卡缺少颜色表或四列队伍。");
            var queues = new PorterTeamDefinition[4][];
            for (int i = 0; i < 4; i++) queues[i] = columns[i]?.Teams;
            return new BlockPortersLevelData(width, height, cells, queues, capacity, palette.Length);
        }

        /// <summary>由编辑器装配工具初始化关卡，校验后保存至资产。</summary>
        /// <param name="title">关卡显示名。</param>
        /// <param name="data">校验通过的关卡定义。</param>
        /// <param name="colors">与规则颜色编号一致的色表。</param>
        /// <param name="referenceSolution">按顺序点击的列号，必须无需复活即可通关。</param>
        public void Configure(string title, BlockPortersLevelData data, Color[] colors, int[] referenceSolution)
        {
            displayName = title; width = data.Width; height = data.Height;
            cells = (int[])data.Cells.Clone(); palette = (Color[])colors.Clone(); capacity = data.Capacity;
            columns = new PorterQueueDefinition[4];
            for (int i = 0; i < 4; i++) columns[i] = new PorterQueueDefinition((PorterTeamDefinition[])data.Columns[i].Clone());
            solution = (int[])referenceSolution.Clone();
            CreateData();
        }
    }
}
