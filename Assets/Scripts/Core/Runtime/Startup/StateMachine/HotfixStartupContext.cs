using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Core.Runtime
{
    public sealed class HotfixStartupContext
    {
        public HotfixStartupContext(StartupContext startupContext)
        {
            Config = startupContext.Config;
            LoadingView = startupContext.LoadingView;
            Runner = startupContext.Runner;
            HotfixAssemblies = startupContext.HotfixAssemblies;
        }

        public HotfixConfig Config { get; }
        public StartupLoadingView LoadingView { get; }
        public MonoBehaviour Runner { get; }
        public IReadOnlyList<Assembly> HotfixAssemblies { get; }
    }
}
