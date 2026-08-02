using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public sealed class CoreEntrance : MonoBehaviour
    {
        [SerializeField] private HotfixConfig hotfixConfig;
        [SerializeField] private StartupLoadingView loadingView;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private void Awake()
        {
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            StartAsync().Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            if (loadingView == null)
            {
                Debug.LogError("[CoreEntrance] 请在启动场景手动挂载 StartupLoadingView。");
                return;
            }

            var pipeline = new StartupPipeline(hotfixConfig, loadingView, this);
            await pipeline.RunAsync();
        }
    }
}
