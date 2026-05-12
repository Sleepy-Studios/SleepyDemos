using System.Collections.Generic;

namespace Core.Runtime
{
    public sealed class BeforeHotfixStartupState : StartupStateBase
    {
        public override int StateId => (int)StartupStateId.BeforeHotfix;
        public override int NextStateId => -1;
        public override string Title => "进入热更";

        protected override void CreateSystems(List<StartupSystemBase> systems)
        {
            systems.Add(new UIInitializeSystem(this));
            systems.Add(new HybridMetadataSystem(this));
            systems.Add(new HotUpdateAssemblySystem(this));
            systems.Add(new RuntimeServiceRegisterSystem(this));
            systems.Add(new HotfixEntrySystem(this));
        }
    }
}
