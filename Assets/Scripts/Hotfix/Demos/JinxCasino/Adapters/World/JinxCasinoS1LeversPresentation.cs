using Hotfix.JinxCasino.Interaction;
using System;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    [Serializable]
    public sealed class CasinoS1LeverVisual
    {
        public Transform Lever;
        public Transform Needle;
        public GameObject Window;
        public GameObject HelpWindow;
        public Renderer WindowRenderer;
        public Renderer HelpWindowRenderer;
        public Material Dark;
        public Material Open;
        public Material Pulled;
        // NPC表盘使用公开设计周期，0号开放真值直接消费TableView.LeverWindowOpen。
        public int WindowStartMilliseconds;
        public int WindowEndMilliseconds;
    }
    /// 单人双仪表：玩家与NPC杆消费各自真实Selected值，针消费公开时钟，不自行推进玩法。
    public sealed class JinxCasinoS1LeversPresentation : JinxCasinoS1Presentation
    {
        [SerializeField] private CasinoS1LeverVisual[] participants = Array.Empty<CasinoS1LeverVisual>();
        [SerializeField] private Transform helpWrench;
        [SerializeField] private Renderer syncLamp;
        [SerializeField] private Material lampDark;
        [SerializeField] private Material lampComplete;
        [SerializeField] private Transform assistantArm;
        [SerializeField] private Transform assistantPalm;
        [SerializeField] private Transform assistantGrip;
        [SerializeField] private Transform assistantHead;
        private Quaternion assistantArmRest;
        private Quaternion assistantHeadRest;
        private Vector3 assistantArmScale;
        private Vector3 assistantPalmRest;
        private Quaternion[] leverRest;
        private Quaternion[] needleRest;
        private float[] currentAngles;
        private float[] desiredAngles;
        protected override CasinoGameKind Game => CasinoGameKind.CooperativeLevers;
        // 时机玩法不能因杆的小演出锁掉下一次合法窗口，互斥由真实Selected/规则处理。
        public override bool IsAnimating => false;

        /// <summary>绑定玩家与助手的实体杆、仪表及状态灯。</summary>
        /// <param name="id">稳定机台ID。</param>
        /// <param name="controls">按玩家、助手顺序保存的两组控制器。</param>
        /// <param name="wrench">已使用协作道具的可见扳手。</param>
        /// <param name="lamp">同步结果灯。</param>
        /// <param name="darkLamp">未完成材质。</param>
        /// <param name="successLamp">完成材质。</param>
        /// <param name="amount">投入铭牌。</param>
        /// <param name="rules">局部规则铭牌。</param>
        /// <param name="result">结果铭牌。</param>
        public void Configure(string id, CasinoS1LeverVisual[] controls, Transform wrench, Renderer lamp,
            Material darkLamp, Material successLamp, TMP_Text amount, TMP_Text rules, TMP_Text result)
        {
            if (controls == null || controls.Length != 2 || Array.Exists(controls, value => value == null || value.Lever == null || value.Needle == null ||
                value.Window == null || value.HelpWindow == null || value.WindowRenderer == null || value.HelpWindowRenderer == null ||
                value.Dark == null || value.Open == null || value.Pulled == null) || wrench == null || lamp == null || darkLamp == null || successLamp == null)
                throw new ArgumentException("S1合拍台需要两组保存杆/针/弧窗及共享状态材质。");
            participants = controls; helpWrench = wrench; syncLamp = lamp; lampDark = darkLamp; lampComplete = successLamp;
            ConfigureLabels(id, amount, rules, result); ResetVisual();
        }
        protected override void CacheBindings()
        {
            if (participants.Length != 2 || Array.Exists(participants, value => value == null || value.Lever == null || value.Needle == null)) return;
            leverRest = Array.ConvertAll(participants, value => value.Lever.localRotation);
            needleRest = Array.ConvertAll(participants, value => value.Needle.localRotation);
            currentAngles = new float[2]; desiredAngles = new float[2];
            if (assistantArm != null && assistantPalm != null)
            {
                assistantArmRest = assistantArm.localRotation;
                assistantArmScale = assistantArm.localScale;
                assistantPalmRest = assistantArm.InverseTransformPoint(assistantPalm.position);
            }
            if (assistantHead != null) assistantHeadRest = assistantHead.localRotation;
        }
        protected override void ResetVisual()
        {
            if (leverRest == null) return;
            for (int index = 0; index < 2; index++)
            {
                currentAngles[index] = desiredAngles[index] = 0;
                participants[index].Lever.localRotation = leverRest[index]; participants[index].Needle.localRotation = needleRest[index];
                participants[index].Window.SetActive(false); participants[index].HelpWindow.SetActive(false);
            }
            if (helpWrench != null) helpWrench.gameObject.SetActive(false);
            SetMaterial(syncLamp, lampDark);
            RenderAssistant();
        }
        protected override void ApplyView(JinxCasinoTableView view, bool newSettlement, bool snap)
        {
            if (leverRest == null) return;
            var round = view.Presentation;
            if (round == null)
            {
                if (!view.HasOtherActiveRound && view.DraftStake > 0) { ResetVisual(); SetResult("确认后合拍"); }
                return;
            }
            if (round.SelectedValues.Length > 2) throw new InvalidOperationException("S1只提供玩家0与NPC1，不能伪装其它参与者。");
            for (int index = 0; index < 2; index++)
            {
                bool active = index < round.SelectedValues.Length;
                desiredAngles[index] = active && round.SelectedValues[index] != 0 ? 40 : 0;
                if (snap) { currentAngles[index] = desiredAngles[index]; participants[index].Lever.localRotation = leverRest[index] * Quaternion.AngleAxis(currentAngles[index], Vector3.right); }
            }
            if (snap || !IsPaused) RenderInstruments(view);
            if (snap) RenderAssistant();
            SetResult(round.IsComplete ? (round.IsObjectiveSuccess ? "合拍完成" : "合拍失败") + " · 返还 " + round.Payout : view.LeverWindowOpen ? "现在拉" : "等指针进入窗口");
        }
        protected override void Tick(float seconds)
        {
            if (leverRest == null || Latest?.Presentation == null) return;
            for (int index = 0; index < 2; index++)
            {
                currentAngles[index] = Mathf.MoveTowards(currentAngles[index], desiredAngles[index], seconds * 300);
                participants[index].Lever.localRotation = leverRest[index] * Quaternion.AngleAxis(currentAngles[index], Vector3.right);
            }
            RenderInstruments(Latest);
            RenderAssistant();
        }
        private void RenderAssistant()
        {
            if (assistantArm == null || assistantPalm == null || assistantGrip == null || assistantPalmRest.sqrMagnitude < .0001f) return;
            // 发条袖臂轻微伸缩，握点始终贴合已按真实结果移动的杆；不另开Update或改变玩法时钟。
            assistantArm.localRotation = assistantArmRest;
            assistantArm.localScale = assistantArmScale;
            Vector3 restReach = assistantArm.TransformVector(assistantPalmRest);
            Vector3 reach = assistantGrip.position - assistantArm.position;
            if (restReach.sqrMagnitude < .0001f || reach.sqrMagnitude < .0001f) return;
            assistantArm.rotation = Quaternion.FromToRotation(restReach, reach) * assistantArm.rotation;
            assistantArm.localScale = assistantArmScale * (reach.magnitude / restReach.magnitude);
            if (assistantHead != null)
                assistantHead.localRotation = assistantHeadRest * Quaternion.Euler(-10 * currentAngles[1] / 40, 0, 0);
        }
        private void RenderInstruments(JinxCasinoTableView view)
        {
            var round = view.Presentation; if (round == null) return;
            long cycle = round.ElapsedMilliseconds % 2000;
            for (int index = 0; index < 2; index++)
            {
                var control = participants[index]; bool active = index < round.SelectedValues.Length;
                bool pulled = active && round.SelectedValues[index] != 0;
                int margin = round.CooperationHelpUsed ? 100 : 0;
                bool open = active && !round.IsComplete && (index == 0 ? view.LeverWindowOpen :
                    cycle >= control.WindowStartMilliseconds - margin && cycle <= control.WindowEndMilliseconds + margin);
                control.Window.SetActive(active && !round.CooperationHelpUsed);
                control.HelpWindow.SetActive(active && round.CooperationHelpUsed);
                SetMaterial(round.CooperationHelpUsed ? control.HelpWindowRenderer : control.WindowRenderer,
                    pulled ? control.Pulled : open ? control.Open : control.Dark);
                // 设计弧窗x=sinθ/y=cosθ，针零位朝+Y，因此绕本地Z负θ与几何窗口一致。
                control.Needle.localRotation = needleRest[index] * Quaternion.AngleAxis(-(float)cycle * 360 / 2000, Vector3.forward);
            }
            if (helpWrench != null) helpWrench.gameObject.SetActive(round.CooperationHelpUsed);
            SetMaterial(syncLamp, round.IsComplete && round.IsObjectiveSuccess ? lampComplete : lampDark);
        }
        private static void SetMaterial(Renderer renderer, Material material)
        { if (renderer != null && material != null && renderer.sharedMaterial != material) renderer.sharedMaterial = material; }
    }
}
