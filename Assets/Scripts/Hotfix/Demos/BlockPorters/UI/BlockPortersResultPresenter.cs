using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Hotfix.BlockPorters
{
    /// 独立结算页，观察会话结果并保留模拟奖励、重开与下一关入口。
    public sealed class BlockPortersResultPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI resultTitle;
        [SerializeField] private TextMeshProUGUI resultDescription;
        [SerializeField] private Button[] resultExtraButtons;
        [SerializeField] private Button next;
        [SerializeField] private Button resultRestart;
        [SerializeField] private Button resultExit;
        [SerializeField] private TextMeshProUGUI nextLabel;
        private BlockPortersController owner;
        private UIMenuScope menu;
        private void Awake()
        {
            menu = GetComponent<UIMenuScope>();
            next.onClick.AddListener(() => owner?.NextLevel());
            resultRestart.onClick.AddListener(() => owner?.Restart());
            resultExit.onClick.AddListener(Exit);
            for (int side = 0; side < resultExtraButtons.Length; side++)
            { int index = side; resultExtraButtons[side].onClick.AddListener(() => owner?.RequestUnlockSlot(index)); }
        }
        /// <summary>显示前绑定结算会话。</summary>
        /// <param name="controller">当前玩法宿主。</param>
        public void Bind(BlockPortersController controller)
        {
            Unbind(); owner = controller; owner.Changed += Refresh; menu.Canceled += Exit; Refresh();
        }
        /// 隐藏时释放订阅。
        public void Unbind()
        {
            if (owner != null) owner.Changed -= Refresh;
            if (menu != null) menu.Canceled -= Exit;
            owner = null;
        }
        private void Exit() => owner?.ReturnToHub();
        private void Refresh()
        {
            if (owner == null) return;
            var session = owner.Session;
            bool failed = session.Status == BlockPortersStatus.Failed;
            resultTitle.text = failed ? "队伍堵住啦" : "搬得真漂亮！";
            resultDescription.text = failed ? session.UnlockedExtraSlots == 3 ? "任务位都已开放。\n重新挑战，先搬开外层吧！" : "解锁额外任务位继续搬运，\n或者重新挑战，先搬开外层。" : "这一幅图案已经全部搬空。\n下一幅，也一起轻松完成吧！";
            next.gameObject.SetActive(!failed);
            nextLabel.text = owner.LevelIndex == owner.LevelCount - 1 ? "再玩一轮" : "下一关";
            resultRestart.interactable = resultExit.interactable = next.interactable = !owner.IsExiting;
            for (int side = 0; side < resultExtraButtons.Length; side++)
            {
                bool unlocked = session.IsSlotAvailable(side + 5);
                resultExtraButtons[side].gameObject.SetActive(failed && !unlocked);
                resultExtraButtons[side].interactable = !unlocked && !owner.IsExiting && !owner.IsRewardPending;
            }
        }
        private void OnDestroy() => Unbind();
    }
}
