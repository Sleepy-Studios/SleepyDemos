using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    public abstract class StartupSystemBase
    {
        protected StartupSystemBase(StartupStateBase state)
        {
            State = state;
        }

        protected StartupStateBase State { get; }
        protected StartupContext Context => State.Context;

        public float Progress { get; protected set; }
        public string Description { get; protected set; }

        public abstract UniTask ExecuteAsync();

        protected void Report(float progress, string description, string size = null)
        {
            Progress = UnityEngine.Mathf.Clamp01(progress);
            Description = description;
            State.ReportSystemProgress(this, size);
        }
    }
}
