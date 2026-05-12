using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Core.Runtime
{
    public sealed class StartupContext
    {
        private readonly List<Assembly> hotUpdateAssemblies = new List<Assembly>();
        private float progressStart;
        private float progressSpan = 1f;
        private Action<float, string, string, string> progressReporter;

        public StartupContext(HotUpdateConfig config, StartupLoadingView loadingView, MonoBehaviour runner)
        {
            Config = config;
            LoadingView = loadingView;
            Runner = runner;
        }

        public HotUpdateConfig Config { get; }
        public StartupLoadingView LoadingView { get; }
        public MonoBehaviour Runner { get; }
        public IReadOnlyList<Assembly> HotUpdateAssemblies => hotUpdateAssemblies;

        public List<Assembly> MutableHotUpdateAssemblies => hotUpdateAssemblies;

        public void SetProgressScope(float start, float span)
        {
            progressStart = Mathf.Clamp01(start);
            progressSpan = Mathf.Clamp01(span);
        }

        public void SetProgressReporter(Action<float, string, string, string> reporter)
        {
            progressReporter = reporter;
        }

        public float ToTotalProgress(float localProgress)
        {
            return Mathf.Clamp01(progressStart + Mathf.Clamp01(localProgress) * progressSpan);
        }

        public void ReportProgress(float localProgress, string title, string description, string size = null)
        {
            progressReporter?.Invoke(localProgress, title, description, size);
        }
    }
}
