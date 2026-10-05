using Core.Runtime;

namespace Hotfix.BlockPorters
{
    public abstract class BlockPortersAction : IAction { }
    public sealed class BlockPortersLoadLevelAction : BlockPortersAction
    {
        public int Index { get; }
        public bool ChooseTheme { get; }
        public BlockPortersLoadLevelAction(int index, bool chooseTheme = true) { Index = index; ChooseTheme = chooseTheme; }
    }
    public sealed class BlockPortersDispatchAction : BlockPortersAction
    {
        public int Column { get; }
        public BlockPortersDispatchAction(int column) => Column = column;
    }
    public sealed class BlockPortersTogglePauseAction : BlockPortersAction { }
    public sealed class BlockPortersToggleSoundAction : BlockPortersAction { }
    public sealed class BlockPortersRestartAction : BlockPortersAction { }
    public sealed class BlockPortersNextLevelAction : BlockPortersAction { }
    public sealed class BlockPortersOpenSettingsAction : BlockPortersAction { }
    public sealed class BlockPortersCloseSettingsAction : BlockPortersAction { }
    public sealed class BlockPortersExitAction : BlockPortersAction { }
    internal sealed class BlockPortersRestoreAction : BlockPortersAction { }
    public sealed class BlockPortersUnlockSlotAction : BlockPortersAction
    {
        public int Side { get; }
        public BlockPortersUnlockSlotAction(int side) => Side = side;
    }
    internal sealed class BlockPortersRewardResultAction : BlockPortersAction
    {
        internal int Version { get; }
        internal int Side { get; }
        internal PorterRewardResult Result { get; }
        internal BlockPortersRewardResultAction(int version, int side, PorterRewardResult result) { Version = version; Side = side; Result = result; }
    }
}
