using System.Collections.Generic;

namespace Core.Runtime
{
    public sealed class PrepareStartupState : StartupStateBase
    {
        public override int StateId => (int)StartupStateId.Prepare;
        public override int NextStateId => (int)StartupStateId.Resource;
        public override string Title => "启动准备";

        protected override void CreateSystems(List<StartupSystemBase> systems)
        {
            systems.Add(new PrepareRuntimeSystem(this));
        }
    }
}
