using System.Threading;
using Core.Runtime;
using Core.Runtime.Localization;
using Core.Runtime.TimeControls;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix
{
    /// 公共设置行承载四种时间体验，本页只持有自己的时间作用域。
    public sealed class PresentationTimePanel : MonoBehaviour
    {
        [SerializeField] private UISettingRow modeRow;
        [SerializeField] private UISettingRow speedRow;
        [SerializeField] private UISettingRow durationRow;
        private GameTimeService.Scope scope;
        private int mode;
        private float speed = 0.25f;
        private float duration = 1;

        /// 绑定制作行，不直接写 Time.timeScale 或 fixedDeltaTime。
        public void Initialize()
        {
            modeRow.SetLabel("Presentation.TimeMode");
            speedRow.SetLabel("Presentation.Speed");
            durationRow.SetLabel("Presentation.Duration");
            ResetControls();
        }

        /// <summary>应用本页选择，渐变和短定格使用未缩放时间。</summary>
        /// <param name="token">停止或页面退出时取消。</param>
        /// <returns>渐变或短定格结束，倍率作用域保留到本页停止。</returns>
        public async UniTask PlayAsync(CancellationToken token)
        {
            Stop();
            scope = GameTimeService.Push(mode == 2 ? 0.1f : mode == 3 ? 1 : speed);
            if (mode == 2)
            {
                await scope.RampSpeedAsync(speed, duration, token);
            }
            else if (mode == 3)
            {
                await scope.FreezeAsync(duration, token);
            }
        }

        /// 解除自己拥有的倍率和定格，不影响其它 owner。
        public void Stop()
        {
            scope?.Dispose();
            scope = null;
        }

        /// 恢复慢动作制作初始选择。
        public void ResetControls()
        {
            Stop();
            mode = 0;
            modeRow.SetOptions(new[] { LanguageService.Get("Presentation.Slow"), LanguageService.Get("Presentation.Fast"),
                LanguageService.Get("Presentation.Ramp"), LanguageService.Get("Presentation.Freeze") }, 0, OnMode);
            speed = 0.25f;
            duration = 1;
            speedRow.SetSlider(new Vector2(0.05f, 3), speed, OnSpeed);
            speedRow.SetAvailable(true);
            durationRow.SetSlider(new Vector2(0.1f, 5), duration, OnDuration);
            OnSpeed(speed);
            OnDuration(duration);
        }

        private void OnMode(int index)
        {
            mode = index;
            speed = index == 0 ? 0.25f : index == 1 || index == 2 ? 2 : 1;
            speedRow.SetSlider(new Vector2(0.05f, 3), speed, OnSpeed);
            speedRow.SetAvailable(index != 3);
            OnSpeed(speed);
        }
        private void OnSpeed(float value)
        {
            speed = value;
            speedRow.SetValue(LanguageService.Format("Presentation.SpeedValue", value));
            if (scope != null && !scope.IsDisposed && mode != 3)
            {
                scope.SetSpeed(value);
            }
        }
        private void OnDuration(float value)
        {
            duration = value;
            durationRow.SetValue(LanguageService.Format("Presentation.Seconds", value));
        }
        private void OnDestroy() => Stop();
    }
}
