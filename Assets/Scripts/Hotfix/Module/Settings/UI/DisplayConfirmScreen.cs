using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix
{
    /// 显示确认与服务同一十五秒窗口，加载耗时也计入倒计时。
    public sealed class DisplayConfirmScreen : MonoBehaviour
    {
        [SerializeField] private Button keep;
        [SerializeField] private Button revert;
        [SerializeField] private UIMenuScope menu;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text countdown;
        [SerializeField] private TMP_Text keepLabel;
        [SerializeField] private TMP_Text revertLabel;
        private DisplayConfirmationRequest request;
        private int shownSeconds = -1;

        /// 初始化制作引用，点击或菜单返回只完成本次请求。
        public void Initialize()
        {
            LanguageService.SetText(title, "Settings.KeepDisplay");
            LanguageService.SetText(keepLabel, "Settings.Keep");
            LanguageService.SetText(revertLabel, "Settings.Revert");
            keep.onClick.AddListener(Keep);
            revert.onClick.AddListener(Revert);
            menu.Canceled += Revert;
        }

        /// <summary>交付本次共享完成源，不重新开始十五秒计时。</summary>
        /// <param name="value">页面外设置事务创建的请求。</param>
        public void Bind(DisplayConfirmationRequest value)
        {
            request = value;
            shownSeconds = -1;
            Tick();
        }

        /// 使用真实时间更新；世界被冻结时仍可确认或超时回滚。
        public void Tick()
        {
            if (request == null)
            {
                return;
            }
            int seconds = Mathf.Max(0, Mathf.CeilToInt((float)(request.Deadline - Time.realtimeSinceStartupAsDouble)));
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                LanguageService.SetText(countdown, "Settings.KeepSeconds", null, seconds);
            }
            if (seconds == 0)
            {
                Revert();
            }
        }

        private void Keep()
        {
            if (request != null)
            {
                request.Completion.TrySetResult(Time.realtimeSinceStartupAsDouble < request.Deadline);
            }
        }
        /// 隐藏、销毁、取消和显式恢复都归还原显示设置。
        public void Revert() => request?.Completion.TrySetResult(false);

        private void OnDestroy()
        {
            Revert();
            if (keep != null)
            {
                keep.onClick.RemoveListener(Keep);
            }
            if (revert != null)
            {
                revert.onClick.RemoveListener(Revert);
            }
            if (menu != null)
            {
                menu.Canceled -= Revert;
            }
        }
    }

    /// 设置事务持有的完成源，不持有任何 View 或控件。
    public sealed class DisplayConfirmationRequest
    {
        public double Deadline { get; }
        internal UniTaskCompletionSource<bool> Completion { get; } = new();

        /// <summary>记录服务开始确认时的真实时间截止点。</summary>
        /// <param name="deadline">Time.realtimeSinceStartupAsDouble 的十五秒截止时间。</param>
        public DisplayConfirmationRequest(double deadline) => Deadline = deadline;
    }
}
