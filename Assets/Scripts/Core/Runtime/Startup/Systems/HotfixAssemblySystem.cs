using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    public sealed class HotfixAssemblySystem : StartupSystemBase
    {
        public HotfixAssemblySystem(StartupStateBase state) : base(state)
        {
        }

        public override async UniTask ExecuteAsync()
        {
            Report(0f, "加载热更程序集并扫描 View 类型");
            Context.MutableHotfixAssemblies.Clear();
            if (Context.Config != null)
            {
                Context.MutableHotfixAssemblies.AddRange(await HotfixAssemblyLoader.LoadAsync(Context.Config.HotfixAssemblies));
            }

            if (Context.MutableHotfixAssemblies.Count > 0)
            {
                UITypeReflection.Init(Context.MutableHotfixAssemblies.ToArray());
            }

            UITypeReflection.Scan(typeof(HotfixAssemblySystem).Assembly);
            if (Context.Runner != null)
            {
                UITypeReflection.Scan(Context.Runner.GetType().Assembly);
            }

            Report(1f, "热更程序集加载完成");
        }
    }
}
