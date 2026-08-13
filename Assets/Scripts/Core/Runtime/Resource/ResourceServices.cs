using UnityEngine;

namespace Core.Runtime
{
    public static class ResourceServices
    {
        private static IResourceService defaultService;

        public static IResourceService Default
        {
            get
            {
                if (defaultService == null)
                {
                    RegisterDefault(new YooAssetResourceService());
                }

                return defaultService;
            }
        }

        public static void RegisterDefault(IResourceService service)
        {
            if (service == null)
            {
                Debug.LogError("[Resource] 注册默认资源服务失败：service 为空。");
                return;
            }

            defaultService = service;
        }

        public static IResourceLoader CreateLoader()
        {
            return Default.CreateLoader();
        }

        /// 创建使用默认资源服务的场景资源加载器。
        public static IResourceSceneLoader CreateSceneLoader()
        {
            return Default.CreateSceneLoader();
        }
    }
}
