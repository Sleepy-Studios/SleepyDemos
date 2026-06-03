using Core.Runtime;
using Cysharp.Threading.Tasks;

namespace Hotfix.AppDelegate
{
    public interface IHotfixBootSystem
    {
        string Name { get; }
        string Description { get; }
        UniTask RunAsync(HotfixStartupContext context);
    }
}
