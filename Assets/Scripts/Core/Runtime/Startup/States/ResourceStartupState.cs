using System.Collections.Generic;

namespace Core.Runtime
{
    public sealed class ResourceStartupState : StartupStateBase
    {
        public override int StateId => (int)StartupStateId.Resource;
        public override int NextStateId => (int)StartupStateId.BeforeHotfix;
        public override string Title => "资源初始化";

        protected override void CreateSystems(List<StartupSystemBase> systems)
        {
            systems.Add(new YooAssetInitializeSystem(this));
            systems.Add(new ResourceDownloadSystem(this));
        }
    }
}
