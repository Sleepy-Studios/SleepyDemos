namespace Hotfix.DroneFlight
{
    /// <summary>不依赖场景对象的航点索引推进器。</summary>
    internal sealed class DroneCruiseProgression
    {
        private int waypointCount;
        private DroneCruiseMode mode;
        private int direction = 1;

        internal int CurrentIndex { get; private set; } = -1;

        internal void Reset(int count, DroneCruiseMode cruiseMode)
        {
            waypointCount = count;
            mode = cruiseMode;
            direction = 1;
            CurrentIndex = count > 0 ? 0 : -1;
        }

        internal bool TryAdvance(out bool completed)
        {
            completed = false;
            if (waypointCount <= 0 || CurrentIndex < 0)
            {
                return false;
            }

            if (mode == DroneCruiseMode.Loop)
            {
                CurrentIndex = (CurrentIndex + 1) % waypointCount;
                return true;
            }

            if (mode == DroneCruiseMode.PingPong)
            {
                var next = CurrentIndex + direction;
                if (next >= waypointCount || next < 0)
                {
                    direction *= -1;
                    next = CurrentIndex + direction;
                }
                CurrentIndex = next;
                return true;
            }

            if (CurrentIndex + 1 < waypointCount)
            {
                CurrentIndex++;
                return true;
            }

            completed = true;
            return false;
        }
    }
}
