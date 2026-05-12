using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public abstract class StartupStateBase : IStartupState
    {
        private readonly List<StartupSystemBase> systems = new List<StartupSystemBase>();

        public abstract int StateId { get; }
        public abstract int NextStateId { get; }
        public abstract string Title { get; }

        public StartupContext Context { get; private set; }
        public string Description { get; private set; }
        public float Progress { get; private set; }

        public void SetContext(StartupContext context)
        {
            Context = context;
        }

        public async UniTask EnterAsync()
        {
            systems.Clear();
            CreateSystems(systems);
            if (systems.Count == 0)
            {
                Progress = 1f;
                return;
            }

            for (var i = 0; i < systems.Count; i++)
            {
                var system = systems[i];
                ReportStateProgress(i, 0f, system.Description);
                await system.ExecuteAsync();
                ReportStateProgress(i, 1f, system.Description);
            }
        }

        public virtual UniTask ExitAsync()
        {
            systems.Clear();
            return UniTask.CompletedTask;
        }

        public void ReportSystemProgress(StartupSystemBase system, string size = null)
        {
            var index = systems.IndexOf(system);
            if (index < 0)
            {
                return;
            }

            ReportStateProgress(index, system.Progress, system.Description, size);
        }

        protected abstract void CreateSystems(List<StartupSystemBase> systems);

        private void ReportStateProgress(int systemIndex, float systemProgress, string description, string size = null)
        {
            Progress = Mathf.Clamp01((systemIndex + Mathf.Clamp01(systemProgress)) / systems.Count);
            Description = description ?? string.Empty;
            Context?.ReportProgress(Progress, Title, Description, size);
        }
    }
}
