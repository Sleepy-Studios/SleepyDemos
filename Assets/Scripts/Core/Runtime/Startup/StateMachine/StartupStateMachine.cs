using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public sealed class StartupStateMachine
    {
        private readonly Dictionary<int, IStartupState> states = new Dictionary<int, IStartupState>();
        private readonly StartupContext context;
        private readonly int totalStateCount;
        private readonly float progressStart;
        private readonly float progressSpan;
        private int finishedStateCount;

        public StartupStateMachine(StartupContext context, int totalStateCount, float progressStart = 0f, float progressSpan = 1f)
        {
            this.context = context;
            this.totalStateCount = Mathf.Max(1, totalStateCount);
            this.progressStart = Mathf.Clamp01(progressStart);
            this.progressSpan = Mathf.Clamp01(progressSpan);
            this.context.SetProgressScope(this.progressStart, this.progressSpan);
            this.context.SetProgressReporter(ReportStateProgress);
        }

        public IStartupState CurrentState { get; private set; }

        public void AddState(IStartupState state)
        {
            state.SetContext(context);
            states[state.StateId] = state;
        }

        public async UniTask RunAsync(int initState)
        {
            var stateId = initState;
            while (states.TryGetValue(stateId, out var state))
            {
                CurrentState = state;
                Debug.Log($"[Startup] enter state: {state.Title}");
                UpdateLoading(state, 0f);
                await state.EnterAsync();
                UpdateLoading(state, 1f);
                await state.ExitAsync();

                finishedStateCount++;

                if (state.NextStateId < 0 || !states.ContainsKey(state.NextStateId))
                {
                    break;
                }

                stateId = state.NextStateId;
            }
        }

        public void UpdateLoading(IStartupState state, float stateProgress)
        {
            ReportStateProgress(stateProgress, state.Title, state.Description, null);
        }

        private void ReportStateProgress(float stateProgress, string title, string description, string size)
        {
            var scopedProgress = Mathf.Clamp01((finishedStateCount + Mathf.Clamp01(stateProgress)) / totalStateCount);
            var totalProgress = context.ToTotalProgress(scopedProgress);
            context.LoadingView?.SetProgress(totalProgress, title, description, size);
        }
    }
}
