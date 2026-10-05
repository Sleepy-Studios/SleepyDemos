using Core.Runtime;
using Core.Runtime.Rendering.Streamline;

namespace Hotfix
{
    public sealed class GraphicsSettingsSetModeAction : IAction
    {
        public StreamlineDlssMode? Mode { get; }
        public GraphicsSettingsSetModeAction(StreamlineDlssMode? mode) => Mode = mode;
    }
}
