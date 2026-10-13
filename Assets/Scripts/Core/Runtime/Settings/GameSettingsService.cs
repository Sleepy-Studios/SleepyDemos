using System;
using System.Collections.Generic;
using System.Threading;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Core.Runtime.Rendering;
using Core.Runtime.Rendering.Streamline;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime.Settings
{
    /// 本机公共设置的唯一保存者；制作配置与实际运行状态分开维护。
    public static class GameSettingsService
    {
        public const string PreferenceKey = "SleepyDemos.Settings";
        public const string ConfigAddress = "LoadResources/Config/Settings/GameSettingsConfig";
        private static GameSettingsSnapshot current = new();
        private static GameSettingsConfig config;
        private static IResourceLoader configLoader;
        private static UniTask initialization;
        private static bool hasInitialization;
        private static bool applying;
        private static GameSettingsRuntimeDriver driver;

        /// 返回独立候选；需要逐帧消费的宿主应在 Changed 时更新自己的快照。
        public static GameSettingsSnapshot Current => current.Copy();
        /// 提示只读取一个枚举，不为每次显示复制整份设置。
        public static GamepadStyle CurrentGamepadStyle => current.Input.GamepadStyle;
        /// 配置与偏好均已完成初始化。
        public static bool IsInitialized { get; private set; }
        /// 正在提交，界面不能再次提交。
        public static bool IsApplying => applying;
        /// 正式制作配置，业务只读其公开入口。
        public static GameSettingsConfig Config => config;
        /// 制作侧编辑范围的独立副本。
        public static SettingsOptions Options => config != null ? config.GetOptions()
            : throw new InvalidOperationException("公共设置尚未初始化。");
        /// 可改键宿主的只读制作身份；用 InputRebindSession 克隆动作后编辑。
        public static IReadOnlyList<InputSettingsCatalog.Entry> InputHosts => config != null ? config.Inputs.Entries
            : throw new InvalidOperationException("公共设置尚未初始化。");
        /// 最近一次设置应用的反馈。
        public static string Feedback { get; private set; }
        /// 公共参数已成功提交；不是语言或文本刷新事件。
        public static event Action<GameSettingsSnapshot> Changed;

        /// 加载正式公共配置；并发启动与 Demo 直启共用一次初始化。
        public static UniTask InitializeAsync()
        {
            if (IsInitialized)
            {
                return UniTask.CompletedTask;
            }

            if (!hasInitialization)
            {
                hasInitialization = true;
                var source = new UniTaskCompletionSource();
                initialization = source.Task;
                InitializeSharedAsync(source).Forget();
            }

            return initialization;
        }

        private static async UniTaskVoid InitializeSharedAsync(UniTaskCompletionSource source)
        {
            try
            {
                await InitializeCoreAsync();
                source.TrySetResult();
            }
            catch (Exception exception)
            {
                source.TrySetException(exception);
            }
        }

        private static async UniTask InitializeCoreAsync()
        {
            try
            {
                configLoader = ResourceServices.CreateLoader();
                config = await configLoader.LoadAssetAsync<GameSettingsConfig>(ConfigAddress);
                if (config == null)
                {
                    throw new InvalidOperationException("公共设置配置加载失败：" + ConfigAddress);
                }

                config.Validate();
                var defaults = config.GetDefaults();
                current = LocalDataManager.LoadData(PreferenceKey, defaults, ValidateLoaded);
                Feedback = null;

                LanguageService.Initialize(config.Language, config.Fonts, current.Language);
                await ApplyRuntimeAsync(current, CancellationToken.None);
                var driverObject = new GameObject("Game Settings Runtime");
                UnityEngine.Object.DontDestroyOnLoad(driverObject);
                driver = driverObject.AddComponent<GameSettingsRuntimeDriver>();
                IsInitialized = true;
                PublishChanged();
            }
            catch
            {
                DisplaySettingsRuntime.Shutdown();
                PostProcessRuntime.Shutdown();
                hasInitialization = false;
                configLoader?.Dispose();
                configLoader = null;
                config = null;
                throw;
            }
        }

        /// 取得当前制作默认值。
        public static GameSettingsSnapshot GetDefaults()
        {
            if (config == null)
            {
                throw new InvalidOperationException("公共设置尚未初始化。");
            }

            return config.GetDefaults();
        }

        /// <summary>应用并保存独立候选；显示确认取消或保存失败恢复旧偏好。</summary>
        /// <param name="candidate">页面的完整候选；方法内复制，调用方后续修改不影响事务。</param>
        /// <param name="confirmDisplay">显示改变后等待保留确认；为空时拒绝实际显示模式变更。</param>
        /// <param name="allowLanguageChange">仅 Hub 协调流程允许设为 true，普通 Demo 设置不改变语言。</param>
        /// <param name="cancellationToken">取消本次请求，回滚本身不受此令牌取消。</param>
        /// <returns>成功、取消或失败反馈。</returns>
        public static async UniTask<SettingsApplyResult> ApplyAsync(GameSettingsSnapshot candidate,
            Func<CancellationToken, UniTask<bool>> confirmDisplay = null, bool allowLanguageChange = false,
            CancellationToken cancellationToken = default)
        {
            if (applying)
            {
                return new SettingsApplyResult(false, "设置正在应用，请稍候。");
            }

            await InitializeAsync();
            if (applying)
            {
                return new SettingsApplyResult(false, "设置正在应用，请稍候。");
            }

            var previous = current.Copy();
            var previousRuntime = previous.Copy();
            previousRuntime.Graphics = DisplaySettingsRuntime.Capture();
            applying = true;
            try
            {
                var next = candidate?.Copy();
                Validate(next);
                if (next.Language != previous.Language && !allowLanguageChange)
                {
                    throw new InvalidOperationException("请返回 Hub 切换语言。");
                }

                bool displayChanged = DisplaySettingsRuntime.RequiresConfirmation(previousRuntime.Graphics, next.Graphics);
                if (displayChanged && confirmDisplay == null)
                {
                    throw new InvalidOperationException("显示模式变更需要保留确认。");
                }

                cancellationToken.ThrowIfCancellationRequested();
                await ApplyRuntimeAsync(next, cancellationToken);
                if (displayChanged)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(15), DelayType.UnscaledDeltaTime);
                    bool keep = await confirmDisplay(timeout.Token).AttachExternalCancellation(timeout.Token);
                    if (!keep)
                    {
                        throw new OperationCanceledException("已恢复原显示设置。");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                LocalDataManager.SaveData(PreferenceKey, next, Validate);
                LanguageService.SelectLanguage(next.Language);
                current = next;
                Feedback = null;
                PublishChanged();
                return new SettingsApplyResult(true);
            }
            catch (Exception exception)
            {
                try
                {
                    await ApplyRuntimeAsync(previousRuntime, CancellationToken.None);
                    LanguageService.SelectLanguage(previous.Language);
                }
                catch (Exception rollbackError)
                {
                    Debug.LogException(rollbackError);
                }

                Feedback = exception is OperationCanceledException ? "设置已取消，保留原设置。" : "设置应用失败：" + exception.Message;
                return new SettingsApplyResult(false, Feedback, exception is OperationCanceledException);
            }
            finally
            {
                applying = false;
            }
        }

        private static void ValidateLoaded(GameSettingsSnapshot value)
        {
            // 旧偏好缺少图标字段或保存为0时，仅补Xbox，不丢弃其余玩家设置。
            if (value?.Input != null && (int)value.Input.GamepadStyle == 0)
            {
                value.Input.GamepadStyle = GamepadStyle.Xbox;
            }
            Validate(value);
        }

        private static void Validate(GameSettingsSnapshot value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            value.Validate();
            config?.GetOptions().ValidateCandidate(value);
            LanguageService.ValidateLanguage(value.Language);
            config?.Inputs.ValidateOverrides(value.BindingOverrides);
            if (value.Graphics.DlssMode != 0 && StreamlineRuntime.DecodePreference(value.Graphics.DlssMode) == null)
            {
                throw new ArgumentException("DLSS 档位无效。");
            }
        }

        private static async UniTask ApplyRuntimeAsync(GameSettingsSnapshot value, CancellationToken token)
        {
            await DisplaySettingsRuntime.ApplyAsync(value.Graphics, config.QualityPresets, token);
            AudioSettingsRuntime.Apply(value.Audio);
            PostProcessRuntime.ApplySettings(value.Graphics, value.Accessibility);
        }

        private static void PublishChanged()
        {
            if (Changed == null)
            {
                return;
            }

            foreach (Action<GameSettingsSnapshot> callback in Changed.GetInvocationList())
            {
                try
                {
                    callback(current.Copy());
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// 释放由本服务持有的制作资源。
        public static void Shutdown()
        {
            var previousDriver = driver;
            driver = null;
            if (previousDriver != null)
            {
                UnityEngine.Object.Destroy(previousDriver.gameObject);
            }
            DisplaySettingsRuntime.Shutdown();
            PostProcessRuntime.Shutdown();
            configLoader?.Dispose();
            configLoader = null;
            config = null;
            current = new GameSettingsSnapshot();
            IsInitialized = false;
            hasInitialization = false;
            applying = false;
            Changed = null;
        }

        internal static void ReleaseDriver(GameSettingsRuntimeDriver value)
        {
            if (driver == value)
            {
                Shutdown();
            }
        }
    }

    internal sealed class GameSettingsRuntimeDriver : MonoBehaviour
    {
        private void OnDestroy()
        {
            GameSettingsService.ReleaseDriver(this);
        }
    }
}
