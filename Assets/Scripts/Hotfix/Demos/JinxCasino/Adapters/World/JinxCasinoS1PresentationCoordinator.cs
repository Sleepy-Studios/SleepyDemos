using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 把具体机台的公开状态持续送给场景表现；离桌后仍继续显示该台活动局。
    [DefaultExecutionOrder(-100)]
    public sealed class JinxCasinoS1PresentationCoordinator : MonoBehaviour
    {
        [SerializeField] private JinxCasinoController owner;
        [SerializeField] private JinxCasinoStation[] stations;
        [SerializeField] private JinxCasinoS1Presentation[] presentations;
        private JinxCasinoTableSession[] observers;

        /// <summary>保存对应机台和表现引用，必须一一对应。</summary>
        /// <param name="controller">当前单机场景宿主。</param>
        /// <param name="tables">稳定机台实例。</param>
        /// <param name="views">同序的专属表现组件。</param>
        public void Configure(JinxCasinoController controller, JinxCasinoStation[] tables, JinxCasinoS1Presentation[] views)
        {
            if (tables == null || views == null || tables.Length != views.Length) throw new System.ArgumentException("机台与表现必须一一对应。");
            owner = controller; stations = tables; presentations = views;
        }
        private void OnEnable()
        {
            if (owner == null || stations == null) return;
            observers = new JinxCasinoTableSession[stations.Length];
            for (int i = 0; i < stations.Length; i++)
                observers[i] = new JinxCasinoTableSession(new JinxCasinoControllerTableOperations(owner, stations[i]), stations[i].StationId, stations[i].Game);
            owner.Changed += Refresh; owner.ImmersionInputChanged += Refresh;
            Refresh();
        }
        private void Update() => Refresh();
        private void Refresh()
        {
            if (owner == null || observers == null) return;
            var state = owner.AdventureState;
            for (int i = 0; i < observers.Length; i++)
            {
                var visual = presentations[i];
                if (visual == null) continue;
                if (state == null) { visual.Present(null, owner.IsImmersionPaused); continue; }
                visual.BeginRun(state.RunId);
                var view = owner.GetFocusedTableView(stations[i]) ?? observers[i].GetView();
                if (owner.IsAdventureRestoreInProgress) { visual.Present(null, owner.IsImmersionPaused); visual.Restore(view); }
                else visual.Present(view, owner.IsImmersionPaused);
            }
        }
        private void OnDisable()
        {
            if (owner != null) { owner.Changed -= Refresh; owner.ImmersionInputChanged -= Refresh; }
            if (observers != null) foreach (var observer in observers) observer?.Close();
            observers = null;
        }
    }
}
