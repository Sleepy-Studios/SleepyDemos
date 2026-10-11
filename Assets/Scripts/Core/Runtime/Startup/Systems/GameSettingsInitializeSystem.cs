using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    /// 在业务页面创建前准备公共设置、词表和字体资源。
    public sealed class GameSettingsInitializeSystem : StartupSystemBase
    {
        /// <summary>插入资源就绪后的公共设置初始化。</summary>
        /// <param name="state">所属正式启动状态。</param>
        public GameSettingsInitializeSystem(StartupStateBase state) : base(state)
        {
        }

        /// 加载正式配置并应用本机偏好。
        public override async UniTask ExecuteAsync()
        {
            Report(0f, "加载公共设置");
            await GameSettingsService.InitializeAsync();
            Report(1f, "公共设置就绪");
        }
    }
}
