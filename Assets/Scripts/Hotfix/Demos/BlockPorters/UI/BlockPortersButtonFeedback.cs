using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hotfix.BlockPorters
{
    /// 队列递补的业务回弹；交互五态由公共 UIStateInteraction 表现。
    public sealed class BlockPortersButtonFeedback : MonoBehaviour
    {
        private float pulse;
        private void OnDisable() { pulse = 0; transform.localScale = Vector3.one; }
        /// 队列递补时触发一次短促回弹。
        public void Pulse() => pulse = .18f;
        /// <summary>由 HUD 每帧调用，避免为各按钮维护独立 Update。</summary>
        /// <param name="delta">不受玩法暂停影响的界面时间。</param>
        public void Tick(float delta)
        {
            if (!gameObject.activeInHierarchy) return;
            pulse = Mathf.Max(0, pulse - delta);
            float target = 1 + Mathf.Sin(pulse / .18f * Mathf.PI) * .035f;
            transform.localScale = Vector3.one * Mathf.MoveTowards(transform.localScale.x, target, delta * 2);
        }
    }
}
