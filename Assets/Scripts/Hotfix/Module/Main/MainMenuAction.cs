using Core.Runtime;
using Hotfix.SceneManagement;

namespace Hotfix
{
    public abstract class MainMenuAction : IAction { }
    public sealed class MainMenuSelectAction : MainMenuAction
    {
        public int Index { get; }
        public MainMenuSelectAction(int index) => Index = index;
    }
    public sealed class MainMenuEnterAction : MainMenuAction
    {
        public GameSceneId Target { get; }
        public MainMenuEnterAction(GameSceneId target) => Target = target;
    }
    internal sealed class MainMenuAvailabilityAction : MainMenuAction
    {
        internal bool Transitioning { get; }
        internal MainMenuAvailabilityAction(bool transitioning) => Transitioning = transitioning;
    }
    internal sealed class MainMenuFeedbackAction : MainMenuAction
    {
        internal string Message { get; }
        internal MainMenuFeedbackAction(string message) => Message = message;
    }
}
