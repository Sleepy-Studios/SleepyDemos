using System;
using System.Collections.Generic;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 保存的时机表盘材质槽；同一Renderer两个槽可分别保留指针和表盘颜色。
    [Serializable]
    public sealed class CasinoLeverLampBinding
    {
        public Renderer Renderer;
        public int MaterialSlot;
        public int PlayerIndex;
        public Material Dark;
        public Material Open;
        public Material Pulled;
    }

    /// 原创机台的确定结果演出；只读公开投影，动画和物理不能决定奖励。
    public sealed class JinxCasinoStationPresentation : MonoBehaviour
    {
        [SerializeField] private JinxCasinoController owner;
        [SerializeField] private JinxCasinoStation station;
        [SerializeField] private Transform visual;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private CasinoLeverLampBinding[] leverLamps = Array.Empty<CasinoLeverLampBinding>();
        private readonly List<LeverLampState> lampStates = new List<LeverLampState>();
        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private readonly Dictionary<Transform, Vector3> positions = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Quaternion> rotations = new Dictionary<Transform, Quaternion>();
        private CasinoMiniGamePresentation view;
        private CasinoGameKind previousGame;
        private string runId;
        private int sequence;
        private int operation;
        private float spinUntil;
        private float PresentationTime => owner != null && owner.PresentationClock != null ? owner.PresentationClock.TimeSeconds : Time.unscaledTime;

        /// <summary>装配保存的视觉与结果牌，内部FBX保持身份变换，外层朝交互位置摆放。</summary>
        /// <param name="controller">场景规则宿主。</param>
        /// <param name="target">真实交互机台；轮换展位类型可以变化。</param>
        /// <param name="model">模型或17款轮换模型的保存容器。</param>
        /// <param name="label">可选结果牌；不创建运行时控件或读取隐藏牌堆。</param>
        /// <param name="lamps">保存的共享材质槽绑定；可空，保留旧机台调用。</param>
        public void Setup(JinxCasinoController controller, JinxCasinoStation target, Transform model, TMP_Text label = null, CasinoLeverLampBinding[] lamps = null)
        { ResetLeverLamps(); owner = controller; station = target; visual = model; resultText = label; leverLamps = lamps ?? Array.Empty<CasinoLeverLampBinding>(); CacheBones(); }

        private void OnEnable() { CacheBones(); if (owner != null) owner.Changed += Refresh; Refresh(); }
        private void OnDisable() { if (owner != null) owner.Changed -= Refresh; ResetPoses(); }
        private void CacheBones()
        {
            ResetLeverLamps(); lampStates.Clear();
            bones.Clear(); positions.Clear(); rotations.Clear();
            if (visual == null) return;
            foreach (var bone in visual.GetComponentsInChildren<Transform>(true))
            { if (!bones.ContainsKey(bone.name)) bones.Add(bone.name, bone); positions[bone] = bone.localPosition; rotations[bone] = bone.localRotation; }
            CacheLeverLamps();
        }

        private void Refresh()
        {
            if (owner == null || station == null) return;
            var state = owner.Game.State;
            if (state == null) { view = null; ResetPoses(); if (resultText != null) resultText.text = string.Empty; return; }
            bool reset = runId != state.RunId || previousGame != station.Game || owner.Game.IsRestoring;
            if (reset) { ResetPoses(); spinUntil = 0; sequence = state.SettledRoundSequence; operation = -1; runId = state.RunId; previousGame = station.Game; }
            var current = owner.Game.GetPresentation();
            string resultStation = owner.Game.HasActiveRound ? state.ActiveStationId : state.LastStationId;
            bool matchesStation = string.IsNullOrEmpty(station.StationId) || station.StationId == resultStation;
            view = current != null && current.Game == station.Game && matchesStation ? current : null;
            if (view != null)
            {
                if (!reset && (sequence != state.SettledRoundSequence || !view.IsComplete && operation != view.OperationCount)) spinUntil = PresentationTime + 1.1f;
                operation = view.OperationCount;
                if (resultText != null) resultText.text = view.IsComplete ? "已结算 · 返还 " + view.Payout + "\n" + string.Join(" / ", view.NumberValues) : "进行中 · " + Math.Max(0, view.RemainingMilliseconds / 1000) + "秒";
            }
            sequence = state.SettledRoundSequence;
        }

        private void Update()
        {
            if (view == null || station == null || (owner != null && (owner.PresentationClock?.IsPaused ?? false))) return;
            float spin = PresentationTime < spinUntil ? PresentationTime * 600 : 0;
            switch (station.Game)
            {
                case CasinoGameKind.Slots:
                    for (int index = 0; index < 3; index++) Rotate("Reel" + index, Vector3.right, spin != 0 ? spin + index * 45 : Value(index) * 60); break;
                case CasinoGameKind.Roulette: Rotate("Wheel", Vector3.up, spin != 0 ? spin : Value(0) * 360f / 37); break;
                case CasinoGameKind.CoinFlip: Rotate("Coin", Vector3.right, spin != 0 ? spin : Value(0) * 180); break;
                case CasinoGameKind.Blackjack: Rotate("DealerArm", Vector3.forward, spin != 0 ? Mathf.Sin(PresentationTime * 8) * 22 : 0); break;
                case CasinoGameKind.HighLow: Rotate("TurnPage", Vector3.up, spin != 0 ? Mathf.Sin(PresentationTime * 6) * 80 : 0); break;
                case CasinoGameKind.LuckyDraw:
                    for (int index = 0; index < 6; index++) Move("Tube" + index, Vector3.up * (spin != 0 ? Mathf.Abs(Mathf.Sin(PresentationTime * 7 + index)) * 0.1f : 0)); break;
                case CasinoGameKind.Bingo: Rotate("Globe", Vector3.up, spin); Rotate("Bell", Vector3.forward, spin != 0 ? Mathf.Sin(PresentationTime * 9) * 16 : 0); break;
                case CasinoGameKind.SicBo:
                    for (int index = 0; index < 3; index++) { Rotate("Die" + index, Vector3.right, spin != 0 ? spin + index * 30 : (Value(index) - 1) * 90); Move("DiceCup" + index, Vector3.up * (spin != 0 ? Mathf.Abs(Mathf.Sin(PresentationTime * 10)) * 0.1f : 0)); } break;
                case CasinoGameKind.PushYourLuckDice: Rotate("DiceTower", Vector3.forward, spin != 0 ? Mathf.Sin(PresentationTime * 10) * 12 : 0); break;
                case CasinoGameKind.Plinko:
                    // 源板15槽沿Blender+X排列，转换为Unity-X；公开Cursor就是绝对槽，不能再减初始选择。
                    Move("Ball", new Vector3((3 + view.Level * 0.5f - view.Cursor) * 0.114f, -view.Level * 0.19875f, view.Level * (0.25f / 8))); break;
                case CasinoGameKind.CooperativeLevers:
                    for (int index = 0; index < view.SelectedValues.Length; index++) Rotate("Lever" + index, Vector3.right, view.SelectedValues[index] != 0 ? -40 : 0); break;
                case CasinoGameKind.PassingBag: Move("Bag", view.IsComplete ? Vector3.zero : new Vector3(Mathf.Sin(PresentationTime * 7) * 0.12f, Mathf.Abs(Mathf.Sin(PresentationTime * 5)) * 0.12f, 0)); break;
                case CasinoGameKind.MechanicalRace:
                    for (int index = 0; index < 4; index++) Move("Racer" + index, Vector3.left * (Mathf.Clamp01(Value(index) / 1200f) * 1.2f)); break;
                case CasinoGameKind.CooperativeVault: Rotate("Wheel", Vector3.forward, view.IsObjectiveSuccess ? 95 : spin); Rotate("Door", Vector3.up, view.IsObjectiveSuccess ? -65 : 0); break;
                case CasinoGameKind.ChickenElevator: Move("Elevator-0.62", Vector3.up * (view.Level * 0.24f)); Move("Elevator0.62", Vector3.up * (view.Level * 0.24f)); break;
                case CasinoGameKind.BlindAuction: Rotate("Lid", Vector3.right, view.IsComplete && view.Cost > 0 ? -70 : 0); Rotate("Gavel", Vector3.right, spin != 0 ? Mathf.Sin(PresentationTime * 8) * 25 : 0); break;
            }
        }

        // 宿主在Update发布当前公开周期，灯在LateUpdate消费，避免执行顺序产生一个tick的旧窗口。
        private void LateUpdate() => RefreshLeverLamps();

        private int Value(int index) => index < view.NumberValues.Length ? view.NumberValues[index] : 0;
        private Transform Bone(string suffix) => bones.TryGetValue(station.Game + "." + suffix, out var bone) ? bone : null;
        private void Rotate(string suffix, Vector3 axis, float angle)
        { var bone = Bone(suffix); if (bone != null) bone.localRotation = rotations[bone] * Quaternion.AngleAxis(angle, axis); }
        private void Move(string suffix, Vector3 offset)
        { var bone = Bone(suffix); if (bone != null) bone.localPosition = positions[bone] + offset; }
        private void ResetPoses()
        {
            ResetLeverLamps();
            foreach (var pair in positions) if (pair.Key != null) pair.Key.localPosition = pair.Value;
            foreach (var pair in rotations) if (pair.Key != null) pair.Key.localRotation = pair.Value;
        }

        private void CacheLeverLamps()
        {
            foreach (var binding in leverLamps)
            {
                if (binding == null || binding.Renderer == null || binding.PlayerIndex < 0 || binding.PlayerIndex >= 6) continue;
                var state = lampStates.Find(value => value.Renderer == binding.Renderer);
                if (state == null)
                {
                    var original = binding.Renderer.sharedMaterials;
                    state = new LeverLampState { Renderer = binding.Renderer, Original = original, Slots = (Material[])original.Clone() };
                    lampStates.Add(state);
                }
                if (binding.MaterialSlot < 0 || binding.MaterialSlot >= state.Slots.Length || binding.Dark == null || binding.Open == null || binding.Pulled == null)
                    throw new InvalidOperationException("拉杆灯绑定缺少保存材质或槽越界：" + binding.Renderer.name);
                if (state.Bindings.Exists(value => value.MaterialSlot == binding.MaterialSlot)) throw new InvalidOperationException("拉杆灯存在重复槽绑定：" + binding.Renderer.name);
                state.Bindings.Add(binding);
            }
        }

        private void RefreshLeverLamps()
        {
            foreach (var state in lampStates)
            {
                if (state.Renderer == null) continue;
                bool changed = false;
                foreach (var binding in state.Bindings)
                {
                    bool available = view != null && view.Game == CasinoGameKind.CooperativeLevers && binding.PlayerIndex < view.SelectedValues.Length;
                    bool pulled = available && view.SelectedValues[binding.PlayerIndex] != 0;
                    long cycle = available ? view.ElapsedMilliseconds % 2000 : -1;
                    int start = 200 + binding.PlayerIndex * 250;
                    int margin = available && view.CooperationHelpUsed ? 100 : 0;
                    bool open = available && !view.IsComplete && cycle >= start - margin && cycle <= start + 200 + margin;
                    var material = pulled ? binding.Pulled : open ? binding.Open : binding.Dark;
                    if (state.Slots[binding.MaterialSlot] == material) continue;
                    state.Slots[binding.MaterialSlot] = material; changed = true;
                }
                // 写sharedMaterials只发生于灯状态变化，复用缓存数组和持久Material，不生成运行时材质实例。
                if (changed) state.Renderer.sharedMaterials = state.Slots;
            }
        }

        private void ResetLeverLamps()
        {
            foreach (var state in lampStates)
                if (state.Renderer != null)
                { state.Renderer.sharedMaterials = state.Original; Array.Copy(state.Original, state.Slots, state.Original.Length); }
        }
        private sealed class LeverLampState
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Slots;
            public readonly List<CasinoLeverLampBinding> Bindings = new List<CasinoLeverLampBinding>();
        }
    }
}
