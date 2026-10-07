using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.WallSqueeze
{
    /// 暂停与结果 Modal 的保存控件，不产生规则状态。
    public sealed class WallSqueezeMenuPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button next;
        [SerializeField] private Button retry;
        [SerializeField] private Button hub;
        private WallSqueezeWorld world;
        /// 当前模式下可用的首个菜单焦点。
        public GameObject FirstSelection => continueButton.gameObject.activeSelf ? continueButton.gameObject : next.gameObject.activeSelf ? next.gameObject : retry.gameObject;

        /// <summary>每次显示前绑定，隐藏或销毁时传 null 释放来源。</summary>
        /// <param name="owner">当前场景。</param>
        public void Bind(WallSqueezeWorld owner)
        {
            world = owner;
            continueButton.onClick.RemoveListener(Continue);
            next.onClick.RemoveListener(Next);
            retry.onClick.RemoveListener(Retry);
            hub.onClick.RemoveListener(Hub);
            if (owner != null)
            {
                continueButton.onClick.AddListener(Continue);
                next.onClick.AddListener(Next);
                retry.onClick.AddListener(Retry);
                hub.onClick.AddListener(Hub);
                Render();
            }
        }

        /// 回显暂停或结果状态，不推进规则。
        public void Render()
        {
            if (world?.Simulation == null)
            {
                return;
            }
            var result = world.Simulation.Result;
            bool timedOut = world.Simulation.Reason == WallSqueezeLossReason.Timeout;
            title.text = result == WallSqueezeResult.Won ? "夹爆成功" : result == WallSqueezeResult.Lost ? timedOut ? "时间到了" : "住户被夹碎了" : "已暂停";
            detail.text = result == WallSqueezeResult.Won
                ? world.Simulation.ResidentsAlive > 0 ? "红怪已全部消灭，蓝色住户安全。" : "红怪已全部消灭。"
                : result == WallSqueezeResult.Lost ? timedOut ? "先救住户，再从上下面夹顶墙怪。" : "先把蓝色住户推到安全的空间，再夹击红怪。" : "已暂停，选择继续。";
            continueButton.gameObject.SetActive(result == WallSqueezeResult.Playing);
            continueButton.interactable = world.CanContinue;
            next.gameObject.SetActive(result == WallSqueezeResult.Won && world.LevelIndex + 1 < world.LevelCount);
        }

        private void Continue() => world?.Dispatch(WallSqueezeCommand.Continue);
        private void Next() => world?.Dispatch(WallSqueezeCommand.Next);
        private void Retry() => world?.Dispatch(WallSqueezeCommand.Retry);
        private void Hub() => world?.Dispatch(WallSqueezeCommand.Hub);
    }
}
