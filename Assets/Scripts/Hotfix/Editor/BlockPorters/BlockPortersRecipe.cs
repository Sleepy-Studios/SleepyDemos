using System;
using Hotfix.BlockPorters;
using UnityEngine;

namespace Hotfix.Editor.BlockPorters
{
    public enum PorterDifficulty { Easy, Normal, Hard }
    public enum PorterBrush { Paint, Fill, Pick, Lock, Erase }

    [Serializable]
    public sealed class PorterRecipeSettings
    {
        [InspectorName("裁剪范围（归一化）")] public Rect Crop = new(0, 0, 1, 1);
        [InspectorName("棋盘宽度")] public int Width = 24;
        [InspectorName("棋盘高度")] public int Height = 24;
        [InspectorName("保持宽高比")] public bool KeepAspect = true;
        [InspectorName("透明度阈值")] public float AlphaThreshold = .1f;
        [InspectorName("剔除指定背景色")] public bool RemoveBackground;
        [InspectorName("背景色")] public Color Background = Color.white;
        [InspectorName("背景色差阈值")] public float BackgroundDelta = 10;
        [InspectorName("色数预算")] public int ColorBudget = 8;
        [InspectorName("近色合并强度")] public float MergeStrength = 1;
        [InspectorName("最小色差 ΔE76")] public float MinDelta = 10;
        [InspectorName("额外改色比例上限")] public float RecolorFraction = .1f;
        [InspectorName("近色辅助组数")] public int NearGroups = 1;
        [InspectorName("目标策略难度")] public PorterDifficulty Difficulty = PorterDifficulty.Normal;
        [InspectorName("随机种子")] public int Seed = 2026;
        [InspectorName("候选数量（最多24）")] public int Candidates = 24;
        [InspectorName("每种策略回放（最多100）")] public int PolicyRuns = 100;
        [InspectorName("搜索状态上限")] public int MaxStates = 20000;
        [InspectorName("每候选搜索秒数")] public double SearchSeconds = 5;
    }

    [CreateAssetMenu(fileName = "Recipe", menuName = "SleepyDemos/BlockPorters/编辑配方")]
    public sealed class BlockPortersRecipe : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Texture2D source;
        [SerializeField] private PorterRecipeSettings settings = new();
        [SerializeField] private int[] pixelCells = Array.Empty<int>();
        [SerializeField] private Color[] pixelPalette = Array.Empty<Color>();
        [SerializeField] private int[] cells = Array.Empty<int>();
        [SerializeField] private Color[] palette = Array.Empty<Color>();
        [SerializeField] private string[] labels = Array.Empty<string>();
        [SerializeField] private bool[] locked = Array.Empty<bool>();
        [SerializeField] private PorterQueueDefinition[] columns = Array.Empty<PorterQueueDefinition>();
        [SerializeField] private BlockPortersLevel exportedLevel;
        [SerializeField] private int revision;
        [SerializeField] private int convertedWidth;
        [SerializeField] private int convertedHeight;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Texture2D Source => source;
        public PorterRecipeSettings Settings => settings;
        public int[] PixelCells => pixelCells;
        public Color[] PixelPalette => pixelPalette;
        public int[] Cells => cells;
        public Color[] Palette => palette;
        public string[] Labels => labels;
        public bool[] Locked => locked;
        public PorterQueueDefinition[] Columns => columns;
        public BlockPortersLevel ExportedLevel => exportedLevel;
        public int Revision => revision;

        /// 所有人工修改都推进版本，后台旧结果和参考解不可继续使用。
        public void Invalidate() => revision++;

        /// <summary>更换源图片并使已有分析失效；转换由独立按钮触发。</summary>
        /// <param name="texture">项目内保存的原图，不要求 TextureImporter readable。</param>
        public void SetSource(Texture2D texture) { source = texture; Invalidate(); }

        /// <summary>保存像素化和难度处理结果，重置锁定区和队列。</summary>
        /// <param name="result">已经完成的图片转换结果。</param>
        public void SetImage(PorterImageResult result)
        {
            convertedWidth = settings.Width; convertedHeight = settings.Height;
            pixelCells = result.PixelCells; pixelPalette = result.PixelPalette;
            cells = result.Cells; palette = result.Palette;
            labels = new string[palette.Length]; locked = new bool[cells.Length]; columns = Array.Empty<PorterQueueDefinition>(); Invalidate();
        }

        /// <summary>写入四列队伍，不改动手工修正过的图案。</summary>
        /// <param name="queues">四个完整列，人数由规则构造器校验。</param>
        public void SetQueues(PorterTeamDefinition[][] queues)
        {
            columns = new PorterQueueDefinition[4];
            for (int i = 0; i < 4; i++) columns[i] = new PorterQueueDefinition(queues[i]);
            Invalidate();
        }

        /// <summary>手工合并色号，更新网格并清除旧队列；锁定区域拒绝受影响合并。</summary>
        /// <param name="from">要移除的色号。</param>
        /// <param name="to">保留色号。</param>
        public void Merge(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= palette.Length || to >= palette.Length) return;
            for (int i = 0; i < cells.Length; i++) if (locked[i] && cells[i] == from) throw new InvalidOperationException("合并影响锁定区域。");
            for (int i = 0; i < cells.Length; i++) { if (cells[i] == from) cells[i] = to; if (cells[i] > from) cells[i]--; }
            var colors = new System.Collections.Generic.List<Color>(palette); colors.RemoveAt(from); palette = colors.ToArray();
            var names = new System.Collections.Generic.List<string>(labels); names.RemoveAt(from); labels = names.ToArray();
            columns = Array.Empty<PorterQueueDefinition>(); Invalidate();
        }

        /// <summary>新增手工色号；后续画笔可拆色，最多十二色。</summary>
        /// <param name="color">新色号的显示颜色。</param>
        public void AddColor(Color color)
        {
            if (palette.Length >= 12) throw new InvalidOperationException("最多十二色。");
            Array.Resize(ref palette, palette.Length + 1); palette[^1] = color;
            Array.Resize(ref labels, palette.Length); Invalidate();
        }

        /// <summary>修改单格或连通区域；锁定区拒绝画笔/填充/擦除，拾色由窗口处理。</summary>
        /// <param name="index">当前网格索引。</param>
        /// <param name="color">Paint/Fill 的色号，Erase 忽略此参数。</param>
        /// <param name="brush">Paint、Fill、Lock 或 Erase。</param>
        public bool EditCell(int index, int color, PorterBrush brush)
        {
            if (index < 0 || index >= cells.Length || brush == PorterBrush.Pick) return false;
            if (brush == PorterBrush.Lock) { locked[index] = !locked[index]; Invalidate(); return true; }
            if (locked[index]) return false;
            color = brush == PorterBrush.Erase ? -1 : color;
            if (color < -1 || color >= palette.Length) throw new ArgumentException("色号非法");
            int original = cells[index]; if (original == color) return false;
            if (brush != PorterBrush.Fill) cells[index] = color;
            else
            {
                var queue = new System.Collections.Generic.Queue<int>(); queue.Enqueue(index);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    if (locked[i] || cells[i] != original) continue;
                    cells[i] = color; int x = i % convertedWidth, y = i / convertedWidth;
                    if (x > 0) queue.Enqueue(i - 1); if (x < convertedWidth - 1) queue.Enqueue(i + 1);
                    if (y > 0) queue.Enqueue(i - convertedWidth); if (y < convertedHeight - 1) queue.Enqueue(i + convertedWidth);
                }
            }
            Invalidate(); return true;
        }

        /// 返回经过人数守恒检查的数据，失败时直接抛出明确错误。
        public BlockPortersLevelData CreateData()
        {
            if (convertedWidth != settings.Width || convertedHeight != settings.Height) throw new InvalidOperationException("尺寸已改变，请重新转换图片。");
            if (columns.Length != 4) throw new InvalidOperationException("请先安排四列队伍。");
            var queues = new PorterTeamDefinition[4][];
            for (int i = 0; i < 4; i++) queues[i] = columns[i].Teams;
            return new BlockPortersLevelData(settings.Width, settings.Height, cells, queues, 5, palette.Length);
        }

        /// <summary>记住已导出的资产，下次更新原对象以保留 GUID。</summary>
        /// <param name="level">本配方导出的关卡。</param>
        public void SetExportedLevel(BlockPortersLevel level) => exportedLevel = level;
    }
}
