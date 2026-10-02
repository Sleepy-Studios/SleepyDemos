using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.Adapters.UI
{
    /// 样板薄HUD与菜单；桌面主操作仍在实体物件，所有控件由Prefab保存。
    public sealed class JinxCasinoImmersionHudPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenu;
        [SerializeField] private GameObject pauseMenu;
        [SerializeField] private GameObject fieldHud;
        [SerializeField] private TMP_Text wallet;
        [SerializeField] private TMP_Text objective;
        [SerializeField] private TMP_Text prompt;
        [SerializeField] private TMP_Text feedback;
        [SerializeField] private Button start;
        [SerializeField] private Button practice;
        [SerializeField] private Button resume;
        [SerializeField] private Button pause;
        [SerializeField] private Button leave;
        [SerializeField] private Button interact;
        [SerializeField] private Button exitTable;
        [SerializeField] private JinxCasinoTouchPad movePad;
        [SerializeField] private JinxCasinoTouchPad lookPad;
        private JinxCasinoController owner;
        private int menuState = -1;

        /// <summary>绑定当前样板宿主，退出后释放具体订阅和触屏指针。</summary>
        /// <param name="controller">所属本地场景。</param>
        public void Bind(JinxCasinoController controller)
        {
            Unbind(); owner = controller; menuState = -1;
            owner.Changed += Refresh; owner.ImmersionInputChanged += Refresh;
            owner.BindTouchPads(movePad, lookPad);
            start.onClick.AddListener(StartAdventure); practice.onClick.AddListener(StartPractice);
            resume.onClick.AddListener(Resume); pause.onClick.AddListener(Pause);
            leave.onClick.AddListener(Leave); interact.onClick.AddListener(Interact); exitTable.onClick.AddListener(ExitTable);
            Refresh();
        }

        /// 释放所有本实例事件，不清空其它控件的监听器。
        public void Unbind()
        {
            if (owner == null) return;
            owner.Changed -= Refresh; owner.ImmersionInputChanged -= Refresh;
            owner.BindTouchPads(null, null); owner.SetImmersionMenuState(false, false, null);
            start.onClick.RemoveListener(StartAdventure); practice.onClick.RemoveListener(StartPractice);
            resume.onClick.RemoveListener(Resume); pause.onClick.RemoveListener(Pause);
            leave.onClick.RemoveListener(Leave); interact.onClick.RemoveListener(Interact); exitTable.onClick.RemoveListener(ExitTable);
            owner = null;
        }
        private void StartAdventure() { owner.StartAdventure(CasinoAdventureMode.Standard); Refresh(); }
        private void StartPractice() { owner.StartAdventure(CasinoAdventureMode.Practice); Refresh(); }
        private void Pause() { owner.PauseImmersion(); Refresh(); }
        private void Resume() { owner.ResumeImmersion(); Refresh(); }
        private void Leave() => owner.RequestExit();
        private void Interact() { owner.InteractWithNearbyStation(); Refresh(); }
        private void ExitTable() { owner.CloseImmersionTable(); Refresh(); }
        private void Update() { if (owner != null) Refresh(); }

        private void Refresh()
        {
            if (owner == null) return;
            int state = !owner.HasAdventure ? 0 : owner.IsImmersionPaused ? 1 : 2;
            if (menuState != state)
            {
                menuState = state;
                mainMenu.SetActive(state == 0); pauseMenu.SetActive(state == 1); fieldHud.SetActive(state == 2);
                owner.SetImmersionMenuState(state != 2, false, state == 0 ? start.gameObject : state == 1 ? resume.gameObject : null);
            }
            var adventure = owner.AdventureState;
            wallet.text = "筹码  " + owner.Balance;
            objective.text = adventure == null ? string.Empty : adventure.Mode == CasinoAdventureMode.Practice ? "自由练习" :
                "目标 " + owner.AdventureTarget + "   ·   " + Mathf.CeilToInt(adventure.RemainingMilliseconds / 1000f) + " 秒";
            var table = owner.TableView;
            bool atDesk = table != null || owner.HasShopFocus;
            bool touching = Application.isMobilePlatform || owner.InputDeviceKind == Input.JinxCasinoInputDeviceKind.Touch;
            movePad.gameObject.SetActive(state == 2 && !atDesk && touching);
            lookPad.gameObject.SetActive(state == 2 && !atDesk && touching);
            interact.gameObject.SetActive(state == 2 && !atDesk && touching);
            exitTable.gameObject.SetActive(state == 2 && atDesk);
            string action = owner.InputDeviceKind == Input.JinxCasinoInputDeviceKind.Gamepad ? "A" : touching ? "交互" : "E";
            var nearby = owner.GetNearbyLocalSocialStation();
            prompt.text = atDesk ? owner.InputDeviceKind == Input.JinxCasinoInputDeviceKind.Gamepad
                ? "方向选择 · A 操作 · X 次要 · Y 规则 · B 离开" : touching ? "点选桌面物件 · 轻触返回离开" : "点击物件 · 方向键 / Enter · H 规则 · Esc 离开"
                : owner.IsShopNearby ? action + " 查看附近机台 / 补给柜台" : nearby != null ? action + " 进入机台" : "走近一张机台，试试今天的运气";
            feedback.text = owner.HasShopFocus ? owner.ShopFeedback ?? "选择实物查看报价；购买按钮确认付款。" : table != null ? owner.TableFeedback ?? table.Description : string.Empty;
        }
        private void OnDestroy() => Unbind();
    }
}
