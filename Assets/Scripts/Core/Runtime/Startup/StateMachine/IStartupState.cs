using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    public interface IStartupState
    {
        int StateId { get; }
        int NextStateId { get; }
        string Title { get; }
        string Description { get; }
        float Progress { get; }

        void SetContext(StartupContext context);
        UniTask EnterAsync();
        UniTask ExitAsync();
    }
}
