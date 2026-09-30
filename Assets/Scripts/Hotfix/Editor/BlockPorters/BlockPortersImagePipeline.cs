using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Hotfix.Editor.BlockPorters
{
    public sealed class PorterImageResult
    {
        public int[] PixelCells;
        public Color[] PixelPalette;
        public int[] Cells;
        public Color[] Palette;
    }

    /// 只接收像素副本；不在后台线程访问 Texture、AssetDatabase 或编辑器对象。
    public static class BlockPortersImagePipeline
    {
        /// <summary>按裁剪、透明度、Lab 聚类和近色预算得到占用轮廓不变的候选。</summary>
        /// <param name="pixels">左下开始的源图片像素副本。</param>
        /// <param name="sourceWidth">源宽度。</param>
        /// <param name="sourceHeight">源高度。</param>
        /// <param name="settings">独立配方快照，宽高不超过 32。</param>
        /// <param name="token">转换、聚类和拆色可取消。</param>
        public static PorterImageResult Convert(Color32[] pixels, int sourceWidth, int sourceHeight, PorterRecipeSettings settings, CancellationToken token = default)
        {
            if (sourceWidth < 1 || sourceHeight < 1 || pixels == null || pixels.Length != sourceWidth * sourceHeight || settings.Width < 1 || settings.Height < 1 || settings.Width > 32 || settings.Height > 32)
                throw new ArgumentException("图片或棋盘尺寸非法。");
            var crop = settings.Crop;
            float left = Mathf.Clamp01(crop.x), bottom = Mathf.Clamp01(crop.y);
            float cw = Mathf.Min(Mathf.Clamp01(crop.width), 1 - left), ch = Mathf.Min(Mathf.Clamp01(crop.height), 1 - bottom);
            if (cw <= 0 || ch <= 0) throw new ArgumentException("裁剪范围为空。");
            float aspect = sourceWidth * cw / (sourceHeight * ch);
            int fitWidth = settings.Width, fitHeight = settings.Height;
            if (settings.KeepAspect)
            {
                if (aspect > (float)fitWidth / fitHeight) fitHeight = Math.Max(1, Mathf.RoundToInt(fitWidth / aspect));
                else fitWidth = Math.Max(1, Mathf.RoundToInt(fitHeight * aspect));
            }
            int offsetX = (settings.Width - fitWidth) / 2, offsetY = (settings.Height - fitHeight) / 2;
            var samples = new Color[settings.Width * settings.Height];
            var occupied = new bool[samples.Length];
            for (int y = 0; y < fitHeight; y++) for (int x = 0; x < fitWidth; x++)
            {
                token.ThrowIfCancellationRequested();
                int x0 = Mathf.Clamp(Mathf.FloorToInt((left + cw * x / fitWidth) * sourceWidth), 0, sourceWidth - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((left + cw * (x + 1) / fitWidth) * sourceWidth), x0 + 1, sourceWidth);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((bottom + ch * y / fitHeight) * sourceHeight), 0, sourceHeight - 1);
                int y1 = Mathf.Clamp(Mathf.CeilToInt((bottom + ch * (y + 1) / fitHeight) * sourceHeight), y0 + 1, sourceHeight);
                Color sum = Color.clear; float alpha = 0; int count = 0;
                // 面积平均，超大图片每格均匀抽样，避免处理数千万像素。
                int step = Math.Max(1, Math.Max(x1 - x0, y1 - y0) / 32);
                for (int sy = y0; sy < y1; sy += step) for (int sx = x0; sx < x1; sx += step)
                {
                    Color c = pixels[sy * sourceWidth + sx]; count++; alpha += c.a;
                    sum.r += c.r * c.a; sum.g += c.g * c.a; sum.b += c.b * c.a;
                }
                int index = (y + offsetY) * settings.Width + x + offsetX;
                if (alpha / Math.Max(1, count) < settings.AlphaThreshold || alpha == 0) continue;
                Color average = new(sum.r / alpha, sum.g / alpha, sum.b / alpha, 1);
                if (settings.RemoveBackground && Delta(average, settings.Background) <= settings.BackgroundDelta) continue;
                samples[index] = average; occupied[index] = true;
            }
            var ids = Enumerable.Range(0, samples.Length).Where(i => occupied[i]).ToArray();
            if (ids.Length == 0) throw new ArgumentException("没有有效像素，请调整透明度或背景剔除参数。");
            int nearBudget = settings.Difficulty == PorterDifficulty.Easy ? 0 : Math.Min(settings.Difficulty == PorterDifficulty.Normal ? 1 : 2, Math.Max(0, settings.NearGroups));
            int budget = Math.Min(12, Math.Max(1, settings.ColorBudget - nearBudget));
            var centers = new List<Vector3> { Lab(samples[ids[0]]) };
            while (centers.Count < budget)
            {
                float farthest = 0; Vector3 selected = default;
                foreach (int i in ids)
                {
                    Vector3 lab = Lab(samples[i]); float distance = centers.Min(c => (lab - c).sqrMagnitude);
                    if (distance > farthest) { farthest = distance; selected = lab; }
                }
                if (farthest < .01f) break;
                centers.Add(selected);
            }
            var cells = Enumerable.Repeat(-1, samples.Length).ToArray();
            for (int iteration = 0; iteration < 16; iteration++)
            {
                token.ThrowIfCancellationRequested();
                var sums = new Vector3[centers.Count]; var counts = new int[centers.Count];
                foreach (int i in ids)
                {
                    Vector3 lab = Lab(samples[i]); int best = Nearest(lab, centers);
                    cells[i] = best; sums[best] += lab; counts[best]++;
                }
                for (int i = 0; i < centers.Count; i++) if (counts[i] > 0) centers[i] = sums[i] / counts[i];
            }
            float mergeDelta = Math.Max(0, settings.MinDelta * settings.MergeStrength);
            for (int a = 0; a < centers.Count; a++) for (int b = centers.Count - 1; b > a; b--)
            {
                if (Vector3.Distance(centers[a], centers[b]) >= mergeDelta) continue;
                int countA = cells.Count(c => c == a), countB = cells.Count(c => c == b);
                centers[a] = (centers[a] * countA + centers[b] * countB) / Math.Max(1, countA + countB);
                centers.RemoveAt(b);
                for (int i = 0; i < cells.Length; i++) { if (cells[i] == b) cells[i] = a; if (cells[i] > b) cells[i]--; }
            }
            var colors = centers.Select(Rgb).ToArray();
            var result = new PorterImageResult { PixelCells = (int[])cells.Clone(), PixelPalette = (Color[])colors.Clone(), Cells = cells, Palette = colors };
            AddNearColors(result, settings, token);
            return result;
        }

        private static void AddNearColors(PorterImageResult result, PorterRecipeSettings settings, CancellationToken token)
        {
            int groups = settings.Difficulty == PorterDifficulty.Easy ? 0 : Math.Min(settings.Difficulty == PorterDifficulty.Normal ? 1 : 2, settings.NearGroups);
            int limit = Mathf.FloorToInt(result.Cells.Count(c => c >= 0) * Mathf.Clamp01(settings.RecolorFraction));
            var colors = result.Palette.ToList(); var random = new System.Random(settings.Seed);
            var ids = Enumerable.Range(0, result.Cells.Length).Where(i => result.Cells[i] >= 0).OrderBy(_ => random.Next()).ToArray();
            int changed = 0;
            foreach (int source in Enumerable.Range(0, result.Palette.Length).OrderByDescending(c => result.Cells.Count(i => i == c)))
            {
                token.ThrowIfCancellationRequested();
                if (groups <= 0 || colors.Count >= Math.Min(12, settings.ColorBudget) || changed >= limit) break;
                Color near = colors[source]; Vector3 lab = Lab(near);
                bool found = false;
                foreach (Vector3 direction in new[] { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                {
                    near = Rgb(lab + direction * (settings.MinDelta + 1));
                    if (colors.All(c => Delta(c, near) >= settings.MinDelta)) { found = true; break; }
                }
                if (!found) continue;
                int quota = Math.Max(1, (limit - changed) / groups), added = 0, newId = colors.Count;
                foreach (int i in ids)
                {
                    if (result.Cells[i] != source || added >= quota || changed >= limit) continue;
                    result.Cells[i] = newId; added++; changed++;
                }
                if (added > 0) { colors.Add(near); groups--; }
            }
            result.Palette = colors.ToArray();
        }

        /// <summary>计算 sRGB 颜色的 ΔE76，供预览、合并和近色检查使用。</summary>
        /// <param name="a">第一个颜色，透明度不参与距离。</param>
        /// <param name="b">第二个颜色。</param>
        public static float Delta(Color a, Color b) => Vector3.Distance(Lab(a), Lab(b));
        private static int Nearest(Vector3 value, List<Vector3> centers)
        {
            int best = 0; float distance = float.MaxValue;
            for (int i = 0; i < centers.Count; i++) { float d = (value - centers[i]).sqrMagnitude; if (d < distance) { best = i; distance = d; } }
            return best;
        }
        private static float Linear(float c) => c <= .04045f ? c / 12.92f : Mathf.Pow((c + .055f) / 1.055f, 2.4f);
        private static float Gamma(float c) => Mathf.Clamp01(c <= .0031308f ? 12.92f * c : 1.055f * Mathf.Pow(c, 1 / 2.4f) - .055f);
        private static float Pivot(float n) => n > .008856f ? Mathf.Pow(n, 1f / 3) : 7.787f * n + 16f / 116;
        private static float Inverse(float n) => n * n * n > .008856f ? n * n * n : (n - 16f / 116) / 7.787f;
        private static Vector3 Lab(Color color)
        {
            float r = Linear(color.r), g = Linear(color.g), b = Linear(color.b);
            float x = Pivot((.4124564f * r + .3575761f * g + .1804375f * b) / .95047f);
            float y = Pivot(.2126729f * r + .7151522f * g + .072175f * b);
            float z = Pivot((.0193339f * r + .119192f * g + .9503041f * b) / 1.08883f);
            return new Vector3(116 * y - 16, 500 * (x - y), 200 * (y - z));
        }
        private static Color Rgb(Vector3 lab)
        {
            float y = (lab.x + 16) / 116, x = lab.y / 500 + y, z = y - lab.z / 200;
            x = .95047f * Inverse(x); y = Inverse(y); z = 1.08883f * Inverse(z);
            return new Color(Gamma(3.2404542f * x - 1.5371385f * y - .4985314f * z), Gamma(-.969266f * x + 1.8760108f * y + .041556f * z), Gamma(.0556434f * x - .2040259f * y + 1.0572252f * z), 1);
        }
    }
}
