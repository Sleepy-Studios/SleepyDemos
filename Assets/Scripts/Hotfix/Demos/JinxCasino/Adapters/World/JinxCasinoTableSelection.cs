using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 桌面独立焦点；实体射线与方向导航共享选择，不接管Core菜单的EventSystem。
    public sealed class JinxCasinoTableSelection : IDisposable
    {
        private readonly List<JinxCasinoTableTarget> targets = new List<JinxCasinoTableTarget>();
        private JinxCasinoTableTarget selected;

        /// 当前有效焦点；物件被禁用后立即视为无选择。
        public JinxCasinoTableTarget Selected => selected != null && selected.IsAvailable ? selected : null;

        /// <summary>绑定具体桌面的保存目标，顺序稳定，不搜索其它机台。</summary>
        /// <param name="station">聚焦的场景实例；null表示清理。</param>
        public void Bind(JinxCasinoStation station)
        {
            SetSelected(null); targets.Clear();
            if (station == null) return;
            // 稳定插入排序，保持同NavigationOrder时资源配置中的原顺序。
            foreach (var target in station.Targets)
            {
                if (target == null || !target.transform.IsChildOf(station.transform)) continue;
                int index = targets.Count;
                while (index > 0 && targets[index - 1].NavigationOrder > target.NavigationOrder) index--;
                targets.Insert(index, target);
            }
        }

        /// <summary>按方向切换实体焦点，跳过不可用目标并允许循环。</summary>
        /// <param name="direction">正数下一个，负数上一个，0保持不变。</param>
        /// <returns>选中的有效物件，无可用目标时为null。</returns>
        public JinxCasinoTableTarget Navigate(int direction)
        {
            if (direction == 0) return Selected;
            int step = direction > 0 ? 1 : -1;
            int start = targets.IndexOf(selected);
            if (start < 0) start = step > 0 ? -1 : 0;
            for (int count = 1; count <= targets.Count; count++)
            {
                int index = (start + step * count + targets.Count * 2) % targets.Count;
                if (targets[index] == null || !targets[index].IsAvailable) continue;
                SetSelected(targets[index]); return selected;
            }
            SetSelected(null); return null;
        }

        /// <summary>以实际相机射线选实体；最近遮挡物不是本台目标时清焦点，不穿透桌面。</summary>
        /// <param name="ray">由本地相机屏幕坐标生成的射线。</param>
        /// <param name="distance">最大交互距离，默认4米。</param>
        /// <returns>命中的可用桌面目标或null。</returns>
        public JinxCasinoTableTarget Point(Ray ray, float distance = 4f)
        {
            JinxCasinoTableTarget target = null;
            if (distance > 0 && Physics.Raycast(ray, out var hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                target = hit.collider.GetComponentInParent<JinxCasinoTableTarget>();
            SetSelected(target != null && targets.Contains(target) && target.IsAvailable ? target : null);
            return Selected;
        }

        /// 提交当前实体操作；不可用或无目标时不产生请求。
        public bool TryInvoke() => Selected != null && Selected.TryInvoke();

        private void SetSelected(JinxCasinoTableTarget target)
        {
            if (selected == target)
            {
                // 暂时禁用会撤掉物件高亮；恢复后指针仍停在同一目标也须重新显示反馈。
                if (selected != null) selected.SetFocused(true);
                return;
            }
            if (selected != null) selected.SetFocused(false);
            selected = target;
            if (selected != null) selected.SetFocused(true);
        }

        /// 清理借用的高亮和目标引用。
        public void Dispose() { SetSelected(null); targets.Clear(); }
    }
}
