using System;
using System.Globalization;

namespace Core.Runtime
{
    /// 可替换的 UTC 墙钟；玩法步进继续使用自身的帧时钟。
    public interface ITimeSource
    {
        /// Unix 毫秒时间戳。
        long UtcNowMilliseconds { get; }
    }

    public enum TimeDisplayFormat
    {
        SecondsOnly,
        MinutesSeconds,
        HoursMinutesSeconds,
        Auto,
        AutoWithUnits
    }

    /// 时间戳明确区分秒和毫秒；日期默认按设备时区显示。
    public static class TimeUtil
    {
        private sealed class SystemTimeSource : ITimeSource
        {
            public long UtcNowMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static readonly ITimeSource SystemSource = new SystemTimeSource();
        private static ITimeSource timeSource = SystemSource;
        public const string StandardFormat = "yyyy-MM-dd HH:mm:ss";
        /// 当前 UTC 毫秒时间戳。
        public static long UtcNowMilliseconds => timeSource.UtcNowMilliseconds;
        /// 当前 UTC 秒时间戳。
        public static long UtcNowSeconds => UtcNow.ToUnixTimeSeconds();
        /// 当前 UTC 日期，精度为毫秒。
        public static DateTimeOffset UtcNow => FromUnixMilliseconds(UtcNowMilliseconds);
        /// 当前设备本地日期。
        public static DateTime LocalNow => UtcNow.LocalDateTime;

        /// <summary>替换公共墙钟；不修改玩法帧时钟或全局 timeScale。</summary>
        /// <param name="source">服务器接入或测试时间源；null 恢复系统时间。调用方在测试结束时负责恢复。</param>
        public static void SetTimeSource(ITimeSource source) => timeSource = source ?? SystemSource;

        /// <summary>将 Unix 毫秒转换为 UTC 日期。</summary>
        /// <param name="timestampMs">Unix 毫秒；超出 DateTimeOffset 范围抛出异常。</param>
        public static DateTimeOffset FromUnixMilliseconds(long timestampMs) => DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);

        /// <summary>将 Unix 秒转换为 UTC 日期。</summary>
        /// <param name="timestampSeconds">Unix 秒；不根据数量级猜测单位。</param>
        public static DateTimeOffset FromUnixSeconds(long timestampSeconds) => DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);

        /// <summary>将带时区的日期转换为 Unix 毫秒。</summary>
        /// <param name="date">明确带偏移的日期。</param>
        public static long ToUnixMilliseconds(DateTimeOffset date) => date.ToUnixTimeMilliseconds();

        /// <summary>将日期转换为 Unix 毫秒。</summary>
        /// <param name="date">UTC/Local 按 Kind 转换；Unspecified 视为设备本地时间。</param>
        public static long ToUnixMilliseconds(DateTime date) => new DateTimeOffset(date).ToUnixTimeMilliseconds();

        /// <summary>将日期转换为 Unix 秒。</summary>
        /// <param name="date">明确带偏移的日期。</param>
        public static long ToUnixSeconds(DateTimeOffset date) => date.ToUnixTimeSeconds();

        /// <summary>将 Unix 毫秒转换为指定时区的日期。</summary>
        /// <param name="timestampMs">Unix 毫秒。</param>
        /// <param name="timeZone">null 使用设备本地时区。</param>
        public static DateTimeOffset ToDateTime(long timestampMs, TimeZoneInfo timeZone = null)
            => TimeZoneInfo.ConvertTime(FromUnixMilliseconds(timestampMs), timeZone ?? TimeZoneInfo.Local);

        /// <summary>按指定时区格式化日期。</summary>
        /// <param name="timestampMs">Unix 毫秒；0 是有效的 Unix 起点。</param>
        /// <param name="format">日期格式，默认 yyyy-MM-dd HH:mm:ss。</param>
        /// <param name="timeZone">null 使用设备本地时区。</param>
        public static string FormatTimestamp(long timestampMs, string format = StandardFormat, TimeZoneInfo timeZone = null)
            => ToDateTime(timestampMs, timeZone).ToString(format, CultureInfo.InvariantCulture);

        /// <summary>输出 UTC ISO 8601，保持现有存档的七位小数和 Z 后缀。</summary>
        /// <param name="date">null 使用公共墙钟。</param>
        public static string ToIso8601(DateTimeOffset? date = null)
            => (date ?? UtcNow).UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

        /// <summary>读取带偏移的 ISO 8601 日期。</summary>
        /// <param name="value">存档中的日期字符串；无时区的旧值按 UTC 解释。</param>
        /// <param name="date">成功时输出 UTC 日期；失败时输出默认值。</param>
        /// <returns>空白或非法输入返回 false。</returns>
        public static bool TryParseIso8601(string value, out DateTimeOffset date)
            => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);

        /// <summary>计算有符号毫秒差，超出 long 范围时饱和。</summary>
        /// <param name="startTimestampMs">起始 Unix 毫秒。</param>
        /// <param name="endTimestampMs">结束 Unix 毫秒。</param>
        public static long DifferenceMilliseconds(long startTimestampMs, long endTimestampMs)
        {
            if (startTimestampMs < 0 && endTimestampMs > long.MaxValue + startTimestampMs) return long.MaxValue;
            if (startTimestampMs > 0 && endTimestampMs < long.MinValue + startTimestampMs) return long.MinValue;
            return endTimestampMs - startTimestampMs;
        }

        /// <summary>计算有符号时间差，超出 TimeSpan 范围时饱和。</summary>
        /// <param name="startTimestampMs">起始 Unix 毫秒。</param>
        /// <param name="endTimestampMs">结束 Unix 毫秒。</param>
        public static TimeSpan CalculateTimeDifference(long startTimestampMs, long endTimestampMs)
        {
            long ms = DifferenceMilliseconds(startTimestampMs, endTimestampMs);
            long limit = long.MaxValue / TimeSpan.TicksPerMillisecond;
            return TimeSpan.FromTicks(Math.Clamp(ms, -limit, limit) * TimeSpan.TicksPerMillisecond);
        }

        /// <summary>读取非负剩余毫秒。</summary>
        /// <param name="endTimestampMs">结束 Unix 毫秒。</param>
        public static long GetRemainingMilliseconds(long endTimestampMs)
            => Math.Max(0, DifferenceMilliseconds(UtcNowMilliseconds, endTimestampMs));

        /// <summary>读取向上取整的非负剩余秒数。</summary>
        /// <param name="endTimestampMs">结束 Unix 毫秒；不足一秒仍返回 1。</param>
        public static long GetRemainingSeconds(long endTimestampMs)
        {
            long ms = GetRemainingMilliseconds(endTimestampMs);
            return ms / 1000 + (ms % 1000 > 0 ? 1 : 0);
        }

        /// <summary>格式化非负时长。</summary>
        /// <param name="seconds">总秒数；负值按零处理。</param>
        /// <param name="format">Auto 按秒、分秒、时分秒切换；累计分钟和小时不回绕。</param>
        public static string FormatSeconds(long seconds, TimeDisplayFormat format = TimeDisplayFormat.Auto)
        {
            seconds = Math.Max(0, seconds);
            if (format == TimeDisplayFormat.Auto)
                format = seconds < 60 ? TimeDisplayFormat.SecondsOnly : seconds < 3600
                    ? TimeDisplayFormat.MinutesSeconds : TimeDisplayFormat.HoursMinutesSeconds;
            switch (format)
            {
                case TimeDisplayFormat.SecondsOnly: return seconds.ToString(CultureInfo.InvariantCulture) + "秒";
                case TimeDisplayFormat.MinutesSeconds: return $"{seconds / 60:D2}:{seconds % 60:D2}";
                case TimeDisplayFormat.HoursMinutesSeconds: return $"{seconds / 3600:D2}:{seconds / 60 % 60:D2}:{seconds % 60:D2}";
                case TimeDisplayFormat.AutoWithUnits:
                    if (seconds < 60) return seconds + "秒";
                    if (seconds < 3600) return Units(seconds / 60, "分钟", seconds % 60, "秒");
                    if (seconds < 86400) return Units(seconds / 3600, "小时", seconds / 60 % 60, "分钟");
                    return Units(seconds / 86400, "天", seconds / 3600 % 24, "小时");
                default: throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        /// <summary>格式化毫秒时长，不足一秒部分舍去。</summary>
        /// <param name="milliseconds">总毫秒；倒计时应使用 GetRemainingSeconds 保留末秒。</param>
        /// <param name="format">时长显示格式。</param>
        public static string FormatMilliseconds(long milliseconds, TimeDisplayFormat format = TimeDisplayFormat.Auto)
            => FormatSeconds(milliseconds / 1000, format);

        /// <summary>显示相对当前时间的中文描述。</summary>
        /// <param name="timestampMs">目标 Unix 毫秒；支持过去和未来。</param>
        public static string FormatTimeAgo(long timestampMs)
        {
            long ms = DifferenceMilliseconds(timestampMs, UtcNowMilliseconds);
            bool future = ms < 0;
            long seconds = (ms == long.MinValue ? long.MaxValue : Math.Abs(ms)) / 1000;
            if (seconds < 1) return "刚刚";
            long amount = seconds < 60 ? seconds : seconds < 3600 ? seconds / 60 : seconds < 86400 ? seconds / 3600 : seconds / 86400;
            string unit = seconds < 60 ? "秒" : seconds < 3600 ? "分钟" : seconds < 86400 ? "小时" : "天";
            return amount + unit + (future ? "后" : "前");
        }

        /// <summary>计算指定日期的整点 Unix 毫秒。</summary>
        /// <param name="date">只使用年月日，不使用其 Kind。</param>
        /// <param name="hour">0 到 23。</param>
        /// <param name="timeZone">null 使用设备时区；夏令时不存在的时间前移至第一个有效分钟。</param>
        public static long GetDateAtHour(DateTime date, int hour, TimeZoneInfo timeZone = null)
        {
            ValidateHour(hour);
            var zone = timeZone ?? TimeZoneInfo.Local;
            DateTime local = DateTime.SpecifyKind(date.Date.AddHours(hour), DateTimeKind.Unspecified);
            while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone)).ToUnixTimeMilliseconds();
        }

        /// <summary>读取今天偏移若干天后的指定整点。</summary>
        /// <param name="offsetDays">相对当前本地日期的天数。</param>
        /// <param name="hour">0 到 23。</param>
        /// <param name="timeZone">null 使用设备时区。</param>
        public static long GetTodayAtHour(int offsetDays, int hour, TimeZoneInfo timeZone = null)
            => GetDateAtHour(ToDateTime(UtcNowMilliseconds, timeZone).Date.AddDays(offsetDays), hour, timeZone);

        /// <summary>按调用方指定的刷新小时计算游戏日。</summary>
        /// <param name="timestampMs">Unix 毫秒。</param>
        /// <param name="resetHour">0 到 23；刷新前属于前一天。</param>
        /// <param name="timeZone">null 使用设备时区。</param>
        public static DateTime GetGameDate(long timestampMs, int resetHour, TimeZoneInfo timeZone = null)
        {
            ValidateHour(resetHour);
            DateTime local = ToDateTime(timestampMs, timeZone).DateTime;
            return (local.Hour < resetHour ? local.AddDays(-1) : local).Date;
        }

        /// <summary>计算严格晚于当前时间的下一次每日刷新。</summary>
        /// <param name="refreshHour">0 到 23；没有业务默认值。</param>
        /// <param name="timeZone">null 使用设备时区。</param>
        public static long GetNextDailyRefreshTime(int refreshHour, TimeZoneInfo timeZone = null)
        {
            long now = UtcNowMilliseconds;
            DateTime date = ToDateTime(now, timeZone).Date;
            long candidate = GetDateAtHour(date, refreshHour, timeZone);
            return candidate > now ? candidate : GetDateAtHour(date.AddDays(1), refreshHour, timeZone);
        }

        private static string Units(long first, string firstUnit, long second, string secondUnit)
            => first + firstUnit + (second == 0 ? string.Empty : second + secondUnit);
        private static void ValidateHour(int hour)
        {
            if (hour < 0 || hour > 23) throw new ArgumentOutOfRangeException(nameof(hour));
        }
    }
}
