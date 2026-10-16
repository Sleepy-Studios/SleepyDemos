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

        /// <summary>等待直启宿主或正式入口完成公共服务初始化。</summary>
        /// <param name="cancellationToken">取消当前等待，退出时不重新启动服务。</param>
        /// <returns>是否已有可用场景导航。</returns>
        public static async UniTask<bool> EnsureReadyAsync(CancellationToken cancellationToken)
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
            GameSceneLanguageScope context = null;
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
                await Core.Runtime.Settings.GameSettingsService.InitializeAsync();
                await UIManager.Instance.InitializeAsync();
                UITypeReflection.Init(typeof(View).Assembly, typeof(DemoIslandEditorBootstrap).Assembly);
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name.StartsWith("Hotfix.", System.StringComparison.Ordinal) &&
                        !assembly.GetName().Name.EndsWith(".Editor", System.StringComparison.Ordinal))
                    {
                        UITypeReflection.Scan(assembly);
                    }
                }
                var startupContext = new StartupContext(config, null, this);
                await HotfixBootService.RunBootSystems(new HotfixStartupContext(startupContext));
                UIManager.Instance.RegisterWorldTransitionProvider(new HotfixWorldTransitionProvider());
                var scene = SceneManager.GetActiveScene();
                if (!GameSceneCatalog.TryFindSceneId(scene.path, out var sceneId))
                {
                    throw new System.InvalidOperationException("当前直启场景未在 GameSceneCatalog 登记：" + scene.path);
                }
                GameSceneCatalog.TryGet(sceneId, out var definition);
                context = await GameSceneLanguageScope.LoadAsync(definition, cancellationToken);
                context?.BindScene(scene);
                var runtime = new EditorDirectGameSceneRuntime(ResourceServices.CreateSceneLoader(), scene);
                GameSceneNavigator.InitializeEditorDirect(runtime, sceneId, context);
                context = null;
                readySource.TrySetResult(true);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                readySource.TrySetResult(false);
            }
            finally
            {
                context?.Dispose();
            }
        }
#endif
    }
}
