using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Core.Runtime
{
    /// 按绝对结束时间刷新文本；每轮任务独立取消，不干涉 UI Transition。
    [DisallowMultipleComponent]
    public sealed class UICountdown : MonoBehaviour
    {
        [SerializeField] private TMP_Text targetText;
        [SerializeField, Min(0.01f)] private float updateInterval = 0.1f;
        private CancellationTokenSource countdownCancellation;
        private int generation;
        /// 本轮正常到期时触发一次；取消、禁用和销毁不触发。
        public event Action Completed;
        /// 当前是否有未结束的计时。
        public bool IsRunning => countdownCancellation != null;

        /// <summary>启动倒计时；立即刷新，已过期时同步完成。</summary>
        /// <param name="endTimestampMs">绝对 Unix 毫秒结束时间。</param>
        /// <param name="format">显示格式，默认中文单位。</param>
        /// <param name="text">可覆盖 Prefab 绑定文本；null 使用已绑定文本。</param>
        public void StartCountdown(long endTimestampMs, TimeDisplayFormat format = TimeDisplayFormat.AutoWithUnits, TMP_Text text = null)
        {
            StopCountdown();
            if (text != null) targetText = text;
            if (!isActiveAndEnabled) return;
            var source = new CancellationTokenSource();
            countdownCancellation = source;
            RunAsync(endTimestampMs, format, ++generation, source).Forget();
        }

        /// 取消本轮倒计时，保留当前文本。
        public void StopCountdown()
        {
            generation++;
            var previous = countdownCancellation;
            countdownCancellation = null;
            previous?.Cancel();
        }

        private async UniTask RunAsync(long endTimestampMs, TimeDisplayFormat format, int version, CancellationTokenSource source)
        {
            try
            {
                long previousSeconds = -1;
                while (!source.IsCancellationRequested && generation == version)
                {
                    long seconds = TimeUtil.GetRemainingSeconds(endTimestampMs);
                    if (seconds != previousSeconds && targetText != null) targetText.SetText(TimeUtil.FormatSeconds(seconds, format));
                    previousSeconds = seconds;
                    if (seconds == 0)
                    {
                        // 回调可以重入启动下一轮，先解除旧轮所有权。
                        countdownCancellation = null;
                        Completed?.Invoke();
                        return;
                    }
                    await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0.01f, updateInterval)), true, cancellationToken: source.Token);
                }
            }
            catch (OperationCanceledException) when (source.IsCancellationRequested) { }
            finally
            {
                if (generation == version) countdownCancellation = null;
                source.Dispose();
            }
        }

        private void OnDisable() => StopCountdown();
        private void OnDestroy() => StopCountdown();
    }
}
