using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    public sealed class HotUpdateAssemblySystem : StartupSystemBase
    {
        public HotUpdateAssemblySystem(StartupStateBase state) : base(state)
        {
        }

        public override async UniTask ExecuteAsync()
        {
            Report(0f, "加载热更程序集并扫描 View 类型");
            Context.MutableHotUpdateAssemblies.Clear();
            if (Context.Config != null)
            {
                Context.MutableHotUpdateAssemblies.AddRange(await HotUpdateAssemblyLoader.LoadAsync(Context.Config.HotUpdateAssemblies));
            }

            if (Context.MutableHotUpdateAssemblies.Count > 0)
            {
                UITypeReflection.Init(Context.MutableHotUpdateAssemblies.ToArray());
            }

            UITypeReflection.Scan(typeof(HotUpdateAssemblySystem).Assembly);
            if (Context.Runner != null)
            {
                UITypeReflection.Scan(Context.Runner.GetType().Assembly);
            }

            Report(1f, "热更程序集加载完成");
        }
    }
}
