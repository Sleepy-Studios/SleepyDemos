using Cfg;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.AppDelegate
{
    /// Hotfix 启动期的 Luban 配置初始化系统。
    public sealed class LubanConfigSystem : IHotfixBootSystem
    {
        /// 启动系统名称。
        public string Name => "LubanConfigSystem";

        /// 加载界面显示的系统说明。
        public string Description => "加载 Luban 业务配置";

        /// <summary>
        /// 在后续业务系统启动前完整加载 Luban 客户端配置。
        /// </summary>
        /// <param name="context">当前 Hotfix 启动上下文。</param>
        public async UniTask RunAsync(HotfixStartupContext context)
        {
            if (!ResourceServices.Default.IsInitialized)
            {
                throw new System.InvalidOperationException("Luban 配置加载前资源服务尚未初始化。");
            }

            await LubanConfigService.InitializeAsync();
            Debug.Log($"[{Name}] {Description}完成。表数量：{Tables.TableCount}。");
        }
    }
}
