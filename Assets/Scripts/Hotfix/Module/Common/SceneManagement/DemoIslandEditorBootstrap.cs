using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.AppDelegate;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hotfix.SceneManagement
{
    /// <summary>仅在 Unity Editor 直接打开 Demo 场景 Play 时补齐最小正式运行时。</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class DemoIslandEditorBootstrap : MonoBehaviour
    {
        private static DemoIslandEditorBootstrap active;
        private static UniTaskCompletionSource<bool> readySource = new();

        private void Awake()
        {
            if (GameSceneNavigator.Instance != null)
            {
                readySource.TrySetResult(true);
                enabled = false;
                return;
            }

#if UNITY_EDITOR
            active = this;
            DontDestroyOnLoad(gameObject);
            InitializeDirectAsync(this.GetCancellationTokenOnDestroy()).Forget();
#else
            readySource.TrySetResult(false);
            enabled = false;
#endif
        }

        private void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }
        }

        internal static async UniTask<bool> EnsureReadyAsync(CancellationToken cancellationToken)
        {
            if (GameSceneNavigator.Instance != null)
            {
                return true;
            }

            if (active == null)
            {
                await UniTask.Yield();
                if (GameSceneNavigator.Instance != null)
                {
                    return true;
                }
            }

            return await readySource.Task.AttachExternalCancellation(cancellationToken);
        }

        internal static void ReleaseForOfficialStartup()
        {
            if (active != null)
            {
                Destroy(active.gameObject);
            }

            active = null;
            readySource = new UniTaskCompletionSource<bool>();
        }

#if UNITY_EDITOR
        private async UniTaskVoid InitializeDirectAsync(CancellationToken cancellationToken)
        {
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<HotfixConfig>(
                    "Assets/LoadResources/Config/HotfixConfig.asset");
                if (config == null)
                {
                    throw new System.InvalidOperationException("找不到 HotfixConfig.asset。");
                }

                if (!ResourceServices.Default.IsInitialized)
                {
                    await ResourceServices.Default.InitializeAsync(new ResourceInitializeOptions(
                        config.PackageName,
                        config.PlayMode,
                        config.BaseServerURL));
                }

                cancellationToken.ThrowIfCancellationRequested();
                await UIManager.Instance.InitializeAsync();
                UITypeReflection.Scan(typeof(DemoIslandEditorBootstrap).Assembly);
                var startupContext = new StartupContext(config, null, this);
                await HotfixBootService.RunBootSystems(new HotfixStartupContext(startupContext));
                UIManager.Instance.RegisterWorldTransitionProvider(new HotfixWorldTransitionProvider());
                var runtime = new EditorDirectGameSceneRuntime(
                    ResourceServices.CreateSceneLoader(),
                    SceneManager.GetActiveScene());
                GameSceneNavigator.InitializeEditorDirect(runtime);
                readySource.TrySetResult(true);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                readySource.TrySetResult(false);
            }
        }
#endif
    }
}
