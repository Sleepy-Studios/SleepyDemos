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
            Report(0f, "加载热更程序集并发现页面");
            Context.MutableHotfixAssemblies.Clear();
            if (Context.Config != null)
            {
                Context.MutableHotfixAssemblies.AddRange(await HotfixAssemblyLoader.LoadAsync(Context.Config.HotfixAssemblies));
            }

            UITypeReflection.Init(Context.MutableHotfixAssemblies.ToArray());
            UITypeReflection.Scan(typeof(View).Assembly);
            UITypeReflection.Scan(Context.Runner?.GetType().Assembly);

            Report(1f, "热更程序集加载完成");
        }
    }
}
