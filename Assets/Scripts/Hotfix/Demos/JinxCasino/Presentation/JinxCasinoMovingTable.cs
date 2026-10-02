using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Presentation
{
    /// 空闲赌桌沿编辑器保存的短路径移动，碰撞或缺少地面时停下，结束后安全归位。
    public sealed class JinxCasinoMovingTable : MonoBehaviour
    {
        [SerializeField] private Transform tableRoot;
        [SerializeField] private Collider bodyCollider;
        [SerializeField] private Vector3 localTravel = new Vector3(0.8f, 0, 0);
        [SerializeField] private CasinoGameKind game;
        private Vector3 origin;
        private Vector3 destination;
        private float startedAt;
        private float endsAt;
        private bool moving;
        private bool returning;
        private bool occupied;
        private int collisionMask = -1;
        private JinxCasinoPresentationClock presentationClock;
        private float PresentationTime => presentationClock?.TimeSeconds ?? Time.unscaledTime;
        private float PresentationDelta => presentationClock?.GetDeltaSeconds(Time.frameCount) ?? Time.unscaledDeltaTime;

        /// <summary>绑定本机暂停时钟，正在移动的桌体保留剩余演出时间。</summary>
        /// <param name="clock">所属Demo共享时钟；null保持旧原型时间。</param>
        public void BindPresentationClock(JinxCasinoPresentationClock clock)
        {
            if (ReferenceEquals(presentationClock, clock)) return;
            float before = PresentationTime; presentationClock = clock;
            float offset = PresentationTime - before;
            startedAt += offset; endsAt += offset;
        }

        public CasinoGameKind Game => game;
        public bool IsMoving => moving || returning;

        /// <summary>绑定已保存的桌体和安全短路径，不在运行时生成机台。</summary>
        /// <param name="root">移动的桌体根；其下包含所有桌体碰撞。</param>
        /// <param name="collider">用于保守碰撞检测的桌体Collider。</param>
        /// <param name="travel">相对根变换的水平短路径，长度上限1.5米。</param>
        /// <param name="kind">领域机台类型，用于排除当前正在操作的桌。</param>
        public void Configure(Transform root, Collider collider, Vector3 travel, CasinoGameKind kind)
        { tableRoot = root; bodyCollider = collider; localTravel = travel; game = kind; }

        /// <summary>开始已获领域许可的空闲桌移动。</summary>
        /// <param name="duration">最多三秒的演出时间。</param>
        /// <param name="layers">需要碰撞和地面校验的物理层，默认全部。</param>
        public void BeginMotion(float duration, int layers = -1)
        {
            if (tableRoot == null || bodyCollider == null || moving || returning) return;
            collisionMask = layers; origin = tableRoot.position;
            Vector3 travel = tableRoot.TransformVector(localTravel); travel.y = 0;
            destination = origin + Vector3.ClampMagnitude(travel, 1.5f);
            startedAt = PresentationTime; endsAt = startedAt + Mathf.Clamp(duration, 0.1f, 3f); moving = true;
        }

        /// 请求安全归位，遇到玩家挡路时等待而不瞬移挤压角色。
        public void EndMotion() { if (moving) { moving = false; returning = true; } }

        /// <summary>当前机台有人操作时冻结桌体，离开机台后继续安全归位。</summary>
        /// <param name="value">领域ActiveGame匹配本桌时为true。</param>
        public void SetOccupied(bool value) { occupied = value; if (occupied) EndMotion(); }

        private void Update()
        {
            if (tableRoot == null || occupied || (presentationClock?.IsPaused ?? false)) return;
            if (moving)
            {
                if (PresentationTime >= endsAt) EndMotion();
                else
                {
                    float progress = Mathf.Clamp01((PresentationTime - startedAt) / Mathf.Max(0.1f, endsAt - startedAt));
                    var goal = Vector3.Lerp(origin, destination, Mathf.Sin(progress * Mathf.PI));
                    JinxCasinoWorldMotion.MoveTable(tableRoot, bodyCollider, goal, collisionMask);
                }
            }
            if (returning)
            {
                var goal = Vector3.MoveTowards(tableRoot.position, origin, PresentationDelta * 1.5f);
                JinxCasinoWorldMotion.MoveTable(tableRoot, bodyCollider, goal, collisionMask);
                if ((tableRoot.position - origin).sqrMagnitude < 0.0001f) returning = false;
            }
        }

        private void OnDisable()
        {
            // 整张桌已停用或场景卸载，此时恢复编辑器原位；活动期间归位始终走碰撞路径。
            if ((moving || returning) && tableRoot != null) tableRoot.position = origin;
            moving = returning = occupied = false;
        }
    }
}
