using Core.Runtime;

namespace Hotfix.Dlss
{
    public abstract class DlssAction : IAction { }
    public sealed class DlssControlAction : DlssAction
    {
        public string Command { get; }
        public DlssControlAction(string command) => Command=command;
    }
    public sealed class DlssSprintAction : DlssAction
    {
        public bool Held { get; }
        public DlssSprintAction(bool held) => Held=held;
    }
    internal sealed class DlssSettingsClosedAction : DlssAction { }
}
