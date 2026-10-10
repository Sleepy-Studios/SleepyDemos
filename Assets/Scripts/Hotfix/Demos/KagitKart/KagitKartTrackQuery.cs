using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 与原作道路查询返回值对应；每辆车持有自己的结果，规则步不分配对象。
    public struct KagitKartQueryInfo
    {
        public int Index;
        public double Distance;
        public float T;
        public float Lateral;
        public float HalfWidth;
        public float OffroadWidth;
        public bool OnRoad;
        public string Surface;
        public float GroundY;
        public Vector3 Up;
        public Vector3 Tangent;
        public Vector3 Right;
        public int EdgeCode;
        public bool HasGround;
        public float WaterY;
        public int RampId;
        public float RampHeight;
        public bool OnLip;
        public int PadId;
        public float CentreY;
        public float EdgeY;
        public float Heading;
        public float Curvature;
    }

    /// 用于网格、救援、道具摆放的赛道表面点。
    public struct KagitKartTrackPoint
    {
        public Vector3 Position;
        public Vector3 Tangent;
        public Vector3 Right;
        public Vector3 Up;
        public float HalfWidth;
        public float Offroad;
        public float Heading;
        public int Index;
        public float T;
    }

    /// 迁移 trackQuery.js：高度加权邻近、空间网格及捷径换段。
    public sealed class KagitKartTrackQuery
    {
        private static readonly string[] SurfaceNames = { "road", "ice", "sand" };
        private readonly Dictionary<Vector2Int, List<int>> cells = new();
        private readonly KagitKartTrackSamples samples;
        private readonly float cellSize;
        private readonly float waterDrop;

        /// 本会话使用的只读赛道采样。
        public KagitKartTrackData Track { get; }
        /// 赛道闭环长度。
        public float Length => (float)Track.data.length;
        /// 原作书页所在高度。
        public float PageY => Track.data.minY - 0.05f;
        /// 原作桌面上方掉落终止高度。
        public float KillY { get; }

        /// <summary>从已校验采样创建查询网格。</summary>
        /// <param name="track">Parse 返回的完整赛道数据。</param>
        /// <param name="gridCell">原作空间网格边长。</param>
        /// <param name="waterDrop">没有绝对水位时，水面低于路缘的距离。</param>
        /// <param name="deskDrop">桌面低于书页的距离。</param>
        public KagitKartTrackQuery(KagitKartTrackData track, float gridCell, float waterDrop, float deskDrop)
        {
            Track = track ?? throw new ArgumentNullException(nameof(track));
            if (!(gridCell > 0) || float.IsInfinity(gridCell))
            {
                throw new ArgumentOutOfRangeException(nameof(gridCell));
            }
            samples = track.data.samples;
            cellSize = gridCell;
            this.waterDrop = waterDrop;
            KillY = PageY - deskDrop + 0.3f;
            for (int i = 0; i < track.data.sampleCount; i++)
            {
                float radius = samples.halfWidth[i] + samples.offroad[i] + 6;
                int x0 = Mathf.FloorToInt((samples.px[i] - radius) / cellSize);
                int x1 = Mathf.FloorToInt((samples.px[i] + radius) / cellSize);
                int z0 = Mathf.FloorToInt((samples.pz[i] - radius) / cellSize);
                int z1 = Mathf.FloorToInt((samples.pz[i] + radius) / cellSize);
                for (int x = x0; x <= x1; x++)
                {
                    for (int z = z0; z <= z1; z++)
                    {
                        var key = new Vector2Int(x, z);
                        if (!cells.TryGetValue(key, out var indices))
                        {
                            indices = new List<int>();
                            cells.Add(key, indices);
                        }
                        indices.Add(i);
                    }
                }
            }
        }

        /// <summary>查询位置下方道路，沿用原作提示索引与捷径换段条件。</summary>
        /// <param name="position">当前规则位置。</param>
        /// <param name="hintIndex">上一次结果；-1 表示强制网格查找。</param>
        /// <param name="result">当前车辆持有的查询结果。</param>
        public void Query(Vector3 position, int hintIndex, ref KagitKartQueryInfo result)
        {
            int i;
            if (hintIndex >= 0 && hintIndex < Track.data.sampleCount)
            {
                i = NearestLocal(position, hintIndex);
                float band = samples.halfWidth[i] + samples.offroad[i];
                double distanceSquared = DistanceSquared(i, position);
                if (distanceSquared > (band + 40) * (band + 40))
                {
                    i = NearestGrid(position);
                }
                else
                {
                    float lateralHint = ((position.x - samples.px[i]) * samples.hx[i] +
                        (position.z - samples.pz[i]) * samples.hz[i]) /
                        MagnitudeOrOne(samples.hx[i], samples.hz[i]);
                    if (Mathf.Abs(lateralHint) > band)
                    {
                        int grid = NearestGrid(position);
                        if (grid != i && DistanceSquared(grid, position) < distanceSquared)
                        {
                            i = grid;
                        }
                    }
                }
            }
            else
            {
                i = NearestGrid(position);
            }
            double along = (((double)position.x - samples.px[i]) * samples.tx[i] +
                ((double)position.z - samples.pz[i]) * samples.tz[i]) / DoubleMagnitudeOrOne(samples.tx[i], samples.tz[i]);
            int j = Wrap(i + (along >= 0 ? 1 : -1));
            double fraction = Math.Min(1, Math.Abs(along) / Track.data.spacing);
            double hx = DoubleInterpolate(samples.hx, i, j, fraction);
            double hz = DoubleInterpolate(samples.hz, i, j, fraction);
            double magnitude = DoubleMagnitudeOrOne(hx, hz);
            double centreY = DoubleInterpolate(samples.py, i, j, fraction);
            double centreX = DoubleInterpolate(samples.px, i, j, fraction);
            double centreZ = DoubleInterpolate(samples.pz, i, j, fraction);
            double lateral = ((position.x - centreX) * hx + (position.z - centreZ) * hz) / magnitude;
            double halfWidth = DoubleInterpolate(samples.halfWidth, i, j, fraction);
            double offroad = DoubleInterpolate(samples.offroad, i, j, fraction);
            double slope = DoubleInterpolate(samples.slopeRight, i, j, fraction);
            double distance = samples.dist[i] + along;
            if (distance < 0)
            {
                distance += Track.data.length;
            }
            if (distance >= Track.data.length)
            {
                distance -= Track.data.length;
            }
            bool right = lateral >= 0;
            int edge = right ? samples.edgeRight[i] : samples.edgeLeft[i];
            bool onRoad = Math.Abs(lateral) <= halfWidth;
            bool inBand = Math.Abs(lateral) <= halfWidth + offroad;
            double edgeY = centreY + (right ? halfWidth : -halfWidth) * slope;
            result.Index = i;
            result.Distance = distance;
            result.T = (float)(distance / Track.data.length);
            result.Lateral = (float)lateral;
            result.HalfWidth = (float)halfWidth;
            result.OffroadWidth = (float)offroad;
            result.OnRoad = onRoad;
            result.EdgeCode = edge;
            result.CentreY = (float)centreY;
            result.EdgeY = (float)edgeY;
            result.Heading = samples.heading[i];
            result.Curvature = samples.curvature[i];
            result.OnLip = samples.rampLip[i] == 1;
            result.PadId = -1;
            result.RampId = -1;
            result.RampHeight = 0;
            result.WaterY = float.NegativeInfinity;
            result.HasGround = true;
            result.Tangent = DoubleInterpolateVector(samples.tx, samples.ty, samples.tz, i, j, fraction).normalized;
            if (onRoad)
            {
                int surface = right ? (samples.surface[i] >> 2) & 3 : samples.surface[i] & 3;
                result.Surface = SurfaceNames[surface];
                result.GroundY = (float)(centreY + lateral * slope);
                int rampId = samples.rampId[i];
                if (rampId >= 0)
                {
                    var ramp = Track.data.ramps[rampId];
                    double offset = distance - ramp.d0;
                    if (offset < -Length / 2)
                    {
                        offset += Length;
                    }
                    if (offset > Length / 2)
                    {
                        offset -= Length;
                    }
                    if (offset >= 0 && offset <= ramp.length)
                    {
                        result.RampId = rampId;
                        result.RampHeight = (float)(ramp.height * offset / ramp.length);
                        result.GroundY += result.RampHeight;
                    }
                }
                int padId = samples.padId[i];
                if (padId >= 0)
                {
                    var pad = Track.data.pads[padId];
                    if (Math.Abs(lateral - pad.lateral) <= pad.halfWidth)
                    {
                        result.Surface = "boost";
                        result.PadId = padId;
                    }
                }
                result.Up = DoubleInterpolateVector(samples.ux, samples.uy, samples.uz, i, j, fraction).normalized;
                result.Right = DoubleInterpolateVector(samples.rx, samples.ry, samples.rz, i, j, fraction).normalized;
            }
            else
            {
                result.Surface = inBand ? "offroad" : "out";
                result.GroundY = (float)edgeY;
                result.Right = new Vector3((float)(hx / magnitude), 0, (float)(hz / magnitude));
                result.Up = Vector3.Cross(result.Right, result.Tangent).normalized;
                if (!inBand && (edge == 2 || edge == 3))
                {
                    result.HasGround = false;
                    if (edge == 3)
                    {
                        result.WaterY = WaterSurface((float)edgeY);
                    }
                }
            }
        }

        /// <summary>按闭环比例读取插值点，比例可超出 [0,1)。</summary>
        /// <param name="t">相对采样零点的闭环比例。</param>
        /// <returns>尚未应用横向偏移的赛道点。</returns>
        public KagitKartTrackPoint SampleAt(float t)
        {
            float coordinate = Mathf.Repeat(t, 1) * Track.data.sampleCount;
            int i = Mathf.FloorToInt(coordinate) % Track.data.sampleCount;
            int j = Wrap(i + 1);
            float fraction = coordinate - Mathf.Floor(coordinate);
            Vector3 tangent = InterpolateVector(samples.tx, samples.ty, samples.tz, i, j, fraction).normalized;
            return new KagitKartTrackPoint
            {
                Position = InterpolateVector(samples.px, samples.py, samples.pz, i, j, fraction),
                Tangent = tangent,
                Right = InterpolateVector(samples.rx, samples.ry, samples.rz, i, j, fraction).normalized,
                Up = InterpolateVector(samples.ux, samples.uy, samples.uz, i, j, fraction).normalized,
                HalfWidth = Interpolate(samples.halfWidth, i, j, fraction),
                Offroad = Interpolate(samples.offroad, i, j, fraction),
                Heading = Mathf.Atan2(tangent.x, tangent.z),
                Index = i,
                T = Mathf.Repeat(t, 1)
            };
        }

        /// <summary>按原作表面放置，横向正值为行驶右侧。</summary>
        /// <param name="t">闭环比例。</param>
        /// <param name="lateral">横向米数。</param>
        /// <returns>道路或路缘上的表面点。</returns>
        public KagitKartTrackPoint PointAt(float t, float lateral)
        {
            var point = SampleAt(t);
            point.Position += point.Right * lateral;
            var info = new KagitKartQueryInfo();
            Query(point.Position, point.Index, ref info);
            if (info.HasGround)
            {
                point.Position.y = info.GroundY;
            }
            return point;
        }

        /// <summary>按原作交错双列网格生成起跑位置。</summary>
        /// <param name="count">赛车数量，单人计时为 1、普通比赛为 8。</param>
        public KagitKartTrackPoint[] GridSlots(int count)
        {
            if (count < 1 || count > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            var slots = new KagitKartTrackPoint[count];
            float lateral = Mathf.Min(3.2f, samples.halfWidth[Track.data.startIndex] * 0.35f);
            for (int i = 0; i < count; i++)
            {
                int column = i % 2;
                float back = 5 + i / 2 * 6 + column * 3;
                slots[i] = PointAt(((float)Track.data.startDist - back) / Length, column == 0 ? -lateral : lateral);
            }
            return slots;
        }

        /// <summary>获取与原作渲染水面一致的绝对水位。</summary>
        /// <param name="edgeY">路缘高度。</param>
        public float WaterSurface(float edgeY) => Mathf.Max(PageY + 0.05f,
            Track.definition.waterLevel.HasValue ? Track.definition.waterLevel.Value + 0.04f : edgeY - waterDrop);

        private int NearestGrid(Vector3 position)
        {
            var key = new Vector2Int(Mathf.FloorToInt(position.x / cellSize), Mathf.FloorToInt(position.z / cellSize));
            int best = -1;
            double distance = double.PositiveInfinity;
            if (cells.TryGetValue(key, out var indices))
            {
                foreach (int index in indices)
                {
                    double candidate = DistanceSquared(index, position);
                    if (candidate < distance)
                    {
                        distance = candidate;
                        best = index;
                    }
                }
            }
            if (best < 0)
            {
                for (int i = 0; i < Track.data.sampleCount; i += 4)
                {
                    double candidate = DistanceSquared(i, position);
                    if (candidate < distance)
                    {
                        distance = candidate;
                        best = i;
                    }
                }
                best = NearestLocal(position, best);
            }
            return best;
        }

        private int NearestLocal(Vector3 position, int hint)
        {
            int i = hint;
            double best = DistanceSquared(i, position);
            for (int iteration = 0; iteration < 64; iteration++)
            {
                int a = Wrap(i - 1);
                int b = Wrap(i + 1);
                double da = DistanceSquared(a, position);
                double db = DistanceSquared(b, position);
                if (da < best && da <= db)
                {
                    best = da;
                    i = a;
                }
                else if (db < best)
                {
                    best = db;
                    i = b;
                }
                else
                {
                    break;
                }
            }
            return i;
        }

        private double DistanceSquared(int index, Vector3 position)
        {
            double x = (double)samples.px[index] - position.x;
            double z = (double)samples.pz[index] - position.z;
            double y = ((double)samples.py[index] - position.y) * 2;
            return x * x + z * z + y * y;
        }

        private int Wrap(int index) => (index % Track.data.sampleCount + Track.data.sampleCount) % Track.data.sampleCount;
        private static float MagnitudeOrOne(float x, float z) => Mathf.Max(Mathf.Sqrt(x * x + z * z), 0.000001f);
        private static float Interpolate(float[] values, int i, int j, float fraction) =>
            values[i] + (values[j] - values[i]) * fraction;
        private static double DoubleMagnitudeOrOne(double x, double z)
        {
            double length = Math.Sqrt(x * x + z * z);
            return length > 0 ? length : 1;
        }

        private static double DoubleInterpolate(float[] values, int i, int j, double fraction) =>
            values[i] + ((double)values[j] - values[i]) * fraction;

        private static Vector3 DoubleInterpolateVector(float[] x, float[] y, float[] z, int i, int j, double fraction) =>
            new Vector3((float)DoubleInterpolate(x, i, j, fraction), (float)DoubleInterpolate(y, i, j, fraction),
                (float)DoubleInterpolate(z, i, j, fraction));

        private static Vector3 InterpolateVector(float[] x, float[] y, float[] z, int i, int j, float fraction) =>
            new Vector3(Interpolate(x, i, j, fraction), Interpolate(y, i, j, fraction), Interpolate(z, i, j, fraction));
    }
}
