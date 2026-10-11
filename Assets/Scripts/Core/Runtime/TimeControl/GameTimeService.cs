using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime.TimeControls
{
    /// 全局演出时间倍率；菜单与转场继续使用未缩放时间，业务暂停保留自己的门闩。
    public static class GameTimeService
    {
        private static readonly List<Scope> Scopes = new();
        private static GameTimeDriver driver;
        private static float originalScale;
        private static float originalFixedStep;

        /// 当前演出倍率，任一作用域定格时为零。
        public static float Scale => Scopes.Count == 0 ? Time.timeScale : ResolveScale();
        /// 给手动模拟宿主使用的缩放帧时长；定格可立即停止同帧后续模拟。
        public static float DeltaTime => Time.unscaledDeltaTime * Scale;

        /// <summary>创建演出作用域；最近创建且未释放的作用域决定倍率。</summary>
        /// <param name="speed">非负有限倍率，默认一；零倍率也会暂停 Unity 物理。</param>
        /// <returns>使用方拥有的作用域，页面或场景退出时必须 Dispose。</returns>
        public static Scope Push(float speed = 1)
        {
            ValidateSpeed(speed);
            if (Scopes.Count == 0)
            {
                originalScale = Time.timeScale;
                originalFixedStep = Time.fixedDeltaTime;
                var root = new GameObject("GameTimeDriver");
                driver = root.AddComponent<GameTimeDriver>();
                if (Application.isPlaying)
                {
                    UnityEngine.Object.DontDestroyOnLoad(root);
                }
            }
            var scope = new Scope(speed);
            Scopes.Add(scope);
            Apply();
            return scope;
        }

        /// 一段有明确所有者的慢动作、快动作或定格演出。
        public sealed class Scope : IDisposable
        {
            private readonly CancellationTokenSource lifetime = new();
            private CancellationTokenSource ramp;
            private bool disposed;
            private float speed;
            private int freezes;

            public float Speed => speed;
            public bool IsFrozen => freezes > 0;
            public bool IsDisposed => disposed;

            private sealed class FreezeLease : IDisposable
            {
                private Scope owner;

                internal FreezeLease(Scope owner)
                {
                    this.owner = owner;
                }

                public void Dispose()
                {
                    var released = owner;
                    owner = null;
                    released?.ReleaseFreeze();
                }
            }

            internal Scope(float speed)
            {
                this.speed = speed;
            }

            /// <summary>立即切换本作用域倍率，取消自己正在进行的速度渐变。</summary>
            /// <param name="value">非负有限倍率。</param>
            public void SetSpeed(float value)
            {
                ThrowIfDisposed();
                ValidateSpeed(value);
                ramp?.Cancel();
                speed = value;
                Apply();
            }

            /// 持续定格直到返回的租约释放；宿主退出释放作用域也会解除全部定格。
            public IDisposable Freeze()
            {
                ThrowIfDisposed();
                freezes++;
                Apply();
                return new FreezeLease(this);
            }

            private void ReleaseFreeze()
            {
                if (disposed)
                {
                    return;
                }
                freezes--;
                Apply();
            }

            /// <summary>按未缩放时间线性渐变；新渐变或 SetSpeed 取消旧渐变。</summary>
            /// <param name="target">目标非负有限倍率。</param>
            /// <param name="seconds">非负有限的真实时长，零立即应用。</param>
            /// <param name="cancellationToken">调用方取消只停止渐变，作用域继续保留当前倍率。</param>
            /// <param name="curve">可选归一化渐变曲线，克隆后使用；为空保持原线性渐变。</param>
            public async UniTask RampSpeedAsync(float target, float seconds, CancellationToken cancellationToken = default, AnimationCurve curve = null)
            {
                ThrowIfDisposed();
                ValidateSpeed(target);
                ValidateDuration(seconds);
                cancellationToken.ThrowIfCancellationRequested();
                var runtimeCurve = curve == null ? null : GameTimePreset.CloneCurve(curve);
                ramp?.Cancel();
                if (seconds == 0)
                {
                    SetSpeed(target);
                    return;
                }
                var operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
                ramp = operation;
                float start = speed;
                float elapsed = 0;
                try
                {
                    while (elapsed < seconds)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, operation.Token);
                        elapsed += Time.unscaledDeltaTime;
                        float normalized = Mathf.Clamp01(elapsed / seconds);
                        float blend = runtimeCurve != null ? runtimeCurve.Evaluate(normalized) : normalized;
                        if (!float.IsFinite(blend))
                        {
                            throw new InvalidOperationException("速度渐变曲线产生了非有限值。");
                        }
                        speed = normalized >= 1 ? target : Mathf.Lerp(start, target, Mathf.Clamp01(blend));
                        Apply();
                    }
                }
                finally
                {
                    if (ReferenceEquals(ramp, operation))
                    {
                        ramp = null;
                    }
                    operation.Dispose();
                }
            }

            /// <summary>按已加载制作预设渐变，本作用域不保存 SO 的可变曲线引用。</summary>
            /// <param name="preset">正式世界时间制作预设。</param>
            /// <param name="cancellationToken">调用方取消，只停止本次渐变。</param>
            public UniTask RampSpeedAsync(GameTimePreset preset, CancellationToken cancellationToken = default)
            {
                if (preset == null)
                {
                    throw new ArgumentNullException(nameof(preset));
                }
                preset.Validate();
                return RampSpeedAsync(preset.TargetSpeed, preset.DurationSeconds, cancellationToken, preset.Curve);
            }

            /// <summary>定格按真实时间结束；不同作用域和同一作用域的定格都须全部解除。</summary>
            /// <param name="seconds">非负有限真实时长，零不改变状态。</param>
            /// <param name="cancellationToken">取消时立即解除本次定格，Dispose 也会取消。</param>
            public async UniTask FreezeAsync(float seconds, CancellationToken cancellationToken = default)
            {
                ThrowIfDisposed();
                ValidateDuration(seconds);
                cancellationToken.ThrowIfCancellationRequested();
                if (seconds == 0)
                {
                    return;
                }
                using var operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
                using var frozen = Freeze();
                float elapsed = 0;
                while (elapsed < seconds)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, operation.Token);
                    elapsed += Time.unscaledDeltaTime;
                }
            }

            private void ThrowIfDisposed()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(Scope));
                }
            }

            /// 取消本作用域的演出并恢复剩余作用域；最后释放时恢复进入前状态。
            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }
                disposed = true;
                Scopes.Remove(this);
                freezes = 0;
                lifetime.Cancel();
                lifetime.Dispose();
                Apply();
                if (Scopes.Count == 0)
                {
                    ReleaseDriver();
                }
            }
        }

        internal static void Tick() => Apply();

        internal static void OnDriverDestroyed(GameTimeDriver value)
        {
            if (!ReferenceEquals(driver, value))
            {
                return;
            }
            driver = null;
            foreach (var scope in Scopes.ToArray())
            {
                scope.Dispose();
            }
            Restore();
        }

        private static float ResolveScale()
        {
            foreach (var scope in Scopes)
            {
                if (scope.IsFrozen)
                {
                    return 0;
                }
            }
            return Scopes[Scopes.Count - 1].Speed;
        }

        private static void Apply()
        {
            if (Scopes.Count > 0)
            {
                Time.timeScale = ResolveScale();
            }
            else if (driver != null)
            {
                Restore();
            }
        }

        private static void Restore()
        {
            Time.timeScale = originalScale;
            Time.fixedDeltaTime = originalFixedStep;
        }

        private static void ReleaseDriver()
        {
            Restore();
            var released = driver;
            driver = null;
            if (released == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(released.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(released.gameObject);
            }
        }

        private static void ValidateSpeed(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "时间倍率必须是非负有限值。");
            }
        }

        private static void ValidateDuration(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "演出时长必须是非负有限值。");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (var scope in Scopes.ToArray())
            {
                scope.Dispose();
            }
            Scopes.Clear();
            driver = null;
        }
    }
}
