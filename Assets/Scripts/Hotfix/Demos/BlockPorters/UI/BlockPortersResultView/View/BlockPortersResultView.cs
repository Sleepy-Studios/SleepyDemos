using Cysharp.Threading.Tasks;
using Core.Runtime;
using Hotfix.BlockPorters;
using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Hotfix
{
    /// 独立结算页，观察会话结果并保留模拟奖励、重开与下一关入口。
    [Module("BlockPorters")]
    [UIBind("BlockPortersResultView")]
    public sealed partial class BlockPortersResultView : View
    {
        private TextMeshProUGUI resultTitle;
        private TextMeshProUGUI resultDescription;
        private Button[] resultExtraButtons;
        private Button next;
        private Button resultRestart;
        private Button resultExit;
        private TextMeshProUGUI nextLabel;
        private BlockPortersData owner;
        private UIMenuScope menu;
        protected override void OnGameObjectInitialize()
        {
            InitializeReferences();
            owner = GlobalData.Get<BlockPortersData>();
            BindData<BlockPortersData>(OnData);

            menu = gameObject.GetComponent<UIMenuScope>();
            menu.Canceled += Exit; AddBinding(() => { if (menu != null) menu.Canceled -= Exit; });
            next.onClick.AddListener(() => GlobalData.Dispatch(new BlockPortersNextLevelAction()));
            resultRestart.onClick.AddListener(() => GlobalData.Dispatch(new BlockPortersRestartAction()));
            resultExit.onClick.AddListener(Exit);
            for (int side = 0; side < resultExtraButtons.Length; side++)
            { int index = side; resultExtraButtons[side].onClick.AddListener(() => GlobalData.Dispatch(new BlockPortersUnlockSlotAction(index))); }
        }



        private void Exit() => GlobalData.Dispatch(new BlockPortersExitAction());
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

        private void OnData(BlockPortersData value) { owner = value; Refresh(); }
        private void InitializeReferences()
        {
            resultTitle = TextMeshProUGUI_ResultTitle;
            resultDescription = TextMeshProUGUI_ResultDescription;
            resultExtraButtons = new UnityEngine.UI.Button[] { Button_ResultUnlock0, Button_ResultUnlock1 };
            next = Button_Next;
            resultRestart = Button_ResultRestart;
            resultExit = Button_ResultExit;
            nextLabel = TextMeshProUGUI_NextLabel;
        }
    }
}
